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
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportDailyIndicatorsHandler(ILoggerFactory loggerFactory, IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService)
        {
            _logger = loggerFactory.CreateLogger<ImportDailyIndicatorsController>();
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
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
                    _logger.LogInformation("Retrieving tickers.");
                    var tickers = await dbContext.Tickers.Select(x => x.TickerName).Take(55).ToListAsync();

                    _logger.LogInformation($"Fetching stock data for tickers: {string.Join(", ", tickers)}.");
                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickers, [timeFrame], "", "", 1, methodContainer);

                    _logger.LogInformation("Processing stock data response.");
                    var tickerId = await dbContext.Tickers.Where(x => x.TickerName == "AAPL").Select(x => x.Id).FirstOrDefaultAsync();

                    dbContext.DailyIndicators.RemoveRange(dbContext.DailyIndicators);
                    dbContext.SaveChanges();

                    foreach (var stockData in stockDataResponse.Data)
                    {
                        foreach (var value in stockData.Values)
                        {
                            var dailyIndicator = MapToStockPrice(value, stockData.Meta, tickerId);
                            dbContext.DailyIndicators.Add(dailyIndicator);
                        }
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
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
                Atr = valueData.ATR,
                Timestamp = valueData.Datetime
            };
        }
    }
}