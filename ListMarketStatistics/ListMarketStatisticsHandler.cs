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

                    var thirtyDaysAgo = DateTime.Now.AddDays(-30);

                    var stockPrices = dbContext.StockPrices.Where(x => x.Timestamp >= thirtyDaysAgo);

                    if (isHistoricalStatistics)
                    {
                        thirtyDaysAgo = Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime).AddDays(-30);
                        stockPrices = dbContext.StockPrices.Where(x => x.Timestamp <= Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime) && x.Timestamp >= thirtyDaysAgo);
                    }

                    List<Ticker> tickers = new List<Ticker>();

                    if (listMarketStatisticsRequest?.TickerNames?.Count > 0)
                    {
                        tickers = await dbContext.Tickers.Where(x => x.Active == true && listMarketStatisticsRequest.TickerNames.Contains(x.TickerName)).ToListAsync(cancellationToken);
                    }
                    else
                    {
                        tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync(cancellationToken);
                    }

                    var revisedStockPrices = await stockPrices.AsNoTracking().ToListAsync(cancellationToken);

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

                return new ListMarketStatisticsResponse { ListMarketStatistics = null, Success = false, Count = 0 };
            }
        }

        private async Task<MarketStatistics> ProcessTickerAsync(ListMarketStatisticsRequest listMarketStatisticsRequest, Ticker ticker, List<StockPrice> stockPrices, List<StockPrice> spyPrices,
                                                                Dictionary<int, decimal?> tickerAtrs, bool isHistoricalStatistics, CancellationToken cancellationToken)
        {
            var tickerPrices = stockPrices.Where(x => x.TickerId == ticker.Id).OrderByDescending(x => x.Timestamp).ToList();
            var spyPricesByDescending = spyPrices.OrderByDescending(x => x.Timestamp).ToList();
            tickerAtrs.TryGetValue(ticker.Id, out var tickerAtr);

            if (tickerPrices.Any())
            {
                var marketStatistics = new MarketStatistics
                {
                    Ticker = ticker.TickerName,
                    ATR = tickerAtr.HasValue ? Math.Round(tickerAtr.Value, 2) : (decimal?)null,
                    Price = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault().ClosePrice,
                    // FiveMin = new()
                    // {
                    //     Rvol = CalculateRelativeVolume("5Min", tickerPrices),
                    //     RsRw = CalculateDynamicRRS("5Min", tickerPrices, spyPricesByDescending)
                    // },
                    // TenMin = new()
                    // {
                    //     Rvol = CalculateRelativeVolume("10Min", tickerPrices),
                    //     RsRw = CalculateDynamicRRS("10Min", tickerPrices, spyPricesByDescending)
                    // },
                    // TwentyMin = new()
                    // {
                    //     Rvol = CalculateRelativeVolume("20Min", tickerPrices),
                    //     RsRw = CalculateDynamicRRS("20Min", tickerPrices, spyPricesByDescending)
                    // },
                    // TwentyFiveMin = new()
                    // {
                    //     Rvol = CalculateRelativeVolume("25Min", tickerPrices),
                    //     RsRw = CalculateDynamicRRS("25Min", tickerPrices, spyPricesByDescending)
                    // },
                    FifteenMin = new()
                    {
                        Rvol = CalculateRelativeVolume("15Min", tickerPrices),
                        RsRw = CalculateDynamicRRS("15Min", tickerPrices, spyPricesByDescending)
                    },
                    ThirtyMin = new()
                    {
                        Rvol = CalculateRelativeVolume("30Min", tickerPrices),
                        RsRw = CalculateDynamicRRS("30Min", tickerPrices, spyPricesByDescending)
                    },
                    OneHour = new()
                    {
                        Rvol = CalculateRelativeVolume("1Hour", tickerPrices),
                        RsRw = CalculateDynamicRRS("1Hour", tickerPrices, spyPricesByDescending)
                    },
                    TwoHour = new()
                    {
                        Rvol = CalculateRelativeVolume("2Hour", tickerPrices),
                        RsRw = CalculateDynamicRRS("2Hour", tickerPrices, spyPricesByDescending)
                    },
                    FourHour = new()
                    {
                        Rvol = CalculateRelativeVolume("4Hour", tickerPrices),
                        RsRw = CalculateDynamicRRS("4Hour", tickerPrices, spyPricesByDescending)
                    },
                    Timestamp = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault().Timestamp
                };

                return marketStatistics;
            }

            return null;
        }
        public static decimal CalculateDynamicRRS(string timeFrame, List<StockPrice> tickerPrices, List<StockPrice> spyPrices)
        {
            var periodCount = GetPeriodCountFromTimeFrame(timeFrame);

            var expectedCount = 50 + periodCount;
            // Ensure there are enough data points
            if (tickerPrices.Count < expectedCount || spyPrices.Count < expectedCount)
                return 0;

            var recentTickerPrices = tickerPrices.Take(periodCount).ToList();
            var recentSpyPrices = spyPrices.Take(periodCount).ToList();

            List<decimal> rrsValues = new List<decimal>();

            for (int i = 0; i < periodCount; i++)
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
                "5Min" => 1,
                "10Min" => 2,
                "15Min" => 3,
                "20Min" => 4,
                "25Min" => 5,
                "30Min" => 6,
                "1Hour" => 12,
                "2Hour" => 24,
                "4Hour" => 48,
                _ => throw new ArgumentException("Invalid time frame", nameof(timeFrame)),
            };
        }

        private static decimal CalculatePeriodVolume(List<StockPrice> prices, DateTime endDate, int periodCount)
        {
            // var count = prices.Where(p => p.Timestamp.Value <= endDate && p.Timestamp.Value > endDate.AddMinutes(-5 * periodCount)).Count();
            return prices.Where(p => p.Timestamp.Value <= endDate && p.Timestamp.Value > endDate.AddMinutes(-5 * periodCount))
                         .Sum(p => p.TradingVolume ?? 0);
        }

        private static decimal CalculateHistoricalAverageVolume(List<StockPrice> prices, DateTime endDate, int periodCount, int daysBack)
        {
            var historicalVolumes = new List<decimal>();
            int daysChecked = 0;
            int successfulDays = 0;

            while (successfulDays < daysBack && daysChecked < 2 * daysBack) // To prevent infinite loops, limit the checks
            {
                daysChecked++;
                DateTime samePeriodStart = endDate.Date.AddDays(-daysChecked).Add(endDate.TimeOfDay).AddMinutes(-5 * (periodCount - 1));
                DateTime samePeriodEnd = samePeriodStart.AddMinutes(5 * periodCount);

                var periodVolume = prices
                    .Where(p => p.Timestamp.Value >= samePeriodStart && p.Timestamp.Value < samePeriodEnd)
                    .Sum(p => p.TradingVolume ?? 0);

                // Only add the volume if there was trading activity in the period
                if (periodVolume > 0)
                {
                    historicalVolumes.Add(periodVolume);
                    successfulDays++;
                }
            }

            return historicalVolumes.Any() ? historicalVolumes.Average() : 0;
        }


        public static decimal? CalculateRelativeVolume(string timeFrame, List<StockPrice> prices)
        {
            try
            {
                var periodCount = GetPeriodCountFromTimeFrame(timeFrame);
                var latestTimestamp = prices.Max(p => p.Timestamp.Value);

                if (prices.Where(x => x.Timestamp.Value.Date == latestTimestamp.Date).Count() < periodCount)
                {
                    return null;
                }
                // Assuming each StockPrice represents a 5-minute interval
                if (prices.Count == 0)
                {
                    return null; // Return null if there are no prices
                }

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
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null; // Return null or handle as needed upon error
            }
        }
    }
}