using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TradeFunctions.ListWatchlist;

namespace ListWatchlist
{
    public class ListWatchlistController
    {
        private readonly ILogger<ListWatchlistController> _logger;

        private readonly IListWatchlist _listWatchlist;

        public ListWatchlistController(ILogger<ListWatchlistController> logger, IListWatchlist listWatchlist)
        {
            _logger = logger;
            _listWatchlist = listWatchlist;
        }

        [Function("listWatchlist")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            var data = await _listWatchlist.ListWatchlist();
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var jsonResponse = JsonSerializer.Serialize(data);
            response.WriteString(jsonResponse);
            return response;
        }
    }
}
