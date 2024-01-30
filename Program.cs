using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TradeFunctions.Models.Postgres.TradeContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AssessmentDeck.Services;
using TradeFunctions.ImportMarketData;
using TradeFunctions.Services;
using TradeFunctions.ImportDailyIndicators;
using TradeFunctions.ListMarketStatistics;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddScoped<IDbConnectionStringService, DbConnectionStringService>();
        services.AddScoped<IImportMarketDataHandler, ImportMarketDataHandler>();
        services.AddScoped<IImportDailyIndicatorsHandler, ImportDailyIndicatorsHandler>();
        services.AddScoped<IListMarketStatisticsHandler, ListMarketStatisticsHandler>();
        services.AddScoped<ITwelveDataService, TwelveDataService>();
        var connectionString = Environment.GetEnvironmentVariable("TradeDatabase");
        services.AddDbContext<TradeContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddOptions<JsonSerializerOptions>()
                .Configure(options =>
                {
                    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.PropertyNameCaseInsensitive = true;
                });

        // Add DbContext configuration
        // var connectionString = hostContext.Configuration.GetConnectionString("TradeDatabase") ??
        //                        hostContext.Configuration["TradeDatabase"];

        // services.AddDbContext<TradeContext>(options =>
        //     options.UseNpgsql(connectionString));

    })
    .ConfigureAppConfiguration((context, builder) =>
    {

        builder.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .Build();

host.Run();
