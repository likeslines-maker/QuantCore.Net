using PortfolioStressLab.Wpf.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;

namespace PortfolioStressLab.Wpf.Services
{
    public sealed class PortfolioLoader
    {
        private readonly TinkoffInvestClient _api;
        private readonly StressLabSettings _cfg;

        public PortfolioLoader(TinkoffInvestClient api, StressLabSettings cfg)
        {
            _api = api;
            _cfg = cfg;
        }

        public async Task<LoadedPortfolio> LoadAsync()
        {
            string accountId = await _api.GetPrimaryAccountIdAsync();

            // balances
            var pos = await _api.Ops.GetPositionsAsync(new PositionsRequest { AccountId = accountId });

            // --- Load directories
            var figiInfo = new Dictionary<string, InstInfo>(StringComparer.Ordinal);
            var posUidToFigi = new Dictionary<string, string>(StringComparer.Ordinal);

            void addFigi(string figi, string ticker, string name, string type, string currency, string positionUid)
            {
                if (string.IsNullOrWhiteSpace(figi)) return;
                figiInfo[figi] = new InstInfo(ticker, name, type, currency, positionUid);
                if (!string.IsNullOrWhiteSpace(positionUid))
                    posUidToFigi[positionUid] = figi;
            }

            // Shares/ETFs/Bonds/Futures in 0.6.18 are fetched without request object
            var shares = await _api.Instruments.SharesAsync(cancellationToken: default);
            foreach (var x in shares.Instruments)
                addFigi(x.Figi, x.Ticker ?? "", x.Name ?? "", "share", x.Currency ?? "RUB", x.PositionUid ?? "");

            var etfs = await _api.Instruments.EtfsAsync(cancellationToken: default);
            foreach (var x in etfs.Instruments)
                addFigi(x.Figi, x.Ticker ?? "", x.Name ?? "", "etf", x.Currency ?? "RUB", x.PositionUid ?? "");

            var bonds = await _api.Instruments.BondsAsync(cancellationToken: default);
            foreach (var x in bonds.Instruments)
                addFigi(x.Figi, x.Ticker ?? "", x.Name ?? "", "bond", x.Currency ?? "RUB", x.PositionUid ?? "");

            var futures = await _api.Instruments.FuturesAsync(cancellationToken: default);
            foreach (var x in futures.Instruments)
                addFigi(x.Figi, x.Ticker ?? "", x.Name ?? "", "future", x.Currency ?? "RUB", x.PositionUid ?? "");

            // Options directory:PositionUid -> Option
            var optByPosUid = new Dictionary<string, Option>(StringComparer.Ordinal);
            var opts = await _api.Instruments.OptionsAsync(cancellationToken: default);
            foreach (var o in opts.Instruments)
            {
                if (!string.IsNullOrWhiteSpace(o.PositionUid))
                    optByPosUid[o.PositionUid] = o;
            }

            // --- Collect FIGIs we can query last prices for:
            var figisForLast = new HashSet<string>(StringComparer.Ordinal);

            foreach (var s in pos.Securities) if (!string.IsNullOrWhiteSpace(s.Figi)) figisForLast.Add(s.Figi);
            foreach (var f in pos.Futures) if (!string.IsNullOrWhiteSpace(f.Figi)) figisForLast.Add(f.Figi);

            // For options:need underlying last price
            foreach (var oPos in pos.Options)
            {
                if (string.IsNullOrWhiteSpace(oPos.PositionUid)) continue;
                if (!optByPosUid.TryGetValue(oPos.PositionUid, out var opt)) continue;

                var underPosUid = opt.BasicAssetPositionUid;
                if (!string.IsNullOrWhiteSpace(underPosUid) && posUidToFigi.TryGetValue(underPosUid, out var underFigi))
                    figisForLast.Add(underFigi);
            }

            var figiList = figisForLast.ToList();

            // last prices by FIGI
            var lp = await _api.MarketData.GetLastPricesAsync(new GetLastPricesRequest { Figi = { figiList } });
            var lastByFigi = lp.LastPrices.ToDictionary(x => x.Figi, x => x.Price.ToDouble(), StringComparer.Ordinal);

            // --- Build positions list
            var positions = new List<PositionInstrument>(pos.Securities.Count + pos.Futures.Count + pos.Options.Count);

            // Securities
            foreach (var s in pos.Securities)
            {
                if (string.IsNullOrWhiteSpace(s.Figi)) continue;

                double last = lastByFigi.TryGetValue(s.Figi, out var p) ? p : 0.0;
                figiInfo.TryGetValue(s.Figi, out var info);

                double qty = s.Balance;

                positions.Add(new PositionInstrument
                {
                    InstrumentId = s.Figi,
                    Figi = s.Figi,
                    PositionUid = info.PositionUid,
                    Ticker = info.Ticker,
                    Name = info.Name,
                    InstrumentType = info.Type,
                    Currency = info.Currency,
                    Quantity = qty,
                    LastPrice = last,
                    MarketValue = last * qty,
                    IsOption = false
                });
            }

            // Futures
            foreach (var f in pos.Futures)
            {
                if (string.IsNullOrWhiteSpace(f.Figi)) continue;

                double last = lastByFigi.TryGetValue(f.Figi, out var p) ? p : 0.0;
                figiInfo.TryGetValue(f.Figi, out var info);

                double qty = f.Balance;

                positions.Add(new PositionInstrument
                {
                    InstrumentId = f.Figi,
                    Figi = f.Figi,
                    PositionUid = info.PositionUid,
                    Ticker = info.Ticker,
                    Name = info.Name,
                    InstrumentType = info.Type,
                    Currency = info.Currency,
                    Quantity = qty,
                    LastPrice = last,
                    MarketValue = last * qty,
                    IsOption = false
                });
            }

            // Options
            foreach (var oPos in pos.Options)
            {
                if (string.IsNullOrWhiteSpace(oPos.PositionUid)) continue;
                if (!optByPosUid.TryGetValue(oPos.PositionUid, out var opt)) continue;

                double qty = oPos.Balance;

                string underPosUid = opt.BasicAssetPositionUid ?? "";
                string underFigi = (!string.IsNullOrWhiteSpace(underPosUid) && posUidToFigi.TryGetValue(underPosUid, out var uf)) ? uf : "";
                double underSpot = (!string.IsNullOrWhiteSpace(underFigi) && lastByFigi.TryGetValue(underFigi, out var us)) ? us : 0.0;

                double strike = opt.StrikePrice != null ? opt.StrikePrice.ToDouble() : 0.0;
                DateTime expUtc = opt.ExpirationDate != null ? opt.ExpirationDate.ToUtcDateTime() : DateTime.MinValue;

                bool isCall = opt.Direction == OptionDirection.Call;
                double contractSize = opt.BasicAssetSize.ToDoubleOrZero();
                if (contractSize <= 0) contractSize = 1.0;

                positions.Add(new PositionInstrument
                {
                    InstrumentId = opt.PositionUid ?? oPos.PositionUid,
                    Figi = "",
                    PositionUid = opt.PositionUid ?? oPos.PositionUid,
                    Ticker = opt.Ticker ?? "",
                    Name = opt.Name ?? "Option",
                    InstrumentType = "option",
                    Currency = opt.Currency ?? "RUB",
                    Quantity = qty,

                    LastPrice = 0.0,
                    MarketValue = 0.0,

                    IsOption = true,
                    UnderlyingPositionUid = underPosUid,
                    UnderlyingFigi = underFigi,
                    UnderlyingSpot = underSpot,
                    Strike = strike,
                    ExpirationUtc = expUtc,
                    IsCall = isCall,
                    ContractSize = contractSize
                });
            }

            string baseCcy = positions.FirstOrDefault()?.Currency ?? "RUB";

            return new LoadedPortfolio
            {
                AccountId = accountId,
                BaseCurrency = baseCcy,
                Positions = positions,
                DefaultOptionVol = _cfg.DefaultOptionVol,
                DefaultRiskFreeRate = _cfg.DefaultRiskFreeRate,
                DefaultDividendYield = _cfg.DefaultDividendYield
            };
        }

        private readonly record struct InstInfo(string Ticker, string Name, string Type, string Currency, string PositionUid);
    }
}
