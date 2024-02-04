using System.Collections.Concurrent;
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

namespace TradeFunctions.ListMarketStatistics
{
    public interface IListMarketStatisticsHandler
    {
        Task<ListMarketStatisticsResponse> ListStatistics(CancellationToken cancellationToken = default);
    }

    public class ListMarketStatisticsHandler : IListMarketStatisticsHandler
    {
        private readonly ITwelveDataService _twelveDataService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ListMarketStatisticsHandler(IDbConnectionStringService dbConnectionStringService, ITwelveDataService twelveDataService)
        {
            _dbConnectionStringService = dbConnectionStringService;
            _twelveDataService = twelveDataService;
        }

        public async Task<ListMarketStatisticsResponse> ListStatistics(CancellationToken cancellationToken = default)
        {
            try
            {
                List<MarketStatistics> listMarketStatistics = new List<MarketStatistics>();
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var thirtyDaysAgo = DateTime.Now.AddDays(-30);

                    var tickers = await dbContext.Tickers.ToListAsync();

                    var stockPrices = await dbContext.StockPrices.Where(x => x.Timestamp >= thirtyDaysAgo).ToListAsync();

                    var spyPrices = stockPrices.Where(x => x.TickerId == 529).ToList();

                    var tickerAtrs = await dbContext.DailyIndicators.ToDictionaryAsync(di => di.TickerId, di => di.Atr, cancellationToken);
                    var parallelOptions = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount };
                    var concurrentListMarketStatistics = new ConcurrentBag<MarketStatistics>();

                    Parallel.ForEach(tickers, parallelOptions, ticker =>
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
                                   FiveMin = new() { Rvol = CalculateRVOL("5Min", tickerPrices), RsRw = CalculateRelativeStrength("5Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   TenMin = new() { Rvol = CalculateRVOL("10Min", tickerPrices), RsRw = CalculateRelativeStrength("10Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   FifteenMin = new() { Rvol = CalculateRVOL("15Min", tickerPrices), RsRw = CalculateRelativeStrength("15Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   TwentyMin = new() { Rvol = CalculateRVOL("20Min", tickerPrices), RsRw = CalculateRelativeStrength("20Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   TwentyFiveMin = new() { Rvol = CalculateRVOL("25Min", tickerPrices), RsRw = CalculateRelativeStrength("25Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   ThirtyMin = new() { Rvol = CalculateRVOL("30Min", tickerPrices), RsRw = CalculateRelativeStrength("30Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   FortyFiveMin = new() { Rvol = CalculateRVOL("45Min", tickerPrices), RsRw = CalculateRelativeStrength("45Min", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   OneHour = new() { Rvol = CalculateRVOL("1Hour", tickerPrices), RsRw = CalculateRelativeStrength("1Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   TwoHour = new() { Rvol = CalculateRVOL("2Hour", tickerPrices), RsRw = CalculateRelativeStrength("2Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   ThreeHour = new() { Rvol = CalculateRVOL("3Hour", tickerPrices), RsRw = CalculateRelativeStrength("3Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   FourHour = new() { Rvol = CalculateRVOL("4Hour", tickerPrices), RsRw = CalculateRelativeStrength("4Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   FiveHour = new() { Rvol = CalculateRVOL("5Hour", tickerPrices), RsRw = CalculateRelativeStrength("5Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   SixHour = new() { Rvol = CalculateRVOL("6Hour", tickerPrices), RsRw = CalculateRelativeStrength("6Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                   SevenAndHalfHours = new() { Rvol = CalculateRVOL("7AndHalfHour", tickerPrices), RsRw = CalculateRelativeStrength("7AndHalfHour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                                };

                               listMarketStatistics.Add(marketStatistics);
                           }
                       });
                }

                return new ListMarketStatisticsResponse { ListMarketStatistics = listMarketStatistics };
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);

                return null;
            }
        }

        private static decimal? CalculateRelativeStrength(string timeFrame, List<StockPrice> tickerPrices, List<StockPrice> spyPrices, decimal? spyAtr, decimal? stockAtr)
        {
            try
            {
                var endTimeStamp = tickerPrices.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp).FirstOrDefault();

                var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp);

                var openingPriceRecord = tickerPrices
                                .FirstOrDefault(x => x.Timestamp == startTimeStamp);

                var openingSpyPriceRecord = spyPrices
                                .FirstOrDefault(x => x.Timestamp == startTimeStamp);

                // Get the closing price for the endTimeStamp (last record on or before endTimeStamp)
                var closingPriceRecord = tickerPrices
                                            .LastOrDefault(x => x.Timestamp == endTimeStamp);

                var closingSpyPriceRecord = spyPrices
                                            .LastOrDefault(x => x.Timestamp == endTimeStamp);

                // Extracting prices from the records
                var openingPrice = openingPriceRecord?.OpenPrice;
                var closingPrice = closingPriceRecord?.ClosePrice;

                var openingSpyPrice = openingSpyPriceRecord?.OpenPrice;
                var closingSpyPrice = closingSpyPriceRecord?.ClosePrice;



                decimal? stockPercentageChange = ((closingPrice - openingPrice) / openingPrice) * 100;
                decimal? spyPercentageChange = ((closingSpyPrice - openingSpyPrice) / openingSpyPrice) * 100;

                decimal? relativeStrength = stockPercentageChange - spyPercentageChange;
                decimal? atrAdjustmentFactor = stockAtr / spyAtr;
                decimal? adjustedRelativeStrength = relativeStrength / atrAdjustmentFactor;

                return adjustedRelativeStrength.HasValue ? Math.Round(adjustedRelativeStrength.Value, 2) : (decimal?)null;

            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while calculating RVOL for ticker {tickerPrices.FirstOrDefault().Ticker.TickerName}: {ex.Message}", ex);
            }
        }

        public static decimal? CalculateRVOL(string timeFrame, List<StockPrice> tickerPrices)
        {
            try
            {
                var volumesByDay = new List<decimal>();

                var currentDate = DateTime.Now.Date;
                var totalDaysChecked = 0;
                var daysWithData = 0;

                // Use FirstOrDefault and check for null to avoid exceptions.
                var lastPrice = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                if (lastPrice == null)
                {
                    throw new InvalidOperationException("Unable to find any timestamps in ticker prices.");
                }

                var endTimeStamp = lastPrice.Timestamp; // Last known timestamp, no need for Nullable.
                var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp); // Assume this returns a DateTime not nullable, or handle nullability inside TimeframeStart.

                // var test = tickerPrices
                //                     .Where(x => x.Timestamp >= startTimeStamp && x.Timestamp <= endTimeStamp);

                var sumOfVolume = tickerPrices
                                    .Where(x => x.Timestamp >= startTimeStamp && x.Timestamp <= endTimeStamp)
                                    .Sum(x => x.TradingVolume ?? 0);

                while (daysWithData < 15 && totalDaysChecked < 30)
                {
                    // Ensure we have a non-nullable DateTime to work with
                    var nonNullableStartTimeStamp = startTimeStamp.GetValueOrDefault();
                    var nonNullableEndTimeStamp = endTimeStamp.GetValueOrDefault();

                    var dayStartTimeStamp = currentDate.AddDays(-totalDaysChecked).Date + nonNullableStartTimeStamp.TimeOfDay;
                    var dayEndTimeStamp = currentDate.AddDays(-totalDaysChecked).Date + nonNullableEndTimeStamp.TimeOfDay;

                    var dayVolume = tickerPrices
                                        .Where(x => x.Timestamp >= dayStartTimeStamp && x.Timestamp <= dayEndTimeStamp)
                                        .Sum(x => x.TradingVolume ?? 0);

                    if (dayVolume > 0)
                    {
                        volumesByDay.Add(dayVolume);
                        daysWithData++;
                    }
                    totalDaysChecked++;
                }
                if (volumesByDay.Count > 0)
                {
                    volumesByDay.RemoveAt(0);
                }
                decimal? result = volumesByDay.Sum() == 0 ? 0 : sumOfVolume / (volumesByDay.Sum() / 14);
                return result.HasValue ? Math.Round(result.Value, 2) : (decimal?)null;
            }
            catch (Exception ex)
            {
                // Assuming each StockPrice has a Ticker property which in turn has a TickerName.
                var tickerName = tickerPrices.FirstOrDefault()?.Ticker?.TickerName ?? "Unknown";
                throw new Exception($"An error occurred while calculating RVOL for ticker {tickerName}: {ex.Message}", ex);
            }
        }


        private static DateTime? TimeframeStart(string timeFrame, DateTime? timeStamp)
        {
            int minutesBack = 0;

            switch (timeFrame)
            {
                case "5Min":
                    minutesBack = 0;
                    break;
                case "10Min":
                    minutesBack = 5;
                    break;
                case "15Min":
                    minutesBack = 10;
                    break;
                case "20Min":
                    minutesBack = 15;
                    break;
                case "25Min":
                    minutesBack = 20;
                    break;
                case "30Min":
                    minutesBack = 25;
                    break;
                case "45Min":
                    minutesBack = 40;
                    break;
                case "1Hour":
                    minutesBack = 55;
                    break;
                case "2Hour":
                    minutesBack = 115;
                    break;
                case "3Hour":
                    minutesBack = 175;
                    break;
                case "4Hour":
                    minutesBack = 235;
                    break;
                case "5Hour":
                    minutesBack = 295;
                    break;
                case "6Hour":
                    minutesBack = 355;
                    break;
                case "7AndHalfHour":
                    minutesBack = 385;
                    break;
                default:
                    throw new ArgumentException("Invalid time frame");
            }

            return timeStamp.Value.AddMinutes(-minutesBack);
        }
    }
}