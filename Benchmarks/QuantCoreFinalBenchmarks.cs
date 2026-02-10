using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using QuantCore.Net;
using QuantCore.Net.MonteCarlo;
using QuantCore.Net.Pricing;
using QuantCore.Net.Risk;
using System;
using BenchmarkDotNet.Engines;
using System.Runtime.CompilerServices;

[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 10)]
[MemoryDiagnoser]
public class QuantCoreFinalBenchmarks
{
    // ------------ params (kept minimal to avoid hours-long runs)
    [Params(100_000)]
    public int BatchSize;

    [Params(32, 64)]
    public int FactorDim;

    // ------------ Black-Scholes single inputs
    private OptionType _type = OptionType.Call;
    private double _s, _k, _r, _q, _sigma, _t;

    // ------------ Black-Scholes batch inputs
    private double[] _S = default!;
    private double[] _K = default!;
    private double[] _R = default!;
    private double[] _Q = default!;
    private double[] _V = default!;
    private double[] _T = default!;
    private double[] _outPrice = default!;
    private Greeks[] _outGreeks = default!;

    // ------------ Monte Carlo
    private int _mcPaths = 10_000;

    // ------------ Risk PnL (VaR/ES)
    private double[] _pnl = default!;
    private double _alpha = 0.99;

    // ------------ Factor model PnL (float fast path using SlidingRank SIMD)
    private SlidingRank.FastOps.EmbeddingMatrix _exposures;
    private float[] _factorReturns = default!;
    private float[] _notionals = default!;
    private float[] _outPnl = default!;

    private readonly Consumer _consumer = new Consumer();

    // single inputs arrays (to prevent hoisting)
    private double[] _s1 = default!;
    private double[] _k1 = default!;
    private double[] _r1 = default!;
    private double[] _q1 = default!;
    private double[] _v1 = default!;
    private double[] _t1 = default!;
    private int _singleIdx;
    private int _singleMask;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(123);

        // Single option
        _s = 100.0;
        _k = 100.0;
        _r = 0.03;
        _q = 0.01;
        _sigma = 0.2;
        _t = 0.5;

        // Prepare a small power-of-two set of varying inputs for single-op benchmark
        int singleN = 1024;
        _s1 = new double[singleN];
        _k1 = new double[singleN];
        _r1 = new double[singleN];
        _q1 = new double[singleN];
        _v1 = new double[singleN];
        _t1 = new double[singleN];

        for (int i = 0; i < singleN; i++)
        {
            _s1[i] = 50.0 + 100.0 * rng.NextDouble();
            _k1[i] = 50.0 + 100.0 * rng.NextDouble();
            _r1[i] = 0.00 + 0.05 * rng.NextDouble();
            _q1[i] = 0.00 + 0.03 * rng.NextDouble();
            _v1[i] = 0.05 + 0.50 * rng.NextDouble();
            _t1[i] = 0.01 + 2.00 * rng.NextDouble();
        }

        _singleIdx = 0;
        _singleMask = singleN - 1; // singleN must be power of two

        // Batch arrays
        int n = BatchSize;
        _S = new double[n];
        _K = new double[n];
        _R = new double[n];
        _Q = new double[n];
        _V = new double[n];
        _T = new double[n];

        _outPrice = new double[n];
        _outGreeks = new Greeks[n];

        for (int i = 0; i < n; i++)
        {
            // Keep inputs realistic and non-degenerate
            _S[i] = 50.0 + 100.0 * rng.NextDouble(); // 50..150
            _K[i] = 50.0 + 100.0 * rng.NextDouble(); // 50..150
            _R[i] = 0.00 + 0.05 * rng.NextDouble(); // 0..5%
            _Q[i] = 0.00 + 0.03 * rng.NextDouble(); // 0..3%
            _V[i] = 0.05 + 0.50 * rng.NextDouble(); // 5%..55%
            _T[i] = 0.01 + 2.00 * rng.NextDouble(); // 0.01..2 years
        }

        // PnL for VaR/ES (negative = losses)
        _pnl = new double[n];
        for (int i = 0; i < n; i++)
        {
            // synthetic heavy-ish tails:normal + occasional shock
            double x = NextNormal(rng) * 1000.0;
            if ((i & 1023) == 0) x -= 8000.0; // rare shock loss
            _pnl[i] = x;
        }

        // Factor exposures (float) for 100k positions
        var expData = new float[n * FactorDim];
        for (int i = 0; i < expData.Length; i++)
            expData[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        _exposures = new SlidingRank.FastOps.EmbeddingMatrix(expData, n, FactorDim);

        _factorReturns = new float[FactorDim];
        for (int i = 0; i < FactorDim; i++)
            _factorReturns[i] = (float)(rng.NextDouble() * 0.02 - 0.01); // -1%..+1%

        _notionals = new float[n];
        for (int i = 0; i < n; i++)
            _notionals[i] = (float)(100_000 + 900_000 * rng.NextDouble()); // 100k..1M

        _outPnl = new float[n];
    }

    // -------------------------
    // Benchmarks
    // -------------------------

    [Benchmark(Description = "BlackScholes_Price_Single_NoHoist")]
    public double BlackScholes_Price_Single_NoHoist()
    {
        int i = _singleIdx;
        _singleIdx = (i + 1) & _singleMask;

        double price = PriceNoInline(
        _type,
        _s1[i], _k1[i],
        _r1[i], _q1[i],
        _v1[i], _t1[i]);

        // prevent dead-code elimination
        _consumer.Consume(price);
        return price;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double PriceNoInline(
     OptionType type,
     double s, double k,
     double r, double q,
     double sigma, double t)
    {
        return BlackScholes.Price(type, s, k, r, q, sigma, t);
    }


    [Benchmark(Description = "BlackScholes_Price_Batch_100k")]
    public double BlackScholes_Price_Batch()
    {
        BlackScholes.PriceBatch(_type, _S, _K, _R, _Q, _V, _T, _outPrice);
        return _outPrice[0];
    }

    [Benchmark(Description = "BlackScholes_Greeks_Batch_100k")]
    public double BlackScholes_Greeks_Batch()
    {
        BlackScholes.GreeksBatch(_type, _S, _K, _R, _Q, _V, _T, _outGreeks);

        // return something stable
        double acc = 0;
        acc += _outGreeks[0].Delta;
        acc += _outGreeks[1].Gamma;
        acc += _outGreeks[2].Vega;
        return acc;
    }

    [Benchmark(Description = "MonteCarlo_Euro_GBM_Antithetic_10kPaths")]
    public double MonteCarlo_Euro_GBM_Antithetic_10kPaths()
    => MonteCarloOptionPricing.PriceEuropeanGbmAntithetic(
    _type, _s, _k, _r, _q, _sigma, _t,
    paths: _mcPaths,
    seed: 12345);

    [Benchmark(Description = "Historical_VaR_99_ArrayPool_100k")]
    public double Historical_VaR_99_ArrayPool()
    => HistoricalRisk.ValueAtRisk(_pnl, _alpha);

    [Benchmark(Description = "Historical_ES_99_ArrayPool_100k")]
    public double Historical_ES_99_ArrayPool()
    => HistoricalRisk.ExpectedShortfall(_pnl, _alpha);

    [Benchmark(Description = "FactorModelPnL_SIMD_100k")]
    public float FactorModelPnL_SIMD_100k()
    {
        FactorModelPnlFast.ComputePnL(_exposures, _factorReturns, _notionals, _outPnl);
        return _outPnl[0];
    }

    // -------------------------
    // Helpers
    // -------------------------

    private static double NextNormal(Random rng)
    {
        // Box–Muller
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
