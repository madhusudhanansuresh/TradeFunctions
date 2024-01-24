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
        public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            _importMarketData.ImportMarketData();
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
