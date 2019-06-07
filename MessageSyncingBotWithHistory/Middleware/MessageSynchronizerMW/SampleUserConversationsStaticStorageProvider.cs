using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MessageSyncingBotWithHistory.Middleware
{
    public class SampleUserConversationsStaticStorageProvider : IUserConversationsStorageProvider
    {
        private static List<SampleUserConversationsStorageStructure> userConversations = new List<SampleUserConversationsStorageStructure>();

        public void AddConvId(string userId, string convId)
        {
            if (userConversations.Where(u => u.UserId == userId).Any())
            {
                userConversations.Where(u => u.UserId == userId).FirstOrDefault().ConversationId = convId;
            }
            else
            {
                userConversations.Add(new SampleUserConversationsStorageStructure(userId, convId));
            }
        }


        public List<IActivity> GetActivities(string userId)
        {
            return userConversations.Where(u => u.UserId == userId).FirstOrDefault().PastActivities;
        }

        public string GetUserConversationId(string userId)
        {
            return userConversations.Where(u => u.UserId == userId).FirstOrDefault().ConversationId;
        }

        public bool HasUser(string userId)
        {
            return userConversations.Where(uc => uc.UserId == userId).Any();
        }

        public void LogActivity(IActivity a, string userId)
        {
            userConversations.Where(uc => uc.UserId == userId).FirstOrDefault()?.PastActivities.Add(a);
        }
    }
}
