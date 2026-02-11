namespace PortfolioStressLab.Wpf.ViewModels
{
    public sealed class PositionRow
    {
        public string Ticker { get; init; } = "";
        public string Name { get; init; } = "";
        public string InstrumentType { get; init; } = "";

        public double Quantity { get; init; }
        public string LastPrice { get; init; } = "";
        public string MarketValue { get; init; } = "";

        public string StressPnl { get; init; } = "";

        // Greeks (only if option recognized)
        public string Delta { get; init; } = "";
        public string Vega { get; init; } = "";
        public string Rho { get; init; } = "";

        // Internal numeric fields (optional)
        public double MarketValueNum { get; init; }
        public double StressPnlNum { get; init; }
    }
}
