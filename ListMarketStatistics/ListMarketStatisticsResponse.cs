namespace TradeFunctions.ListMarketStatistics
{
    public class ListMarketStatisticsResponse
    {
        public string Ticker { get; set; }
        public decimal? ATR { get; set; }
        public Statistics FiveMin { get; set; }
        public Statistics TenMin { get; set; }
        public Statistics FifteenMin { get; set; }
        public Statistics TwentyMin { get; set; }
        public Statistics TwentyFiveMin { get; set; }
        public Statistics ThirtyMin { get; set; }
        public Statistics FortyFiveMin { get; set; }
        public Statistics OneHour { get; set; }
        public Statistics TwoHour { get; set; }
        public Statistics ThreeHour { get; set; }
        public Statistics FourHour { get; set; }
        public Statistics FiveHour { get; set; }
        public Statistics SixHour { get; set; }
        public Statistics SevenHour { get; set; }
    }

    public class Statistics
    {
        public decimal? Rvol { get; set; }
        public decimal? RsRw { get; set; }
    }
}