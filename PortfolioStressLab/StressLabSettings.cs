namespace PortfolioStressLab.Wpf.Settings
{
    public sealed class StressLabSettings
    {
        public int HistoryDays { get; set; } = 180;
        public int MaxHistoryInstruments { get; set; } = 50;

        public double DefaultOptionVol { get; set; } = 0.50;
        public double DefaultRiskFreeRate { get; set; } = 0.10;
        public double DefaultDividendYield { get; set; } = 0.00;
    }
}
