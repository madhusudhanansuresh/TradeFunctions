using System.Collections.Generic;

namespace TradeFunctions.Models.Helpers;
public class StockDataResponse
{
    public List<StockData> Data { get; set; }
    public string Status { get; set; }
}

public class StockData
{
    public MetaData Meta { get; set; }
    public List<ValueData> Values { get; set; }
    public string Status { get; set; }
}

public class MetaData
{
    public string Symbol { get; set; }
    public string Interval { get; set; }
    public string Currency { get; set; }
    public string ExchangeTimezone { get; set; }
    public string Exchange { get; set; }
    public string MicCode { get; set; }
    public string Type { get; set; }
}

public class ValueData
{
    public string Datetime { get; set; }
    public string Open { get; set; }
    public string High { get; set; }
    public string Low { get; set; }
    public string Close { get; set; }
    public string Volume { get; set; }
}
