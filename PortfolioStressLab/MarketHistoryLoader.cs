using PortfolioStressLab.Wpf.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;

namespace PortfolioStressLab.Wpf.Services
{
    public sealed class PortfolioHistory
    {
        public int InstrumentsUsed { get; init; }
        public double[] PortfolioPnl { get; init; } = Array.Empty<double>();
    }

    public sealed class MarketHistoryLoader
    {
        private readonly TinkoffInvestClient _api;
        private readonly StressLabSettings _cfg;

        public MarketHistoryLoader(TinkoffInvestClient api, StressLabSettings cfg)
        {
            _api = api;
            _cfg = cfg;
        }

        public async Task<PortfolioHistory> LoadPortfolioHistoryAsync(LoadedPortfolio portfolio)
        {
            var selected = portfolio.Positions
            .Where(p => p.LastPrice > 0 && p.Quantity != 0)
            .OrderByDescending(p => Math.Abs(p.MarketValue))
            .Take(_cfg.MaxHistoryInstruments)
            .ToList();

            int days = _cfg.HistoryDays;
            DateTime to = DateTime.UtcNow;
            DateTime from = to.AddDays(-days);

            var series = new Dictionary<string, double[]>(StringComparer.Ordinal);

            foreach (var p in selected)
            {
                var resp = await _api.MarketData.GetCandlesAsync(new GetCandlesRequest
                {
                    Figi = p.Figi,
                    From = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(from.ToUniversalTime()),
                    To = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(to.ToUniversalTime()),
                    Interval = CandleInterval.Day
                });

                if (resp.Candles.Count < 20) continue;

                var closes = new double[resp.Candles.Count];
                for (int i = 0; i < closes.Length; i++)
                    closes[i] = resp.Candles[i].Close.ToDouble();

                series[p.Figi] = closes;
            }

            int used = series.Count;
            if (used == 0) return new PortfolioHistory { InstrumentsUsed = 0, PortfolioPnl = Array.Empty<double>() };

            int minLen = series.Values.Min(a => a.Length);
            if (minLen < 2) return new PortfolioHistory { InstrumentsUsed = used, PortfolioPnl = Array.Empty<double>() };

            var figis = series.Keys.ToArray();
            var qtyByFigi = portfolio.Positions.ToDictionary(x => x.Figi, x => x.Quantity, StringComparer.Ordinal);

            var pnl = new double[minLen - 1];
            for (int t = 1; t < minLen; t++)
            {
                double dayPnl = 0.0;
                for (int i = 0; i < figis.Length; i++)
                {
                    string figi = figis[i];
                    var closes = series[figi];
                    double qty = qtyByFigi.TryGetValue(figi, out var q) ? q : 0.0;
                    dayPnl += (closes[t] - closes[t - 1]) * qty;
                }
                pnl[t - 1] = dayPnl;
            }

            return new PortfolioHistory
            {
                InstrumentsUsed = used,
                PortfolioPnl = pnl
            };
        }
    }
}
