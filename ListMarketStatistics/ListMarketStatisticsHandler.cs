using System.Collections.Concurrent;
using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.ImportDailyIndicators;

namespace TradeFunctions.ListMarketStatistics
{
    public interface IListMarketStatisticsHandler
    {
        Task<ListMarketStatisticsResponse> ListStatistics(ListMarketStatisticsRequest listMarketStatisticsRequest, CancellationToken cancellationToken = default);
    }

    public class ListMarketStatisticsHandler : IListMarketStatisticsHandler
    {
        private readonly IImportDailyIndicatorsHandler _importDailyIndicatorsHandler;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ListMarketStatisticsHandler(IDbConnectionStringService dbConnectionStringService, IImportDailyIndicatorsHandler importDailyIndicatorsHandler)
        {
            _dbConnectionStringService = dbConnectionStringService;
            _importDailyIndicatorsHandler = importDailyIndicatorsHandler;
        }

        public async Task<ListMarketStatisticsResponse> ListStatistics(ListMarketStatisticsRequest listMarketStatisticsRequest, CancellationToken cancellationToken = default)
        {
            try
            {

                var isHistoricalStatistics = !string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime) ? true : false;

                var listMarketStatistics = new List<MarketStatistics>();
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var lastUpdatedDailyIndicator = await dbContext.DailyIndicators.Select(x => x.Timestamp).FirstOrDefaultAsync();

                    if (isHistoricalStatistics && lastUpdatedDailyIndicator?.ToString("yyyy-MM-dd HH:mm:ss") != listMarketStatisticsRequest.EndDateTime)
                    {
                        await _importDailyIndicatorsHandler.ImportATR(listMarketStatisticsRequest.EndDateTime.Substring(0, 10) + " 00:00:00");
                    }

                    var thirtyDaysAgo = DateTime.Now.AddDays(-30);

                    var stockPrices = dbContext.StockPrices.Where(x => x.Timestamp >= thirtyDaysAgo);

                    if (isHistoricalStatistics)
                    {
                        thirtyDaysAgo = Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime).AddDays(-30);
                        stockPrices = dbContext.StockPrices.Where(x => x.Timestamp <= Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime));
                    }


                    var tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync(cancellationToken);

                    var revisedStockPrices = await stockPrices.ToListAsync(cancellationToken);


                    var spyPrices = revisedStockPrices.Where(x => x.TickerId == 529).ToList();

                    var tickerAtrs = await dbContext.DailyIndicators.ToDictionaryAsync(di => di.TickerId, di => di.Atr, cancellationToken);

                    var tasks = tickers.Select(ticker => ProcessTickerAsync(listMarketStatisticsRequest, ticker, revisedStockPrices, spyPrices, tickerAtrs, isHistoricalStatistics, cancellationToken)).ToList();

                    var results = await Task.WhenAll(tasks);

                    listMarketStatistics.AddRange(results.Where(statistics => statistics != null));
                }

                return new ListMarketStatisticsResponse { ListMarketStatistics = listMarketStatistics, Success = true, Count = listMarketStatistics.Count };
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);

                return null;
            }
        }

        private async Task<MarketStatistics> ProcessTickerAsync(ListMarketStatisticsRequest listMarketStatisticsRequest, Ticker ticker, List<StockPrice> stockPrices, List<StockPrice> spyPrices,
                                                                Dictionary<int, decimal?> tickerAtrs, bool isHistoricalStatistics, CancellationToken cancellationToken)
        {
            var tickerPrices = stockPrices.Where(x => x.TickerId == ticker.Id).ToList();
            tickerAtrs.TryGetValue(ticker.Id, out var tickerAtr);
            var spyAtr = tickerAtrs.TryGetValue(529, out var sa) ? sa : null;

            if (tickerPrices.Any())
            {
                var marketStatistics = new MarketStatistics
                {
                    Ticker = ticker.TickerName,
                    ATR = tickerAtr.HasValue ? Math.Round(tickerAtr.Value, 2) : (decimal?)null,
                    Price = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault().ClosePrice,
                    FifteenMin = new()
                    {
                        Rvol = CalculateRelativeVolume("15Min", listMarketStatisticsRequest, tickerPrices, isHistoricalStatistics),
                        RsRw = CalculateDynamicRRS("15Min", tickerPrices, spyPrices, listMarketStatisticsRequest, isHistoricalStatistics)
                    },
                    ThirtyMin = new()
                    {
                        Rvol = CalculateRelativeVolume("30Min", listMarketStatisticsRequest, tickerPrices, isHistoricalStatistics),
                        RsRw = CalculateDynamicRRS("30Min", tickerPrices, spyPrices, listMarketStatisticsRequest, isHistoricalStatistics)
                    },
                    OneHour = new()
                    {
                        Rvol = CalculateRelativeVolume("1Hour", listMarketStatisticsRequest, tickerPrices, isHistoricalStatistics),
                        RsRw = CalculateDynamicRRS("1Hour", tickerPrices, spyPrices, listMarketStatisticsRequest, isHistoricalStatistics)
                    },
                    TwoHour = new()
                    {
                        Rvol = CalculateRelativeVolume("2Hour", listMarketStatisticsRequest, tickerPrices, isHistoricalStatistics),
                        RsRw = CalculateDynamicRRS("2Hour", tickerPrices, spyPrices, listMarketStatisticsRequest, isHistoricalStatistics)
                    },
                    FourHour = new()
                    {
                        Rvol = CalculateRelativeVolume("4Hour", listMarketStatisticsRequest, tickerPrices, isHistoricalStatistics),
                        RsRw = CalculateDynamicRRS("4Hour", tickerPrices, spyPrices, listMarketStatisticsRequest, isHistoricalStatistics)
                    },
                    Timestamp = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault().Timestamp
                };

                return marketStatistics;
            }

            return null;
        }
        public static decimal CalculateDynamicRRS(string timeFrame, List<StockPrice> tickerPrices, List<StockPrice> spyPrices, ListMarketStatisticsRequest listMarketStatisticsRequest, bool isHistoricalStatistics)
        {
            var periodCount = GetPeriodCountFromTimeFrame(timeFrame);

            var expectedCount = 50 + periodCount;
            // Ensure there are enough data points
            if (tickerPrices.Count < expectedCount || spyPrices.Count < expectedCount)
                return 0;

            // Focus on the 6 most recent records for the 30-minute window
            var recentTickerPrices = tickerPrices.Take(6).ToList();
            var recentSpyPrices = spyPrices.Take(6).ToList();

            List<decimal> rrsValues = new List<decimal>();

            for (int i = 0; i < periodCount; i++) // Loop through each of the 6 intervals
            {
                var tickerSegmentATR = tickerPrices.Skip(i + periodCount).Take(50).ToList();
                var spySegmentATR = spyPrices.Skip(i + periodCount).Take(50).ToList();

                // Assuming CalculateATR and other necessary calculations are adjusted to use just the required interval
                decimal tickerATR = CalculateATR(tickerSegmentATR);
                decimal spyATR = CalculateATR(spySegmentATR);

                decimal tickerPriceChange = recentTickerPrices[i].ClosePrice.Value - recentTickerPrices[i].OpenPrice.Value;
                decimal spyPriceChange = recentSpyPrices[i].ClosePrice.Value - recentSpyPrices[i].OpenPrice.Value;

                decimal spyPowerIndex = spyPriceChange / spyATR;
                decimal expectedChange = spyPowerIndex * tickerATR;
                decimal rrs = (tickerPriceChange - expectedChange) / tickerATR;

                rrsValues.Add(rrs);
            }

            // Average the RRS values for the 30-minute window
            return rrsValues.Average();
        }

        private static decimal CalculateATR(List<StockPrice> prices)
        {
            var atrValues = prices.Skip(1).Select((price, index) => CalculateTrueRange(price, prices[index])).ToList();
            return atrValues.Any() ? atrValues.Average() : 0;
        }

        private static decimal CalculateTrueRange(StockPrice current, StockPrice previous)
        {
            var highMinusLow = current.HighPrice.Value - current.LowPrice.Value;
            var highMinusClose = Math.Abs(current.HighPrice.Value - previous.ClosePrice.Value);
            var lowMinusClose = Math.Abs(current.LowPrice.Value - previous.ClosePrice.Value);
            return Math.Max(highMinusLow, Math.Max(highMinusClose, lowMinusClose));
        }

        private static int GetPeriodCountFromTimeFrame(string timeFrame)
        {
            // Convert timeFrame to period count
            return timeFrame switch
            {
                "15Min" => 3,
                "30Min" => 6,
                "1Hour" => 12,
                "2Hour" => 24,
                "4Hour" => 48,
                _ => throw new ArgumentException("Invalid time frame", nameof(timeFrame)),
            };
        }

        private static decimal CalculatePeriodVolume(List<StockPrice> prices, DateTime endDate, int periodCount)
        {
            var count = prices.Where(p => p.Timestamp.Value <= endDate && p.Timestamp.Value > endDate.AddMinutes(-5 * periodCount)).Count();
            return prices.Where(p => p.Timestamp.Value <= endDate && p.Timestamp.Value > endDate.AddMinutes(-5 * periodCount))
                         .Sum(p => p.TradingVolume ?? 0);
        }

        private static decimal CalculateHistoricalAverageVolume(List<StockPrice> prices, DateTime endDate, int periodCount, int daysBack)
        {
            var historicalVolumes = new List<decimal>();

            for (int i = 1; i <= daysBack; i++)
            {
                DateTime samePeriodStart = endDate.Date.AddDays(-i).Add(endDate.TimeOfDay).AddMinutes(-5 * (periodCount - 1));
                DateTime samePeriodEnd = samePeriodStart.AddMinutes(5 * periodCount);

                var periodVolume = prices.Where(p => p.Timestamp.Value >= samePeriodStart && p.Timestamp.Value <= samePeriodEnd)
                                         .Sum(p => p.TradingVolume ?? 0);

                if (periodVolume > 0)
                {
                    historicalVolumes.Add(periodVolume);
                }
            }

            return historicalVolumes.Any() ? historicalVolumes.Average() : 0;
        }

        public static decimal? CalculateRelativeVolume(string timeFrame, ListMarketStatisticsRequest listMarketStatisticsRequest, List<StockPrice> prices, bool isHistoricalStatistics)
        {
            var periodCount = GetPeriodCountFromTimeFrame(timeFrame);
            // Assuming each StockPrice represents a 5-minute interval
            var latestTimestamp = prices.Max(p => p.Timestamp.Value);

            // Calculate today's volume for the specified period
            var todayVolume = CalculatePeriodVolume(prices, latestTimestamp, periodCount);

            // Calculate average volume for the same period over the last 14 days
            var averageHistoricalVolume = CalculateHistoricalAverageVolume(prices, latestTimestamp, periodCount, 14);

            if (averageHistoricalVolume == 0)
            {
                return null; // Avoid division by zero
            }

            // Calculate Relative Volume (RVOL)
            var rvol = (todayVolume / averageHistoricalVolume) * 100;
            return Math.Round(rvol, 2);
        }
    }
}