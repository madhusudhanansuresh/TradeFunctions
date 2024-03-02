using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ImportMarketData
{
    public class ImportHistoricalDataController
    {
        private readonly ILogger _logger;
        private readonly IImportAdhocMarketDataHandler _importMarketData;
        public ImportHistoricalDataController(ILoggerFactory loggerFactory, IImportAdhocMarketDataHandler importMarketData)
        {
            _logger = loggerFactory.CreateLogger<ImportHistoricalDataController>();
            _importMarketData = importMarketData;
        }

        [Function("ImportHistoricalDataController")]
        public async Task Run([TimerTrigger("0 * 9 * * 0,6")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var startDate = GetStartDate23DaysBackEST();
            var endDate = GetRecent15MinuteMarkEST();

            var importRequest = new ImportAdhocMarketDataRequest
            {
                Symbols = new List<string>(),
                Intervals = new List<string> { "15min" },
                StartDate = startDate,
                EndDate = endDate
            };

            _logger.LogInformation($"start time: {startDate} and endDate: {endDate}");

            await _importMarketData.ImportMarketData(importRequest);

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }

        private static string GetStartDate23DaysBackEST()
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentTimeEST = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);

            DateTime startDateEST = currentTimeEST.AddDays(-30);
            startDateEST = new DateTime(startDateEST.Year, startDateEST.Month, startDateEST.Day, 9, 30, 0);

            return startDateEST.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string GetRecent15MinuteMarkEST()
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentTimeEST = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);

            int minutes = currentTimeEST.Minute / 15 * 15;
            DateTime roundedTimeEST = new DateTime(currentTimeEST.Year, currentTimeEST.Month, currentTimeEST.Day, currentTimeEST.Hour, minutes, 0);

            return roundedTimeEST.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
