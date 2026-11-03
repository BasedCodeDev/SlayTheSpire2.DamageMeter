using System.Text.Json;
namespace Based.Spires2DamageMeter;
public sealed class PersistencePayload
{
 public string Profile => Path==null?throw new InvalidOperationException("Missing payload path"):Path.Replace('\\','/') is string normalized&&normalized.Contains('/')?normalized[..normalized.LastIndexOf('/')]:".";
 public string? Hash {get;private set;}
 public string? Json {get;private set;}
 public string? Path {get;private set;}
 public void Capture(string path,string json){Hash=RunStatisticsStore.PayloadFingerprint(json);Json=json;Path=path;}
 public static RunIdentity Identity(string profile,string json){using var doc=JsonDocument.Parse(json);var r=doc.RootElement;return new(profile,r.GetProperty("game_mode").ToString(),r.GetProperty("start_time").ToString(),r.GetProperty("rng").GetProperty("seed").ToString(),string.Join(",",r.GetProperty("players").EnumerateArray().Select(p=>p.GetProperty("net_id").ToString()).OrderBy(s=>s,StringComparer.Ordinal)));}
}
