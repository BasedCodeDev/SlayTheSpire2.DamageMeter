using Based.Spires2DamageMeter;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
static class AdditiveChecks
{
 static AdditiveSnapshot Base(string model)=>new(model,4,true,true,true,true,true,true,true,true,true,false,true,true,0,new[]{new Share("A","bonus",1)});
 public static void Run(Action<bool,string> check)
 {
  foreach(var model in AdditiveSnapshot.Models)
  {
   var active=Base(model);if(model=="LeadershipPower")active=active with{DealerOwner=false};
   check(active.Expected==4,model+" active");
   check((active with{Powered=false}).Expected==0,model+" unpowered");
   foreach(var amount in new[]{-3m,0m,2m})check((active with{Amount=amount}).Expected==amount,model+" signed amount");
   if(model is "StrengthPower" or "VigorPower" or "AccuracyPower" or "PhantomBladesPower")check((active with{DealerOwner=false}).Expected==0,model+" unrelated dealer");
  }
  var vigor=Base("VigorPower");
  check((vigor with{HasCard=false,RestrictedCommand=false}).Expected==4,"Vigor unrestricted null-card attack");
  foreach(var v in new[]{vigor with{RestrictedCommand=true,HasCard=false},vigor with{RestrictedCommand=true,SourceMatches=false},vigor with{RestrictedCommand=true,AttackerMatches=false}})check(v.Expected==0,"Vigor restricted identity mismatch");
  check((vigor with{RestrictedCommand=true}).Expected==4,"Vigor matching restricted command");
  var phantom=Base("PhantomBladesPower");check((phantom with{PriorFinished=1}).Expected==0,"Phantom completed matching play blocks bonus");
  check((phantom with{PriorFinished=0}).Expected==4,"Phantom multi-hit before finish retains bonus");
  var history=new[]{new AdditiveHistoryEntry(false,true,true),new AdditiveHistoryEntry(true,true,false),new AdditiveHistoryEntry(true,false,true)};
  check(AdditiveSnapshot.CountPrior(history)==0,"Phantom ignores previous turns, other players and untagged plays");
  check(AdditiveSnapshot.CountPrior(history.Append(new(true,true,true)))==1,"Phantom counts matching owner/tag/turn finished history");
  foreach(var model in new[]{"AccuracyPower","PhantomBladesPower"}){check((Base(model) with{HasCard=false}).Expected==0,model+" null card");check((Base(model) with{Tagged=false}).Expected==0,model+" untagged card");}
  check((Base("CalcifyPower") with{IsOsty=false}).Expected==0&&(Base("CalcifyPower") with{PetOwnerMatches=false}).Expected==0,"Calcify rejects other pets and other owner's Osty");
  check(Base("LeadershipPower").Expected==0&&(Base("LeadershipPower") with{DealerOwner=false,SameSide=false}).Expected==0&&(Base("LeadershipPower") with{HasDealer=false}).Expected==0,"Leadership owner/opposing/null dealer excluded");
  check((Base("TaintedPower") with{TargetOwner=false}).Expected==0,"Tainted target ownership");
  foreach(var hp in new[]{1m,100m})foreach(var block in new[]{0m,7m})
  {
   var hit=Amplification.Calculate(6,hp,block,new[]{new DamageModifier("bonus",ModifierKind.Add,4,true,vigor.Providers),new DamageModifier("vulnerable",ModifierKind.Multiply,1.5m,true,new[]{new Share("B","Vulnerable",1)})});check(hit.Red+hit.OrangeHp==hit.Actual.Hp&&hit.Blue+hit.OrangeBlock==hit.Actual.Block,"additive/multiplier cap budgets");
   var a=new Accounting();a.AddResolved(new(new object(),"Direct",hit.Actual.Hp,hit.Actual.Block,new[]{new Share("A","attack",hit.Actual.Hp)}));a.ApplyAmplification("A",hit);
   check(a.Rows.Values.Sum(t=>t.Hp+t.Block-t.SupportReceived+t.SupportGiven)==hit.Actual.Hp+hit.Actual.Block&&a.Transfers.All(t=>t.Provider!="A"),"additive self/team/source contribution conserves actual damage");
  }
  var unknown=Amplification.Calculate(6,100,0,new[]{new DamageModifier("bonus",ModifierKind.Add,4,true,new[]{new Share(StatusOwnershipLedger.Unknown,"missing",1)})});
  check(unknown.HpProviders.Single().Owner==StatusOwnershipLedger.Unknown,"additive missing provenance remains Unknown");
  var json=JsonSerializer.Serialize(new{schemaVersion=6,accountingVersion=2,amplificationVersion=3,session="s",role="Host",combat=1,sequence=1,kind="additive_snapshot",data=new{snapshot=vigor,expected=4,observed=3}});
  var report=new TraceReport();report.Read("synthetic",new[]{json});check(report.Groups.Single().Failures.Any(f=>f.Contains("additive snapshot")),"analyser detects native versus rule mismatch");
  PatchChecks(check);
 }
 public static void Installed(Assembly a,Action<bool,string> check)=>check(AdditiveContract.Missing(a).Count==0,"additive tag/history/command metadata contract");
 static int seen;static readonly Stack<int> frames=new();
 [MethodImpl(MethodImplOptions.NoInlining)]static int Callback(int depth){if(depth>0)Callback(depth-1);if(depth<0)throw new InvalidOperationException("original");return depth;}
 public static void Prefix(int __0,out int __state){__state=__0;frames.Push(__0);}
 public static void Postfix(int __result,int __state){if(__result!=__state)throw new Exception("state lost");seen++;}
 public static Exception? Finalizer(Exception? __exception,int __state){if(frames.Pop()!=__state)throw new Exception("context leaked");return __exception;}
 static void PatchChecks(Action<bool,string> check)
 {
  var h=new Harmony("additive.synthetic");try{h.Patch(typeof(AdditiveChecks).GetMethod("Callback",BindingFlags.Static|BindingFlags.NonPublic)!,prefix:new(typeof(AdditiveChecks),nameof(Prefix)),postfix:new(typeof(AdditiveChecks),nameof(Postfix)),finalizer:new(typeof(AdditiveChecks),nameof(Finalizer)));Callback(2);check(seen==3&&frames.Count==0,"patched nested additive contexts preserve per-call state");try{Callback(-1);}catch(InvalidOperationException e){check(e.Message=="original"&&frames.Count==0,"patched exception identity and cleanup preserved");}}finally{h.UnpatchAll(h.Id);}
 }
}
