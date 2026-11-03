using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
namespace Based.Spires2DamageMeter;

public static class NativeDamageCapture
{
    private sealed class Scope { public string Id=Guid.NewGuid().ToString("N"); public List<Trace> Pending = new(); public Trace? Active; public ResultRegistry<Trace> Results=new(); }
    private sealed class Trace
    {
        public required Creature Target; public decimal Raw, Hp, Block, Output;
        public List<DamageModifier> Modifiers = new();
        public List<HpLossRule> HpLoss = new();
        public List<object> Phases = new();
        public bool BypassBlock;
        public Creature? RedirectedReceiver;
        public HashSet<string> UnsupportedReasons = new();
        public bool Unsupported;
        public string Id=Guid.NewGuid().ToString("N");
        public Creature? Dealer; public Creature? BlockOwner;
    }
    private static readonly AsyncLocal<Scope?> scope = new();
    private static readonly AsyncLocal<Trace?> trace = new();
    private static readonly AsyncLocal<Trace?> hpTrace = new();
    private sealed record HpSnapshot(Trace Trace,decimal Input,HpLossRule? Rule,string Method);
    public static TraceCoverage Coverage { get; }=new();
    public static int Matched=>Coverage.Matched;
    public static int Unmatched=>Coverage.Unmeasured;
    public static bool Enabled { get; private set; }
    private static readonly HashSet<object> observed=new(ReferenceEqualityComparer.Instance);
    public static bool Observe(object result,out string eventId){eventId=Guid.NewGuid().ToString("N");lock(observed)return observed.Add(result);}
    public static bool? Bypasses(DamageResult result)=>scope.Value?.Results.Find(result)?.BypassBlock;
    public static void Reset() { Coverage.Clear(); lock(observed)observed.Clear(); }
    public static void Register()
    {
        var h=new Harmony(Probe.Id+".DamageTrace");
        try
        {
            var hooks=DamageHookContract.Resolve(typeof(Hook).Assembly);
            var missing=ModifierContracts.MissingStateAccessors(typeof(Hook).Assembly);
            if(missing.Count>0)throw new InvalidOperationException("Missing modifier state: "+string.Join(", ",missing));
            var ctor=typeof(DamageResult).GetConstructor(new[]{typeof(Creature),typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp)})??throw new InvalidOperationException("Unsupported result constructor");
            var lose=AccessTools.Method(typeof(Creature),"LoseHpInternal",new[]{typeof(decimal),typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp)})??throw new InvalidOperationException("Unsupported HP-loss method");
            if(typeof(Creature).Module.ModuleVersionId.ToString()!=HpLossAdapterAudit.ModuleId)throw new InvalidOperationException("Unaudited result construction module");
            var block=AccessTools.Method(typeof(Creature),"DamageBlockInternal",new[]{typeof(decimal),typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp)})??throw new InvalidOperationException("Unsupported block method");
            if(lose.ReturnType!=typeof(DamageResult)||block.ReturnType!=typeof(decimal))throw new InvalidOperationException("Unsupported damage boundary return types");
            h.Patch(block,prefix:new(typeof(NativeDamageCapture),nameof(BlockPrefix)),postfix:new(typeof(NativeDamageCapture),nameof(BlockPostfix)));
            h.Patch(ctor,postfix:new(typeof(NativeDamageCapture),nameof(ResultConstructed)));
            h.Patch(lose,postfix:new(typeof(NativeDamageCapture),nameof(HpReturned)));
            var damage=typeof(CreatureCmd).GetMethods().Single(m=>m.Name=="Damage" && m.GetParameters().Length==6 && m.GetParameters()[1].ParameterType==typeof(IEnumerable<Creature>) && m.GetParameters()[2].ParameterType==typeof(decimal));
            h.Patch(damage,prefix:new(typeof(NativeDamageCapture),nameof(DamagePrefix)),postfix:new(typeof(NativeDamageCapture),nameof(DamagePostfix)),finalizer:new(typeof(NativeDamageCapture),nameof(DamageFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyDamage)],prefix:new(typeof(NativeDamageCapture),nameof(ModifyPrefix)),postfix:new(typeof(NativeDamageCapture),nameof(ModifyPostfix)),finalizer:new(typeof(NativeDamageCapture),nameof(ModifyFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyHpLost)],prefix:new(typeof(NativeDamageCapture),nameof(HpPhasePrefix)),postfix:new(typeof(NativeDamageCapture),nameof(HpPhasePostfix)),finalizer:new(typeof(NativeDamageCapture),nameof(HpPhaseFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyUnblockedDamageTarget)],postfix:new(typeof(NativeDamageCapture),nameof(RedirectPostfix)));
            foreach(var type in typeof(PowerModel).Assembly.GetTypes().Where(t=>typeof(AbstractModel).IsAssignableFrom(t)))
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>!m.IsAbstract && m.ReturnType==typeof(decimal) && m.Name is "ModifyDamageAdditive" or "ModifyDamageMultiplicative" or "ModifyDamageCap" or "EnchantDamageAdditive" or "EnchantDamageMultiplicative"))
                h.Patch(method,prefix:new(typeof(ModifierCapturePatch),nameof(ModifierCapturePatch.Prefix)),postfix:new(typeof(ModifierCapturePatch),nameof(ModifierCapturePatch.Postfix)),finalizer:new(typeof(ModifierCapturePatch),nameof(ModifierCapturePatch.Finalizer)));
            foreach(var type in typeof(AbstractModel).Assembly.GetTypes().Where(t=>typeof(AbstractModel).IsAssignableFrom(t)&&t!=typeof(AbstractModel)))
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>!m.IsAbstract&&m.Name is "ModifyHpLostBeforeOsty" or "ModifyHpLostBeforeOstyLate" or "ModifyHpLostAfterOsty" or "ModifyHpLostAfterOstyLate"))
                h.Patch(method,prefix:new(typeof(NativeDamageCapture),nameof(HpModifierPrefix)),postfix:new(typeof(NativeDamageCapture),nameof(HpModifierPostfix)));
            foreach(var type in typeof(AbstractModel).Assembly.GetTypes())
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>m.Name is "ModifyWeakMultiplier" or "ModifyVulnerableMultiplier"))
                h.Patch(method,prefix:new(typeof(NativeMultiplierCapture),nameof(NativeMultiplierCapture.ChildPrefix)),postfix:new(typeof(NativeMultiplierCapture),nameof(NativeMultiplierCapture.ChildPostfix)));
            Enabled=true; Probe.Emit("damage_trace_registered",new{status="capture enabled; full coverage audit pending"});
        }
        catch(Exception e) { h.UnpatchAll(h.Id); Enabled=false; Probe.Emit("damage_trace_error",new{error=e.ToString()}); }
    }
    public static void DamagePrefix(out object? __state) { __state=scope.Value; scope.Value=new(); }
    public static void DamagePostfix(ref Task<IEnumerable<DamageResult>> __result,object? __state)
    { var captured=scope.Value; scope.Value=(Scope?)__state; if(captured!=null)__result=Completed(__result,captured); }
    private static async Task<IEnumerable<DamageResult>> Completed(Task<IEnumerable<DamageResult>> task,Scope captured)
    {
        try { var results=await task; try { var returned=new HashSet<object>(results,ReferenceEqualityComparer.Instance); foreach(var orphan in captured.Results.Identities)if(!returned.Contains(orphan))Probe.Emit("damage_binding_error",new{invocationId=captured.Id,reason="bound result was not returned"}); foreach(var result in results)if(!captured.Results.Contains(result))Probe.Emit("damage_binding_error",new{invocationId=captured.Id,reason="returned result was not bound"}); } catch(Exception e){Probe.Emit("damage_binding_error",new{error=e.Message});} return results; }
        finally { captured.Results.Clear();captured.Pending.Clear();captured.Active=null; }
    }
    private sealed record BlockSnapshot(Trace Trace,Creature Owner,int Before);
    public static void BlockPrefix(Creature __instance,out object? __state)
    { __state=null;var active=scope.Value?.Active;if(active!=null){active.BlockOwner=__instance;__state=new BlockSnapshot(active,__instance,__instance.Block);} }
    public static void BlockPostfix(decimal __result,object? __state)
    { if(__state is BlockSnapshot s)s.Trace.Phases.Add(new{phase="block",owner=Probe.Get(s.Owner,"CombatId"),before=s.Before,after=s.Owner.Block,absorbed=__result}); }
    public static void ResultConstructed(DamageResult __instance)=>Bind(__instance);
    public static void HpReturned(DamageResult __result)=>Bind(__result);
    private static void Bind(DamageResult result)
    {
        try { var current=scope.Value?.Active;if(current==null)return;
            if(!ReferenceEquals(result.Receiver,current.Target)&&!ReferenceEquals(result.Receiver,current.RedirectedReceiver))return;
            scope.Value!.Results.Bind(result,current);
        }catch(Exception e){Probe.Emit("damage_binding_error",new{error=e.Message});}
    }
    public static Exception? DamageFinalizer(Exception? __exception,object? __state) { scope.Value=(Scope?)__state; return __exception; }
    public static void ModifyPrefix(object[] __args,out object? __state)
    {
        __state=trace.Value; trace.Value=null;
        if(scope.Value==null || __args[2] is not Creature target || Convert.ToInt32(__args[8])!=0)return;
        trace.Value=new(){Target=target,Dealer=__args[3] as Creature,Raw=(decimal)__args[4],Hp=target.CurrentHp,Block=target.Block,BypassBlock=(Convert.ToInt32(__args[5])&2)!=0};
    }
    public static void ModifyPostfix(decimal __result,object? __state)
    {
        var current=trace.Value; trace.Value=(Trace?)__state;
        if(current==null)return; current.Output=__result; scope.Value?.Pending.Add(current); if(scope.Value!=null)scope.Value.Active=current;
    }
    public static Exception? ModifyFinalizer(Exception? __exception,object? __state) { trace.Value=(Trace?)__state; return __exception; }
    public static bool Capturing=>trace.Value!=null;
    public static void MarkUnsupported(string reason){if(trace.Value is Trace t){t.Unsupported=true;t.UnsupportedReasons.Add(reason);}}
    public static void RecordMultiplier(AttackMultiplierSnapshot snapshot,decimal observed){if(trace.Value is Trace t)Probe.Emit("multiplicative_snapshot",new{resolutionId=t.Id,snapshot,expected=snapshot.Expected,observed});}
    public static void RecordAdditive(AdditiveSnapshot snapshot,decimal observed){if(trace.Value is Trace t)Probe.Emit("additive_snapshot",new{resolutionId=t.Id,snapshot,expected=snapshot.Expected,observed});}
    public static void ModifierPostfix(AbstractModel __instance,MethodBase __originalMethod,decimal __result,object? __state,AdditiveSnapshot? additive=null,AttackMultiplierSnapshot? multiplier=null)
    {
        try
        {
        var chain=NativeMultiplierCapture.Finish(__state,__result);
        var current=trace.Value; if(current==null)return;
        if(__originalMethod is not MethodInfo method || (!ModifierContracts.Supported(method)||!ModifierAudit.InputIndependent(method))){current.Unsupported=true;current.UnsupportedReasons.Add("input-dependent callback: "+__originalMethod);}
        var kind=__originalMethod.Name switch {"ModifyDamageAdditive" or "EnchantDamageAdditive"=>ModifierKind.Add,"ModifyDamageMultiplicative" or "EnchantDamageMultiplicative"=>ModifierKind.Multiply,_=>ModifierKind.Cap};
        if(__originalMethod.DeclaringType!=typeof(AbstractModel)&&__originalMethod.DeclaringType!=typeof(EnchantmentModel))Probe.Emit("modifier_snapshot",new{resolutionId=current.Id,model=__instance.GetType().FullName,method=__originalMethod.Name,audit=__originalMethod is MethodInfo audited&&ModifierContracts.Supported(audited)?"hash_matched_structural_contract":"unsupported",output=__result});
        if((kind==ModifierKind.Add && __result==0)||(kind==ModifierKind.Multiply && __result==1 && chain==null))return;
        if(kind==ModifierKind.Cap && __result==decimal.MaxValue)return;
        var owners=multiplier?.Providers??additive?.Providers??(__instance is PowerModel p?Probe.PowerOwnership(p):Array.Empty<Share>());
        current.Modifiers.Add(new(chain?.Root??$"{__instance.GetType().Name}:{current.Modifiers.Count}",kind,__result,__instance is PowerModel,owners){Chain=chain});
        }
        catch(Exception e){if(trace.Value!=null)trace.Value.Unsupported=true;Probe.Emit("damage_trace_error",new{error=e.ToString()});}
    }
    private static Trace? Pending(Creature target)=>scope.Value?.Active is Trace t && (ReferenceEquals(t.Target,target)||ReferenceEquals(t.RedirectedReceiver,target))?t:null;
    public static void HpPhasePrefix(Creature __2,out object? __state)
    { __state=hpTrace.Value;hpTrace.Value=Pending(__2); }
    // Harmony's __args writeback can overwrite the original's out-modifiers list.
    // Observe only the required input arguments; never request an argument array here.
    public static void HpPhasePostfix(Creature __2,decimal __3,HpLossHookPhase __7,decimal __result,object? __state)
    {
        var current=hpTrace.Value;hpTrace.Value=(Trace?)__state;
        if(current!=null)current.Phases.Add(new{phase=__7.ToString(),input=__3,output=__result,target=Probe.Get(__2,"CombatId")});
    }
    public static Exception? HpPhaseFinalizer(Exception? __exception,object? __state){hpTrace.Value=(Trace?)__state;return __exception;}
    public static void HpModifierPrefix(AbstractModel __instance,MethodBase __originalMethod,object[] __args,out object? __state)
    {
        __state=null;var current=hpTrace.Value;if(current==null)return;
        try
        {
            var rule=NativeHpLossAdapters.Snapshot(__instance,(MethodInfo)__originalMethod,__args);
            __state=new HpSnapshot(current,(decimal)__args[1],rule,__originalMethod.ToString()!);
        }
        catch(Exception e){current.Unsupported=true;current.UnsupportedReasons.Add(e.Message);}
    }
    public static void HpModifierPostfix(decimal __result,object? __state)
    {
        if(__state is not HpSnapshot observed)return;
        var expected=observed.Rule?.Apply(observed.Input)??observed.Input;
        if(expected!=__result){observed.Trace.Unsupported=true;observed.Trace.UnsupportedReasons.Add("HP-loss adapter mismatch: "+observed.Method);}
        if(observed.Rule!=null)observed.Trace.HpLoss.Add(observed.Rule);
    }
    public static void RedirectPostfix(object[] __args,Creature __result)
    {
        if(__args[1] is not Creature target)return;var current=Pending(target);if(current==null)return;
        current.RedirectedReceiver=__result;
        if(!ReferenceEquals(target,__result)){current.Unsupported=true;current.UnsupportedReasons.Add("multi-receiver redirection requires allocation adapter");}
        current.Phases.Add(new{phase="redirect",input=__args[2],original=Probe.Get(target,"CombatId"),receiver=Probe.Get(__result,"CombatId")});
    }
    public static AmplificationResult? Resolve(Creature target,DamageResult result,string eventId)
    {
        var receiver=result.Receiver;
        var outgoing=receiver.Side.ToString()=="Enemy";
        var normalized=EngineDamage.Normalize(result.UnblockedDamage,result.BlockedDamage,result.OverkillDamage);
        try
        {
            var bound=scope.Value?.Results.Find(result);
            Probe.Emit("damage_binding",new{eventId,resultId=eventId,invocationId=scope.Value?.Id,resolutionId=bound?.Id,originalTarget=Probe.Get(bound?.Target,"CombatId"),receiver=Probe.Get(receiver,"CombatId"),blockOwner=Probe.Get(bound?.BlockOwner,"CombatId"),status=scope.Value?.Results.Conflicted(result)==true?"conflict":bound==null?"unmatched":"bound"});
            if(!outgoing)
            {
                var observed=bound;
                var outcome=TraceCoverage.IncomingOutcome(normalized,observed!=null&&!ReferenceEquals(observed.Target,receiver));
                Coverage.Record(false,outcome);
                Probe.Emit("incoming_damage_diagnostic",new{eventId,invocationId=scope.Value?.Id,traceId=observed?.Id,dealer=Probe.Get(observed?.Dealer,"CombatId"),originalTarget=Probe.Get(observed?.Target,"CombatId"),receiver=Probe.Get(receiver,"CombatId"),observed?.Raw,observed?.Output,observed?.Phases,observed?.HpLoss,observed?.UnsupportedReasons,hpBefore=observed?.Hp,blockBefore=observed?.Block,outcome,normalized});return null;
            }
            if(bound==null) { Coverage.Record(true,"missing_trace"); Probe.Emit("damage_trace_missing",new{eventId,invocationId=scope.Value?.Id,normalized});return null; }
            var current=bound;
            if(current.Unsupported){Coverage.Record(true,"unsupported");Probe.Emit("damage_trace_unsupported",new{eventId,invocationId=scope.Value?.Id,traceId=current.Id,reasons=current.UnsupportedReasons,current.Phases,current.HpLoss,normalized});return null;}
            var calculated=Amplification.Calculate(current.Raw,current.Hp,current.Block,current.Modifiers,current.HpLoss,current.BypassBlock);
            if(calculated.Actual.Hp!=normalized.Hp||calculated.Actual.Block!=normalized.Block)
            { Coverage.Record(true,"reconciliation_failure"); Probe.Emit("damage_trace_mismatch",new{eventId,invocationId=scope.Value?.Id,traceId=current.Id,originalTarget=Probe.Get(target,"CombatId"),receiver=Probe.Get(receiver,"CombatId"),current.Raw,current.Output,calculated.Actual,normalized});return null; }
            Coverage.Record(true,"matched"); Probe.Emit("damage_trace",new{eventId,invocationId=scope.Value?.Id,traceId=current.Id,current.Raw,current.Output,current.Modifiers,current.HpLoss,current.Phases,current.BypassBlock,hpBefore=current.Hp,blockBefore=current.Block,calculated,normalized});return calculated;
        }
        catch(Exception e){Coverage.Record(outgoing,"capture_error");Probe.Emit("damage_trace_error",new{error=e.ToString()});return null;}
    }
}
