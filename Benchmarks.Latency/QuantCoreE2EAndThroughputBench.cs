using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using QuantCore.Net;
using QuantCore.Net.Pricing;
using System;
using System.Runtime.CompilerServices;
using QuantCore.Net.Benchmarks.Latency;

[Config(typeof(Config))]
[MemoryDiagnoser]
public class QuantCoreE2EAndThroughputBench
{
    // ---- End-to-end batch sizes
    [Params(500, 5_000, 50_000)]
    public int N;

    // ---- Fixed throughput size
    private const int N1M = 1_000_000;

    private OptionInput[] _inputs = default!;
    private double[] _outPricesE2E = default!;

    // SoA arrays for throughput (PriceBatch)
    private double[] _S = default!;
    private double[] _K = default!;
    private double[] _R = default!;
    private double[] _Q = default!;
    private double[] _V = default!;
    private double[] _T = default!;
    private double[] _outPrices1M = default!;

    private OptionType _type = OptionType.Call;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(123);

        // Allocate max once; E2E will use first N
        _inputs = new OptionInput[Math.Max(N, 50_000)]; // enough for max param
        _outPricesE2E = new double[_inputs.Length];

        FillInputs(_inputs, rng);

        // Throughput arrays
        _S = new double[N1M];
        _K = new double[N1M];
        _R = new double[N1M];
        _Q = new double[N1M];
        _V = new double[N1M];
        _T = new double[N1M];
        _outPrices1M = new double[N1M];

        for (int i = 0; i < N1M; i++)
        {
            _S[i] = 50.0 + 100.0 * rng.NextDouble();
            _K[i] = 50.0 + 100.0 * rng.NextDouble();
            _R[i] = 0.00 + 0.05 * rng.NextDouble();
            _Q[i] = 0.00 + 0.03 * rng.NextDouble();
            _V[i] = 0.05 + 0.50 * rng.NextDouble();
            _T[i] = 0.01 + 2.00 * rng.NextDouble();
        }
    }

    // -----------------------
    // 1) End-to-End latency
    // -----------------------
    // "Data in -> data out":we consume an AoS input (struct array) and produce prices in an output array.
    // This represents a realistic integration surface and prevents JIT hoisting.
    [Benchmark(Description = "E2E_Price_AoS_To_Array_P99Target")]
    public double E2E_Price_AoS_To_Array()
    {
        var inp = _inputs.AsSpan(0, N);
        var dst = _outPricesE2E.AsSpan(0, N);

        // no allocations; full pass
        for (int i = 0; i < inp.Length; i++)
        {
            ref readonly var x = ref inp[i];
            dst[i] = PriceNoInline(_type, x.S, x.K, x.R, x.Q, x.Sigma, x.T);
        }

        // return a checksum to avoid DCE
        return dst[0] + dst[dst.Length - 1];
    }

    // -----------------------
    // 2) Throughput (1M+)
    // -----------------------
    [Benchmark(Description = "THROUGHPUT_PriceBatch_1M_SoA")]
    public double Throughput_PriceBatch_1M()
    {
        BlackScholes.PriceBatch(_type, _S, _K, _R, _Q, _V, _T, _outPrices1M);
        return _outPrices1M[0];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double PriceNoInline(OptionType type, double s, double k, double r, double q, double sigma, double t)
    => BlackScholes.Price(type, s, k, r, q, sigma, t);

    private static void FillInputs(Span<OptionInput> a, Random rng)
    {
        for (int i = 0; i < a.Length; i++)
        {
            a[i] = new OptionInput(
            S: 50.0 + 100.0 * rng.NextDouble(),
            K: 50.0 + 100.0 * rng.NextDouble(),
            R: 0.00 + 0.05 * rng.NextDouble(),
            Q: 0.00 + 0.03 * rng.NextDouble(),
            Sigma: 0.05 + 0.50 * rng.NextDouble(),
            T: 0.01 + 2.00 * rng.NextDouble()
            );
        }
    }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
            .WithWarmupCount(3)
            .WithIterationCount(20)
            .WithLaunchCount(1));

            AddDiagnoser(MemoryDiagnoser.Default);

            AddColumn(
            StatisticColumn.Min,
            StatisticColumn.Mean,
            StatisticColumn.Median,
            new PxxColumn(95),
            new PxxColumn(99),
            StatisticColumn.Max
            );

            AddColumnProvider(DefaultColumnProviders.Instance);
        }
    }



    public readonly record struct OptionInput(double S, double K, double R, double Q, double Sigma, double T);

    
}
