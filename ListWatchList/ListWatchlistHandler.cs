using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeFunctions.Models.Postgres.TradeContext;

namespace TradeFunctions.ListWatchlist
{
    public interface IListWatchlist
    {
        Task<ListWatchlistResponse> ListWatchlist();
    }


    public class ListWatchlistHandler : IListWatchlist
    {
        private readonly ILogger _logger;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public ListWatchlistHandler(ILoggerFactory loggerFactory, IDbConnectionStringService dbConnectionStringService)
        {
            _logger = loggerFactory.CreateLogger<ListWatchlistHandler>();
            _dbConnectionStringService = dbConnectionStringService;
        }

        public async Task<ListWatchlistResponse> ListWatchlist()
        {
            try
            {
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var watchlist = await dbContext.Watchlists.Select(x => new WatchlistItem
                    {
                        TickerName = x.Ticker.TickerName,
                        Reason = x.Reason,
                        LastUpdated = x.LastUpdateDt
                    }).ToListAsync();

                    return new ListWatchlistResponse
                    {
                        Success = true,
                        Message = "Watchlist retrieved successfully.",
                        Watchlist = watchlist
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");

                return new ListWatchlistResponse
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    Watchlist = new List<WatchlistItem>()
                };
            }
        }

    }
}