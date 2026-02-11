namespace PortfolioStressLab.Wpf.Settings
{
    public sealed class TinkoffSettings
    {
        public string? Token { get; set; }
        public string? AccountId { get; set; }
        public bool UseSandbox { get; set; } = false;
    }
}