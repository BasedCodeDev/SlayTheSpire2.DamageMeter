using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Based.Spires2DamageMeter;
public sealed record RunIdentity(string Profile, string Mode, string Started, string Seed, string Party);
public sealed record EncounterSnapshot(string Encounter, Dictionary<string, Totals> Players, Dictionary<string, Dictionary<string, decimal>> Sources)
{
    public EffectiveSnapshot? Effective { get; init; }
    public AttributionSnapshot? Attribution { get; init; }
    public List<SupportTransfer> Transfers { get; init; } = new();
    public List<SourceTotals> StructuredSources { get; init; } = new();
}

public sealed record RunCheckpoint(int Schema, RunIdentity Run, string Fingerprint, List<EncounterSnapshot> Completed)
{
    public int FingerprintVersion { get; init; }
    public int EffectiveVersion { get; init; }
    public int AttributionVersion { get; init; }
    public int AmplificationVersion { get; init; }
    public int AccountingVersion { get; init; }
    public bool Partial { get; init; }
    public string? Warning { get; init; }
}

/// <summary>Exact-checkpoint storage; active attempts are intentionally never restored as completed encounters.</summary>
public sealed class RunStatisticsStore
{
    internal readonly string directory;
    public bool Partial { get; internal set; }
    public string? Warning { get; internal set; }
    public Dictionary<string, EncounterSnapshot> Completed { get; } = new();

    public RunStatisticsStore(string directory) => this.directory = directory;
    public void MarkPartial(string reason) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunStatisticsStore.MarkPartial", this, reason);
    public static string Hash(string value) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunStatisticsStore.Hash", value);
    public static string PayloadFingerprint(string json) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunStatisticsStore.PayloadFingerprint", json);
    public static string Fingerprint(string json) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunStatisticsStore.Fingerprint", json);
    internal static string Fingerprint(string json, bool legacy) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunStatisticsStore.Fingerprint", json, legacy);
    internal string PathFor(RunIdentity run, string fingerprint) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunStatisticsStore.PathFor", this, run, fingerprint);
    public void Save(RunIdentity run, string fingerprint) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunStatisticsStore.Save", this, run, fingerprint);
    public void Restore(RunIdentity run, string fingerprint) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunStatisticsStore.Restore", this, run, fingerprint);
}
