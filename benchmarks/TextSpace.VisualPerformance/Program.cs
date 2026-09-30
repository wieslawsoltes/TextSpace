using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TextSpace.Core;
using TextSpace.Layout;

// A deterministic typography/query microbenchmark, not a renderer, GPU or input
// latency benchmark. Result equivalence is checked outside the timed regions.
const int warmups = 3, measurements = 7, iterations = 1000;
var metrics = new MonospaceTextMetrics();
var equation = new EquationBlock { Root = EquationTemplates.Create("quadratic") };
var shape = new ShapeBlock { Text = string.Join(" ", Enumerable.Repeat("Editable text", 12)), Width = 240, Height = 120 };
var cache = new VisualLayoutCache(metrics);
var beforeEquation = new EquationLayouter(metrics).Layout(equation.Root, equation.FontSize);
var afterEquation = cache.GetEquation(equation);
if (beforeEquation.Width != afterEquation.Width || beforeEquation.Height != afterEquation.Height || beforeEquation.Glyphs.Count != afterEquation.Glyphs.Count || beforeEquation.Slots.Count != afterEquation.Slots.Count)
    throw new InvalidOperationException("Equation geometry differs.");
for (var i = 0; i < beforeEquation.Glyphs.Count; i++) if (beforeEquation.Glyphs[i] != afterEquation.Glyphs[i]) throw new InvalidOperationException("Equation glyph differs.");
for (var i = 0; i < beforeEquation.Slots.Count; i++) if (beforeEquation.Slots[i] != afterEquation.Slots[i]) throw new InvalidOperationException("Equation slot differs.");
var beforeShape = VisualTextLayout.Layout(shape, metrics);
var afterShape = cache.GetShape(shape);
if (!beforeShape.SequenceEqual(afterShape)) throw new InvalidOperationException("Shape geometry differs.");

static double Score(EquationSlot slot, double x, double y)
{
    var r = slot.Bounds; var dx = Math.Max(r.X - x, Math.Max(0, x - r.Right)); var dy = Math.Max(r.Y - y, Math.Max(0, y - r.Bottom));
    return dx * dx + dy * dy;
}
var random = new Random(39);
var points = Enumerable.Range(0, iterations).Select(_ => (X: random.NextDouble() * afterEquation.Width, Y: random.NextDouble() * afterEquation.Height)).ToArray();
foreach (var p in points)
    if (afterEquation.Slots.OrderBy(s => Score(s, p.X, p.Y)).First() != afterEquation.HitTest(p.X, p.Y)) throw new InvalidOperationException("Hit-test selection differs.");

object Measure(Func<double> action)
{
    for (var i = 0; i < warmups; i++) _ = action();
    var samples = new List<(double Time, long Bytes, double Result)>();
    for (var i = 0; i < measurements; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        var checksum = action(); var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        samples.Add((elapsed, allocated, checksum)); GC.KeepAlive(checksum);
    }
    if (samples.Any(s => s.Result != samples[0].Result)) throw new InvalidOperationException("Benchmark result changed between samples.");
    return new { MedianMs = samples.Select(s => s.Time).Order().ElementAt(measurements / 2), MedianAllocatedBytes = samples.Select(s => s.Bytes).Order().ElementAt(measurements / 2),
        Samples = samples.Select(s => new { Milliseconds = s.Time, AllocatedBytes = s.Bytes, Checksum = s.Result }).ToArray() };
}
double FreshEquation() { double result = 0; for (var i = 0; i < iterations; i++) result += new EquationLayouter(metrics).Layout(equation.Root, equation.FontSize).Width; return result; }
double CachedEquation() { double result = 0; for (var i = 0; i < iterations; i++) result += cache.GetEquation(equation).Width; return result; }
double FreshShape() { double result = 0; for (var i = 0; i < iterations; i++) result += VisualTextLayout.Layout(shape, metrics).Count; return result; }
double CachedShape() { double result = 0; for (var i = 0; i < iterations; i++) result += cache.GetShape(shape).Count; return result; }
double OrderedHitTest() { double result = 0; foreach (var p in points) result += afterEquation.Slots.OrderBy(s => Score(s, p.X, p.Y)).First().Bounds.X; return result; }
double DirectHitTest() { double result = 0; foreach (var p in points) result += afterEquation.HitTest(p.X, p.Y)!.Bounds.X; return result; }
if (FreshEquation() != CachedEquation() || FreshShape() != CachedShape() || OrderedHitTest() != DirectHitTest()) throw new InvalidOperationException("Measured paths differ.");
var results = new[]
{
    new { Name = "Unchanged equation measurement requests", Before = Measure(FreshEquation), After = Measure(CachedEquation) },
    new { Name = "Unchanged shape text measurement requests", Before = Measure(FreshShape), After = Measure(CachedShape) },
    new { Name = "Nearest equation slot queries", Before = Measure(OrderedHitTest), After = Measure(DirectHitTest) }
};
Console.WriteLine(JsonSerializer.Serialize(new
{
    commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local",
    scope = "Deterministic MonospaceTextMetrics, warm cache, unchanged content. No Skia rasterization, native glyph shaping, storage, browser scheduling or end-to-end typing latency is timed. Before paths repeat measurement or use the previous stable ordered lookup; equivalence is asserted.",
    framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"), readyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun"),
    warmups, measurements, operationsPerSample = iterations, results
}, new JsonSerializerOptions { WriteIndented = true }));
