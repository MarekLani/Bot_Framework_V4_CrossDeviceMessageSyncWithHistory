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
            try
            {
                IDatabase db = redis.GetDatabase();
                db.StringSet(String.Format(userConvKey, userId), "");
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
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
