using System.Text.Json.Serialization;

namespace TradeFunctions.ListWatchlist
{
    public class ListWatchlistResponse
    {
        [JsonPropertyName("watchlist")]
        public List<WatchlistItem> Watchlist { get; set; }
        
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
    public class WatchlistItem
    {
        [JsonPropertyName("tickerName")]
        public string TickerName { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime? LastUpdated { get; set; }
    }
}