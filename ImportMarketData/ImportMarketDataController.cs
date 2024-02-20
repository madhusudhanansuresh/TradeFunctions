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
        public async Task Run([TimerTrigger("40 */15 13-21 * * 1-5")] TimerInfo myTimer)
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);
            _logger.LogInformation($"C# Timer trigger function executed at EST: {estTime}");
            
            // Check if current EST time is within the desired range (9:30 AM to 4:00 PM)
            if ((estTime.Hour == 9 && estTime.Minute >= 30) || (estTime.Hour > 9 && estTime.Hour < 16) || (estTime.Hour == 16 && estTime.Minute == 0))
            {
                await _importMarketData.ImportMarketData();
            }
            else
            {
                _logger.LogInformation($"Skipping execution, outside schedule hours EST: {estTime}");
            }


            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at EST: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
