using System.Text.Json;
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.PersistencePayload;

namespace Based.Spires2DamageMeter.Runtime;
internal static class PersistencePayloadLogic
{
    public static void Capture(PersistencePayload self, string path, string json)
    {
        self.Hash = RunStatisticsStore.PayloadFingerprint(json);
        self.Json = json;
        self.Path = path;
    }

    public static RunIdentity Identity(string profile, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new(profile, r.GetProperty("game_mode").ToString(), r.GetProperty("start_time").ToString(), r.GetProperty("rng").GetProperty("seed").ToString(), string.Join(",", r.GetProperty("players").EnumerateArray().Select(p => p.GetProperty("net_id").ToString()).OrderBy(s => s, StringComparer.Ordinal)));
    }
}
