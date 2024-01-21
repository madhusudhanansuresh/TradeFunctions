using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TradeFunctions.Models.Helpers;

namespace TradeFunctions.Services
{
    public class TwelveDataService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey = "U1BBcIJmO2FU20QpAz820E6HXHcwipK3"; // Securely retrieve this
        public TwelveDataService()
        {
            _client = new HttpClient();
        }

        public async Task<StockDataResponse> FetchStockDataAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize)
        {
            var requestUri = $"https://api.twelvedata.com/complex_data?apikey={_apiKey}";

            var payload = new
            {
                symbols,
                intervals,
                start_date = startDate,
                end_date = endDate,
                outputsize = outputSize,
                methods = new[] { "time_series" }
            };

            var data = JsonContent.Create(payload);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(requestUri),
                Content = data
            };

            using (var response = await _client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON response into a StockDataResponse object
                var stockDataResponse = JsonConvert.DeserializeObject<StockDataResponse>(body);
                return stockDataResponse;
            }
        }

    }
}