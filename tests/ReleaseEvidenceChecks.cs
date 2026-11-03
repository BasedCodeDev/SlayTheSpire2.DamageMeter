using Based.Spires2DamageMeter;
using System.Text.Json;
using System.Security.Cryptography;
static class ReleaseEvidenceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(AppContext.BaseDirectory,"evidence-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"evidence.txt"),"synthetic test evidence, not a release result");
            var sha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"evidence.txt"))));
            var data=new Dictionary<string,object>{{"amplificationVersion",Amplification.Version},{"schemaVersion",2},{"accountingVersion",2},{"modVersion","0.3.0"},{"gameVersion",ModifierCatalogue.GameVersion}};
            foreach(var gate in ReleaseEvidence.Gates)data[gate]=new{status="passed",reviewedBy="synthetic reviewer",reviewedAt="2026-11-01T12:00:00Z",cases=ReleaseEvidence.RequiredScenarios[gate].Select(s=>new{scenario=s,status="passed",expected="fixture",observed="fixture",references=new[]{new{path="evidence.txt",sha256=sha,locator="line 1"}}}).ToArray()};
            string Json()=>JsonSerializer.Serialize(data);
            check(ReleaseEvidence.Validate(Json(),root,"0.3.0").Count==0,"release gate accepts structured versioned results with existing hashed evidence");
            check(ReleaseEvidence.Validate(Json(),root,"0.3.1").Count>0,"release evidence cannot certify another mod version");
            File.AppendAllText(Path.Combine(root,"evidence.txt"),"changed");
            check(ReleaseEvidence.Validate(Json(),root,"0.3.0").Any(f=>f.Contains("hash mismatch")),"modified evidence invalidates release gate");
            data["hostAttribution"]="passed";
            check(ReleaseEvidence.Validate(Json(),root,"0.3.0").Any(f=>f.Contains("hostAttribution: pending")),"bare passed string cannot open stable release gate");
        }
        finally {Directory.Delete(root,true);}
    }
}
