using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TradeFunctions.AddOrRemoveWatchlist;

namespace AddOrRemoveWatchlist
{
    public class AddOrRemoveWatchlistController
    {
        private readonly ILogger<AddOrRemoveWatchlistController> _logger;

         private readonly IAddOrRemoveWatchlistHandler _addOrRemoveWatchlistHandler;


        public AddOrRemoveWatchlistController(ILogger<AddOrRemoveWatchlistController> logger, IAddOrRemoveWatchlistHandler addOrRemoveWatchlistHandler)
        {
            _logger = logger;
            _addOrRemoveWatchlistHandler = addOrRemoveWatchlistHandler;
        }

        [Function("addOrRemoveWatchlist")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var addOrRemoveRequest = JsonSerializer.Deserialize<AddOrRemoveWatchlistRequest>(requestBody, options);

            var data = await _addOrRemoveWatchlistHandler.AddOrRemoveItem(addOrRemoveRequest);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            var jsonResponse = JsonSerializer.Serialize(data);
            response.WriteString(jsonResponse);
            return response;
        }
    }
}
