using System;
using System.Text.Json.Serialization;

namespace TradeFunctions.ListMarketStatistics
{
    public class ListMarketStatisticsRequest
    {
         [JsonPropertyName("endDateTime")]
        public string EndDateTime { get; set; }
    }   
}