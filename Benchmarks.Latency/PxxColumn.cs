using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using System;
using System.Linq;

namespace QuantCore.Net.Benchmarks.Latency
{
    internal sealed class PxxColumn : IColumn
    {
        private readonly int _p; // percentile,e.g. 95 or 99
        private readonly string _id;

        public PxxColumn(int p)
        {
            if (p <= 0 || p >= 100) throw new ArgumentOutOfRangeException(nameof(p));
            _p = p;
            _id = $"P{p}";
        }

        public string Id => _id;
        public string ColumnName => _id;

        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Statistics;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Time;
        public string Legend => $"{_p}th percentile of iteration times";

        public bool IsAvailable(Summary summary) => true;
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var report = summary.Reports.FirstOrDefault(r => r.BenchmarkCase == benchmarkCase);
            var stats = report?.ResultStatistics;

            var values = stats?.OriginalValues;
            if (values == null || values.Count == 0)
                return "NA";

            double p = Percentile(values, _p);
            return FormatNs(p);
        }

        private static double Percentile(System.Collections.Generic.IReadOnlyList<double> values, int p)
        {
            // p in (0,100). Use nearest-rank on sorted copy.
            int n = values.Count;
            if (n == 0) return double.NaN;

            var tmp = new double[n];
            for (int i = 0; i < n; i++) tmp[i] = values[i];

            Array.Sort(tmp);

            int rank = (int)System.Math.Ceiling(p / 100.0 * n);
            int idx = System.Math.Clamp(rank - 1, 0, n - 1);
            return tmp[idx];
        }


        private static string FormatNs(double ns)
        {
            // input is in nanoseconds for BenchmarkDotNet statistics values (it is TimeInterval in ns)
            // stats.OriginalValues are in nanoseconds in BDN.
            if (ns < 1_000) return $"{ns:0.00} ns";
            if (ns < 1_000_000) return $"{ns / 1_000.0:0.00} μs";
            if (ns < 1_000_000_000) return $"{ns / 1_000_000.0:0.00} ms";
            return $"{ns / 1_000_000_000.0:0.00} s";
        }
    }
}
