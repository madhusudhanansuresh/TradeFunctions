using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ImportMarketData
{
    public class ImportMarketDataController
    {
        private readonly ILogger _logger;

        private readonly IImportMarketDataHandler _importMarketData;

        public ImportMarketDataController(ILoggerFactory loggerFactory, IImportMarketDataHandler importMarketData)
        {
            _logger = loggerFactory.CreateLogger<ImportMarketDataController>();
            _importMarketData = importMarketData;
        }

        [Function("ImportMarketData")]
        public async Task Run([TimerTrigger("0 */5 9-16 * * 1-5")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at EST: {DateTime.Now}");

            if (DateTime.Now.Hour == 9 && DateTime.Now.Minute < 35)
            {
                _logger.LogInformation($"Skipping execution, outside schedule hours EST: {DateTime.Now}");
                return;
            }

            if (DateTime.Now.Hour >= 16)
            {
                _logger.LogInformation("Skipping execution: After 4:00 PM");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(30));
            await _importMarketData.ImportMarketData();

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at EST: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
