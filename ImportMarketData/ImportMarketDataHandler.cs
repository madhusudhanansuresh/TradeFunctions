using AssessmentDeck.Services;
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
                await ProcessFailedRetries();

                List<string> retryStockList = new List<string>();
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new SimpleMethod("time_series"));
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var removeCount = 0;
                    var timeFrame = "5min";

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
                            continue; 
                        }

                        if (stockData.Values == null)
                        {
                            retryStockList.Add(stockData.Meta.Symbol);
                            _logger.LogWarning("Values in stockData is null. Meta: {Meta}", stockData.Meta);
                            continue; 
                        }
                        foreach (var value in stockData.Values)
                        {
                            var stockPrice = MapToStockPrice(value, stockData.Meta, tickerId, chartId);
                            dbContext.StockPrices.Add(stockPrice);
                        }
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);

                    if (retryStockList.Count > 0)
                    {   
                        string tickerNamesConcatenated = String.Join(", ", retryStockList);
                        await _pushOverService.SendNotificationAsync($"Failed to pull data. Tickers: {tickerNamesConcatenated}", "Failure - Time Series Import", "", "", "1");                      
                        await RetryFailedStocks(tickers, retryStockList, startDate, endDate, 1, methodContainer);
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

            int minutesToSubtract = nowEst.Minute % 5;
            DateTime roundedTimeEst = nowEst.AddMinutes(-minutesToSubtract).AddSeconds(-nowEst.Second);

            roundedTimeEst = roundedTimeEst.AddMinutes(-5);

            return roundedTimeEst.ToString("yyyy-MM-dd HH:mm:00");
        }

        public async Task ProcessFailedRetries(CancellationToken cancellationToken = default)
        {
            List<RetryFailed> failedRetries;
            using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
            {
                failedRetries = await dbContext.RetryFaileds.Include(r => r.Ticker).ToListAsync(cancellationToken);
            }

            if (failedRetries.Count > 0)
            {
                var methodContainer = new MethodContainer();
                methodContainer.AddMethod(new SimpleMethod("time_series"));

                foreach (var failedRetry in failedRetries)
                {
                    try
                    {
                        var tickerNames = new List<string> { failedRetry.Ticker.TickerName };
                        var startDate = failedRetry.Timestamp?.ToString("yyyy-MM-dd HH:mm:00");
                        var endDate = failedRetry.Timestamp?.ToString("yyyy-MM-dd HH:mm:00");

                        bool isSuccessful = await RetryFailedStocks(new List<Ticker> { failedRetry.Ticker }, tickerNames, startDate, endDate, 1, methodContainer);

                        if (isSuccessful)
                        {
                            using (var dbDeleteContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                            {
                                dbDeleteContext.RetryFaileds.Remove(failedRetry);
                                await dbDeleteContext.SaveChangesAsync(cancellationToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to process retry for TickerId {failedRetry.TickerId}: {ex.Message}");
                    }
                }
            }
        }

        public async Task<bool> RetryFailedStocks(List<Ticker> tickers, List<string> tickerNames, string startDate, string endDate, int outputSize, MethodContainer methodContainer)
        {
            int retryCount = 0;
            int maxRetries = 1;
            bool isSuccessful = false;

            while (retryCount < maxRetries && !isSuccessful)
            {
                try
                {
                    var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickerNames, new List<string> { "5min" }, startDate, endDate, outputSize, methodContainer);

                    bool dataIsValid = true;

                    using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                    {
                        var chartId = await dbContext.ChartPeriods.Where(x => x.TimeFrame == "5min").Select(x => x.Id).FirstOrDefaultAsync();

                        foreach (var stockData in stockDataResponse.Data)
                        {
                            if (stockData == null || stockData.Values == null)
                            {
                                dataIsValid = false;
                                _logger.LogWarning($"Encountered invalid stockData during retry. Null data: {stockData == null}, Null values: {stockData?.Values == null}");
                                break;
                            }
                            else
                            {
                                var tickerId = tickers.FirstOrDefault(x => x.TickerName == stockData.Meta.Symbol)?.Id;
                                foreach (var value in stockData.Values)
                                {
                                    var stockPrice = MapToStockPrice(value, stockData.Meta, tickerId, chartId);
                                    dbContext.StockPrices.Add(stockPrice);
                                }
                            }
                        }

                        if (dataIsValid)
                        {
                            await dbContext.SaveChangesAsync();
                            isSuccessful = true;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Attempt {retryCount + 1} failed: {ex.Message}");
                }
                finally
                {
                    retryCount++;
                    if (!isSuccessful && retryCount < maxRetries)
                    {
                        await Task.Delay(30000);
                    }
                }
            }
            if (!isSuccessful)
            {
                string tickerNamesConcatenated = String.Join(", ", tickerNames);
                _logger.LogError($"Failed to complete operation after retries due to invalid stock data. Tickers for time period: {startDate} and stocks: {tickerNamesConcatenated}.");
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    foreach (var tickerName in tickerNames)
                    {
                        var tickerFromTable = dbContext.Tickers.FirstOrDefault(t => t.TickerName == tickerName);
                        if (tickerFromTable != null)
                        {
                            DateTime startDateTime = Convert.ToDateTime(startDate);

                            bool recordExists = dbContext.RetryFaileds.Any(rf => rf.TickerId == tickerFromTable.Id && rf.Timestamp == startDateTime);
                            if (!recordExists)
                            {
                                var retryFailedRecord = new RetryFailed
                                {
                                    TickerId = tickerFromTable.Id,
                                    Timestamp = startDateTime,
                                    Reason = "Failed to fetch or process stock data after 3 retries",
                                    CreateDt = DateTime.UtcNow,
                                    LastUpdateDt = DateTime.UtcNow
                                };

                                dbContext.RetryFaileds.Add(retryFailedRecord);
                            }
                            else
                            {
                                _logger.LogInformation($"Skipped insertion for Ticker ID {tickerFromTable.Id} with Timestamp {startDateTime} as it already exists.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Ticker not found: {tickerName}");
                        }
                    }

                    try
                    {
                        int insertedRecords = await dbContext.SaveChangesAsync();
                        _logger.LogInformation($"Successfully inserted {insertedRecords} records into RetryFailed table.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to insert records into RetryFailed table: {ex.Message}");
                    }
                }
                return false;
            }

            return isSuccessful;
        }

    }
}