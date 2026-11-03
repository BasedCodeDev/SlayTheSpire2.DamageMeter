using Based.Spires2DamageMeter;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Reflection.Emit;
static class DispatchChecks
{
 public static void Installed(Assembly game,Action<bool,string> check)
 {
  using(var fixture=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","dispatch-failure.json"))))
   check(fixture.RootElement.GetProperty("count").GetInt32()==35&&fixture.RootElement.GetProperty("reasons").EnumerateArray().Any(r=>r.GetString()!.Contains("ValueProp")),"observed dispatch-failure fixture preserves original unsupported reasons");
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
  var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root,".godot/mono/temp/bin/Release/Based.Spires2DamageMeter.dll"));
  var bridge=mod.GetType("Based.Spires2DamageMeter.ModifierCapturePatch")!;
  var chain=mod.GetType("Based.Spires2DamageMeter.NativeMultiplierCapture")!;
  var native=mod.GetType("Based.Spires2DamageMeter.NativeDamageCapture")!;
  const BindingFlags hidden=BindingFlags.Static|BindingFlags.NonPublic;
  var traceLocal=native.GetField("trace",hidden)!.GetValue(null)!;var traceValue=traceLocal.GetType().GetProperty("Value")!;var priorTrace=traceValue.GetValue(traceLocal);
  var chainLocal=chain.GetField("current",hidden)!.GetValue(null)!;var chainValue=chainLocal.GetType().GetProperty("Value")!;var priorChain=chainValue.GetValue(chainLocal);
  var trace=Activator.CreateInstance(native.GetNestedType("Trace",BindingFlags.NonPublic)!,true)!;
  var parent=Activator.CreateInstance(chain.GetNestedType("Frame",BindingFlags.NonPublic)!,true)!;
  bool Unsupported()=>(bool)trace.GetType().GetField("Unsupported")!.GetValue(trace)!;
  void Reset(){trace.GetType().GetField("Unsupported")!.SetValue(trace,false);((HashSet<string>)trace.GetType().GetField("UnsupportedReasons")!.GetValue(trace)!).Clear();}
  try
  {
   traceValue.SetValue(traceLocal,trace);chainValue.SetValue(chainLocal,parent);
   foreach(var name in new[]{"WeakPower","VulnerablePower"})
   {
    var type=game.GetType("MegaCrit.Sts2.Core.Models.Powers."+name)!;var instance=RuntimeHelpers.GetUninitializedObject(type);
    var rootMethod=type.GetMethod("ModifyDamageMultiplicative")!;
    check(ModifierDispatch.Select(type,rootMethod)==ModifierHandler.Chain,name+" exact callback dispatches to chain");
    foreach(var methodName in new[]{"ModifyDamageAdditive","ModifyDamageCap"})
    {
     Reset();var method=type.GetMethod(methodName)!;
     check(ModifierDispatch.Select(type,method)==ModifierHandler.Generic,name+" inherited "+methodName+" bypasses specialized handler");
     object?[] direct={instance,method,Array.Empty<object>(),null};chain.GetMethod("Prefix")!.Invoke(null,direct);
     check(direct[3]==null&&!Unsupported()&&ReferenceEquals(chainValue.GetValue(chainLocal),parent),"production root guard creates no frame or argument reads");
     object?[] args={instance,method,Array.Empty<object>(),null};bridge.GetMethod("Prefix")!.Invoke(null,args);
     bridge.GetMethod("Postfix")!.Invoke(null,new[]{instance,(object)method,methodName=="ModifyDamageCap"?decimal.MaxValue:0m,args[3]});
     bridge.GetMethod("Finalizer")!.Invoke(null,new[]{null,args[3]});
     check(!Unsupported()&&ReferenceEquals(chainValue.GetValue(chainLocal),parent),"active production coordinator preserves generic callback and parent context");
    }
    Reset();object?[] broken={instance,rootMethod,Array.Empty<object>(),null};chain.GetMethod("Prefix")!.Invoke(null,broken);
    var result=chain.GetMethod("Finish")!.Invoke(null,new[]{broken[3],(object)1m});
    var reasons=(HashSet<string>)trace.GetType().GetField("UnsupportedReasons")!.GetValue(trace)!;
    check(result==null&&Unsupported()&&reasons.Count==1&&ReferenceEquals(chainValue.GetValue(chainLocal),parent),"failed production snapshot rejected without secondary chain mismatch");
    chain.GetMethod("Finalizer")!.Invoke(null,new[]{new InvalidOperationException("original"),broken[3]});
    check(ReferenceEquals(chainValue.GetValue(chainLocal),parent),"production exception finalizer restores parent");
   }
   var fake=typeof(DispatchChecks).GetMethod(nameof(ModifyDamageMultiplicative))!;
   check(ModifierDispatch.Select(game.GetType("MegaCrit.Sts2.Core.Models.Powers.WeakPower")!,fake)==ModifierHandler.Unsupported,"malformed intended root fails closed");
   Reset();parent.GetType().GetField("Weak")!.SetValue(parent,true);parent.GetType().GetField("Valid")!.SetValue(parent,true);
   var childType=game.GetType("MegaCrit.Sts2.Core.Models.Powers.DebilitatePower")!;
   object?[] unexpected={RuntimeHelpers.GetUninitializedObject(childType),childType.GetMethod("ModifyVulnerableMultiplier")!,Array.Empty<object>(),null};
   chain.GetMethod("ChildPrefix")!.Invoke(null,unexpected);
   check(unexpected[3]==null&&Unsupported()&&((HashSet<string>)trace.GetType().GetField("UnsupportedReasons")!.GetValue(trace)!).Any(r=>r.Contains("Unexpected child family")),"wrong child family rejected before positional reads");
   parent.GetType().GetField("Valid")!.SetValue(parent,true);Reset();traceValue.SetValue(traceLocal,null);
   var weakType=game.GetType("MegaCrit.Sts2.Core.Models.Powers.WeakPower")!;
   object?[] preview={RuntimeHelpers.GetUninitializedObject(weakType),weakType.GetMethod("ModifyDamageMultiplicative")!,Array.Empty<object>(),null};
   bridge.GetMethod("Prefix")!.Invoke(null,preview);bridge.GetMethod("Finalizer")!.Invoke(null,new[]{null,preview[3]});
   check(ReferenceEquals(chainValue.GetValue(chainLocal),parent),"preview coordinator bypasses state construction and positional reads");
   traceValue.SetValue(traceLocal,trace);
   // Reproduce the old cap indexing: argument 2 is Creature, not ValueProp.
   var creature=RuntimeHelpers.GetUninitializedObject(game.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature")!);
   var cast=new DynamicMethod("old_bad_cast",typeof(void),new[]{typeof(object)});var il=cast.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Unbox_Any,game.GetType("MegaCrit.Sts2.Core.ValueProps.ValueProp")!);il.Emit(OpCodes.Pop);il.Emit(OpCodes.Ret);
   try{cast.CreateDelegate<Action<object>>()(creature);check(false,"old cast must fail");}catch(InvalidCastException){check(true,"negative control reproduces cap Creature-to-ValueProp failure");}
  }
  finally{chainValue.SetValue(chainLocal,priorChain);traceValue.SetValue(traceLocal,priorTrace);}
 }
 public static decimal ModifyDamageMultiplicative(string wrong)=>1;
}
