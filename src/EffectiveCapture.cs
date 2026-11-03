using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
namespace Based.Spires2DamageMeter;
public static class EffectiveCapture
{
 public static bool Enabled {get;private set;}
 private static string? Key(Creature c)=>Probe.Get(c,"CombatId")?.ToString();
 public static void Register()
 {
  var h=new Harmony(Probe.Id+".Effective");
  try{
   var gain=AccessTools.Method(typeof(Creature),"GainBlockInternal",new[]{typeof(decimal)})??throw new Exception("Missing block gain");
   var clear=AccessTools.Method(typeof(Creature),"ClearBlock",Type.EmptyTypes)??throw new Exception("Missing block clear");
   if(gain.ReturnType!=typeof(void)||clear.ReturnType!=typeof(Task))throw new Exception("Unsupported block lifecycle");
   h.Patch(gain,prefix:new(typeof(EffectiveCapture),nameof(Before)),postfix:new(typeof(EffectiveCapture),nameof(Gained)));
   h.Patch(clear,prefix:new(typeof(EffectiveCapture),nameof(Before)),postfix:new(typeof(EffectiveCapture),nameof(Cleared)));
   Enabled=true;
  }catch(Exception e){h.UnpatchAll(h.Id);Probe.Effective.Incomplete=true;Probe.Emit("effective_capture_error",new{error=e.Message});}
 }
 public static void Before(Creature __instance,out int __state)=>__state=__instance.Block;
 public static void Gained(Creature __instance,int __state){try{if(__instance.Side.ToString()=="Enemy"&&__state==0&&__instance.Block>0&&Key(__instance) is string id){Probe.Effective.Discard(id);Probe.Emit("effective_shield_started",new{target=id});}}catch{Probe.Effective.Incomplete=true;}}
 public static void Cleared(Creature __instance,int __state,ref Task __result)=>__result=AfterClear(__result,__instance,__state);
 private static async Task AfterClear(Task task,Creature c,int before){await task;try{if(c.Side.ToString()=="Enemy"&&before>0&&c.Block==0&&Key(c) is string id){Probe.Effective.Discard(id);Probe.Emit("effective_shield_expired",new{target=id});}}catch{Probe.Effective.Incomplete=true;}}
}
