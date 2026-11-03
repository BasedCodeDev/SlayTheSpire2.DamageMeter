using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
namespace Based.Spires2DamageMeter;

public static class RunSession
{
    private static RunStatisticsStore? store;
    private static RunIdentity? identity;
    private static string? storageError;
    private static int generation;
    private static object? currentState;
    public static string Encounter { get; set; } = "";
    public static bool Active { get; set; }
    public static void AttachState(object? state)
    {
        if(state==null || ReferenceEquals(currentState,state))return;
        if(currentState!=null&&StateKey(currentState)!=StateKey(state)){generation++;store=null;identity=null;Encounter="";Active=false;}
        currentState=state;
    }
    private static string StateKey(object state)
    {
        var players=Probe.Get(state,"Players") as System.Collections.IEnumerable;
        var party=players==null?"":string.Join(",",players.Cast<object>().Select(p=>Probe.Get(p,"NetId")?.ToString()??"").OrderBy(p=>p,StringComparer.Ordinal));
        return $"{Probe.Get(state,"StartTime")}|{Probe.Get(Probe.Get(state,"Rng"),"StringSeed")}|{Probe.Get(state,"GameMode")}|{party}";
    }
    private static List<EncounterSnapshot> VisibleEncounters()
    {
        var snapshots=store?.Completed.Values.ToList()??new();
        if(Active){snapshots.RemoveAll(s=>s.Encounter==Encounter);snapshots.Add(Capture());}
        return snapshots;
    }
    public static List<SourceTotals> SourceBreakdowns()=>VisibleEncounters().SelectMany(e=>e.StructuredSources).ToList();
    public static List<SupportTransfer> SupportTransfers()=>VisibleEncounters().SelectMany(e=>e.Transfers).ToList();
    public static CreditedDamage CreditedRun(){var result=new CreditedDamage();foreach(var e in VisibleEncounters())if(e.Attribution!=null)result.Merge(e.Attribution);return result;}
    public static EffectiveDamageLedger EffectiveRun(){var result=new EffectiveDamageLedger();foreach(var e in VisibleEncounters())if(e.Effective!=null)result.Merge(e.Effective);else result.Incomplete=true;return result;}
    public static bool AttributionPartial=>VisibleEncounters().Any(e=>e.Attribution?.Version!=CreditedDamage.Version);
    public static List<SupportTransfer> CreditedTransfers()=>VisibleEncounters().Where(e=>e.Attribution?.Version==CreditedDamage.Version).SelectMany(e=>e.Transfers).ToList();
    public static bool StorageIncomplete=>storageError!=null;
    public static bool HistoryPartial=>store?.Partial==true||identity==null;
    public static string Status => storageError != null ? "Run storage incomplete: " + storageError : store?.Partial == true ? "Partial run: " + store.Warning : identity == null ? "Run history not attached" : "Checkpoint history attached";
    public static EncounterSnapshot Capture() => JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(new EncounterSnapshot(Encounter, Probe.Meter.Rows, Probe.Meter.Sources){Effective=Probe.Effective.Snapshot(),Transfers=Probe.Meter.Transfers,StructuredSources=Probe.Meter.StructuredSources,Attribution=Probe.Credited.Snapshot()}))!;
    public static void Commit() { if (store != null && Encounter != "") store.Completed[Encounter] = Capture(); }
    public static Dictionary<string, Dictionary<string, decimal>> RunSources()
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if(Active) { encounters.RemoveAll(e=>e.Encounter==Encounter); encounters.Add(Capture()); }
        foreach(var encounter in encounters) foreach(var (id, leaves) in encounter.Sources)
        { if(!result.TryGetValue(id,out var target)) result[id]=target=new(); foreach(var (key,value) in leaves) target[key]=target.GetValueOrDefault(key)+value; }
        return result;
    }
    public static Dictionary<string, Totals> RunRows()
    {
        var rows = new Dictionary<string, Totals>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if (Active) { encounters.RemoveAll(e => e.Encounter == Encounter); encounters.Add(Capture()); }
        foreach (var encounter in encounters) foreach (var (id, totals) in encounter.Players)
        {
            if (!rows.TryGetValue(id, out var row)) rows[id] = row = new();
            row.Hp += totals.Hp; row.Block += totals.Block; row.Poison += totals.Poison; row.Doom += totals.Doom;
            row.AmplifiedHp += totals.AmplifiedHp; row.AmplifiedBlock += totals.AmplifiedBlock; row.SupportGiven += totals.SupportGiven; row.SupportReceived += totals.SupportReceived;
            row.SupportGivenHp+=totals.SupportGivenHp;row.SupportGivenBlock+=totals.SupportGivenBlock;row.SupportReceivedHp+=totals.SupportReceivedHp;row.SupportReceivedBlock+=totals.SupportReceivedBlock;
        }
        return rows;
    }
    private sealed class SaveOperation
    {
        public int Generation=generation;public long Order=Interlocked.Increment(ref saveOrder);public PersistencePayload Payload=new();
        public List<EncounterSnapshot> Encounters=new(); public bool Partial;public string? Warning;public bool CaptureValid=true;
    }
    private sealed class LoadOperation {public PersistencePayload? Accepted;}
    private static readonly AsyncLocal<SaveOperation?> saving=new();
    private static readonly AsyncLocal<LoadOperation?> loading=new();
    private static long saveOrder,publishedOrder;
    private static readonly object publicationLock=new();
    private static string? loadedHash;
    private static string DirectoryPath=>Path.Combine(OS.GetUserDataDir(),Probe.Id,"checkpoints");
    private static void Error(Exception e){storageError=e.Message;Probe.Emit("statistics_storage_error",new{error=e.Message});}
    public static void Register()
    {
        var h=new Harmony(Probe.Id+".RunStorage");
        try{
            h.Patch(AccessTools.Method(typeof(RunSaveManager),"SaveRun",new[]{typeof(SerializableRun),typeof(bool)}),prefix:new(typeof(RunSession),nameof(SavePrefix)),postfix:new(typeof(RunSession),nameof(SavePostfix)),finalizer:new(typeof(RunSession),nameof(SaveFinalizer)));
            foreach(var name in new[]{"LoadRunSave","LoadMultiplayerRunSave","LoadAndCanonicalizeMultiplayerRunSave"})
                h.Patch(AccessTools.Method(typeof(RunSaveManager),name),prefix:new(typeof(RunSession),nameof(LoadPrefix)),postfix:new(typeof(RunSession),nameof(LoadPostfix)),finalizer:new(typeof(RunSession),nameof(LoadFinalizer)));
            var assembly=typeof(SerializableRun).Assembly;
            var contract=assembly.GetType("MegaCrit.Sts2.Core.Saves.ISaveStore")!;int readers=0,writers=0;
            foreach(var type in assembly.GetTypes().Where(t=>!t.IsAbstract&&contract.IsAssignableFrom(t)))
            foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)){
                var ps=method.GetParameters();
                if(method.Name=="ReadFile"&&method.ReturnType==typeof(string)&&ps.Length==1&&ps[0].ParameterType==typeof(string)){h.Patch(method,postfix:new(typeof(RunSession),nameof(ReadPayload)));readers++;}
                if(method.Name is "WriteFile" or "WriteFileAsync"&&ps.Length==2&&ps[0].ParameterType==typeof(string)&&ps[1].ParameterType==typeof(byte[])&&(method.ReturnType==typeof(void)||method.ReturnType==typeof(Task))){h.Patch(method,prefix:new(typeof(RunSession),nameof(WritePayload)));writers++;}
            }
            if(readers==0||writers==0)throw new Exception("Unsupported run-save I/O contracts");
            Probe.Emit("statistics_adapter_registered",new{fingerprintVersion=2,readers,writers});
        }catch(Exception e){h.UnpatchAll(h.Id);Error(e);}
    }
    public static void SavePrefix(SerializableRun __0,out object? __state)
    {
        __state=saving.Value;var operation=new SaveOperation();saving.Value=operation;
        try{
            operation.Encounters=store?.Completed.Values.ToList()??new();
            if(__0.PreFinishedRoom!=null&&Active&&Encounter!=""){operation.Encounters.RemoveAll(e=>e.Encounter==Encounter);operation.Encounters.Add(Capture());}
            operation.Encounters=JsonSerializer.Deserialize<List<EncounterSnapshot>>(JsonSerializer.Serialize(operation.Encounters))!;
            operation.Partial=store?.Partial==true;operation.Warning=store?.Warning;
        }catch(Exception e){operation.CaptureValid=false;Error(e);}
    }
    public static void WritePayload(string __0,byte[] __1){try{if(saving.Value is {} op)op.Payload.Capture(__0,System.Text.Encoding.UTF8.GetString(__1));}catch(Exception e){Error(e);}}
    public static void SavePostfix(ref Task __result,object? __state){var op=saving.Value;saving.Value=(SaveOperation?)__state;if(op!=null)__result=Saved(__result,op);}
    public static Exception? SaveFinalizer(Exception? __exception,object? __state){saving.Value=(SaveOperation?)__state;return __exception;}
    private static async Task Saved(Task task,SaveOperation op)
    {
        await task;
        try{
            lock(publicationLock){
            if(op.Generation!=generation||op.Order<publishedOrder)return;
            if(!op.CaptureValid)throw new Exception("Statistics snapshot capture failed; checkpoint preserved");
            if(op.Payload.Json==null||op.Payload.Hash==null)throw new Exception("No accepted run-save payload observed");
            var run=PersistencePayload.Identity(op.Payload.Profile,op.Payload.Json);
            if(identity!=null&&identity!=run)op.Encounters.Clear();
            var checkpoint=new RunStatisticsStore(DirectoryPath);if(op.Partial)checkpoint.MarkPartial(op.Warning??"Partial history");
            foreach(var e in op.Encounters)checkpoint.Completed[e.Encounter]=e;
            checkpoint.Save(run,op.Payload.Hash);publishedOrder=op.Order;store=checkpoint;identity=run;
            Probe.Emit("statistics_checkpoint_saved",new{operationId=op.Order,fingerprintVersion=2,hash=op.Payload.Hash,encounters=checkpoint.Completed.Count});
            }
        }catch(Exception e){Error(e);}
    }
    public static void LoadPrefix(out object? __state){__state=loading.Value;loading.Value??=new();}
    public static void ReadPayload(string __0,string __result)
    {
        try{
            if(loading.Value is not {} op)return;
            var name=__0.Replace('\\','/').Split('/').Last();
            if(name is not ("current_run.save" or "current_run.save.backup" or "current_run_mp.save" or "current_run_mp.save.backup"))return;
            // Observe raw I/O only. Never patch a shared generic migration/loader body.
            using var doc=JsonDocument.Parse(__result);var root=doc.RootElement;
            if(!root.TryGetProperty("players",out _)||!root.TryGetProperty("rng",out _)||!root.TryGetProperty("start_time",out _))return;
            var payload=new PersistencePayload();payload.Capture(__0,__result);op.Accepted=payload;
        }catch(Exception e){Error(e);}
    }
    public static void LoadPostfix(ReadSaveResult<SerializableRun> __result,object? __state)
    {
        var op=loading.Value;loading.Value=(LoadOperation?)__state;
        if(__state!=null||__result==null||!__result.Success||__result.SaveData==null)return;
        try{
            var payload=op?.Accepted??throw new Exception("No accepted run-load payload observed");
            var run=PersistencePayload.Identity(payload.Profile,payload.Json!);
            if(!Active&&identity==run&&loadedHash==payload.Hash)return;
            generation++;loadedHash=payload.Hash;currentState=null;identity=run;storageError=null;
            store=new(DirectoryPath);store.Restore(run,payload.Hash!);Encounter="";Active=false;
            Probe.Emit("statistics_checkpoint_loaded",new{fingerprintVersion=2,hash=payload.Hash,backup=payload.Path?.EndsWith(".backup")==true,encounters=store.Completed.Count,store.Partial,store.Warning});
        }catch(Exception e){store=null;identity=null;Encounter="";Active=false;Error(e);}
    }
    public static Exception? LoadFinalizer(Exception? __exception,object? __state){loading.Value=(LoadOperation?)__state;return __exception;}
}
