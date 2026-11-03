using System.Security.Cryptography;
using System.Text.Json;
namespace Based.Spires2DamageMeter;

public static class ReleaseEvidence
{
    public static readonly string[] Gates={"modifierInteractionAudit","hostAttribution","guestAttribution","checkpointReloads","ui1080p","ui1440p"};
    public static readonly IReadOnlyDictionary<string,string[]> RequiredScenarios=new Dictionary<string,string[]>
    {
        ["modifierInteractionAudit"]=new[]{"catalogue-active-inactive","additive-multiplicative","reduction-cap","lethal-block","hp-loss","enchantment","redirection","shared-provider","poison-doom"},
        ["hostAttribution"]=new[]{"direct","shared-poison","doom-execution","shared-amplification"},
        ["guestAttribution"]=new[]{"direct","shared-poison","doom-execution","shared-amplification"},
        ["checkpointReloads"]=new[]{"save-resume","replayed-encounter","consecutive-runs","delayed-save","legacy-exclusion"},
        ["ui1080p"]=new[]{"mixed-segments","tiny-segments","long-names","collapse-expand","resize","partial-data"},
        ["ui1440p"]=new[]{"mixed-segments","tiny-segments","long-names","collapse-expand","resize","partial-data"}
    };
    public static IReadOnlyList<string> Validate(string json,string root,string version)
    {
        var failures=new List<string>();
        try
        {
            using var doc=JsonDocument.Parse(json);var data=doc.RootElement;
            if(data.GetProperty("schemaVersion").GetInt32()!=2)failures.Add("Evidence schema must be 2");
            if(data.GetProperty("modVersion").GetString()!=version)failures.Add("Evidence mod version does not match package");
            if(data.GetProperty("gameVersion").GetString()!=ModifierCatalogue.GameVersion)failures.Add("Evidence game version is unsupported");
            if(!data.TryGetProperty("amplificationVersion",out var av)||av.GetInt32()!=Amplification.Version)failures.Add("Evidence amplification version does not match");
            if(data.GetProperty("accountingVersion").GetInt32()!=EngineDamage.AccountingVersion)failures.Add("Evidence accounting version does not match");
            var basePath=Path.GetFullPath(root)+Path.DirectorySeparatorChar;
            foreach(var name in Gates)
            {
                if(!data.TryGetProperty(name,out var gate)||gate.ValueKind!=JsonValueKind.Object||!gate.TryGetProperty("status",out var status)||status.GetString()!="passed") {failures.Add(name+": pending or missing structured result");continue;}
                if(string.IsNullOrWhiteSpace(gate.GetProperty("reviewedBy").GetString())||!DateTimeOffset.TryParse(gate.GetProperty("reviewedAt").GetString(),out _))failures.Add(name+": review identity/date required");
                var cases=gate.GetProperty("cases").EnumerateArray().ToArray();
                if(cases.Length==0)failures.Add(name+": no verified scenarios");
                foreach(var required in RequiredScenarios[name])
                    if(!cases.Any(c=>c.GetProperty("scenario").GetString()==required&&c.GetProperty("status").GetString()=="passed"))failures.Add(name+": missing scenario "+required);
                foreach(var test in cases)
                {
                    var id=test.GetProperty("scenario").GetString();
                    if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(test.GetProperty("expected").GetString())||string.IsNullOrWhiteSpace(test.GetProperty("observed").GetString())||test.GetProperty("status").GetString()!="passed")failures.Add(name+": incomplete scenario result");
                    var refs=test.GetProperty("references").EnumerateArray().ToArray();if(refs.Length==0)failures.Add(name+"/"+id+": no evidence references");
                    foreach(var reference in refs)
                    {
                        var path=Path.GetFullPath(Path.Combine(root,reference.GetProperty("path").GetString()!));
                        if(!path.StartsWith(basePath,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)){failures.Add(name+"/"+id+": missing or external artifact");continue;}
                        var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                        if(!sha.Equals(reference.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))failures.Add(name+"/"+id+": evidence hash mismatch");
                        if(string.IsNullOrWhiteSpace(reference.GetProperty("locator").GetString()))failures.Add(name+"/"+id+": line/event or screenshot locator required");
                    }
                }
            }
        }
        catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or IOException or ArgumentException){failures.Add("Invalid release evidence: "+e.Message);}
        return failures;
    }
}
