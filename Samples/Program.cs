using QuantCore.Net;
using QuantCore.Net.MonteCarlo;
using QuantCore.Net.Pricing;
using QuantCore.Net.Risk;
using QuantCore.Net.TimeSeries;
using SlidingRank.FastOps;
using System;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("QuantCore.Net Sample");
        Console.WriteLine("====================");
        Console.WriteLine();

        Example_BlackScholes_Single();
        Example_BlackScholes_Batch_And_Greeks();
        Example_MonteCarlo();
        Example_VaR_ES();
        Example_FactorModelPnL();
        Example_TimeSeriesMoments();

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    private static void Example_BlackScholes_Single()
    {
        Console.WriteLine("1) Black–Scholes (single)");
        var type = OptionType.Call;

        double s = 100;
        double k = 100;
        double r = 0.03;
        double q = 0.01;
        double vol = 0.20;
        double t = 0.5;

        double price = BlackScholes.Price(type, s, k, r, q, vol, t);
        Greeks g = BlackScholes.ComputeGreeks(type, s, k, r, q, vol, t);

        Console.WriteLine($" Price:{price:F6}");
        Console.WriteLine($" Greeks:Δ={g.Delta:F6} Γ={g.Gamma:F6} V={g.Vega:F6} Θ={g.Theta:F6} ρ={g.Rho:F6}");
        Console.WriteLine();
    }

    private static void Example_BlackScholes_Batch_And_Greeks()
    {
        Console.WriteLine("2) Black–Scholes (batch pricing + greeks)");

        const int n = 100_000;
        var rng = new Random(123);

        var S = new double[n];
        var K = new double[n];
        var R = new double[n];
        var Q = new double[n];
        var V = new double[n];
        var T = new double[n];

        for (int i = 0; i < n; i++)
        {
            S[i] = 50 + 100 * rng.NextDouble();
            K[i] = 50 + 100 * rng.NextDouble();
            R[i] = 0.00 + 0.05 * rng.NextDouble();
            Q[i] = 0.00 + 0.03 * rng.NextDouble();
            V[i] = 0.05 + 0.50 * rng.NextDouble();
            T[i] = 0.01 + 2.00 * rng.NextDouble();
        }

        var prices = new double[n];
        var greeks = new Greeks[n];

        BlackScholes.PriceBatch(OptionType.Call, S, K, R, Q, V, T, prices);
        BlackScholes.GreeksBatch(OptionType.Call, S, K, R, Q, V, T, greeks);

        // simple sanity:print first element
        Console.WriteLine($" Price[0]:{prices[0]:F6}");
        Console.WriteLine($" Delta[0]:{greeks[0].Delta:F6},Gamma[0]:{greeks[0].Gamma:F6}");
        Console.WriteLine();
    }

    private static void Example_MonteCarlo()
    {
        Console.WriteLine("3) Monte Carlo (European GBM,antithetic,deterministic)");

        var type = OptionType.Call;
        double s = 100, k = 100, r = 0.03, q = 0.01, vol = 0.20, t = 0.5;

        double mc10k = MonteCarloOptionPricing.PriceEuropeanGbmAntithetic(
        type, s, k, r, q, vol, t,
        paths: 10_000,
        seed: 12345);

        double bs = BlackScholes.Price(type, s, k, r, q, vol, t);

        Console.WriteLine($" MC(10k paths):{mc10k:F6}");
        Console.WriteLine($" BS (analytic):{bs:F6}");
        Console.WriteLine($" Abs diff:{Math.Abs(mc10k - bs):F6}");
        Console.WriteLine();
    }

    private static void Example_VaR_ES()
    {
        Console.WriteLine("4) Historical VaR / ES (100k PnL)");

        const int n = 100_000;
        var rng = new Random(7);

        var pnl = new double[n];
        for (int i = 0; i < n; i++)
        {
            // synthetic PnL:normal + rare shock loss
            double x = NextNormal(rng) * 1000.0;
            if ((i & 1023) == 0) x -= 8000.0;
            pnl[i] = x;
        }

        double var99 = HistoricalRisk.ValueAtRisk(pnl, alpha: 0.99);
        double es99 = HistoricalRisk.ExpectedShortfall(pnl, alpha: 0.99);

        Console.WriteLine($" VaR 99%:{var99:F2}");
        Console.WriteLine($" ES 99%:{es99:F2}");
        Console.WriteLine();
    }

    private static void Example_FactorModelPnL()
    {
        Console.WriteLine("5) Factor-model PnL (SIMD dot via SlidingRank.FastOps)");

        const int positions = 100_000;
        const int factors = 64;

        var rng = new Random(42);

        // exposures matrix:positions x factors (float)
        var data = new float[positions * factors];
        for (int i = 0; i < data.Length; i++)
            data[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        var exposures = new EmbeddingMatrix(data, positions, factors);

        // factor returns
        var factorReturns = new float[factors];
        for (int i = 0; i < factors; i++)
            factorReturns[i] = (float)(rng.NextDouble() * 0.02 - 0.01);

        // notionals
        var notionals = new float[positions];
        for (int i = 0; i < positions; i++)
            notionals[i] = (float)(100_000 + 900_000 * rng.NextDouble());

        var outPnl = new float[positions];

        FactorModelPnlFast.ComputePnL(exposures, factorReturns, notionals, outPnl);

        Console.WriteLine($" PnL[0]:{outPnl[0]:F2}");
        Console.WriteLine($" PnL[1]:{outPnl[1]:F2}");
        Console.WriteLine();
    }

    private static void Example_TimeSeriesMoments()
    {
        Console.WriteLine("6) Time-series moments");

        var rng = new Random(99);
        double[] x = new double[10_000];
        for (int i = 0; i < x.Length; i++)
            x[i] = NextNormal(rng);

        var m = Stats.ComputeMoments(x);

        Console.WriteLine($" Mean:{m.Mean:F6}");
        Console.WriteLine($" Var:{m.Variance:F6}");
        Console.WriteLine($" Skew:{m.Skewness:F6}");
        Console.WriteLine($" Kurtosis:{m.Kurtosis:F6}");
        Console.WriteLine();
    }

    private static double NextNormal(Random rng)
    {
        // Box–Muller
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
