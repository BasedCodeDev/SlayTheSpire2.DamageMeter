using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.RunStatisticsStore;

namespace Based.Spires2DamageMeter.Runtime;
internal static class RunStatisticsStoreLogic
{
    public static void MarkPartial(RunStatisticsStore self, string reason)
    {
        self.Partial = true;
        self.Warning = reason;
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string PayloadFingerprint(string json) => Fingerprint(json, false);
    public static string Fingerprint(string json) => Fingerprint(json, true);
    public static string Fingerprint(string json, bool legacy)
    {
        using var doc = JsonDocument.Parse(json);
        var ignored = new HashSet<string>
        {
            "save_time",
            "run_time",
            "win_time",
            "num_reloads",
            "map_drawings"
        };
        void Write(Utf8JsonWriter writer, JsonElement element, bool root)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (legacy && root && ignored.Contains(p.Name))
                        continue;
                    writer.WritePropertyName(p.Name);
                    Write(writer, p.Value, false);
                }

                writer.WriteEndObject();
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    Write(writer, item, false);
                writer.WriteEndArray();
            }
            else
                element.WriteTo(writer);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            Write(writer, doc.RootElement, true);
        return Hash(Encoding.UTF8.GetString(stream.ToArray()));
    }

    public static string PathFor(RunStatisticsStore self, RunIdentity run, string fingerprint) => Path.Combine(self.directory, "fingerprint-v2", "accounting-v" + EngineDamage.AccountingVersion, Hash(JsonSerializer.Serialize(run)), fingerprint + ".json");
    public static void Save(RunStatisticsStore self, RunIdentity run, string fingerprint)
    {
        var path = self.PathFor(run, fingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, new RunCheckpoint(7, run, fingerprint, self.Completed.Values.ToList()) { FingerprintVersion = 2, EffectiveVersion = EffectiveDamageLedger.Version, AttributionVersion = CreditedDamage.Version, AccountingVersion = EngineDamage.AccountingVersion, AmplificationVersion = Amplification.Version, Partial = self.Partial, Warning = self.Warning });
            stream.Flush(true);
        }

        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak");
        else
            File.Move(temp, path);
    }

    public static void Restore(RunStatisticsStore self, RunIdentity run, string fingerprint)
    {
        self.Completed.Clear();
        self.Partial = false;
        self.Warning = null;
        var path = self.PathFor(run, fingerprint);
        foreach (var candidate in new[]
        {
            path,
            path + ".bak"
        }

        )
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;
                var data = JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllText(candidate));
                if (data == null || data.Run != run || data.Fingerprint != fingerprint)
                    continue;
                if (data.Schema == 7 && data.FingerprintVersion != 2)
                {
                    self.Warning = "Incompatible checkpoint fingerprint version";
                    continue;
                }

                if (data.Schema is not (3 or 4 or 5 or 6 or 7) || data.AccountingVersion != EngineDamage.AccountingVersion)
                {
                    self.Warning = "Earlier totals excluded: incompatible damage accounting version";
                    continue;
                }

                bool legacyAmplification = data.Schema == 3 || data.AmplificationVersion != Amplification.Version;
                if (legacyAmplification)
                {
                    foreach (var e in data.Completed)
                    {
                        foreach (var t in e.Players.Values)
                        {
                            t.AmplifiedHp = t.AmplifiedBlock = t.SupportGiven = t.SupportReceived = t.SupportGivenHp = t.SupportGivenBlock = t.SupportReceivedHp = t.SupportReceivedBlock = 0;
                        }

                        foreach (var t in e.StructuredSources)
                        {
                            t.AmplifiedHp = t.AmplifiedBlock = t.SupportGivenHp = t.SupportGivenBlock = t.SupportReceivedHp = t.SupportReceivedBlock = 0;
                        }

                        e.Transfers.Clear();
                    }
                }

                foreach (var encounter in data.Completed)
                    self.Completed[encounter.Encounter] = data.Schema >= 5 && data.AttributionVersion == CreditedDamage.Version ? encounter : encounter with
                    {
                        Attribution = null
                    };
                self.Partial = data.Partial || legacyAmplification;
                self.Warning = legacyAmplification ? "Earlier amplification/support history excluded; actual damage retained" : data.Warning;
                if (candidate.EndsWith(".bak", StringComparison.Ordinal))
                {
                    self.Partial = true;
                    self.Warning = (self.Warning == null ? "" : self.Warning + "; ") + "Restored backup; most recent statistics may be missing";
                }

                return;
            }
            catch (Exception e)when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                self.Warning = e.Message;
            }
        }

        self.Partial = true;
        self.Warning ??= "No matching statistics checkpoint";
        var legacy = Path.Combine(self.directory, Hash(JsonSerializer.Serialize(run)), fingerprint + ".json");
        if (File.Exists(legacy))
            self.Warning = "Earlier totals excluded: pre-fix damage accounting is incompatible; original files preserved";
    }
}
