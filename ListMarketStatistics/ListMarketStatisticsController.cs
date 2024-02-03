using System;
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
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

           var data = await _listMarketStatisticsHandler.ListStatistics();

            return new OkObjectResult(data);
        }
    }
}
