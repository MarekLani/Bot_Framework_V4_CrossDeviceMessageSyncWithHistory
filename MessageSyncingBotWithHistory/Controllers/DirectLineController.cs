using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MessageSyncingBotWithHistory.Helpers;
using MessageSyncingBotWithHistory.Middleware;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace MessageSyncingBotWithHistory.Controllers
{
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
            //Add Trusted Origin test
            //const origin = req.header('origin');
            //if (!trustedOrigin(origin))
            //{
            //    return res.send(403, 'not trusted origin');
            //}
            StringValues token;
            string res;
            
            try
            {
                res =  await GenerateDirectLineToken(user.UserId);
                //If we would not be using Web Chat there would be need for token renewal logic, web chat handles it by itself
                //if (Request.Query.TryGetValue("token", out token))
                //{
                //    res = await RenewDirectLineToken(token);
                //}
                //else
                //{
                //    res = await GenerateDirectLineToken(user.UserId);
                //}
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
            //var token = string.Empty;
            var clnt = new HttpClient();
            clnt.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["DirectLineSecret"]);

            HttpResponseMessage rsp;
            string convId = "";

            if (userConversationStorageProvider.HasUser(userId))
            {
                convId = userConversationStorageProvider.GetUserConversationId(userId);
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
    
                return JsonConvert.SerializeObject(obj);
            }
            return "";
        }

        private async Task<string> RenewDirectLineToken(string token)
        {
            //var token = string.Empty;
            var clnt = new HttpClient();
            clnt.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration["DirectLineSecret"]);

            var rsp = await clnt.PostAsync("https://directline.botframework.com/v3/directline/tokens/refresh", new StringContent(string.Empty, Encoding.UTF8, "application/json"));

            if (rsp.IsSuccessStatusCode)
            {
                var str = rsp.Content.ReadAsStringAsync().Result;
                //var obj = JsonConvert.DeserializeObject<DirectlineResponse>(str);
                //token = obj.token;
                return str;
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
}