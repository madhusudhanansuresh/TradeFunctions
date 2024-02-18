using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace TradeFunctions.AddOrRemoveWatchlist
{
    public class AddOrRemoveWatchlistResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}