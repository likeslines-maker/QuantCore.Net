QuantCore.Net



QuantCore.Net is a high-performance .NET library for quantitative finance computations with low latency,deterministic behavior,and allocation-aware APIs.



It is designed as an embedded compute core (in-process):you call it directly from your .NET pricing engines,risk services,or research tools.



---



Why QuantCore.Net



Financial systems often require:

very high throughput (100k+ instruments per request)

predictable latency (tight SLAs / low jitter)

numerical stability under extreme inputs

control over allocations (GC pressure matters in 24/7 services)



QuantCore.Net focuses on a minimal set of core quantitative “bricks” that are fast and composable.



---



Features (MVP core)



Pricing

Black–Scholes–Merton (European options,continuous dividend yield q)

Batch pricing (PriceBatch)

Batch Greeks (GreeksBatch)



Monte Carlo

European GBM pricing with antithetic variates

Deterministic RNG (XorShift128Plus) for reproducible results



Risk Analytics

Historical VaR and Expected Shortfall (ES/CVaR)

In-place and non-mutating overloads (ArrayPool-backed)



Time Series

Statistical moments:mean,variance,skewness,kurtosis (stable one-pass)



Factor Risk / PnL

Fast factor-model PnL approximation using SIMD dot products via SlidingRank.FastOps



---



Install

```bash

dotnet add package QuantCore.Net

```



QuantCore.Net packages the required low-level SIMD core (SlidingRank.dll) inside the same NuGet package for convenience.



---



Quick Start



Black–Scholes price

```csharp

using QuantCore.Net;

using QuantCore.Net.Pricing;



double price = BlackScholes.Price(

&nbsp;type:OptionType.Call,

&nbsp;s:100,

&nbsp;k:100,

&nbsp;r:0.03,

&nbsp;q:0.01,

&nbsp;sigma:0.20,

&nbsp;t:0.5);

```



Batch pricing (zero-alloc output)

```csharp

using QuantCore.Net;

using QuantCore.Net.Pricing;



BlackScholes.PriceBatch(

&nbsp;type:OptionType.Call,

&nbsp;s:S,

&nbsp;k:K,

&nbsp;r:R,

&nbsp;q:Q,

&nbsp;sigma:Vol,

&nbsp;t:T,

&nbsp;outPrice:prices);

```



Batch Greeks

```csharp

using QuantCore.Net;

using QuantCore.Net.Pricing;



BlackScholes.GreeksBatch(

&nbsp;type:OptionType.Put,

&nbsp;s:S,

&nbsp;k:K,

&nbsp;r:R,

&nbsp;q:Q,

&nbsp;sigma:Vol,

&nbsp;t:T,

&nbsp;outGreeks:greeks);

```



Monte Carlo (European GBM,antithetic,deterministic)

```csharp

using QuantCore.Net;

using QuantCore.Net.MonteCarlo;



double mc = MonteCarloOptionPricing.PriceEuropeanGbmAntithetic(

&nbsp;type:OptionType.Call,

&nbsp;s:100,k:100,

&nbsp;r:0.03,q:0.01,

&nbsp;sigma:0.20,t:0.5,

&nbsp;paths:10\_000,

&nbsp;seed:12345);

```



Historical VaR / ES (non-mutating overloads)

```csharp

using QuantCore.Net.Risk;



double var99 = HistoricalRisk.ValueAtRisk(pnl,alpha:0.99);

double es99 = HistoricalRisk.ExpectedShortfall(pnl,alpha:0.99);

```



Factor model PnL (SIMD dot)

```csharp

using QuantCore.Net.Risk;

using SlidingRank.FastOps;



FactorModelPnlFast.ComputePnL(exposures,factorReturns,notionals,outPnl);

```



---



Performance



Environment

CPU:Intel Core i5-11400F (6C/12T)

OS:Windows 11

.NET:8.0.23

BenchmarkDotNet:0.15.8



Benchmark summary (BatchSize = 100,000)



| Method                                          | Mean               | Notes                                   |
|-------------------------------------------------|--------------------|-----------------------------------------|
| BlackScholes_Price_Single_NoHoist               | ~41–44 ns          | single call (varying inputs + anti-hoist guard) |
| BlackScholes_Price_Batch_100k                   | ~5.11–5.13 ms      | ~19.5M options/sec                     |
| BlackScholes_Greeks_Batch_100k                  | ~10.34–10.52 ms    | batch greeks                           |
| MonteCarlo_Euro_GBM_Antithetic_10kPaths         | ~0.264 ms          | deterministic antithetic               |
| Historical_VaR_99_ArrayPool_100k                | ~0.436–0.442 ms    | non-mutating overload                  |
| Historical_ES_99_ArrayPool_100k                 | ~0.486–0.487 ms    | non-mutating overload                  |
| FactorModelPnL_SIMD_100k (32 factors)          | ~2.77 ms           | SIMD dot (float)                       |
| FactorModelPnL_SIMD_100k (64 factors)          | ~5.04 ms           | SIMD dot (float)                       |


Reproduce:

bash

dotnet run -c Release --project .\\benchmarks\\QuantCore.Net.Benchmarks.Final



Reports are generated under:

benchmarks/QuantCore.Net.Benchmarks.Final/BenchmarkDotNet.Artifacts/results/



Note:Single-call microbenchmarks can be sensitive to JIT/inlining. Batch benchmarks are the recommended indicator for production throughput.



---



Commercial licensing \& pricing



QuantCore.Net is a commercial library.



Evaluation and non-commercial use are allowed free of charge.

Commercial / production use requires a paid license.

We operate on a “trust-based” model for professional users. If you use QuantCore.Net commercially,please purchase a license.



Pricing (realistic for fintech/quant middleware)



Starter — $299 / month

Individual / small teams

1 organization

Up to 2 production services





Professional — $1,499 / month

Up to 10 developers

Up to 10 production services





Enterprise — $4,999 / month

Unlimited developers/services within one organization





Purchase / contact

Email:vipvodu@yandex.ru

Telegram:@vipvodu



License text:LICENSE.txt.



---



Disclaimer

QuantCore.Net provides computation primitives. Market data ingestion,streaming adapters,databases,execution,or regulatory workflows are intentionally out of scope.

