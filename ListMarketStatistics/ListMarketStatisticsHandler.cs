using System.Collections.Concurrent;
using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.ListMarketStatistics
{
    public interface IListMarketStatisticsHandler
    {
        Task<ListMarketStatisticsResponse> ListStatistics(ListMarketStatisticsRequest listMarketStatisticsRequest, CancellationToken cancellationToken = default);
    }

    public class ListMarketStatisticsHandler : IListMarketStatisticsHandler
    {
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ListMarketStatisticsHandler(IDbConnectionStringService dbConnectionStringService)
        {
            _dbConnectionStringService = dbConnectionStringService;
        }

        public async Task<ListMarketStatisticsResponse> ListStatistics(ListMarketStatisticsRequest listMarketStatisticsRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                var listMarketStatistics = new List<MarketStatistics>();
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var thirtyDaysAgo = DateTime.Now.AddDays(-30);

                    var tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync(cancellationToken);

                    var stockPrices = await dbContext.StockPrices.Where(x => x.Timestamp >= thirtyDaysAgo).ToListAsync(cancellationToken);

                    var spyPrices = stockPrices.Where(x => x.TickerId == 529).ToList();

                    var tickerAtrs = await dbContext.DailyIndicators.ToDictionaryAsync(di => di.TickerId, di => di.Atr, cancellationToken);

                    // Create tasks for each ticker
                    var tasks = tickers.Select(ticker => ProcessTickerAsync(listMarketStatisticsRequest, ticker, stockPrices, spyPrices, tickerAtrs, cancellationToken)).ToList();

                    // Wait for all tasks to complete
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

        private async Task<MarketStatistics> ProcessTickerAsync(ListMarketStatisticsRequest listMarketStatisticsRequest, Ticker ticker, List<StockPrice> stockPrices, List<StockPrice> spyPrices, Dictionary<int, decimal?> tickerAtrs, CancellationToken cancellationToken)
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
                    FifteenMin = new() { Rvol = CalculateRVOL("15Min", listMarketStatisticsRequest, tickerPrices), RsRw = CalculateRelativeStrength("15Min", listMarketStatisticsRequest, tickerPrices, spyPrices, spyAtr, tickerAtr) },
                    ThirtyMin = new() { Rvol = CalculateRVOL("30Min", listMarketStatisticsRequest, tickerPrices), RsRw = CalculateRelativeStrength("30Min", listMarketStatisticsRequest, tickerPrices, spyPrices, spyAtr, tickerAtr) },
                    OneHour = new() { Rvol = CalculateRVOL("1Hour", listMarketStatisticsRequest, tickerPrices), RsRw = CalculateRelativeStrength("1Hour", listMarketStatisticsRequest, tickerPrices, spyPrices, spyAtr, tickerAtr) },
                    TwoHour = new() { Rvol = CalculateRVOL("2Hour", listMarketStatisticsRequest, tickerPrices), RsRw = CalculateRelativeStrength("2Hour", listMarketStatisticsRequest, tickerPrices, spyPrices, spyAtr, tickerAtr) },
                    FourHour = new() { Rvol = CalculateRVOL("4Hour", listMarketStatisticsRequest, tickerPrices), RsRw = CalculateRelativeStrength("4Hour", listMarketStatisticsRequest, tickerPrices, spyPrices, spyAtr, tickerAtr) },
                    Timestamp = tickerPrices.OrderByDescending(x => x.Timestamp).FirstOrDefault().Timestamp
                };

                return marketStatistics;
            }

            return null; // Return null if no prices found for the ticker
        }

        private static decimal? CalculateRelativeStrength(string timeFrame, ListMarketStatisticsRequest listMarketStatisticsRequest, List<StockPrice> tickerPrices, List<StockPrice> spyPrices, decimal? spyAtr, decimal? stockAtr)
        {
            try
            {
                var endTimeStamp = tickerPrices.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp).FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime))
                {
                    endTimeStamp = GetRoundedEndDateTime(listMarketStatisticsRequest.EndDateTime);
                }

                var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp);

                if (!tickerPrices.Any(x => x.Timestamp == startTimeStamp))
                {
                    return null;
                }

                var openingPriceRecord = tickerPrices.FirstOrDefault(x => x.Timestamp == startTimeStamp);
                var openingSpyPriceRecord = spyPrices.FirstOrDefault(x => x.Timestamp == startTimeStamp);
                var closingPriceRecord = tickerPrices.FirstOrDefault(x => x.Timestamp == endTimeStamp);
                var closingSpyPriceRecord = spyPrices.FirstOrDefault(x => x.Timestamp == endTimeStamp);

                var openingPrice = openingPriceRecord?.OpenPrice;
                var closingPrice = closingPriceRecord?.ClosePrice;
                var openingSpyPrice = openingSpyPriceRecord?.OpenPrice;
                var closingSpyPrice = closingSpyPriceRecord?.ClosePrice;

                decimal? stockMovePercentage = (closingPrice - openingPrice) / openingPrice;
                decimal? spyMovePercentage = (closingSpyPrice - openingSpyPrice) / openingSpyPrice;

                decimal? relativeStrength = stockMovePercentage - spyMovePercentage;

                decimal? atrAdjustmentFactor = stockAtr / spyAtr;

                decimal? adjustedRelativeStrength = (relativeStrength / atrAdjustmentFactor) * 100;

                return adjustedRelativeStrength.HasValue ? Math.Round(adjustedRelativeStrength.Value, 2) : (decimal?)null;
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred while calculating relative strength: {ex.Message}", ex);
            }
        }


        public static decimal? CalculateRVOL(string timeFrame, ListMarketStatisticsRequest listMarketStatisticsRequest, List<StockPrice> tickerPrices)
        {
            try
            {
                var volumesByDay = new List<decimal>();

                var currentDate = DateTime.Now.Date;
                var totalDaysChecked = 0;
                var daysWithData = 0;

                var endTimeStamp = tickerPrices.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp).FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime))
                {
                    endTimeStamp = GetRoundedEndDateTime(listMarketStatisticsRequest.EndDateTime);
                }

                var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp);

                var startPrice = tickerPrices.Where(x => x.Timestamp == startTimeStamp).Any();

                if (!startPrice)
                {
                    return null;
                }

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
                decimal? result = volumesByDay.Sum() == 0 ? 0 : sumOfVolume / (volumesByDay.Sum() / 14) * 100;
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

                case "15Min":
                    minutesBack = 0;
                    break;
                case "30Min":
                    minutesBack = 15;
                    break;
                case "1Hour":
                    minutesBack = 45;
                    break;
                case "2Hour":
                    minutesBack = 105;
                    break;
                case "4Hour":
                    minutesBack = 225;
                    break;
                default:
                    throw new ArgumentException("Invalid time frame");
            }

            return timeStamp.Value.AddMinutes(-minutesBack);
        }

        public static DateTime? GetRoundedEndDateTime(string endDateTimeString)
        {
            DateTime endTimeStamp = Convert.ToDateTime(endDateTimeString);

            int minutesToSubtract = endTimeStamp.Minute % 15;
            endTimeStamp = endTimeStamp.AddMinutes(-minutesToSubtract - 15);
            endTimeStamp = new DateTime(endTimeStamp.Year, endTimeStamp.Month, endTimeStamp.Day, endTimeStamp.Hour, endTimeStamp.Minute, 0);

            return endTimeStamp;
        }

    }
}