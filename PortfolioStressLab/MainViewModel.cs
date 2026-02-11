using PortfolioStressLab.Wpf.Infrastructure;
using PortfolioStressLab.Wpf.Services;
using PortfolioStressLab.Wpf.Settings;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using PortfolioStressLab.Wpf.Services;

namespace PortfolioStressLab.Wpf.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly TinkoffInvestClient _api;
        private readonly PortfolioLoader _loader;
        private readonly MarketHistoryLoader _history;
        private readonly StressCalculator _calc;
        private readonly StressLabSettings _cfg;

        private LoadedPortfolio? _portfolio;
        private PortfolioStressLab.Wpf.Services.PortfolioHistory? _portfolioHistory;

        public ObservableCollection<PositionRow> Positions { get; } = new();

        // Sliders
        private double _indexShockPct = -12;
        public double IndexShockPct { get => _indexShockPct; set { if (Set(ref _indexShockPct, value)) Recalculate(); } }

        private double _volShockPct = 40;
        public double VolShockPct { get => _volShockPct; set { if (Set(ref _volShockPct, value)) Recalculate(); } }

        private double _rateShockBps = 150;
        public double RateShockBps { get => _rateShockBps; set { if (Set(ref _rateShockBps, value)) Recalculate(); } }

        private double _corrCrisis = 0.35;
        public double CorrelationCrisis { get => _corrCrisis; set { if (Set(ref _corrCrisis, value)) Recalculate(); } }

        // UI text
        private string _status = "Ready.";
        public string Status { get => _status; set => Set(ref _status, value); }

        private string _connectedInfo = "";
        public string ConnectedInfo { get => _connectedInfo; set => Set(ref _connectedInfo, value); }

        private string _totalMarketValueText = "Total value:—";
        public string TotalMarketValueText { get => _totalMarketValueText; set => Set(ref _totalMarketValueText, value); }

        private string _stressPnlText = "Stress PnL:—";
        public string StressPnlText { get => _stressPnlText; set => Set(ref _stressPnlText, value); }

        private string _varText = "VaR(99%):—";
        public string VarText { get => _varText; set => Set(ref _varText, value); }

        private string _esText = "ES(99%):—";
        public string EsText { get => _esText; set => Set(ref _esText, value); }

        private string _notes = "";
        public string Notes { get => _notes; set => Set(ref _notes, value); }

        public AsyncRelayCommand LoadCommand { get; }
        public AsyncRelayCommand LoadHistoryCommand { get; }
        public RelayCommand RecalcCommand { get; }

        public MainViewModel(
        TinkoffInvestClient api,
        PortfolioLoader loader,
        MarketHistoryLoader history,
        StressCalculator calc,
        StressLabSettings cfg)
        {
            _api = api;
            _loader = loader;
            _history = history;
            _calc = calc;
            _cfg = cfg;

            LoadCommand = new AsyncRelayCommand(LoadPortfolioAsync);
            LoadHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => _portfolio != null);
            RecalcCommand = new RelayCommand(Recalculate, () => _portfolio != null);
        }

        private async Task LoadPortfolioAsync()
        {
            try
            {
                Status = "Connecting to Tinkoff Invest API...";
                var user = await _api.GetUserInfoAsync();

                ConnectedInfo = $"User:{user.UserId}";

                Status = "Loading portfolio positions and last prices...";
                _portfolio = await _loader.LoadAsync();

                Status = $"Loaded positions:{_portfolio.Positions.Count}.";
                Notes = "Tip:click “Load History (VaR/ES)” to enable Historical VaR/ES.";

                Recalculate();
            }
            catch (Exception ex)
            {
                Status = "Error:" + ex.Message;
            }
        }

        private async Task LoadHistoryAsync()
        {
            if (_portfolio == null) return;
            try
            {
                Status = "Loading candles history for VaR/ES (may take time)...";
                _portfolioHistory = await _history.LoadPortfolioHistoryAsync(_portfolio);

                Status = "History loaded. VaR/ES enabled.";
                Notes = $"HistoryDays={_cfg.HistoryDays},InstrumentsUsed={_portfolioHistory.InstrumentsUsed}/{_portfolio.Positions.Count}";

                Recalculate();
            }
            catch (Exception ex)
            {
                Status = "History load error:" + ex.Message;
            }
        }

        private void Recalculate()
        {
            if (_portfolio == null) return;

            var p = _portfolio;

            var inputs = new StressInputs
            {
                IndexShock = IndexShockPct / 100.0,
                VolShock = VolShockPct / 100.0,
                RateShock = RateShockBps / 10_000.0,// bps -> decimal rate
                CorrelationCrisis = CorrelationCrisis,
                DefaultVol = p.DefaultOptionVol,
                DefaultRate = p.DefaultRiskFreeRate,
                DefaultDividendYield = p.DefaultDividendYield
            };

            var result = _calc.Calculate(p, inputs, _portfolioHistory);

            // update header metrics
            TotalMarketValueText = $"Total value:{result.TotalMarketValue:0.00} {p.BaseCurrency}";
            StressPnlText = $"Stress PnL:{result.StressPnl:0.00} {p.BaseCurrency}";

            if (result.Var99.HasValue)
                VarText = $"VaR(99%):{result.Var99.Value:0.00} {p.BaseCurrency}";
            else
                VarText = "VaR(99%):— (load history)";

            if (result.Es99.HasValue)
                EsText = $"ES(99%):{result.Es99.Value:0.00} {p.BaseCurrency}";
            else
                EsText = "ES(99%):— (load history)";

            // update grid
            Positions.Clear();
            foreach (var row in result.Rows
            .OrderByDescending(x => Math.Abs(x.StressPnlNum))
            .Take(500))
            {
                Positions.Add(row);
            }

            // enable/disable buttons
            LoadHistoryCommand.RaiseCanExecuteChanged();
            RecalcCommand.RaiseCanExecuteChanged();
        }
    }
}
