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
        public static decimal? CalculateRelativeVolume(string timeFrame, ListMarketStatisticsRequest listMarketStatisticsRequest, List<StockPrice> prices, bool isHistoricalStatistics)
        {
            var periodCount = PeriodCount(timeFrame);
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

        public static decimal CalculateDynamicRRS(string timeFrame, List<StockPrice> tickerPrices, List<StockPrice> spyPrices, ListMarketStatisticsRequest listMarketStatisticsRequest, bool isHistoricalStatistics)
        {
            var periodCount = PeriodCount(timeFrame);
            tickerPrices = tickerPrices.OrderBy(p => p.Timestamp).ToList();
            spyPrices = spyPrices.OrderBy(p => p.Timestamp).ToList();

            List<decimal> rrsValues = new List<decimal>();

            for (int i = 50; i < tickerPrices.Count - periodCount; i++)
            {
                var tickerSegmentATR = tickerPrices.Skip(i - 50).Take(50).ToList();
                var tickerSegmentRRS = tickerPrices.Skip(i).Take(periodCount).ToList();
                var spySegmentATR = spyPrices.Skip(i - 50).Take(50).ToList();
                var spySegmentRRS = spyPrices.Skip(i).Take(periodCount).ToList();

                decimal tickerATR = CalculateATR(tickerSegmentATR);
                decimal spyATR = CalculateATR(spySegmentATR);

                decimal tickerPriceChange = tickerSegmentRRS.Last().ClosePrice.Value - tickerSegmentRRS.First().ClosePrice.Value;
                decimal spyPriceChange = spySegmentRRS.Last().ClosePrice.Value - spySegmentRRS.First().ClosePrice.Value;

                decimal spyPowerIndex = spyPriceChange / spyATR;
                decimal expectedChange = spyPowerIndex * tickerATR;
                decimal rrs = (tickerPriceChange - expectedChange) / tickerATR;

                rrsValues.Add(rrs);
            }

            return rrsValues.Any() ? rrsValues.Average() : 0;
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


        private static decimal CalculateATR(List<StockPrice> prices)
        {
            var atrValues = new List<decimal>();
            for (int i = 1; i < prices.Count; i++)
            {
                var current = prices[i];
                var previous = prices[i - 1];
                var tr = new[]
                {
                    current.HighPrice.Value - current.LowPrice.Value,
                    Math.Abs(current.HighPrice.Value - previous.ClosePrice.Value),
                    Math.Abs(current.LowPrice.Value - previous.ClosePrice.Value)
                }.Max();

                atrValues.Add(tr);
            }

            return atrValues.Any() ? atrValues.Average() : 0;
        }

        private static int PeriodCount(string timeFrame)
        {
            int count = 0;

            switch (timeFrame)
            {

                case "15Min":
                    count = 3;
                    break;
                case "30Min":
                    count = 6;
                    break;
                case "1Hour":
                    count = 6;
                    break;
                case "2Hour":
                    count = 12;
                    break;
                case "4Hour":
                    count = 24;
                    break;
                default:
                    throw new ArgumentException("Invalid time frame");
            }
            return count;
        }
    }
}