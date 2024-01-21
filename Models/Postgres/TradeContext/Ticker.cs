using System;
using System.Collections.Generic;

namespace TradeFunctions.Models.Postgres.TradeContext;

public partial class Ticker
{
    public int Id { get; set; }

    public string? TickerName { get; set; }

    public string? CompanyName { get; set; }

    public DateTime? CreateDt { get; set; }

    public DateTime? LastUpdateDt { get; set; }

    public virtual ICollection<StockPrice> StockPrices { get; set; } = new List<StockPrice>();
}
