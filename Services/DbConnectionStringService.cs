using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TradeFunctions.Models.Postgres.TradeContext;

namespace AssessmentDeck.Services
{
    public interface IDbConnectionStringService
    {
        DbContextOptions<TradeContext> ConnectionString();
    }

    public class DbConnectionStringService : IDbConnectionStringService
    {
        private readonly IConfiguration _config;

        public DbConnectionStringService(IConfiguration config)
        {
            _config = config;
        }
        public DbContextOptions<TradeContext> ConnectionString()
        {
          return new DbContextOptionsBuilder<TradeContext>().UseNpgsql(Environment.GetEnvironmentVariable("TradeDatabase", EnvironmentVariableTarget.Process)) 
          .EnableSensitiveDataLogging()
          .Options;
        }
    }
}