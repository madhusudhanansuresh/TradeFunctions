using System;
using System.Text.Json.Serialization;

namespace TradeFunctions.AddOrRemoveWatchlist
{
    public class AddOrRemoveWatchlistRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("tickerName")]
        public string TickerName { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }
    }
}