using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TextSpace.Core;
using TextSpace.Layout;
using TextSpace.Skia;

if (args.Contains("--glyphs")) { Console.WriteLine(GlyphBenchmark.Run()); return; }

var document = new DocumentModel { Blocks = Enumerable.Range(0, 1000).Select(i => (Block)new Paragraph($"Paragraph {i}: The editor lays out a document with reusable paragraph geometry, rich formatting, and reliable caret navigation.")).ToList() };
using var renderer = new DocumentRenderer();
var measurement = new Dictionary<string, object>();
object Measure(string label, Action operation, int iterations)
{
    for (var i=0;i<3;i++) operation();
    var times = new List<double>(); var allocations = new List<long>();
    for (var i=0;i<iterations;i++) {
        var before = GC.GetAllocatedBytesForCurrentThread(); var watch=Stopwatch.StartNew();
        operation(); watch.Stop(); times.Add(watch.Elapsed.TotalMilliseconds);
        allocations.Add(GC.GetAllocatedBytesForCurrentThread()-before);
    }
    times.Sort(); allocations.Sort();
    return new { medianMs=times[times.Count/2], medianAllocatedBytes=allocations[allocations.Count/2], iterations };
}
var layout=renderer.Layout(document);
measurement["warmLayout1000Paragraphs"]=Measure("layout",()=>layout=renderer.Layout(document),10);
var random=new Random(41); var textLength=document.PlainText.Length; var offsets=Enumerable.Range(0,20000).Select(_=>random.Next(textLength)).ToArray();
double checksum=0;
measurement["caret20000Queries"]=Measure("caret",()=> { foreach(var p in offsets) checksum+=layout.Caret(p).X; },7);
measurement["hitTest2000Queries"]=Measure("hit",()=> { for(var i=0;i<2000;i++) checksum+=layout.HitTest(200, (i*13)%Math.Max(1,layout.Height)); },7);
Console.WriteLine(JsonSerializer.Serialize(new { label=args.FirstOrDefault()??"unknown", framework=RuntimeInformation.FrameworkDescription, os=RuntimeInformation.OSDescription, architecture=RuntimeInformation.ProcessArchitecture.ToString(), paragraphs=1000, pages=layout.Pages.Count, characters=document.PlainText.Length, measurement, checksum },new JsonSerializerOptions{WriteIndented=true}));
