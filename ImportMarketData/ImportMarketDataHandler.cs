using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradeFunctions.Models.Postgres.TradeContext;
using TradeFunctions.Services;

namespace TradeFunctions.ImportMarketData
{
    public class ImportMarketDataHandler
    {
        private readonly TwelveDataService _twelveDataService;
        private readonly TradeContext _context;

        public ImportMarketDataHandler(TradeContext context, TwelveDataService twelveDataService)
        {
            _context = context;
            _twelveDataService = twelveDataService;
        }

        public async Task<bool> ImportMarketData(CancellationToken cancellationToken = default)
        {
            
         var tickers =  await _context.Tickers.Select(x => x.TickerName).ToListAsync();  

         var stockDataResponse = await _twelveDataService.FetchStockDataAsync(tickers, ["5min"], "", "", 1);

         return false;
        }


    }
}