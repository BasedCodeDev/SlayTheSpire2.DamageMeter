using System.Reflection;
using MegaCrit.Sts2.Core.Models;
namespace Based.Spires2DamageMeter;
public static class ModifierCapturePatch
{
 private sealed class State{public object? Chain;public AdditiveSnapshot? Additive;public AttackMultiplierSnapshot? Multiplier;}
 public static void Prefix(AbstractModel __instance,MethodBase __originalMethod,object[] __args,out object? __state)
 {
  var state=new State();__state=state;if(!NativeDamageCapture.Capturing)return;
  var handler=ModifierDispatch.Select(__instance.GetType(),__originalMethod);
  if(handler==ModifierHandler.Unsupported){NativeDamageCapture.MarkUnsupported("handler=Unsupported; model="+__instance.GetType().FullName+"; method="+__originalMethod);return;}
  if(handler==ModifierHandler.Chain)NativeMultiplierCapture.Prefix(__instance,__originalMethod,__args,out state.Chain);
  try{if(handler==ModifierHandler.Additive&&__instance is PowerModel p)state.Additive=NativeAdditiveCapture.Capture(p,__args);}
  catch(Exception e){NativeDamageCapture.MarkUnsupported("additive snapshot: "+e.Message);}
  try{if(handler==ModifierHandler.AttackMultiplier&&__instance is PowerModel m)state.Multiplier=NativeAttackMultiplierCapture.Capture(m,__args);}
  catch(Exception e){NativeDamageCapture.MarkUnsupported("multiplier snapshot: "+e.Message);}
 }
 public static void Postfix(AbstractModel __instance,MethodBase __originalMethod,decimal __result,object? __state)
 {
  var state=__state as State;
  if(state?.Additive is AdditiveSnapshot a)
  {
   if(a.Expected!=__result)NativeDamageCapture.MarkUnsupported("additive callback output mismatch: "+a.Model);
   NativeDamageCapture.RecordAdditive(a,__result);
  }
  if(state?.Multiplier is AttackMultiplierSnapshot m){if(m.Expected!=__result)NativeDamageCapture.MarkUnsupported("attack multiplier mismatch: "+m.Model);NativeDamageCapture.RecordMultiplier(m,__result);}
  NativeDamageCapture.ModifierPostfix(__instance,__originalMethod,__result,state?.Chain,state?.Additive,state?.Multiplier);
 }
 public static Exception? Finalizer(Exception? __exception,object? __state)=>NativeMultiplierCapture.Finalizer(__exception,(__state as State)?.Chain);
}
