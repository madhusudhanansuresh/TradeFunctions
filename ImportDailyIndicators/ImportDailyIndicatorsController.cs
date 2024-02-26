using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ImportDailyIndicators
{
    public class ImportDailyIndicatorsController
    {
        private readonly ILogger _logger;

        private readonly IImportDailyIndicatorsHandler _importDailyIndicatorsHandler;

        public ImportDailyIndicatorsController(ILoggerFactory loggerFactory, IImportDailyIndicatorsHandler importDailyIndicatorsHandler)
        {
            _logger = loggerFactory.CreateLogger<ImportDailyIndicatorsController>();
            _importDailyIndicatorsHandler = importDailyIndicatorsHandler;
        }

        [Function("ImportATR")]
        public async Task Run([TimerTrigger("0 13 * * 1-5")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estDateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);

            string dateTimeString = estDateTime.ToString("yyyy-MM-dd HH:mm:ss");

            await _importDailyIndicatorsHandler.ImportATR(dateTimeString.Substring(0, 10) + " 00:00:00");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
