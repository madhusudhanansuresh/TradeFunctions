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

        // public async Task<StockDataResponse> FetchStockDataAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize, MethodContainer methodContainer)
        // {
        //     var requestUri = $"https://api.twelvedata.com/complex_data?apikey={_apiKey}";

        //      _client.Timeout = TimeSpan.FromSeconds(300);

        //     var transformedMethods = methodContainer.Methods.Select(method =>
        //         {
        //             if (method is SimpleMethod simpleMethod)
        //             {
        //                 return (object)simpleMethod.GetName();
        //             }
        //             else if (method is ComplexMethod complexMethod)
        //             {
        //                 return new { name = complexMethod.GetName(), complexMethod.Parameters };
        //             }
        //             else
        //             {
        //                 throw new InvalidOperationException("Unknown method type.");
        //             }
        //         }).ToList();

        //     var payload = new
        //     {
        //         symbols,
        //         intervals,
        //         start_date = startDate,
        //         end_date = endDate,
        //         outputsize = outputSize,
        //         methods = transformedMethods //new[] { "time_series" }
        //     };

        //     var data = JsonContent.Create(payload);

        //     var request = new HttpRequestMessage
        //     {
        //         Method = HttpMethod.Post,
        //         RequestUri = new Uri(requestUri),
        //         Content = data
        //     };

        //     using (var response = await _client.SendAsync(request))
        //     {
        //         response.EnsureSuccessStatusCode();
        //         var body = await response.Content.ReadAsStringAsync();

        //         // Deserialize the JSON response into a StockDataResponse object
        //         var stockDataResponse = JsonConvert.DeserializeObject<StockDataResponse>(body);
        //         return stockDataResponse;
        //     }
        // }

        public async Task<StockDataResponse> FetchStockDataAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize, MethodContainer methodContainer)
        {
            var requestUri = $"https://api.twelvedata.com/complex_data?apikey={_apiKey}";
            _client.Timeout = TimeSpan.FromSeconds(300);

            // Function to split the symbols into chunks
            static List<List<T>> SplitList<T>(List<T> items, int size = 5)
            {
                var list = new List<List<T>>();
                for (int i = 0; i < items.Count; i += size)
                {
                    list.Add(items.GetRange(i, Math.Min(size, items.Count - i)));
                }
                return list;
            }

            var symbolChunks = SplitList(symbols, 5);
            var tasks = symbolChunks.Select(chunk => SendRequestAsync(chunk, intervals, startDate, endDate, outputSize, methodContainer, requestUri)).ToList();

            var batchResponses = await Task.WhenAll(tasks);

            // Aggregate all StockData into a single StockDataResponse
            var aggregatedResponse = new StockDataResponse
            {
                Data = new List<StockData>(),
                Status = "Success"
            };

            foreach (var response in batchResponses)
            {
                if (response.Data != null)
                {
                    aggregatedResponse.Data.AddRange(response.Data);
                }
                else
                {
                    // Handle the case where a batch request might not return data
                    aggregatedResponse.Status = "Partial Success";
                }
            }

            return aggregatedResponse;
        }

        private async Task<StockDataResponse> SendRequestAsync(List<string> symbols, List<string> intervals, string startDate, string endDate, int outputSize, MethodContainer methodContainer, string requestUri)
        {
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
                symbols = symbols,
                intervals = intervals,
                start_date = startDate,
                end_date = endDate,
                outputsize = outputSize,
                methods = transformedMethods
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
                return JsonConvert.DeserializeObject<StockDataResponse>(body);
            }
        }


    }
}