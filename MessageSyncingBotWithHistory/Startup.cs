// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MessageSyncingBotWithHistory.Bots;
using MessageSyncingBotWithHistory.Dialogs.Root;
using MessageSyncingBotWithHistory.Helpers;
using Microsoft.Extensions.Configuration;
using MessageSyncingBotWithHistory.Middleware;

namespace MessageSyncingBotWithHistory
{
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

                adpt.Use(new ConversationSynchronizationMiddleware(ucsp, adpt, _configuration));

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
