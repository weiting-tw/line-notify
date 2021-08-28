using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace line_notify.Controllers
{
    [Route("/line")]
    public class LineController : Controller
    {
        private static Dictionary<string, string> TestDatabase { get; set; } = new Dictionary<string, string>()
        {
        };
        private readonly ILogger<LineController> _logger;

        public LineController(ILogger<LineController> logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// 跳轉至指定使用者的LINE Notify授權畫面
        /// </summary>
        [HttpGet("{userId}/bind")]
        public IActionResult BindAsync(string userId)
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
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException($"'{nameof(code)}' 不得為 Null 或空白字元。", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException($"'{nameof(state)}' 不得為 Null 或空白字元。", nameof(state));
            }

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
            var response = await httpClient.PostAsync("https://notify-bot.line.me/oauth/token",
                new FormUrlEncodedContent(dictionary)).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                // 記錄使用者對應的access token
                var content = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                TestDatabase[userId] = (string)content.GetValue("access_token");
                return Content("綁定LINE Notify成功");
            }
            return Content("綁定LINE Notify失敗");
        }

        [HttpGet("{userId}/revoke")]
        public async Task<IActionResult> RevokeAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException($"'{nameof(userId)}' 不得為 Null 或空白字元。", nameof(userId));
            }

            using var content = new HttpRequestMessage(HttpMethod.Post, "https://notify-api.line.me/api/revoke")
            {
                Content = new StringContent(
                   string.Empty,
                   Encoding.UTF8,
                   "application/json")
            };
            content.Headers.TryAddWithoutValidation("Authorization", $"Bearer {TestDatabase[userId]}");
            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(content, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? Content("已成功解除 LINE Notify 綁定")
                : Content("解除 LINE Notify 綁定失敗");
        }

        [HttpGet("{userId}/message/{message}")]
        public async Task<IActionResult> SendMessageAsync(string userId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException($"'{nameof(message)}' 不得為 Null 或空白字元。", nameof(message));
            }
            var dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(JsonConvert.SerializeObject(new
            {
                message
            }));
            using var content = new HttpRequestMessage(HttpMethod.Post, "https://notify-api.line.me/api/notify")
            {
                Content = new FormUrlEncodedContent(dictionary)
            };
            content.Headers.TryAddWithoutValidation("Authorization", $"Bearer {TestDatabase[userId]}");
            var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(content, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? Content("已將訊息傳出")
                : Content("傳送失敗");
        }

    }
}