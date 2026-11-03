using System.Text.Json;
namespace Based.Spires2DamageMeter;

public sealed class TraceReportGroup
{
    public required string Key { get; init; }
    public Dictionary<string,int> Counts { get; }=new();
    public List<string> Failures { get; }=new();
    public List<string> Evidence { get; }=new();
    public HashSet<string> Modifiers { get; }=new();
    public int KnownShares { get; set; }
    public int UnknownShares { get; set; }
    public int CompatibleTraces { get; set; }
    public string CorrelationStatus {get;set;}="not_available_legacy";
    public string Conclusion=>Failures.Count>0?"observed_failures":CompatibleTraces>0?"observed_consistency_only":"not_exercised";
}
public sealed class TraceReport
{
    public List<TraceReportGroup> Groups { get; }=new();
    public List<string> ParseFailures { get; }=new();
    public int LegacyNumericalEventsExcluded { get; private set; }
    private readonly Dictionary<string,TraceReportGroup> groups=new();
    private readonly HashSet<string> ids=new();
    private static decimal N(JsonElement e,string property)=>e.GetProperty(property).GetDecimal();
    private static string S(JsonElement e,string property)=>e.TryGetProperty(property,out var v)?v.ToString():"unknown";
    public void Read(string path,IEnumerable<string> lines)
    {
        var linked=new Dictionary<TraceReportGroup,List<(string Kind,JsonElement Data,string Ref)>>();
        var game="unknown";var mod="unknown";var line=0;
        foreach(var json in lines)
        {
            line++;if(string.IsNullOrWhiteSpace(json))continue;
            try
            {
                using var document=JsonDocument.Parse(json);var root=document.RootElement;var kind=S(root,"kind");
                if(!root.TryGetProperty("data",out var data)){ParseFailures.Add($"{path}:{line}: missing data");continue;}
                if(kind=="game_version")game=S(data,"version");
                if(kind=="startup")mod=S(data,"modVersion");
                if(kind is "game_version" or "startup" or "hook_registered" or "ownership_target_validated" or "ownership_target_installed" or "ownership_registration")continue;
                var key=string.Join(" | ",game,mod,S(root,"accountingVersion"),S(root,"session"),S(root,"role"),S(root,"combat"),S(root,"schemaVersion"),S(root,"amplificationVersion"));
                if(!groups.TryGetValue(key,out var group)){groups[key]=group=new(){Key=key};Groups.Add(group);}
                group.Counts[kind]=group.Counts.GetValueOrDefault(kind)+1;
                var eventId=S(data,"eventId");var evidence=$"{path}:{line} event={eventId} sequence={S(root,"sequence")}";
                bool numerical=kind is "damage" or "damage_trace" or "damage_allocation" or "combat_summary";
                if(numerical && (!root.TryGetProperty("accountingVersion",out var accounting)||accounting.ValueKind!=JsonValueKind.Number||accounting.GetInt32()!=EngineDamage.AccountingVersion || !root.TryGetProperty("schemaVersion",out var schema)||schema.ValueKind!=JsonValueKind.Number||schema.GetInt32() is not (3 or 4 or 5 or 6))){LegacyNumericalEventsExcluded++;continue;}
                if(root.TryGetProperty("schemaVersion",out var logSchema)&&logSchema.GetInt32()>=4)
                {if(!linked.TryGetValue(group,out var entries))linked[group]=entries=new();entries.Add((kind,data.Clone(),evidence));}
                if(eventId!="unknown" && !ids.Add(S(root,"session")+":"+kind+":"+eventId))group.Failures.Add("duplicate event identity: "+evidence);
                if(kind is "damage_trace_mismatch" or "damage_trace_error" or "capture_error" or "statistics_storage_error" or "damage_binding_error")group.Failures.Add(kind+": "+evidence);
                if(kind is "damage_trace_missing" or "damage_trace_unsupported" or "statistics_checkpoint_loaded" or "statistics_checkpoint_saved" or "incoming_damage_diagnostic")group.Evidence.Add(kind+": "+evidence);
                if(kind=="multiplicative_snapshot")
                {
                    var snapshot=JsonSerializer.Deserialize<AttackMultiplierSnapshot>(data.GetProperty("snapshot").GetRawText())!;
                    if(snapshot.Expected!=N(data,"observed")||snapshot.Expected!=N(data,"expected"))group.Failures.Add("multiplicative snapshot mismatch: "+evidence);
                }
                if(kind=="additive_snapshot")
                {
                    var snapshot=JsonSerializer.Deserialize<AdditiveSnapshot>(data.GetProperty("snapshot").GetRawText())!;
                    if(snapshot.Expected!=N(data,"observed")||snapshot.Expected!=N(data,"expected"))group.Failures.Add("additive snapshot mismatch: "+evidence);
                }
                if(kind=="damage")
                { if(N(data,"hp")!=Math.Max(0,N(data,"rawUnblocked")))group.Failures.Add("native HP normalization mismatch: "+evidence); }
                if(kind=="damage_allocation" && data.TryGetProperty("shares",out var shares))
                {
                    decimal sum=0;foreach(var share in shares.EnumerateArray()){sum+=N(share,"Amount");if(S(share,"Owner")==StatusOwnershipLedger.Unknown)group.UnknownShares++;else group.KnownShares++;}
                    if(sum!=N(data,"hp"))group.Failures.Add("allocation does not reconcile: "+evidence);
                }
                if(kind=="damage_trace")
                {
                    group.Evidence.Add("matched trace: "+evidence);
                    var calculated=data.GetProperty("calculated");var actual=calculated.GetProperty("Actual");var normalized=data.GetProperty("normalized");
                    if(N(actual,"Hp")!=N(normalized,"Hp")||N(actual,"Block")!=N(normalized,"Block"))group.Failures.Add("trace disagrees with engine: "+evidence);
                    if(Math.Abs(N(calculated,"Red")+N(calculated,"OrangeHp")-N(actual,"Hp"))>0.000000000000000000000001m||Math.Abs(N(calculated,"Blue")+N(calculated,"OrangeBlock")-N(actual,"Block"))>0.000000000000000000000001m)group.Failures.Add("segments do not reconcile: "+evidence);
                    foreach(var m in data.GetProperty("Modifiers").EnumerateArray())
                    {
                        if((!m.TryGetProperty("Chain",out var chain)||chain.ValueKind==JsonValueKind.Null)&&m.TryGetProperty("Kind",out var k)&&m.TryGetProperty("Value",out var v)&&((k.GetInt32()==0&&v.GetDecimal()==0)||(k.GetInt32()==1&&v.GetDecimal()==1)||(k.GetInt32()==2&&v.GetDecimal()==decimal.MaxValue)))continue;
                        group.Modifiers.Add(S(m,"Id"));
                    }
                    if(root.TryGetProperty("amplificationVersion",out var av)&&av.GetInt32() is 2 or 3)
                    {
                        foreach(var destination in new[]{("HpProviders","OrangeHp"),("BlockProviders","OrangeBlock")})
                        {
                            decimal sum=0;foreach(var provider in calculated.GetProperty(destination.Item1).EnumerateArray()){var amount=N(provider,"Amount");if(amount<0)group.Failures.Add("negative provider allocation: "+evidence);sum+=amount;}
                            if(Math.Abs(sum-N(calculated,destination.Item2))>0.000000000000000000000001m)group.Failures.Add("provider allocations do not reconcile: "+evidence);
                        }
                        foreach(var weight in calculated.GetProperty("Weights").EnumerateArray())if(N(weight,"Weight")<0)group.Failures.Add("negative allocation weight: "+evidence);
                    }
                    if(root.TryGetProperty("amplificationVersion",out var chainVersion)&&chainVersion.GetInt32()==3)
                    foreach(var modifier in data.GetProperty("Modifiers").EnumerateArray())
                    {
                        if(!modifier.TryGetProperty("Chain",out var encoded)||encoded.ValueKind==JsonValueKind.Null)continue;
                        var chain=JsonSerializer.Deserialize<MultiplierChain>(encoded.GetRawText())!;
                        if(!chain.Reconciles(N(modifier,"Value")))group.Failures.Add("multiplier chain continuity/final mismatch: "+evidence);
                        foreach(var step in chain.Steps)group.Modifiers.Add(step.Model);
                    }
                    group.CompatibleTraces++;
                }
                if(kind=="combat_summary" && data.TryGetProperty("totals",out var totals))
                {
                    decimal given=0,received=0;foreach(var p in totals.EnumerateObject()){given+=N(p.Value,"SupportGiven");received+=N(p.Value,"SupportReceived");}
                    if(Math.Abs(given-received)>.000000000000000000000001m)group.Failures.Add("support transfers do not conserve totals: "+evidence);
                }
            }
            catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException){ParseFailures.Add($"{path}:{line}: {e.Message}");}
        }
        foreach(var pair in linked)
        {
            var g=pair.Key;var entries=pair.Value;try { bool complete=entries.Any(e=>e.Kind=="combat_summary");
            g.CorrelationStatus=complete?"completed_fight":"incomplete_observation";
            foreach(var hit in entries.Where(e=>e.Kind=="damage"))
            {
                var id=S(hit.Data,"eventId");var records=entries.Where(e=>S(e.Data,"eventId")==id).ToArray();
                foreach(var required in new[]{"damage_allocation","damage_binding"})
                    if(records.Count(e=>e.Kind==required)!=1 && complete)g.Failures.Add("missing or duplicate "+required+": "+hit.Ref);
                var outcomes=records.Count(e=>e.Kind is "damage_trace" or "damage_trace_missing" or "damage_trace_unsupported" or "damage_trace_mismatch" or "incoming_damage_diagnostic");
                if(outcomes!=1&&complete)g.Failures.Add("missing or duplicate trace outcome: "+hit.Ref);
                foreach(var allocation in records.Where(e=>e.Kind=="damage_allocation"))if(N(allocation.Data,"hp")!=N(hit.Data,"hp"))g.Failures.Add("linked allocation differs from damage: "+hit.Ref);
                foreach(var trace in records.Where(e=>e.Kind=="damage_trace"))if(N(trace.Data.GetProperty("normalized"),"Hp")!=N(hit.Data,"hp")||N(trace.Data.GetProperty("normalized"),"Block")!=N(hit.Data,"blocked"))g.Failures.Add("linked trace differs from damage: "+hit.Ref);
            }
            foreach(var binding in entries.Where(e=>e.Kind=="damage_binding"))
                if(S(binding.Data,"status")=="conflict")g.Failures.Add("conflicting result binding: "+binding.Ref);
            if(complete)
            {
                foreach(var orphan in entries.Where(e=>e.Kind is "damage_binding" or "damage_allocation" or "damage_trace"))
                    if(!entries.Any(e=>e.Kind=="damage"&&S(e.Data,"eventId")==S(orphan.Data,"eventId")))g.Failures.Add("orphan linked record: "+orphan.Ref);
                decimal hp=0,block=0;
                foreach(var e in entries.Where(e=>e.Kind is "damage" or "doom_execution"))
                    if(e.Data.TryGetProperty("enemy",out var enemy)&&enemy.ValueKind==JsonValueKind.True){hp+=N(e.Data,"hp");if(e.Kind=="damage")block+=N(e.Data,"blocked");}
                foreach(var summary in entries.Where(e=>e.Kind=="combat_summary"))
                {decimal totalHp=0,totalBlock=0;foreach(var row in summary.Data.GetProperty("totals").EnumerateObject()){totalHp+=N(row.Value,"Hp");totalBlock+=N(row.Value,"Block");}if(totalHp!=hp||totalBlock!=block)g.Failures.Add("fight totals differ from canonical enemy results: "+summary.Ref);}
            }
            } catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException){g.Failures.Add("malformed linked event: "+e.Message);}
        }
    }
}
