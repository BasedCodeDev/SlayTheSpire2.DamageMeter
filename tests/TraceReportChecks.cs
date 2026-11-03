using Based.Spires2DamageMeter;
using System.Text.Json;

static class TraceReportChecks
{
    public static void Run(Action<bool,string> check)
    {
        string Line(string kind, object data, int accounting=2, int schema=3, string role="Host") => JsonSerializer.Serialize(new{schemaVersion=schema, accountingVersion=accounting, session="fixture",combat=1,sequence=1,role,kind,data});
        var trace=new {eventId="hit", calculated=new{Actual=new{Hp=6,Block=0},Red=4,OrangeHp=2,Blue=0,OrangeBlock=0},normalized=new{Hp=6,Block=0},Modifiers=new[]{new{Id="StrengthPower:0"}}};
        var report=new TraceReport();
        report.Read("good.jsonl",new[]{Line("startup",new{modVersion="preview"}),Line("game_version",new{version="v0.107.1"}),Line("damage_trace",trace),Line("damage",new{eventId="hit",hp=6,rawUnblocked=6,overkill=3}),Line("damage_allocation",new{eventId="hit",hp=6,shares=new[]{new Share("A","strike",4),new Share(StatusOwnershipLedger.Unknown,"unknown",2)}})});
        var group=report.Groups.Single();
        check(report.ParseFailures.Count==0 && group.Failures.Count==0 && group.Conclusion=="observed_consistency_only","analyser accepts consistent capped HP, never claims native verification");
        check(group.KnownShares==1 && group.UnknownShares==1 && group.Modifiers.Contains("StrengthPower:0") && group.Evidence.Any(e=>e.Contains("good.jsonl:3")),"analyser preserves ownership coverage and source event references");
        report.Read("guest.jsonl",new[]{Line("damage_trace",trace,role:"Client")});
        check(report.Groups.Count==2,"analyser separates host and guest evidence");
        var legacy=new TraceReport();legacy.Read("legacy",new[]{Line("damage_trace",trace,1,2)});
        check(legacy.LegacyNumericalEventsExcluded==1 && legacy.Groups.Single().Conclusion=="not_exercised","legacy traces never qualify as current consistency evidence");
        var bad=new TraceReport();bad.Read("bad",new[]{Line("damage",new{eventId="hit",hp=3,rawUnblocked=6}),Line("damage_allocation",new{eventId="hit",hp=6,shares=new[]{new Share("A","strike",3)}}),Line("combat_summary",new{totals=new{A=new{SupportGiven=2,SupportReceived=0}}})});
        check(bad.Groups.Single().Failures.Count==3 && bad.Groups.Single().Conclusion=="observed_failures","analyser detects normalization, allocation and support failures");
        var duplicate=new TraceReport();duplicate.Read("duplicates",new[]{Line("damage_trace",trace),Line("damage_trace",trace)});
        check(duplicate.Groups.Single().Failures.Count==1,"analyser rejects duplicate canonical events");
        var malformed=new TraceReport();malformed.Read("broken",new[]{"{",Line("damage",new{hp=0}),Line("damage_trace",new{eventId="missing"})});
        check(malformed.ParseFailures.Count==3,"malformed and missing numeric fields cannot silently pass as zero");
        var startup=new TraceReport();startup.Read("startup",new[]{Line("startup",new{modVersion="preview"})});
        check(startup.Groups.Count==0,"startup-only log does not supply combat evidence");
        var native=new TraceReport();native.Read("native-traces-v3.jsonl",File.ReadLines(Path.Combine(AppContext.BaseDirectory,"fixtures","native-traces-v3.jsonl")));
        check(native.ParseFailures.Count==0&&native.Groups.Single().Failures.Count==0&&native.Groups.Single().CompatibleTraces==2,"analyser reconciles anonymised native lethal amplification traces");
        check(native.Groups.Single().Modifiers.SetEquals(new[]{"StrengthPower:0","AccuracyPower:1","WeakPower:2"}),"native fixture excludes neutral caps from exercised modifier coverage");
        var linked=new TraceReport();
        var damage=Line("damage",new{eventId="hit",hp=6,rawUnblocked=6,blocked=0,enemy=true},schema:4);
        var allocation=Line("damage_allocation",new{eventId="hit",hp=6,shares=new[]{new Share("A","strike",6)}},schema:4);
        var binding=Line("damage_binding",new{eventId="hit",resolutionId="resolution",status="bound"},schema:4);
        var outcome=Line("damage_trace_missing",new{eventId="hit"},schema:4);
        var summary=Line("combat_summary",new{totals=new{A=new{Hp=6,Block=0,SupportGiven=0,SupportReceived=0}}},schema:4);
        linked.Read("linked",new[]{damage,allocation,binding,outcome,summary});
        check(linked.ParseFailures.Count==0&&linked.Groups.Single().Failures.Count==0,"schema-4 completed fight reconciles canonical records and totals");
        var incomplete=new TraceReport();incomplete.Read("incomplete",new[]{damage});
        check(incomplete.Groups.Single().CorrelationStatus=="incomplete_observation"&&incomplete.Groups.Single().Failures.Count==0,"unfinished log defers missing-link verdict");
        var missing=new TraceReport();missing.Read("missing",new[]{damage,summary});
        check(missing.Groups.Single().Failures.Count==3,"completed fight detects missing allocation binding and outcome");
        var mismatch=new TraceReport();mismatch.Read("mismatch",new[]{damage,allocation,binding,outcome,summary.Replace("\"Hp\":6","\"Hp\":5")});
        check(mismatch.Groups.Single().Failures.Any(f=>f.Contains("fight totals")),"completed fight detects canonical versus summary mismatch");
    }
}
