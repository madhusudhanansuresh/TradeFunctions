using System;

namespace TradeFunctions.ImportMarketData
{
    public class ImportAdhocMarketDataRequest
    {
        public List<string> Symbols { get; set; }
        public List<string> Intervals { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}