using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TradeFunctions.Models.Helpers;

namespace TradeFunctions.Services
{
    public interface ITwelveDataService
    {
        Task<StockDataResponse> FetchStockDataAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize, MethodContainer methodContainer);
    }
    public class TwelveDataService : ITwelveDataService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey = "c0ab0a8407ed42e4a89605bc8077e141"; // Securely retrieve this
        public TwelveDataService()
        {
            _client = new HttpClient();
        }

        public async Task<StockDataResponse> FetchStockDataAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize, MethodContainer methodContainer)
        {
            var requestUri = $"https://api.twelvedata.com/complex_data?apikey={_apiKey}";

            var transformedMethods = methodContainer.Methods.Select(method =>
                {
                    if (method is SimpleMethod simpleMethod)
                    {
                        return (object)simpleMethod.GetName();
                    }
                    else if (method is ComplexMethod complexMethod)
                    {
                        return new { name = complexMethod.GetName(), complexMethod.Parameters };
                    }
                    else
                    {
                        throw new InvalidOperationException("Unknown method type.");
                    }
                }).ToList();

            var payload = new
            {
                symbols,
                intervals,
                start_date = startDate,
                end_date = endDate,
                outputsize = outputSize,
                methods = transformedMethods //new[] { "time_series" }
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