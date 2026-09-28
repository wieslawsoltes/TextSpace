using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TextSpace.Core;

internal static class TabValidationBenchmark
{
    private const int Iterations = 20_000;
    private sealed record Sample(double Milliseconds, long AllocatedBytes);
    private sealed record Result(double MedianMs, long MedianAllocatedBytes, Sample[] Samples);

    public static string Run()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") != "0"
            || Environment.GetEnvironmentVariable("DOTNET_ReadyToRun") != "0")
            throw new InvalidOperationException("Run this benchmark in a new process with DOTNET_TieredCompilation=0 and DOTNET_ReadyToRun=0. Application runtime settings are not changed.");
        VerifyEquivalentAcceptance();
        var measurements = new List<object>();
        var inputIndex = 0;
        foreach (var count in new[] { 0, 1, 4, 8, 32, 128 })
        {
            var stops = Enumerable.Range(0, count)
                .Select(i => new TabStop(i * 12, TabAlignment.Right, TabLeader.Dot)).ToImmutableArray();
            void Legacy() { for (var i = 0; i < Iterations; i++) PreviousValidate(stops); }
            void Current() { for (var i = 0; i < Iterations; i++) TabStopRules.Validate(stops); }
            Result before, after;
            if ((inputIndex++ & 1) == 0) { before = Measure(Legacy); after = Measure(Current); }
            else { after = Measure(Current); before = Measure(Legacy); }
            measurements.Add(new { stops = count, before, after });
        }
        return JsonSerializer.Serialize(new
        {
            baselineCommit = "d6059e2112697baa9e69123de07c2959682710c8",
            scope = "Tab validation only; prior/current acceptance verified. No document load, JSON parsing, rendering or storage I/O is timed. Not an application-default runtime or browser benchmark.",
            compilation = "Single-tier optimized JIT; ReadyToRun disabled for this measurement process only",
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmups = 3, measurements = 7, validationsPerSample = Iterations,
            results = measurements
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Result Measure(Action action)
    {
        for (var i = 0; i < 3; i++) action();
        var samples = new Sample[7];
        for (var i = 0; i < samples.Length; i++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            action();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            samples[i] = new(elapsed, bytes);
        }
        return new(samples.Select(s => s.Milliseconds).Order().ElementAt(3),
            samples.Select(s => s.AllocatedBytes).Order().ElementAt(3), samples);
    }

    private static void VerifyEquivalentAcceptance()
    {
        static bool Accepts(Action action)
        {
            try { action(); return true; }
            catch (InvalidDataException) { return false; }
        }
        var random = new Random(617);
        List<ImmutableArray<TabStop>> inputs =
        [
            default, [], [new(double.NaN)], [new(4001)], [new(1), new(1.001)],
            [new(12), new(12, relativeToRightEdge: true)], [null!]
        ];
        for (var n = 0; n < 300; n++)
        {
            var count = random.Next(0, 132);
            inputs.Add(Enumerable.Range(0, count).Select(_ => new TabStop(
                random.Next(-10, 300), (TabAlignment)random.Next(0, 6),
                (TabLeader)random.Next(0, 7), random.Next(0, 2) != 0,
                random.Next(0, 5) == 0 ? 'x' : '.')).ToImmutableArray());
        }
        foreach (var input in inputs)
            if (Accepts(() => PreviousValidate(input)) != Accepts(() => TabStopRules.Validate(input)))
                throw new InvalidOperationException("Prior and current validation acceptance differ.");
    }

    // Tab-collection portion of DocumentJson.ValidateTypography at the recorded
    // baseline. Diagnostic wording differs intentionally; acceptance must not.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PreviousValidate(ImmutableArray<TabStop> stops)
    {
        if (stops.IsDefault || stops.Length > 128) throw new InvalidDataException();
        var positions = new HashSet<(long, bool)>();
        foreach (var stop in stops)
            if (stop is null || !double.IsFinite(stop.Position) || stop.Position is < -4000 or > 4000
                || !Enum.IsDefined(stop.Alignment) || !Enum.IsDefined(stop.Leader)
                || stop.DecimalCharacter is not ('.' or ',')
                || !positions.Add(((long)Math.Round(stop.Position * 20), stop.RelativeToRightEdge)))
                throw new InvalidDataException();
    }
}
