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
                var listMarketStatistics = new List<MarketStatistics>();
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var lastUpdatedDailyIndicator = await dbContext.DailyIndicators.Select(x => x.Timestamp).FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime) && lastUpdatedDailyIndicator?.ToString("yyyy-MM-dd HH:mm:ss") != listMarketStatisticsRequest.EndDateTime)
                    {
                        await _importDailyIndicatorsHandler.ImportATR(listMarketStatisticsRequest.EndDateTime.Substring(0, 10) + " 00:00:00");
                    }

                    var thirtyDaysAgo = string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime) ? DateTime.Now.AddDays(-30) : Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime).AddDays(-30);

                    var tickers = await dbContext.Tickers.Where(x => x.Active == true).ToListAsync(cancellationToken);

                    var stockPrices = dbContext.StockPrices.Where(x => x.Timestamp >= thirtyDaysAgo);

                    if (!string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime))
                    {
                        stockPrices = dbContext.StockPrices.Where(x => x.Timestamp <= Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime));
                    }

                    var revisedStockPrices = await stockPrices.ToListAsync(cancellationToken);


                    var spyPrices = revisedStockPrices.Where(x => x.TickerId == 529).ToList();

                    var tickerAtrs = await dbContext.DailyIndicators.ToDictionaryAsync(di => di.TickerId, di => di.Atr, cancellationToken);

                    // Create tasks for each ticker
                    var tasks = tickers.Select(ticker => ProcessTickerAsync(listMarketStatisticsRequest, ticker, revisedStockPrices, spyPrices, tickerAtrs, cancellationToken)).ToList();

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

            return null;
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

                decimal? stockMovement = closingPrice - openingPrice / openingPrice;

                decimal? spyMovement = closingSpyPrice - openingSpyPrice / openingSpyPrice;

                decimal? stockAtrAdjusted = (stockMovement / stockAtr);
                decimal? spyAtrAdjusted = (spyMovement / spyAtr);

                decimal? relativeStrength;
                if (stockAtrAdjusted < 0 || spyAtrAdjusted < 0)
                {
                    // If either is negative, calculate the total change and adjust the comparison
                    decimal totalChange = Math.Abs(stockAtrAdjusted.Value) + Math.Abs(spyAtrAdjusted.Value);
                    decimal stockChangeProportion = Math.Abs(stockAtrAdjusted.Value) / totalChange;
                    decimal spyChangeProportion = Math.Abs(spyAtrAdjusted.Value) / totalChange;

                    // Use proportions to determine relative strength when considering negative values
                    relativeStrength = stockChangeProportion / spyChangeProportion;
                    if (stockAtrAdjusted < 0)
                    {
                        relativeStrength = -relativeStrength;
                    }
                }
                else
                {
                    // Directly calculate relative strength if both are positive
                    relativeStrength = stockAtrAdjusted / spyAtrAdjusted;
                }

                return relativeStrength.HasValue ? relativeStrength : (decimal?)null;
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

                var currentDate = string.IsNullOrWhiteSpace(listMarketStatisticsRequest.EndDateTime) ? DateTime.Now.Date : Convert.ToDateTime(listMarketStatisticsRequest.EndDateTime);
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