using System;
using System.Collections.Generic;

namespace TradeFunctions.Models.Postgres.TradeContext;

public partial class ChartPeriod
{
    public int Id { get; set; }

    public int? TimePeriod { get; set; }

    public string? TimeFrame { get; set; }

    public DateTime? CreateDt { get; set; }

    public DateTime? LastUpdateDt { get; set; }

    public virtual ICollection<StockPrice> StockPrices { get; set; } = new List<StockPrice>();
}
