using Newtonsoft.Json;

namespace TradeFunctions.ListMarketStatistics
{
    public class ListMarketStatisticsResponse
    {
        [JsonProperty("listMarketStatistics")]
        public List<MarketStatistics> ListMarketStatistics { get; set; }
    }

    public class MarketStatistics
    {
        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        [JsonProperty("atr")]
        public decimal? ATR { get; set; }

        [JsonProperty("price")]
        public decimal? Price { get; set; }

        [JsonProperty("fifteenMin")]
        public Statistics FifteenMin { get; set; }

        [JsonProperty("thirtyMin")]
        public Statistics ThirtyMin { get; set; }

        [JsonProperty("oneHour")]
        public Statistics OneHour { get; set; }

        [JsonProperty("twoHour")]
        public Statistics TwoHour { get; set; }

        [JsonProperty("fourHour")]
        public Statistics FourHour { get; set; }
    }

    public class Statistics
    {
        [JsonProperty("rvol")]
        public decimal? Rvol { get; set; }

        [JsonProperty("rsRw")]
        public decimal? RsRw { get; set; }
    }

}