using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TextSpace.Core;

namespace TextSpace.Documents;

public sealed record RecoveryRepairIssue(string ParagraphId, int OriginalCount,
    int RetainedCount, int InvalidCount, int DuplicateCount, int ExcessCount);

/// <summary>An immutable repair proposal. The source is never mutated or persisted by this API.</summary>
public sealed record RecoveryRepairPlan(string RepairedJson, ImmutableArray<RecoveryRepairIssue> Issues)
{
    public int RemovedStops => Issues.Sum(i => i.InvalidCount + i.DuplicateCount + i.ExcessCount);
    public DocumentModel CreateDocument() => DocumentJson.Load(RepairedJson);
}

/// <summary>
/// Explicit, bounded recovery for tab metadata only. This is not a general-purpose
/// corrupt-document converter. Review the plan and archive the source before adoption.
/// </summary>
public static class DocumentRecovery
{
    public const int MaximumExaminedStops = 100_000;

    public static RecoveryRepairPlan PrepareTabRepair(string original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Length > DocumentJson.MaxFileBytes || Encoding.UTF8.GetByteCount(original) > DocumentJson.MaxFileBytes)
            throw new InvalidDataException("The recovery document exceeds 32 MB.");
        // A UTF-8 file may carry a BOM. Strip it only from the parseable copy;
        // callers must archive the untouched original, including that marker.
        var parseable = original.StartsWith('\uFEFF') ? original[1..] : original;
        var root = JsonNode.Parse(parseable, documentOptions: new JsonDocumentOptions { MaxDepth = 64 }) as JsonObject
            ?? throw new InvalidDataException("Recovery requires a native JSON document object.");
        if (root["formatVersion"]?.GetValue<int>() != 1)
            throw new InvalidDataException("Unsupported native recovery format version.");
        var issues = ImmutableArray.CreateBuilder<RecoveryRepairIssue>();
        var blocksVisited = 0;
        var stopsExamined = 0;
        void Walk(JsonArray blocks, int depth)
        {
            if (depth > 8) throw new InvalidDataException("Tables are nested too deeply for recovery.");
            foreach (var item in blocks)
            {
                if (++blocksVisited > 50_000) throw new InvalidDataException("Too many blocks for recovery.");
                if (item is not JsonObject block) throw new InvalidDataException("Invalid document block.");
                var kind = block["$type"]?.GetValue<string>();
                if (kind == "paragraph" && block["format"] is JsonObject format)
                {
                    if (!format.TryGetPropertyValue("tabStops", out var tabNode) || tabNode is null)
                    {
                        // Missing and null collections mean no custom stops. Report
                        // explicit null normalization; absent legacy fields need no edit.
                        if (format.ContainsKey("tabStops"))
                        {
                            format["tabStops"] = new JsonArray();
                            issues.Add(new(block["id"]?.GetValue<string>() ?? "", 0, 0, 0, 0, 0));
                        }
                        continue;
                    }
                    if (tabNode is not JsonArray tabs)
                        throw new InvalidDataException("Tab-stop metadata is not an array; this repair cannot infer its meaning.");
                    if (tabs.Count > MaximumExaminedStops - stopsExamined)
                        throw new InvalidDataException("Recovery tab examination budget exceeded.");
                    stopsExamined += tabs.Count;
                    var kept = new JsonArray();
                    var positions = new HashSet<TabStopRules.Key>();
                    var invalid = 0; var duplicate = 0; var excess = 0;
                    foreach (var node in tabs)
                    {
                        TabStop? stop;
                        try { stop = node?.Deserialize(DocumentJsonContext.Default.TabStop); }
                        catch (JsonException) { invalid++; continue; }
                        if (!TabStopRules.IsValid(stop)) { invalid++; continue; }
                        if (!positions.Add(TabStopRules.GetKey(stop!))) { duplicate++; continue; }
                        if (kept.Count == TabStopRules.MaximumCount) { excess++; continue; }
                        kept.Add(node!.DeepClone());
                    }
                    if (invalid + duplicate + excess == 0) continue;
                    issues.Add(new(block["id"]?.GetValue<string>() ?? "", tabs.Count, kept.Count, invalid, duplicate, excess));
                    format["tabStops"] = kept;
                }
                else if (kind == "table" && block["rows"] is JsonArray rows)
                {
                    foreach (var row in rows.OfType<JsonObject>())
                        if (row["cells"] is JsonArray cells)
                            foreach (var cell in cells.OfType<JsonObject>())
                                if (cell["blocks"] is JsonArray children) Walk(children, depth + 1);
                }
            }
        }
        if (root["blocks"] is not JsonArray body) throw new InvalidDataException("Missing document blocks.");
        Walk(body, 0);
        var repaired = issues.Count == 0 ? parseable : root.ToJsonString();
        // The full existing validator remains authoritative for every other
        // structure, review anchor, field, bookmark, geometry and size limit.
        _ = DocumentJson.Load(repaired);
        return new(repaired, issues.ToImmutable());
    }
}
