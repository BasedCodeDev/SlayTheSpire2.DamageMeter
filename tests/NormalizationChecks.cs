using Based.Spires2DamageMeter;
using System.Text.Json;

public static class NormalizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","native-lethal-hits.json")));
        foreach(var e in doc.RootElement.EnumerateArray())
        {
            decimal N(string key)=>e.GetProperty(key).GetDecimal();
            var result=EngineDamage.Normalize(N("unblocked"),N("block"),N("overkill"));
            var accounting=new Accounting(); accounting.Add(new(),"A",N("unblocked"),N("block"),N("overkill"),"Direct");
            var trace=Amplification.Calculate(N("modified"),N("preHp"),N("block"),Array.Empty<DamageModifier>());
            check(result.Hp==N("unblocked") && accounting.Rows["A"].Hp==result.Hp && trace.Actual.Hp==result.Hp && trace.Actual.Block==result.Block,e.GetProperty("label").GetString()!);
            check(accounting.StructuredSources.Sum(s=>s.Hp+s.Block)==result.Hp+result.Block,"native fields reconcile to sources");
        }
        var poison=new Accounting(); poison.Add(new(),"A",1,0,6,"Poison"); check(poison.Rows["A"].Poison==1,"poison HP already capped by engine");
        var coverage=new TraceCoverage(); coverage.Record(false,TraceCoverage.IncomingOutcome(new(0,0,0),false)); coverage.Record(false,TraceCoverage.IncomingOutcome(new(0,0,0),true));
        check(coverage.Unmeasured==0 && coverage.Incoming.Count==2,"incoming prevention/redirection does not inflate offensive coverage");
        coverage.Record(true,"missing_trace");coverage.Record(true,"matched");check(coverage.Matched==1 && coverage.Unmeasured==1,"offensive coverage tracks reasons independently");
        var run=new RunIdentity("profile","mp","start","seed","A");
        var dir=Path.Combine(AppContext.BaseDirectory,"normalization-store-"+Guid.NewGuid().ToString("N"));
        try
        {
            var legacy=Path.Combine(dir,RunStatisticsStore.Hash(JsonSerializer.Serialize(run)));Directory.CreateDirectory(legacy);
            var file=Path.Combine(legacy,"checkpoint.json");var old=JsonSerializer.Serialize(new RunCheckpoint(2,run,"checkpoint",new(){new("fight",new(){["A"]=new(){Hp=3}},new())}));File.WriteAllText(file,old);
            var store=new RunStatisticsStore(dir);store.Restore(run,"checkpoint");check(store.Partial && store.Completed.Count==0 && File.ReadAllText(file)==old,"old aggregates excluded and preserved");
            store.Completed["fight"]=new("fight",new(){["A"]=new(){Hp=6}},new());store.Save(run,"checkpoint");store.Restore(run,"checkpoint");
            check(store.Partial && store.Completed["fight"].Players["A"].Hp==6,"partial history flag survives corrected save/restore");
            store.Completed["fight"]=new("fight",new(){["A"]=new(){Hp=8}},new());store.Save(run,"checkpoint");store.Restore(run,"checkpoint");check(store.Completed.Count==1 && store.Completed.Values.Sum(e=>e.Players["A"].Hp)==8,"replayed encounter replaces earlier attempt");
        }
        finally{if(Path.GetFullPath(dir).StartsWith(Path.GetFullPath(AppContext.BaseDirectory),StringComparison.OrdinalIgnoreCase)&&Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
