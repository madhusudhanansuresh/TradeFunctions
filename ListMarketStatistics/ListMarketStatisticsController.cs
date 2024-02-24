using System;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ListMarketStatistics
{
    public class ListMarketStatisticsController
    {
        private readonly ILogger _logger;

        private readonly IListMarketStatisticsHandler _listMarketStatisticsHandler;

        public ListMarketStatisticsController(ILoggerFactory loggerFactory, IListMarketStatisticsHandler listMarketStatisticsHandler)
        {
            _logger = loggerFactory.CreateLogger<ListMarketStatisticsController>();
            _listMarketStatisticsHandler = listMarketStatisticsHandler;
        }

        [Function("marketStatistics")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
             string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var listMarketStatisticsRequest = JsonSerializer.Deserialize<ListMarketStatisticsRequest>(requestBody);
            var data = await _listMarketStatisticsHandler.ListStatistics(listMarketStatisticsRequest);
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var jsonResponse = JsonSerializer.Serialize(data);
            response.WriteString(jsonResponse);


            return response;
        }
    }
}
