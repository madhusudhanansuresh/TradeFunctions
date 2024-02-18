using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TradeFunctions.Models.Postgres.TradeContext;

public partial class TradeContext : DbContext
{
    public TradeContext()
    {
    }

    public TradeContext(DbContextOptions<TradeContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChartPeriod> ChartPeriods { get; set; }

    public virtual DbSet<StockPrice> StockPrices { get; set; }

    public virtual DbSet<Ticker> Tickers { get; set; }

    public virtual DbSet<DailyIndicator> DailyIndicators { get; set; }

    public virtual DbSet<Watchlist> Watchlists { get; set; }

    //     protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // #warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //         => optionsBuilder.UseNpgsql("Host=devtradepostgres.postgres.database.azure.com;Database=postgres;Username=postgres_admin;Password=Sql-4567");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pg_catalog", "azure")
            .HasPostgresExtension("pg_catalog", "pgaadauth");

        modelBuilder.Entity<ChartPeriod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("chart_period_pkey");

            entity.ToTable("chart_period", "trade");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("create_dt");
            entity.Property(e => e.LastUpdateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_update_dt");
            entity.Property(e => e.TimeFrame)
                .HasMaxLength(255)
                .HasColumnName("time_frame");
            entity.Property(e => e.TimePeriod).HasColumnName("time_period");
        });

        modelBuilder.Entity<StockPrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("stock_price_pkey");

            entity.ToTable("stock_price", "trade");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChartId).HasColumnName("chart_id");
            entity.Property(e => e.ClosePrice).HasColumnName("close_price");
            entity.Property(e => e.CreateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("create_dt");
            entity.Property(e => e.HighPrice).HasColumnName("high_price");
            entity.Property(e => e.LastUpdateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_update_dt");
            entity.Property(e => e.Timestamp)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.LowPrice).HasColumnName("low_price");
            entity.Property(e => e.OpenPrice).HasColumnName("open_price");
            entity.Property(e => e.Rsrw).HasColumnName("rsrw");
            entity.Property(e => e.Rvol).HasColumnName("rvol");
            entity.Property(e => e.TickerId).HasColumnName("ticker_id");
            entity.Property(e => e.TradingVolume).HasColumnName("trading_volume");
            entity.Property(e => e.TransactionCount).HasColumnName("transaction_count");
            entity.Property(e => e.Vwap).HasColumnName("vwap");

            entity.HasOne(d => d.Chart).WithMany(p => p.StockPrices)
                .HasForeignKey(d => d.ChartId)
                .HasConstraintName("stock_price_chart_id_fkey");

            entity.HasOne(d => d.Ticker).WithMany(p => p.StockPrices)
                .HasForeignKey(d => d.TickerId)
                .HasConstraintName("stock_price_ticker_id_fkey");
        });

        modelBuilder.Entity<Ticker>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tickers_pkey");

            entity.ToTable("tickers", "trade");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(255)
                .HasColumnName("company_name");
            entity.Property(e => e.Active)
                .HasColumnName("active");
            entity.Property(e => e.CreateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("create_dt");
            entity.Property(e => e.LastUpdateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_update_dt");
            entity.Property(e => e.TickerName)
                .HasMaxLength(255)
                .HasColumnName("ticker_name");
        });

        modelBuilder.Entity<DailyIndicator>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("daily_indicators_pkey");

            entity.ToTable("daily_indicators", "trade");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Atr).HasColumnName("atr");
            entity.Property(e => e.CreateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("create_dt");
            entity.Property(e => e.LastUpdateDt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("last_update_dt");
            entity.Property(e => e.Timestamp)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("timestamp");
            entity.Property(e => e.TickerId).HasColumnName("ticker_id");

            entity.HasOne(d => d.Ticker).WithMany(p => p.DailyIndicators)
                .HasForeignKey(d => d.TickerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("daily_indicators_ticker_id_fkey");
        });

        modelBuilder.Entity<Watchlist>(entity =>
       {
           entity.HasKey(e => e.Id).HasName("watchlist_pkey");

           entity.ToTable("watchlist", "trade");

           entity.Property(e => e.Id).HasColumnName("id");
           entity.Property(e => e.CreateDt)
               .HasDefaultValueSql("CURRENT_TIMESTAMP")
               .HasColumnName("create_dt");
           entity.Property(e => e.LastUpdateDt)
               .HasDefaultValueSql("CURRENT_TIMESTAMP")
               .HasColumnName("last_update_dt");
           entity.Property(e => e.Reason).HasColumnName("reason");
           entity.Property(e => e.TickerId).HasColumnName("ticker_id");

           entity.HasOne(d => d.Ticker).WithMany(p => p.Watchlists)
               .HasForeignKey(d => d.TickerId)
               .HasConstraintName("fk_ticker_id");
       });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
