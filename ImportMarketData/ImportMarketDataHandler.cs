using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssessmentDeck.Services;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeFunctions.Models.Helpers;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.ImportMarketData
{
    public interface IImportMarketDataHandler
    {
        Task<bool> ImportMarketData(CancellationToken cancellationToken = default);
    }


    public class ImportMarketDataHandler : IImportMarketDataHandler
    {
        private readonly ILogger _logger;
        private readonly ITwelveDataService _twelveDataService;
        private readonly IPushoverService _pushOverService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportMarketDataHandler(ILoggerFactory loggerFactory, IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService, IPushoverService pushoverService)
        {
            _logger = loggerFactory.CreateLogger<ImportMarketDataHandler>();
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
            _pushOverService = pushoverService;
        }

        public async Task<bool> ImportMarketData(CancellationToken cancellationToken = default)
        {
            try
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new SimpleMethod("time_series"));
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var timeFrame = "15min";

                    var tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync();

                    var tickerNames = tickers.Select(x => x.TickerName).ToList();

                    var startDate = GetRoundedTime();
                    _logger.LogInformation($"startDate: {startDate}");

                    var endDate = GetRoundedTime();
                    _logger.LogInformation($"endDate: {endDate}");

                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickerNames, [timeFrame], startDate, endDate, 1, methodContainer);

                    var chartId = await dbContext.ChartPeriods.Where(x => x.TimeFrame == timeFrame).Select(x => x.Id).FirstOrDefaultAsync();

                    foreach (var stockData in stockDataResponse.Data)
                    {
                        var tickerId = tickers.Where(x => x.TickerName == stockData?.Meta?.Symbol).Select(x => x.Id).FirstOrDefault();
                        if (stockData == null)
                        {
                            _logger.LogWarning("Encountered a null stockData in the collection.");
                            continue; // Skip this iteration.
                        }

                        if (stockData.Values == null)
                        {
                            _logger.LogWarning("Values in stockData is null. Meta: {Meta}", stockData.Meta);
                            continue; // Skip this iteration.
                        }
                        foreach (var value in stockData.Values)
                        {
                            var stockPrice = MapToStockPrice(value, stockData.Meta, tickerId, chartId);
                            dbContext.StockPrices.Add(stockPrice);
                        }
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    if (tickers.Count != stockDataResponse.Data.Count)
                    {
                        _pushOverService.SendNotificationAsync($"Scheduled Time series import failed Total count: {tickers.Count} not matching retrieved count {stockDataResponse.Data.Count}", "Failure - Time Series Import", "", "", "1");
                        _logger.LogInformation("Scheduled Time series import failed Total count: {tickers.Count} not matching retrieved count {stockDataResponse.Data.Count}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {

                Console.WriteLine("An error occurred: " + ex.Message);

                return false;
            }
        }

        private StockPrice MapToStockPrice(ValueData valueData, MetaData metaData, int? tickerId, int chartId)
        {
            return new StockPrice
            {
                TickerId = tickerId,
                ChartId = chartId,
                TransactionCount = 0,
                Vwap = 0,
                ClosePrice = decimal.Parse(valueData.Close),
                HighPrice = decimal.Parse(valueData.High),
                LowPrice = decimal.Parse(valueData.Low),
                OpenPrice = decimal.Parse(valueData.Open),
                TradingVolume = decimal.Parse(valueData.Volume),
                Timestamp = valueData.Datetime
            };
        }

        public static string GetRoundedTime()
        {
            TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime nowEst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);

            int minutesToSubtract = nowEst.Minute % 15;
            DateTime roundedTimeEst = nowEst.AddMinutes(-minutesToSubtract).AddSeconds(-nowEst.Second);

            roundedTimeEst = roundedTimeEst.AddMinutes(-15);

            return roundedTimeEst.ToString("yyyy-MM-dd HH:mm:00");
        }


    }
}