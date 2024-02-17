using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TradeFunctions.Services
{
    public interface IPushoverService
    {
        Task<bool> SendNotificationAsync(string message, string title = null, string url = null, string urlTitle = null, string priority = null);
    }

    public class PushoverService : IPushoverService
    {
        private readonly HttpClient _client;
        private readonly string _apiToken = "afu7pg72mfogf9jec1nxzdcjmbcqar";
        private readonly string _userKey = "usqyfbnudv4ow1ffyhimsupww179nh";

        public PushoverService()
        {
            _client = new HttpClient();
        }

        public async Task<bool> SendNotificationAsync(string message, string title = null, string url = null, string urlTitle = null, string priority = null)
        {
            var requestUri = "https://api.pushover.net/1/messages.json";

            var payload = new
            {
                token = _apiToken,
                user = _userKey,
                message = message,
                title = title,       // Optional
                url = url,           // Optional
                url_title = urlTitle,// Optional
                priority = priority  // Optional
            };

            var data = JsonContent.Create(payload);

            using (var response = await _client.PostAsync(requestUri, data))
            {
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.Error.WriteLine($"Failed to send Pushover notification: {errorContent}");
                    return false;
                }
            }
        }
    }
}
