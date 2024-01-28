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
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var twentyTwoDaysAgo = DateTime.Now.AddDays(-22);

                    var tickers = await dbContext.Tickers.ToListAsync();

                    var stockPrices = await dbContext.StockPrices.Where(x => x.Timestamp >= twentyTwoDaysAgo).ToListAsync();

                    var spyPrices = stockPrices.Where(x => x.TickerId == 529).ToList();

                    var spyAtr = await dbContext.DailyIndicators.Where(x => x.TickerId == 529).Select(x => x.Atr).FirstOrDefaultAsync();


                    foreach (var ticker in tickers)
                    {
                        var tickerPrices = stockPrices.Where(x => x.TickerId == ticker.Id).ToList();
                        var tickerAtr = await dbContext.DailyIndicators.Where(x => x.TickerId == ticker.Id).Select(x => x.Atr).FirstOrDefaultAsync();

                        var listMarketStatisticsResponse = new ListMarketStatisticsResponse
                        {
                            Ticker = ticker.TickerName,
                            ATR = tickerAtr,
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
                            SevenHour = new() { Rvol = CalculateRVOL("7Hour", tickerPrices), RsRw = CalculateRelativeStrength("7Hour", tickerPrices, spyPrices, spyAtr, tickerAtr) },
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);

                return null;
            }
        }

        private static decimal? CalculateRelativeStrength(string timeFrame, List<StockPrice> tickerPrices, List<StockPrice> spyPrices, decimal? spyAtr, decimal? stockAtr)
        {

            var endTimeStamp = tickerPrices.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp).First();

            var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp);

            var openingPriceRecord = tickerPrices
                            .FirstOrDefault(x => x.Timestamp >= startTimeStamp);

            var openingSpyPriceRecord = spyPrices
                            .FirstOrDefault(x => x.Timestamp >= startTimeStamp);

            // Get the closing price for the endTimeStamp (last record on or before endTimeStamp)
            var closingPriceRecord = tickerPrices
                                        .LastOrDefault(x => x.Timestamp <= endTimeStamp);

            var closingSpyPriceRecord = spyPrices
                                        .LastOrDefault(x => x.Timestamp <= endTimeStamp);

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

            return adjustedRelativeStrength;
        }
        public static decimal? CalculateRVOL(string timeFrame, List<StockPrice> tickerPrices)
        {

            var volumesByDay = new List<decimal>();

            var currentDate = DateTime.Now.Date;

            var totalDaysChecked = 0;

            var daysWithData = 0;

            var endTimeStamp = tickerPrices.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp).First();

            var startTimeStamp = TimeframeStart(timeFrame, endTimeStamp);

            var sumOfVolume = tickerPrices
                              .Where(x => x.Timestamp >= startTimeStamp && x.Timestamp <= endTimeStamp)
                              .Sum(x => x.TradingVolume);

            while (daysWithData < 14 && totalDaysChecked < 22)
            {
                var dayStartTimeStamp = currentDate.AddDays(-totalDaysChecked).Date + startTimeStamp.Value.TimeOfDay;
                var dayEndTimeStamp = currentDate.AddDays(-totalDaysChecked).Date + endTimeStamp.Value.TimeOfDay;

                var dayVolume = tickerPrices
                                    .Where(x => x.Timestamp >= dayStartTimeStamp && x.Timestamp <= dayEndTimeStamp)
                                    .Sum(x => x.TradingVolume);

                if (dayVolume > 0)
                {
                    volumesByDay.Add(dayVolume ?? 0);
                    daysWithData++;
                }
                // If there's no data for the day, don't add to volumesByDay; just try the next day.
                totalDaysChecked++;
            }

            var sumOfLast14DaysVolume = volumesByDay.Sum();

            return sumOfVolume / (sumOfLast14DaysVolume / 14);
        }

        private static DateTime? TimeframeStart(string timeFrame, DateTime? timeStamp)
        {
            int minutesBack = 0;

            switch (timeFrame)
            {
                case "5Min":
                    minutesBack = 5;
                    break;
                case "10Min":
                    minutesBack = 10;
                    break;
                case "15Min":
                    minutesBack = 15;
                    break;
                case "20Min":
                    minutesBack = 20;
                    break;
                case "25Min":
                    minutesBack = 25;
                    break;
                case "30Min":
                    minutesBack = 30;
                    break;
                case "45Min":
                    minutesBack = 45;
                    break;
                case "1Hour":
                    minutesBack = 60;
                    break;
                case "2Hour":
                    minutesBack = 120;
                    break;
                case "3Hour":
                    minutesBack = 180;
                    break;
                case "4Hour":
                    minutesBack = 240;
                    break;
                case "5Hour":
                    minutesBack = 300;
                    break;
                case "6Hour":
                    minutesBack = 360;
                    break;
                case "7hour":
                    minutesBack = 420;
                    break;
                default:
                    throw new ArgumentException("Invalid time frame");
            }

            return timeStamp.Value.AddMinutes(-minutesBack);
        }
    }
}