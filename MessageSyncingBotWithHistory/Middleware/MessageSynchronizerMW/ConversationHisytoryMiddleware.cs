using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MessageSyncingBotWithHistory.Middleware
{
    public class ConversationHistoryMiddleware: IMiddleware
    {
        private IUserConversationsStorageProvider _ucs;
        private BotAdapter _adapter;
        private IConfiguration _configuration;

        private static int timeOffset = 0;

        public ConversationHistoryMiddleware(IUserConversationsStorageProvider ucs, BotAdapter adapter, IConfiguration configuration)
        {
            _ucs = ucs;
            _adapter = adapter;
            _configuration = configuration;
        }

        private static JsonSerializerSettings _jsonSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };


        private static IActivity CloneActivity(IActivity activity)
        {
            activity = JsonConvert.DeserializeObject<Activity>(JsonConvert.SerializeObject(activity, _jsonSettings));
            return activity;
        }

        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {

            if (turnContext.Activity.Type == ActivityTypes.Event)
            {
                //If there was any previous conversation with the user, 
                //we try to obtain history from storage provider and push it to newly opened conversation
                if (turnContext.Activity.Name == "webchat/join")
                {
                    var reference = turnContext.Activity.GetConversationReference();

                    //Create conversation id user id pair
                    _ucs.AddConvId(turnContext.Activity.From.Id, turnContext.Activity.Conversation.Id);

                    var pastActivities = _ucs.GetActivities(turnContext.Activity.From.Id);
                    if (pastActivities.Count > 0)
                    {
                        var connectorClient = turnContext.TurnState.Get<ConnectorClient>(typeof(IConnectorClient).FullName);

                        //We select only activities of type Message
                        var activities = pastActivities
                            .Where(a => a.Type == ActivityTypes.Message)
                            .Select(ia => (Activity)ia)
                            .ToList();


                        // DirectLine only allows the upload of at most 500 activities at a time. The limit of 1500 below is
                        // arbitrary and up to the Bot author to decide.
                        
                        var count = 0;
                        while(count < pastActivities.Count)
                        {
                            var take = Math.Min(500, (activities.Count - count));
                            var transcript = new Transcript((activities.GetRange(count, take) as IList<Activity>));

                            //Thanks to channelData field activities will only get displayed in Web Chat Windows, which did not display them previously
                            await connectorClient.Conversations.SendConversationHistoryAsync(turnContext.Activity.Id, transcript, cancellationToken: cancellationToken);
                            count += 500;
                        }
                    }
                }
            }

            // log incoming activity at beginning of turn
            if (turnContext.Activity != null)
            {
                if (turnContext.Activity.From == null)
                {
                    turnContext.Activity.From = new ChannelAccount();
                }

                if (string.IsNullOrEmpty((string)turnContext.Activity.From.Properties["role"]))
                {
                    turnContext.Activity.From.Properties["role"] = "user";
                }

                LogActivity(CloneActivity(turnContext.Activity), turnContext.Activity.From.Id);
            }

            // hook up onSend pipeline
            turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
            {
                // run full pipeline
                var responses = await nextSend().ConfigureAwait(false);

                foreach (var activity in activities)
                {
                    LogActivity(CloneActivity(activity), ctx.Activity.From.Id);
                }

                return responses;
            });

            // hook up update activity pipeline
            turnContext.OnUpdateActivity(async (ctx, activity, nextUpdate) =>
            {
                // run full pipeline
                var response = await nextUpdate().ConfigureAwait(false);

                // add Message Update activity
                var updateActivity = CloneActivity(activity);
                updateActivity.Type = ActivityTypes.MessageUpdate;
                LogActivity( updateActivity, ctx.Activity.From.Id);
                return response;
            });

            // hook up delete activity pipeline
            turnContext.OnDeleteActivity(async (ctx, reference, nextDelete) =>
            {
                // run full pipeline
                await nextDelete().ConfigureAwait(false);

                // add MessageDelete activity
                // log as MessageDelete activity
                var deleteActivity = new Activity
                {
                    Type = ActivityTypes.MessageDelete,
                    Id = reference.ActivityId,
                }
                .ApplyConversationReference(reference, isIncoming: false)
                .AsMessageDeleteActivity();

                LogActivity( deleteActivity, ctx.Activity.From.Id);
            });

            // process bot logic
            await next(cancellationToken).ConfigureAwait(false);
        }

        private void LogActivity( IActivity activity, string userId)
        {
            if (activity.Timestamp == null)
            {
                activity.Timestamp = DateTime.UtcNow;
            }

            _ucs.LogActivity(activity, userId);
        }

    }
}
