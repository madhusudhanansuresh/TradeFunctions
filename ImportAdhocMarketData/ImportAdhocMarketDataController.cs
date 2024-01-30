using System;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace TradeFunctions.ImportMarketData
{
    public class ImportAdhocMarketDataController
    {
        private readonly ILogger _logger;

        private readonly IImportAdhocMarketDataHandler _importMarketData;

        public ImportAdhocMarketDataController(ILoggerFactory loggerFactory, IImportAdhocMarketDataHandler importMarketData)
        {
            _logger = loggerFactory.CreateLogger<ImportAdhocMarketDataController>();
            _importMarketData = importMarketData;
        }

        [Function("importAdhocMarketData")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData request)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
                var importRequest = JsonSerializer.Deserialize<ImportAdhocMarketDataRequest>(requestBody);

                _importMarketData.ImportMarketData(importRequest);

                var response = request.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                response.WriteString("Market data import initiated successfully.");
                return response;
            }
            catch (JsonException je)
            {
                _logger.LogError($"JSON Error: {je.Message}");
                var errorResponse = request.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("Invalid request data.");
                return errorResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.WriteString("An error occurred during the market data import.");
                return errorResponse;
            }


        }
    }
}

//Request
// {
//     "symbols": [],
//     "intervals": ["5min"],
//     "startDate": "2024-01-04 00:00:00",
//     "endDate": "2024-01-30 00:00:00"
// }
