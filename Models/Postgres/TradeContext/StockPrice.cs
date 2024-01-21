using System;
using System.Collections.Generic;

namespace TradeFunctions.Models.Postgres.TradeContext;

public partial class StockPrice
{
    public int Id { get; set; }

    public int? TickerId { get; set; }

    public int? ChartId { get; set; }

    public decimal? ClosePrice { get; set; }

    public decimal? HighPrice { get; set; }

    public decimal? LowPrice { get; set; }

    public int? TransactionCount { get; set; }

    public decimal? OpenPrice { get; set; }

    public decimal? TradingVolume { get; set; }

    public decimal? Vwap { get; set; }

    public decimal? Rsrw { get; set; }

    public decimal? Rvol { get; set; }

    public DateTime? Timestamp { get; set; }

    public DateTime? CreateDt { get; set; }

    public DateTime? LastUpdateDt { get; set; }

    public virtual ChartPeriod? Chart { get; set; }

    public virtual Ticker? Ticker { get; set; }
}
