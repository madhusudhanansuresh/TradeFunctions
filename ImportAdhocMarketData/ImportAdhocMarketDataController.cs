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

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            var importRequest = JsonSerializer.Deserialize<ImportAdhocMarketDataRequest>(requestBody, options);

            var data = await _importMarketData.ImportMarketData(importRequest);

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            var jsonResponse = JsonSerializer.Serialize(data);

            response.WriteString(jsonResponse);

            return response;

        }
    }
}

