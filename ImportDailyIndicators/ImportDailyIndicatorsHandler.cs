using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Helpers;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.ImportDailyIndicators
{
    public interface IImportDailyIndicatorsHandler
    {
        Task<bool> ImportATR(CancellationToken cancellationToken = default);
    }

    public class ImportDailyIndicatorsHandler : IImportDailyIndicatorsHandler
    {
        private readonly ITwelveDataService _twelveDataService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportDailyIndicatorsHandler(IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService)
        {
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
        }

        public async Task<bool> ImportATR(CancellationToken cancellationToken = default)
        {
            try
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new ComplexMethod("ATR", new Dictionary<string, object> { { "time_period", 14 } }));

                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var timeFrame = "1day";
                    var tickers = await dbContext.Tickers.Select(x => x.TickerName).Take(1).ToListAsync();

                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(["AAPL"], [timeFrame], "", "", 1, methodContainer);

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
                }

                return true;
            }
            catch (Exception ex)
            {

                Console.WriteLine("An error occurred: " + ex.Message);

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