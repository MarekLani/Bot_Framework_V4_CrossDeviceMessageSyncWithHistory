## Bot Framework Cross Window Conversation Sync and Chat History Middleware for Webchat (C#)

When implementing chat bot solution there might be requirement for implementation of cross window/device conversation synchronization and loading of history of previous chats with the user. Example of scenario when such functionality might be needed is scenario of returning of ordered goods within e-commerce chat bot solution. Before returning goods, many e-commerce websites require customer to fill and scan goods return form. In case of using bot, user may initiate return goods conversation flow on his computer, but at some point when he needs to scan the form, he opens new chat window on a smartphone, which enables him to scan the form in much more convenient way. In order to keep the context of conversation, there is need for loading of chat history and message synchronization between original and new chat window.

Existing popular messaging solutions, which may act as chat bot channel, support this functionality out of the box, so for you as chat bot developer, there is no need to do anything in terms of message history and synchronization. However in case you are using directline and webchat component in your solution, whether integrated to your web site or mobile application, it requires some extra steps to have this functionality available. 

There are two pieces of functionality, that needs to be implemented:

- **Cross Chat Window Message Syncing** - by this term we mean specific behavior of chat bot, when it simultaneously displays sent and received messages in all the chat windows that specific user had opened. When using Web Chat, implementation of this behavior is quite straightforward, because conversation will be by default synced between all the web chat windows which share the same Conversation ID. SO in order to have this conversation synching capability available in the bot, there is need for implementation of Web Chat activation with per user Conversation ID.

- **Message History** - to implement message history, there is need to implement message logging feature and subsequently loading of history into newly opened window.


This repository is split into two parts:  

- **Sample website integrating webchat component** (we used react web chat component)   
- **Bot builder backend with middleware for logging of conversation history and it's injecting into newly opened chat window**

Bellow we state code blocks containing most important part of the solution and 

Note: We do not discuss basics of how to activate direct line channel. For information on that piece please see official documentation:

### **Sample website integrating webchat component**

As stated, to implement cross chat windows message syncing it is needed to activate Web Chat windows with the same Conversation Id per user. It is role of the backend to store key value pairs of user ids and conversation ids, however webchat activation needs to be different in case of activation of first Web Chat windows for the user and all subsequent windows. In order to activate web chat window, there is need to obtain directline token from bot backend. In our solution besides token bot backend returns also conversation Id field, which is empty in case there was no previous conversation with the user. Otherwise it holds conversation id of first conversation with the user. This way it is possible to secure one and only one conversation Id for the user. When obtaining token there is need to provide user id, which will uniquely identify the user.

 When webchat window gets activated it sends WebChat/join event to bot backend, which triggers loading of history. 

Bellow we state implementation of Web Chat windows activation:

```javascript
<div id="webchat" role="main"></div>
<script type="text/babel">
    (async function () {
    
	var user = {
        id: 'Marek L',
        name: 'Marek L'
    };
		
	//obtain direct lin content form bot backend, change to your bot backend URL
	const res = await fetch('http://localhost:3978/api/directline/token', { 
		body: JSON.stringify({userId: user.id } ),
		headers: {
			'Content-Type': 'application/json'
		},
		method: 'POST' 
	});
		
    const { token, conversationId } = await res.json();
    const { ReactWebChat } = window.WebChat;
		
	var directLine;
	if(conversationId == '')
	    directLine = window.WebChat.createDirectLine({ token: token })
	els{
        //Initialize Web Chat with conversation id received from bot backend
		directLine = window.WebChat.createDirectLine({ token: token, conversationId: conversationId })	 
	   
    const store = window.WebChat.createStore(
        {},
        ({ dispatch }) => next => action => {
        if (action.type === 'DIRECT_LINE/POST_ACTIVITY') {
			//example of setting channelData
            action = window.simpleUpdateIn(action, ['payload', 'activity', 'channelData', 'chatWindowID'], () => 'myId');
        }
		   
		if (action.type === 'DIRECT_LINE/CONNECT_FULFILLED') {
            // When we receive DIRECT_LINE/CONNECT_FULFILLED action, we will send an event activity using WEB_CHAT/SEND_EVENT
            dispatch({
                type: 'WEB_CHAT/SEND_EVENT',
                payload: {
                name: 'webchat/join',
                value: { userId: user.id }
                }
            });
        }

        return next(action);
        }
    );

    window.ReactDOM.render(
        <ReactWebChat directLine={directLine} store={store} userID={user.id} />,
        document.getElementById('webchat')
    );

    document.querySelector('#webchat > *').focus();
    })().catch(err => console.error(err));
</script>
```



### Bot Builder Conversation History Middleware

There are three important components on the backend part, it is **direct line token generation logic**, **user conversation storage provider**, which determines underlying storage used for mapping conversation ids to user ids and for storing of chat history and last but not least **history middleware** responsible for storing and displaying chat history. 

#### Direct line token generation

 As we implemented bot backend using bot builder v 4.4 packages, we utilized possibility to create new controller with actions. Token generation logic can be found in *DirectLineController* class. Controller accesses *IConfiguration* and *IUserConversationStorageProvider* object from dependency injection controller.  Configuration object is needed to obtain direct line secret from app settings, User Conversation Storage Provider is needed to check if there was any previous conversation with the user. If so, it provides us with the conversation Id. Otherwise new key value pair is created. See implementation of *DirectLineController* class bellow:

```C#
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MessageSyncingBotWithHistory.Bots;
using MessageSyncingBotWithHistory.Dialogs.Root;
using MessageSyncingBotWithHistory.Helpers;
using Microsoft.Extensions.Configuration;
using MessageSyncingBotWithHistory.Middleware;

[Route("api/[controller]")]
[ApiController]
public class DirectLineController : ControllerBase
{
    IConfiguration configuration;
    IUserConversationsStorageProvider userConversationStorageProvider;
    public DirectLineController(IConfiguration configuration, IUserConversationsStorageProvider ucsp)
    {
        this.configuration = configuration;
        this.userConversationStorageProvider = ucsp;
    }

    [HttpPost("token")]
    [EnableCors("AllowAllOrigins")]
    public async Task<IActionResult> PostAsync([FromBody]User user )
    {
        StringValues token;
        string res;
            
        try
        {
            res = await RenewDirectLineToken(token);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
        if (res != "")
            return Ok(res);
        else
            return BadRequest("Error obtaining token");
    }

    private async Task<string> GenerateDirectLineToken(string userId)
    {
        var clnt = new HttpClient();
        clnt.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["DirectLineSecret"]);

        HttpResponseMessage rsp;
        string convId = "";

        if (userConversationStorageProvider.HasUser(userId))
        {
            convId = userConversationStorageProvider.GetUserConversationId(userId);
            userConversationStorageProvider.AddUser(userId);
            rsp = await clnt.GetAsync($"https://directline.botframework.com/v3/directline/conversations/{convId}");
        }
        else
            rsp = await clnt.PostAsync("https://directline.botframework.com/v3/directline/tokens/generate", new StringContent($"{{ \"User\": {{ \"Id\": \"{userId}\" }} }}", Encoding.UTF8, "application/json"));

        if (rsp.IsSuccessStatusCode)
        {
            var str = rsp.Content.ReadAsStringAsync().Result;
            var obj = JsonConvert.DeserializeObject<DirectlineResponse>(str);

            //If convId is empty string we are activating conversation with the user for the first time
            if (convId == "")
                obj.conversationId = "";
             
            //token = obj.token;
    
            return JsonConvert.SerializeObject(obj);
        }
        return "";
    }
}

class DirectlineResponse
{
    public string conversationId { get; set; }
    public string token { get; set; }
    public int expires_in { get; set; }
}

public class User
{
    [JsonProperty("userId")]
    public string UserId { get; set; }
}
```

#### User Conversation Storage Provider

User conversation storage provider is important part to the solution, because it enables simple hooking up of the solution to storage service of your choice. There are two different implementations of storage provider within the solution. First *SampleUserConversationStaticStorageProvider* uses static properties to store user ids, conversation ids and conversation history. This means all the data gets erased on bot restart and so this sample provider is meant only for testing purposes. Second provider is built on top of Redis database and so data is stored persistently.  So the providers can be used and accessed from dependency injection container inside of *DirectLineController* and *HistoryMiddleware* they need to implement *IUserConversationStorageProvider* interface. This approach makes it simple for you to implement your own storage provider, because neither *DirectlineController* neither *HistoryMiddleware* needs to know specific implementation of the storage provider. Each user conversation storage provider needs to override six methods specified by the interface:

```C#
public interface IUserConversationsStorageProvider
{
    void AddConvId(string userId, string convId);
    void AddUser(string userId);
    string GetUserConversationId(string userId);
    bool HasUser(string userId);
    void LogActivity(IActivity a, string userId);
    List<IActivity> GetActivities(string userId);
}
```

Bellow we state also implementation of *RedisUserConversationStorageProvider*:

```c#
using System;
using System.Collections.Generic;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace MessageSyncingBotWithHistory.Middleware
{
    public class RedisUserConversationStorageProvider : IUserConversationsStorageProvider
    {
        private ConnectionMultiplexer redis;
        private const string userConvKey = @"user:{0}";
        private const string userActivitiesListKey = @"userActivities:{0}";
        private static JsonSerializerSettings _jsonSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        public RedisUserConversationStorageProvider(IConfiguration config)
        {
            this.redis = ConnectionMultiplexer.Connect(config["RedisConnectionString"]);
        }

        public void AddConvId(string userId, string convId)
        {
            IDatabase db = redis.GetDatabase();
            db.StringSet(String.Format(userConvKey, userId), convId);
        }

        public void AddUser(string userId)
        {

			IDatabase db = redis.GetDatabase();
			db.StringSet(String.Format(userConvKey, userId), "");
        }

        public List<IActivity> GetActivities(string userId)
        {
            IDatabase db = redis.GetDatabase();
            var redisValues = db.ListRange(String.Format(userActivitiesListKey, userId));
            List<IActivity> activities = new List<IActivity>();
            foreach(var a in redisValues.ToStringArray())
            {
                activities.Add(JsonConvert.DeserializeObject<Activity>(a));
            }

            return activities;
        }

        public string GetUserConversationId(string userId)
        {
            IDatabase db = redis.GetDatabase();
            return db.StringGet(String.Format(userConvKey, userId)).ToString();
        }

        public bool HasUser(string userId)
        {
            IDatabase db = redis.GetDatabase();
            return db.KeyExists(String.Format(userConvKey, userId));
        }

        public void LogActivity(IActivity a, string userId)
        {
            IDatabase db = redis.GetDatabase();
            db.ListLeftPush(String.Format(userActivitiesListKey, userId), JsonConvert.SerializeObject(a, _jsonSettings));
        }
    }
}

```



#### Conversation History Middleware

History logging and loading is implemented within piece of middleware which enables us to intercept message processing flow within our bot backend. Read more about middleware concept in official documentation. 

Middleware is capable to log every message sent to or form user + activity updates a delete events. Data storage is determined by UseeConversationStorageProvider object obtained from dependency injection container.

Loading of history activities and sending them to newly opened chat windows happens on webchat/join event received. Activities are obtained from user conversation storage provider (only messages) and are sent to all web chat windows which share the same Conversation Id. Web Chat window acts intelligently in a way that it only displays messages if they were not displayed or sent and received previously. This behavior is possible thanks to channelData field of the activity, which holds unique identification of the activity.

```C#
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

```

#### Configuring services in Startup.cs

To put all pieces together there were changes needed also in startup.cs file where Dependency Injection container is being fed with the objects.

```c#
 public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        private IConfiguration _configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddCors(o => o.AddPolicy("AllowAllOrigins", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            }));

            services.AddMvc().SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);

            services.AddSingleton<IUserConversationsStorageProvider, RedisUserConversationStorageProvider>();

            services.AddSingleton<ICredentialProvider, ConfigurationCredentialProvider>();

            services.AddSingleton<IBotFrameworkHttpAdapter, BotFrameworkHttpAdapter>((provider) => {
                var cred = provider.GetRequiredService<ICredentialProvider>();
                var ucsp = provider.GetRequiredService<IUserConversationsStorageProvider>();
                var adpt = new BotFrameworkHttpAdapter(cred);

                adpt.Use(new ConversationHistoryMiddleware(ucsp, adpt, _configuration));

                return adpt;
            });
            
            services.AddSingleton<IStorage, MemoryStorage>();

            services.AddSingleton<UserState>();

            services.AddSingleton<ConversationState>();

            services.AddSingleton<RootDialog>();

            services.AddTransient<IBot, MainBot<RootDialog>>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseMvc();
        }
    }
}
```

