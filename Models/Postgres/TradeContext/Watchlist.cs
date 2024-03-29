﻿using System;
using System.Collections.Generic;

namespace TradeFunctions.Models.Postgres.TradeContext;

public partial class Watchlist
{
    public int Id { get; set; }

    public int TickerId { get; set; }

    public string? Reason { get; set; }

    public DateTime? CreateDt { get; set; }

    public DateTime? LastUpdateDt { get; set; }

    public virtual Ticker Ticker { get; set; } = null!;
}
