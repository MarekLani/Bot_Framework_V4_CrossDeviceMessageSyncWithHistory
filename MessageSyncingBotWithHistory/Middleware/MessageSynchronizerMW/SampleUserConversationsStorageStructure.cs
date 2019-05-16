using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MessageSyncingBotWithHistory.Middleware
{
    public class SampleUserConversationsStorageStructure
    {
        public SampleUserConversationsStorageStructure(string userId)
        {
            UserId = userId;
            this.PastActivities = new List<IActivity>();
        }

        public SampleUserConversationsStorageStructure(string userId, string conversationId)
        {
            this.ConversationId = conversationId;
            this.UserId = userId;
            this.PastActivities = new List<IActivity>();
        }

        public string UserId { get; }

        public string ConversationId { get; set;  }

        public List<IActivity> PastActivities { get; }

        
        
    }
}
