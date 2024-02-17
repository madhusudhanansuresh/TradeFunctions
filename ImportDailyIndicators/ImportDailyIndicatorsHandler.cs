using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Helpers;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ImportDailyIndicators
{
    public interface IImportDailyIndicatorsHandler
    {
        Task<bool> ImportATR(CancellationToken cancellationToken = default);
    }

    public class ImportDailyIndicatorsHandler : IImportDailyIndicatorsHandler
    {
        private readonly ILogger _logger;

        private readonly ITwelveDataService _twelveDataService;
        private readonly IPushoverService _pushOverService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportDailyIndicatorsHandler(ILoggerFactory loggerFactory, IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService, IPushoverService pushoverService)
        {
            _logger = loggerFactory.CreateLogger<ImportDailyIndicatorsHandler>();
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
            _pushOverService = pushoverService;
        }

        public async Task<bool> ImportATR(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting ImportATR operation.");
            try
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new ComplexMethod("ATR", new Dictionary<string, object> { { "time_period", 14 } }));

                _logger.LogInformation("Fetching connection string.");
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var timeFrame = "1day";

                    var tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync();

                    var tickerNames = tickers.Select(x => x.TickerName).ToList();

                    _logger.LogInformation($"Fetching stock data for tickers: {string.Join(", ", tickers)}.");
                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickerNames, [timeFrame], "", "", 1, methodContainer);


                    dbContext.DailyIndicators.RemoveRange(dbContext.DailyIndicators);
                    dbContext.SaveChanges();

                    // Assuming stockDataResponse or its properties could be null.
                    if (stockDataResponse?.Data == null)
                    {
                        _logger.LogWarning("stockDataResponse or its Data is null.");
                        return false; // Or handle accordingly.
                    }

                    foreach (var stockData in stockDataResponse?.Data)
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
                        foreach (var value in stockData?.Values)
                        {
                            var dailyIndicator = MapToStockPrice(value, stockData.Meta, tickerId);
                            dbContext.DailyIndicators.Add(dailyIndicator);
                        }
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);

                    if (tickers.Count != stockDataResponse.Data.Count || stockDataResponse.Data.Any(x => x.Values[0].ATR <= 0))
                    {
                        _pushOverService.SendNotificationAsync("Scheduled Atr Import failed", "Failure - Atr Import", "", "", "1");
                        _logger.LogInformation("Issue in importing ATR");
                    }

                    _logger.LogInformation("ImportATR operation completed successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the ImportATR operation.");
                return false;
            }
        }


        private DailyIndicator MapToStockPrice(ValueData valueData, MetaData metaData, int tickerId)
        {
            return new DailyIndicator
            {
                TickerId = tickerId,
                Atr = valueData?.ATR,
                Timestamp = valueData?.Datetime
            };
        }
    }
}