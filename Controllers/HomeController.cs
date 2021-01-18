using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace line_notify.Controllers
{
    [Route("/")]
    public class HomeController : Controller
    {
        private static Dictionary<string, string> TestDatabase { get; set; } = new Dictionary<string, string>()
        {
            ["tester"] = null
        };
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// 跳轉至指定使用者的LINE Notify授權畫面
        /// </summary>
        [HttpGet("bind")]
        public IActionResult Bind(string userId)
        {
            var lineAuthUrl = $"https://notify-bot.line.me/oauth/authorize";
            var authParameters = new Dictionary<string, string>()
            {
                ["response_type"] = "code", // 固定
                ["scope"] = "notify", // 固定
                ["client_id"] = LineNotifyConfig.ClientId, // 填寫自己的ClientId
            };

            // 取得並產生LINE Notify授權返回的網址
            var bindCallbackUrl = Url.ActionLink(nameof(BindCallback)); // 取得BindCallback這個Action的網址
            // 返回網址也要傳送出去
            authParameters["redirect_uri"] = bindCallbackUrl;

            // 狀態值，這個值可以用來防止CSRF攻擊，在本文用來防止返回網址的userId參數被變更(這裡只是簡單做)
            // 本文只是簡單的做一下HASH來檢查userId是否被竄改等等。
            // 這個值將再返回時附加在QueryString中
            authParameters["state"] = userId;

            // 組裝網址
            lineAuthUrl = QueryHelpers.AddQueryString(lineAuthUrl, authParameters);
            // 跳轉畫面到LINE Notify授權畫面
            return Redirect(lineAuthUrl);
        }

        /// <summary>
        /// LINE Notify授權畫面的返回目標
        /// </summary>
        /// <returns></returns>
        [HttpGet("bind-callback")]
        public async Task<IActionResult> BindCallback(string code, string state)
        {
            var userId = state;
            // 使用code取得access token
            var dictionary =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(new
                {
                    grant_type = "authorization_code",
                    code,
                    redirect_uri = Url.ActionLink(nameof(BindCallback)),
                    client_id = LineNotifyConfig.ClientId,
                    client_secret = LineNotifyConfig.ClientSecret
                }));
            var httpClient = new HttpClient();
            var tokenResult = await httpClient.PostAsync("https://notify-bot.line.me/oauth/token",
                new FormUrlEncodedContent(dictionary)).ConfigureAwait(false);
            // 記錄使用者對應的access token
            TestDatabase[userId] = await tokenResult.Content.ReadAsStringAsync().ConfigureAwait(false);
            return Content("綁定LINE Notify成功");
        }

        private static class LineNotifyConfig
        {
            public static string ClientId { get; set; }
            public static string ClientSecret { get; set; }
        }
    }
}