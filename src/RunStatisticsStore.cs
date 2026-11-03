using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Based.Spires2DamageMeter;

public sealed record RunIdentity(string Profile, string Mode, string Started, string Seed, string Party);
public sealed record EncounterSnapshot(string Encounter, Dictionary<string, Totals> Players, Dictionary<string, Dictionary<string, decimal>> Sources)
{ public List<SupportTransfer> Transfers { get; init; } = new(); public List<SourceTotals> StructuredSources { get; init; } = new(); }
public sealed record RunCheckpoint(int Schema, RunIdentity Run, string Fingerprint, List<EncounterSnapshot> Completed)
{ public int AmplificationVersion {get;init;} public int AccountingVersion { get; init; } public bool Partial { get; init; } public string? Warning { get; init; } }

/// <summary>Exact-checkpoint storage; active attempts are intentionally never restored as completed encounters.</summary>
public sealed class RunStatisticsStore
{
    private readonly string directory;
    public bool Partial { get; private set; }
    public string? Warning { get; private set; }
    public Dictionary<string, EncounterSnapshot> Completed { get; } = new();
    public RunStatisticsStore(string directory) => this.directory = directory;
    public void MarkPartial(string reason){Partial=true;Warning=reason;}
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Fingerprint(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var ignored = new HashSet<string> { "save_time", "run_time", "win_time", "num_reloads", "map_drawings" };
        void Write(Utf8JsonWriter writer, JsonElement element, bool root)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { if (root && ignored.Contains(p.Name)) continue; writer.WritePropertyName(p.Name); Write(writer, p.Value, false); }
                writer.WriteEndObject();
            }
            else if (element.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Write(writer, item, false); writer.WriteEndArray(); }
            else element.WriteTo(writer);
        }
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Write(writer, doc.RootElement, true);
        return Hash(Encoding.UTF8.GetString(stream.ToArray()));
    }
    private string PathFor(RunIdentity run, string fingerprint) => Path.Combine(directory,"accounting-v"+EngineDamage.AccountingVersion, Hash(JsonSerializer.Serialize(run)), fingerprint + ".json");
    public void Save(RunIdentity run, string fingerprint)
    {
        var path = PathFor(run, fingerprint); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, new RunCheckpoint(4, run, fingerprint, Completed.Values.ToList()){AccountingVersion=EngineDamage.AccountingVersion,AmplificationVersion=Amplification.Version,Partial=Partial,Warning=Warning}); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
    }
    public void Restore(RunIdentity run, string fingerprint)
    {
        Completed.Clear(); Partial = false; Warning = null;
        var path = PathFor(run, fingerprint);
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var data = JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllText(candidate));
                if (data == null || data.Run != run || data.Fingerprint != fingerprint) continue;
                if(data.Schema is not (3 or 4) || data.AccountingVersion!=EngineDamage.AccountingVersion){Warning="Earlier totals excluded: incompatible damage accounting version";continue;}
                bool legacyAmplification=data.Schema==3||data.AmplificationVersion!=Amplification.Version;
                if(legacyAmplification)
                {
                    foreach(var e in data.Completed)
                    {
                        foreach(var t in e.Players.Values){t.AmplifiedHp=t.AmplifiedBlock=t.SupportGiven=t.SupportReceived=t.SupportGivenHp=t.SupportGivenBlock=t.SupportReceivedHp=t.SupportReceivedBlock=0;}
                        foreach(var t in e.StructuredSources){t.AmplifiedHp=t.AmplifiedBlock=t.SupportGivenHp=t.SupportGivenBlock=t.SupportReceivedHp=t.SupportReceivedBlock=0;}
                        e.Transfers.Clear();
                    }
                }
                foreach (var encounter in data.Completed) Completed[encounter.Encounter] = encounter;
                Partial=data.Partial||legacyAmplification;Warning=legacyAmplification?"Earlier amplification/support history excluded; actual damage retained":data.Warning;
                if (candidate.EndsWith(".bak", StringComparison.Ordinal)) { Partial = true; Warning = (Warning==null?"":Warning+"; ")+"Restored backup; most recent statistics may be missing"; }
                return;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { Warning = e.Message; }
        }
        Partial = true; Warning ??= "No matching statistics checkpoint";
        var legacy=Path.Combine(directory,Hash(JsonSerializer.Serialize(run)),fingerprint+".json");
        if(File.Exists(legacy))Warning="Earlier totals excluded: pre-fix damage accounting is incompatible; original files preserved";
    }
}
