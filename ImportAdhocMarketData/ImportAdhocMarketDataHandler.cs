using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssessmentDeck.Services;
using EFCore.BulkExtensions;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeFunctions.Models.Helpers;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.ImportMarketData
{
    public interface IImportAdhocMarketDataHandler
    {
        Task<bool> ImportMarketData(ImportAdhocMarketDataRequest request, CancellationToken cancellationToken = default);
    }


    public class ImportAdhocMarketDataHandler : IImportAdhocMarketDataHandler
    {
        private readonly ILogger _logger;
        private readonly ITwelveDataService _twelveDataService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ImportAdhocMarketDataHandler(ILoggerFactory loggerFactory, IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService)
        {
            _logger = loggerFactory.CreateLogger<ImportMarketDataHandler>();
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
        }

        public async Task<bool> ImportMarketData(ImportAdhocMarketDataRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new SimpleMethod("time_series"));
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {

                    await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE trade.stock_price");
                    await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE trade.retry_failed");


                    var tickers = await dbContext.Tickers.AsNoTracking().Where(x => x.Active == true).ToListAsync(cancellationToken);
                    var tickerNames = tickers.Select(x => x.TickerName).ToList();
                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickerNames, request.Intervals, request.StartDate, request.EndDate, 5000, methodContainer);
                    var chartId = await dbContext.ChartPeriods.Where(x => x.TimeFrame == request.Intervals.FirstOrDefault()).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);

                    List<StockPrice> stockPricesToInsert = new List<StockPrice>();

                    foreach (var stockData in stockDataResponse.Data)
                    {
                        var tickerId = tickers.FirstOrDefault(x => x.TickerName == stockData?.Meta?.Symbol)?.Id;
                        if (stockData?.Values == null)
                        {
                            _logger.LogWarning($"Encountered invalid stockData. Null data: {stockData == null}, Null values: {stockData?.Values == null}");
                            continue;
                        }

                        foreach (var value in stockData.Values)
                        {
                            var stockPrice = MapToStockPrice(value, stockData.Meta, tickerId, chartId);
                            stockPricesToInsert.Add(stockPrice);
                        }
                    }

                    // Bulk insert new records
                    if (stockPricesToInsert.Any())
                    {
                        var bulkConfig = new BulkConfig { BulkCopyTimeout = 0, BatchSize = 4000 }; 
                        await dbContext.BulkInsertAsync(stockPricesToInsert, bulkConfig, cancellationToken: cancellationToken);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex}");
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
    }
}