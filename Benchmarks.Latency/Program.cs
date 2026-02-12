using BenchmarkDotNet.Running;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<QuantCoreE2EAndThroughputBench>();
    }
}
