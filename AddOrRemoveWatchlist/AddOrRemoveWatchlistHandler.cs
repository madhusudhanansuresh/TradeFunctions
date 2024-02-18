using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using AssessmentDeck.Services;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.AddOrRemoveWatchlist
{
    public interface IAddOrRemoveWatchlistHandler
    {
        Task<AddOrRemoveWatchlistResponse> AddOrRemoveItem(AddOrRemoveWatchlistRequest addOrRemoveWatchlistRequest, CancellationToken cancellationToken = default);
    }

    public class AddOrRemoveWatchlistHandler : IAddOrRemoveWatchlistHandler
    {
        private readonly ITwelveDataService _twelveDataService;
        public IDbConnectionStringService _dbConnectionStringService { get; }

        public AddOrRemoveWatchlistHandler(IDbConnectionStringService dbConnectionStringService)
        {
            _dbConnectionStringService = dbConnectionStringService;
        }

        public async Task<AddOrRemoveWatchlistResponse> AddOrRemoveItem(AddOrRemoveWatchlistRequest addOrRemoveWatchlistRequest, CancellationToken cancellationToken = default)
        {
            var addOrRemoveWatchlistResponse = new AddOrRemoveWatchlistResponse();

            if (addOrRemoveWatchlistRequest.Action == "add")
            {
                addOrRemoveWatchlistResponse = await AddOrUpdateItem(addOrRemoveWatchlistRequest, cancellationToken);
            }
            else if (addOrRemoveWatchlistRequest.Action == "remove")
            {
                addOrRemoveWatchlistResponse = await RemoveItem(addOrRemoveWatchlistRequest, cancellationToken);
            }

            return addOrRemoveWatchlistResponse;

        }

        public async Task<AddOrRemoveWatchlistResponse> AddOrUpdateItem(AddOrRemoveWatchlistRequest addOrRemoveWatchlistRequest, CancellationToken cancellationToken)
        {
            try
            {
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var tickerId = await dbContext.Tickers
                        .Where(x => x.TickerName == addOrRemoveWatchlistRequest.TickerName)
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync();

                    if (tickerId == default)
                    {
                        return new AddOrRemoveWatchlistResponse { Success = false, Message = "Ticker not found." };
                    }

                    var existingWatchlist = await dbContext.Watchlists
                        .Where(x => x.TickerId == tickerId)
                        .FirstOrDefaultAsync();

                    if (existingWatchlist != null)
                    {
                        existingWatchlist.Reason = addOrRemoveWatchlistRequest.Reason;
                    }
                    else
                    {
                        var watchList = new Watchlist
                        {
                            TickerId = tickerId,
                            Reason = addOrRemoveWatchlistRequest.Reason
                        };
                        await dbContext.AddAsync(watchList);
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return new AddOrRemoveWatchlistResponse { Success = true, Message = "Watchlist updated successfully." };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                return new AddOrRemoveWatchlistResponse { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }


        public async Task<AddOrRemoveWatchlistResponse> RemoveItem(AddOrRemoveWatchlistRequest addOrRemoveWatchlistRequest, CancellationToken cancellationToken)
        {
            try
            {
                using (var dbContext = new TradeContext(_dbConnectionStringService.ConnectionString()))
                {
                    var tickerId = await dbContext.Tickers
                        .Where(x => x.TickerName == addOrRemoveWatchlistRequest.TickerName)
                        .Select(x => x.Id)
                        .FirstOrDefaultAsync();

                    if (tickerId == default)
                    {
                        return new AddOrRemoveWatchlistResponse { Success = false, Message = "Ticker not found." };
                    }

                    var watchlistItem = await dbContext.Watchlists
                        .Where(x => x.TickerId == tickerId)
                        .FirstOrDefaultAsync();

                    if (watchlistItem == null)
                    {
                        return new AddOrRemoveWatchlistResponse { Success = false, Message = "Watchlist item not found." };
                    }

                    dbContext.Remove(watchlistItem);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    return new AddOrRemoveWatchlistResponse { Success = true, Message = "Successfully removed from watchlist." };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return new AddOrRemoveWatchlistResponse { Success = false, Message = $"An error occurred: {ex.Message}" };
            }
        }
    }
}