using System;
using Tinkoff.InvestApi.V1;

namespace PortfolioStressLab.Wpf.Services
{
    public static class QuotationExtensions
    {
        public static double ToDouble(this Quotation q)
        => q.Units + q.Nano / 1_000_000_000.0;

        public static double ToDouble(this MoneyValue v)
        => v.Units + v.Nano / 1_000_000_000.0;

        public static double ToDoubleOrZero(this Quotation? q)
        => q is null ? 0.0 : q.ToDouble();

        public static DateTime ToUtcDateTime(this Google.Protobuf.WellKnownTypes.Timestamp ts)
        => ts.ToDateTime().ToUniversalTime();
    }
}
