using System;
using System.Collections.Generic;

namespace PortfolioStressLab.Wpf.Services
{
    public sealed class LoadedPortfolio
    {
        public string AccountId { get; init; } = "";
        public string BaseCurrency { get; init; } = "RUB";

        public List<PositionInstrument> Positions { get; init; } = new();

        public double DefaultOptionVol { get; init; }
        public double DefaultRiskFreeRate { get; init; }
        public double DefaultDividendYield { get; init; }
    }

    public sealed class PositionInstrument
    {
        public string InstrumentId { get; init; } = ""; // FIGI for shares/futures; PositionUid for options (fallback)
        public string Figi { get; init; } = ""; // FIGI if available (shares/futures)
        public string PositionUid { get; init; } = ""; // For options and instruments that have it
        public string Ticker { get; init; } = "";
        public string Name { get; init; } = "";
        public string InstrumentType { get; init; } = "";
        public string Currency { get; init; } = "RUB";

        public double Quantity { get; init; }
        public double LastPrice { get; init; } // for options may be 0 (we may not have last price)
        public double MarketValue { get; init; } // for options we can compute theoretical approx,or keep 0

        // --- Option metadata (if IsOption=true)
        public bool IsOption { get; init; }
        public string UnderlyingPositionUid { get; init; } = "";
        public string UnderlyingFigi { get; init; } = "";
        public double UnderlyingSpot { get; init; } // last price of underlying

        public double Strike { get; init; }
        public DateTime ExpirationUtc { get; init; }
        public bool IsCall { get; init; } // true=call,false=put
        public double ContractSize { get; init; } // BasicAssetSize (usually 1,10,100)
    }
}
