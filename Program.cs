using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TradeFunctions.Models.Postgres.TradeContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();

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
