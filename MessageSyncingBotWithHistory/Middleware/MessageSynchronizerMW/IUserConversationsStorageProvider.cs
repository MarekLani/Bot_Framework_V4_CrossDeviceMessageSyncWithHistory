using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MessageSyncingBotWithHistory.Middleware
{

    /// <summary>
    /// This interface should be implemented when you create your own User Conversation storage 
    /// It should be defined in a way you are capable to map conversations and conversations references to specific user.
    /// </summary>
    public interface IUserConversationsStorageProvider
    {
        void AddConvId(string userId, string convId);
        string GetUserConversationId(string userId);
        bool HasUser(string userId);

        void LogActivity(IActivity a, string userId);

        List<IActivity> GetActivities(string userId);
    }
    
}
