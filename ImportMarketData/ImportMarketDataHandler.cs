using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssessmentDeck.Services;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.EntityFrameworkCore;
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
        private readonly ITwelveDataService _twelveDataService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportMarketDataHandler(IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService)
        {
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
        }

        public async Task<bool> ImportMarketData(CancellationToken cancellationToken = default)
        {
            try
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new SimpleMethod("time_series"));
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var timeFrame = "5min";
                    var tickers = await dbContext.Tickers.Select(x => x.TickerName).Take(1).ToListAsync();

                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(["AAPL"], [timeFrame], "", "", 1, methodContainer);
                    
                    var tickerId = await dbContext.Tickers.Where(x => x.TickerName == "AAPL").Select(x => x.Id).FirstOrDefaultAsync();
                    var chartId = await dbContext.ChartPeriods.Where(x => x.TimeFrame == timeFrame).Select(x => x.Id).FirstOrDefaultAsync();

                    foreach (var stockData in stockDataResponse.Data)
                    {
                        foreach (var value in stockData.Values)
                        {
                            var stockPrice = MapToStockPrice(value, stockData.Meta, tickerId, chartId);
                            dbContext.StockPrices.Add(stockPrice);
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

        private StockPrice MapToStockPrice(ValueData valueData, MetaData metaData, int? tickerId, int chartId)
        {
            return new StockPrice
            {
                // Assuming TickerId and ChartId are determined separately
                TickerId = tickerId,
                ChartId = chartId,
                TransactionCount = 0,
                Vwap = 0,                
                ClosePrice = decimal.Parse(valueData.Close),
                HighPrice = decimal.Parse(valueData.High),
                LowPrice = decimal.Parse(valueData.Low),
                OpenPrice = decimal.Parse(valueData.Open),
                TradingVolume = decimal.Parse(valueData.Volume),
               // Timestamp = Convert.ToDateTime(valueData.Datetime),
                Timestamp = valueData.Datetime
            };
        }
    }
}