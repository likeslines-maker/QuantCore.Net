using PortfolioStressLab.Wpf.ViewModels;
using QuantCore.Net;
using QuantCore.Net.Pricing;
using QuantCore.Net.Risk;
using System;
using System.Collections.Generic;

namespace PortfolioStressLab.Wpf.Services
{
    public sealed class StressInputs
    {
        public double IndexShock { get; init; } // -0.12
        public double VolShock { get; init; } // +0.40
        public double RateShock { get; init; } // +0.015
        public double CorrelationCrisis { get; init; } // 0..1

        public double DefaultVol { get; init; }
        public double DefaultRate { get; init; }
        public double DefaultDividendYield { get; init; }
    }

    public sealed class StressResult
    {
        public double TotalMarketValue { get; init; }
        public double StressPnl { get; init; }
        public double? Var99 { get; init; }
        public double? Es99 { get; init; }
        public List<PositionRow> Rows { get; init; } = new();
    }

    public sealed class StressCalculator
    {
        public StressResult Calculate(LoadedPortfolio portfolio, StressInputs s, PortfolioHistory? history)
        {
            int n = portfolio.Positions.Count;

            // stressed params for options
            double r = s.DefaultRate + s.RateShock;
            double q = s.DefaultDividendYield;
            double vol = s.DefaultVol * (1.0 + s.VolShock);
            if (vol < 0.0001) vol = 0.0001;

            double crisisMul = 1.0 + 0.25 * s.CorrelationCrisis;

            double totalValue = 0.0;
            double totalStressPnl = 0.0;

            var rows = new List<PositionRow>(n);

            for (int i = 0; i < n; i++)
            {
                var p = portfolio.Positions[i];

                double delta = 0, vega = 0, rho = 0;
                double marketValueDisplay = p.MarketValue;

                double stressPnl;

                if (!p.IsOption)
                {
                    // SHARE/ETF/BOND/FUTURE MVP:
                    // Stress PnL from market shock only
                    totalValue += p.MarketValue;

                    stressPnl = p.MarketValue * s.IndexShock;
                }
                else
                {
                    // OPTION:
                    // If we have underlying spot,strike,expiration:use BSM Greeks
                    double spot = p.UnderlyingSpot;
                    double strike = p.Strike;
                    DateTime exp = p.ExpirationUtc;

                    double tYears = (exp > DateTime.UtcNow)
                    ? (exp.ToUniversalTime() - DateTime.UtcNow).TotalDays / 365.0
                    : 0.0;

                    var optType = p.IsCall ? OptionType.Call : OptionType.Put;

                    if (spot > 0 && strike > 0 && tYears > 0)
                    {
                        double stressedSpot = spot * (1.0 + s.IndexShock);

                        var g = BlackScholes.ComputeGreeks(optType, stressedSpot, strike, r, q, vol, tYears);
                        delta = g.Delta;
                        vega = g.Vega;
                        rho = g.Rho;

                        double scale = p.Quantity * p.ContractSize;

                        // Stress PnL approximation via Greeks:
                        // dV ≈ Δ  dS + ρ  dr + V * dσ
                        // Here dS = S * IndexShock; dr = RateShock; dσ = VolShock (relative,treated as absolute multiplier in this MVP)
                        double dS = stressedSpot * s.IndexShock;
                        double dr = s.RateShock;
                        double dSig = s.VolShock * vol; // simple proxy

                        stressPnl = scale*(delta * dS + rho * dr + vega * dSig);

                        // approximate value for totals (optional)
                        double theo = BlackScholes.Price(optType, stressedSpot, strike, r, q, vol, tYears) * scale;
                        totalValue += theo;
                        marketValueDisplay = theo;
                    }
                    else
                    {
                        // insufficient option metadata → no-op
                        stressPnl = 0.0;
                    }
                }

                stressPnl *= crisisMul;
                totalStressPnl += stressPnl;

                rows.Add(new PositionRow
                {
                    Ticker = p.Ticker,
                    Name = p.Name,
                    InstrumentType = p.IsOption ? "option" : p.InstrumentType,
                    Quantity = p.Quantity,
                    LastPrice = p.LastPrice > 0 ? p.LastPrice.ToString("0.0000") : "—",
                    MarketValue = marketValueDisplay > 0 ? marketValueDisplay.ToString("0.00") : "—",
                    StressPnl = stressPnl.ToString("0.00"),

                    Delta = delta != 0 ? delta.ToString("0.0000") : "",
                    Vega = vega != 0 ? vega.ToString("0.00") : "",
                    Rho = rho != 0 ? rho.ToString("0.00") : "",

                    MarketValueNum = marketValueDisplay,
                    StressPnlNum = stressPnl
                });
            }

            // VaR/ES from history (if loaded)
            double? var99 = null;
            double? es99 = null;

            if (history != null && history.PortfolioPnl.Length >= 20)
            {
                double scale = 1.0 + 0.8 * s.CorrelationCrisis;

                var tmp = new double[history.PortfolioPnl.Length];
                for (int i = 0; i < tmp.Length; i++)
                    tmp[i] = history.PortfolioPnl[i] * scale;

                var99 = HistoricalRisk.ValueAtRisk(tmp, 0.99);
                es99 = HistoricalRisk.ExpectedShortfall(tmp, 0.99);
            }

            return new StressResult
            {
                TotalMarketValue = totalValue,
                StressPnl = totalStressPnl,
                Var99 = var99,
                Es99 = es99,
                Rows = rows
            };
        }
    }
}
