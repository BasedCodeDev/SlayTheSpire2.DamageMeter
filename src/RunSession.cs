using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace Based.Spires2DamageMeter;
public static class RunSession
{
    internal static RunStatisticsStore? store;
    internal static RunIdentity? identity;
    internal static string? storageError;
    internal static int generation;
    internal static object? currentState;
    public static string Encounter { get; set; } = "";
    public static bool Active { get; set; }

    public static void AttachState(object? state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.AttachState", state);
    internal static string StateKey(object state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("RunSession.StateKey", state);
    internal static List<EncounterSnapshot> VisibleEncounters() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<List<EncounterSnapshot>>("RunSession.VisibleEncounters");
    public static List<SourceTotals> SourceBreakdowns() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<List<SourceTotals>>("RunSession.SourceBreakdowns");
    public static List<SupportTransfer> SupportTransfers() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<List<SupportTransfer>>("RunSession.SupportTransfers");
    public static CreditedDamage CreditedRun() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<CreditedDamage>("RunSession.CreditedRun");
    public static EffectiveDamageLedger EffectiveRun() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<EffectiveDamageLedger>("RunSession.EffectiveRun");
    public static bool AttributionPartial => VisibleEncounters().Any(e => e.Attribution?.Version != CreditedDamage.Version);

    public static List<SupportTransfer> CreditedTransfers() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<List<SupportTransfer>>("RunSession.CreditedTransfers");
    public static bool StorageIncomplete => storageError != null;
    public static bool HistoryPartial => store?.Partial == true || identity == null;
    public static string Status => storageError != null ? "Run storage incomplete: " + storageError : store?.Partial == true ? "Partial run: " + store.Warning : identity == null ? "Run history not attached" : "Checkpoint history attached";

    public static EncounterSnapshot Capture() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<EncounterSnapshot>("RunSession.Capture");
    public static void Commit() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.Commit");
    public static Dictionary<string, Dictionary<string, decimal>> RunSources() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Dictionary<string, Dictionary<string, decimal>>>("RunSession.RunSources");
    public static Dictionary<string, Totals> RunRows() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Dictionary<string, Totals>>("RunSession.RunRows");
    internal sealed class SaveOperation
    {
        public int Generation = generation;
        public long Order = Interlocked.Increment(ref saveOrder);
        public PersistencePayload Payload = new();
        public List<EncounterSnapshot> Encounters = new();
        public bool Partial;
        public string? Warning;
        public bool CaptureValid = true;
    }

    internal sealed class LoadOperation
    {
        public PersistencePayload? Accepted;
    }

    internal static readonly AsyncLocal<SaveOperation?> saving = new();
    internal static readonly AsyncLocal<LoadOperation?> loading = new();
    internal static long saveOrder, publishedOrder;
    internal static readonly object publicationLock = new();
    internal static string? loadedHash;
    internal static string DirectoryPath => Path.Combine(OS.GetUserDataDir(), Probe.Id, "checkpoints");

    internal static void Error(Exception e) => storageError=e.Message;
    private static void Observe(Action action){try{action();}catch(Exception e){Error(e);}}
    public static void Register()
    {
        var h = new Harmony(Probe.Id + ".RunStorage");
        try
        {
            h.Patch(AccessTools.Method(typeof(RunSaveManager), "SaveRun", new[] { typeof(SerializableRun), typeof(bool) }), prefix: new(typeof(RunSession), nameof(SavePrefix)), postfix: new(typeof(RunSession), nameof(SavePostfix)), finalizer: new(typeof(RunSession), nameof(SaveFinalizer)));
            foreach (var name in new[]
            {
                "LoadRunSave",
                "LoadMultiplayerRunSave",
                "LoadAndCanonicalizeMultiplayerRunSave"
            }

            )
                h.Patch(AccessTools.Method(typeof(RunSaveManager), name), prefix: new(typeof(RunSession), nameof(LoadPrefix)), postfix: new(typeof(RunSession), nameof(LoadPostfix)), finalizer: new(typeof(RunSession), nameof(LoadFinalizer)));
            var assembly = typeof(SerializableRun).Assembly;
            var contract = assembly.GetType("MegaCrit.Sts2.Core.Saves.ISaveStore")!;
            int readers = 0, writers = 0;
            foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && contract.IsAssignableFrom(t)))
                foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    var ps = method.GetParameters();
                    if (method.Name == "ReadFile" && method.ReturnType == typeof(string) && ps.Length == 1 && ps[0].ParameterType == typeof(string))
                    {
                        h.Patch(method, postfix: new(typeof(RunSession), nameof(ReadPayload)));
                        readers++;
                    }

                    if (method.Name is "WriteFile" or "WriteFileAsync" && ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(byte[]) && (method.ReturnType == typeof(void) || method.ReturnType == typeof(Task)))
                    {
                        h.Patch(method, prefix: new(typeof(RunSession), nameof(WritePayload)));
                        writers++;
                    }
                }

            if (readers == 0 || writers == 0)
                throw new Exception("Unsupported run-save I/O contracts");
            Probe.Emit("statistics_adapter_registered", new { fingerprintVersion = 2, readers, writers });
        }
        catch (Exception e)
        {
            h.UnpatchAll(h.Id);
            Error(e);
        }
    }

    public static void SavePrefix(SerializableRun __0, out object? __state)
    {
        object? [] values = new object? []
        {
            __0,
            saving.Value
        };
        Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.SavePrefix", values));
        __state = (object? )values[1]!;
    }

    public static void WritePayload(string __0, byte[] __1) {if(RunSavePolicy.IsRunSave(__0))Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.WritePayload", __0, __1));}
    public static void SavePostfix(ref Task __result, object? __state)
    {
        var op = saving.Value;
        saving.Value = (SaveOperation? )__state;
        if (op != null && !ReferenceEquals(op,__state))
            __result = Saved(__result, op);
    }

    public static Exception? SaveFinalizer(Exception? __exception, object? __state) {saving.Value=__state as SaveOperation;return __exception;}
    internal static async Task Saved(Task task, SaveOperation op)
    {
        await task;
        Publish(op);
    }

    internal static void Publish(SaveOperation op) => Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.Publish", op));
    public static void LoadPrefix(out object? __state)
    {
        object? [] values = new object? []
        {
            loading.Value
        };
        Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.LoadPrefix", values));
        __state = (object? )values[0]!;
    }

    public static void ReadPayload(string __0, string __result) {if(RunSavePolicy.IsRunSave(__0))Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.ReadPayload", __0, __result));}
    public static void LoadPostfix(ReadSaveResult<SerializableRun> __result, object? __state) {try{Observe(()=>global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("RunSession.LoadPostfix", __result, __state));}finally{loading.Value=__state as LoadOperation;}}
    public static Exception? LoadFinalizer(Exception? __exception, object? __state) {loading.Value=__state as LoadOperation;return __exception;}
}
