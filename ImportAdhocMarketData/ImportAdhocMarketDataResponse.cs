using System.Text.Json.Serialization;

namespace TradeFunctions.ImportMarketData
{
    public class ImportAdhocMarketDataResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

    }
}