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
        public void Run([TimerTrigger("0 */5 13-21 * * 1-5")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);


            // Check if current EST time is within the desired range (8:00 AM to 5:00 PM)
            if (estTime.Hour >= 8 && estTime.Hour < 17)
            {
                _importMarketData.ImportMarketData();
            }
            else
            {
                _logger.LogInformation($"Skipping execution, outside schedule hours EST: {estTime}");
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
