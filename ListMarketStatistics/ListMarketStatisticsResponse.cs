using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace TradeFunctions.ListMarketStatistics
{
    public class ListMarketStatisticsResponse
    {
        [JsonPropertyName("listMarketStatistics")]
        public List<MarketStatistics> ListMarketStatistics { get; set; }
        
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class MarketStatistics
    {
        [JsonPropertyName("ticker")]
        public string Ticker { get; set; }

        [JsonPropertyName("atr")]
        public decimal? ATR { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("fifteenMin")]
        public Statistics FifteenMin { get; set; }

        [JsonPropertyName("thirtyMin")]
        public Statistics ThirtyMin { get; set; }

        [JsonPropertyName("oneHour")]
        public Statistics OneHour { get; set; }

        [JsonPropertyName("twoHour")]
        public Statistics TwoHour { get; set; }

        [JsonPropertyName("fourHour")]
        public Statistics FourHour { get; set; }

        [JsonPropertyName("timeStamp")]
        public DateTime? Timestamp { get; set; }
    }

    public class Statistics
    {
        [JsonPropertyName("rvol")]
        public decimal? Rvol { get; set; }

        [JsonPropertyName("rsrw")]
        public decimal? RsRw { get; set; }
    }

}