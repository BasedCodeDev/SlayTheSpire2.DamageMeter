using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
static class OutParameterChecks
{
 private static IEnumerable<object>? assigned;
 private static readonly object item=new();
 [MethodImpl(MethodImplOptions.NoInlining)]
 public static decimal Original(object run,object combat,object target,decimal amount,object props,object dealer,object card,int phase,out IEnumerable<object> modifiers)
 { modifiers=phase==0?Array.Empty<object>():new[]{item};assigned=modifiers;return amount; }
 public static void Prefix(object __2,out object? __state){__state=__2;}
 public static void Postfix(object __2,decimal __3,int __7,decimal __result,object? __state)
 {if(!ReferenceEquals(__2,__state)||__3!=__result)throw new Exception("observer state/result changed");}
 public static Exception? Finalizer(Exception? __exception,object? __state)=>__exception;
 // A negative control reproduces the installed Harmony argument-array writeback bug.
 public static void UnsafePrefix(object[] __args,out object? __state){__state=null;}
 public static void UnsafePostfix(object[] __args,decimal __result,object? __state){}
 static decimal Invoke(int phase,out IEnumerable<object> modifiers)=>Original(item,item,item,6,item,item,item,phase,out modifiers);
 public static void Run(Action<bool,string> check)
 {
  var method=typeof(OutParameterChecks).GetMethod(nameof(Original))!;var h=new Harmony("output.preservation.regression");
  try
  {
   h.Patch(method,prefix:new(typeof(OutParameterChecks),nameof(UnsafePrefix)),postfix:new(typeof(OutParameterChecks),nameof(UnsafePostfix)));
   Invoke(1,out var broken);check(broken==null,"negative control reproduces out-list corruption with argument-array postfix");
   h.UnpatchAll(h.Id);
   h.Patch(method,prefix:new(typeof(OutParameterChecks),nameof(Prefix)),postfix:new(typeof(OutParameterChecks),nameof(Postfix)),finalizer:new(typeof(OutParameterChecks),nameof(Finalizer)));
   foreach(var phase in new[]{0,1,2})
   {
    var amount=Invoke(phase,out var modifiers);
    check(amount==6&&ReferenceEquals(modifiers,assigned)&&modifiers!=null,"typed observer preserves exact native result/list identity, phase "+phase);
    check(modifiers!.Contains(item)==(phase!=0),"downstream Contains accepts preserved output, phase "+phase);
   }
  }finally{h.UnpatchAll(h.Id);}
 }
 public static void Production(Assembly game,Action<bool,string> check)
 {
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
  var path=Path.Combine(root,".godot/mono/temp/bin/Release/Based.Spires2DamageMeter.dll");
  var mod=System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
  var observer=mod.GetType("Based.Spires2DamageMeter.NativeDamageCapture")!;
  var native=game.GetType("MegaCrit.Sts2.Core.Hooks.Hook")!.GetMethod("ModifyHpLost")!;
  var types=native.GetParameters().Select(p=>p.ParameterType).ToArray();
  var assembly=System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("NativeOutputFixture"),System.Reflection.Emit.AssemblyBuilderAccess.Run);
  var type=assembly.DefineDynamicModule("test").DefineType("NativeOutputFixture",TypeAttributes.Public);
  var method=type.DefineMethod("Invoke",MethodAttributes.Public|MethodAttributes.Static,typeof(decimal),types);
  method.SetImplementationFlags(MethodImplAttributes.NoInlining);
  var il=method.GetILGenerator();il.Emit(System.Reflection.Emit.OpCodes.Ldarg_S,(byte)8);il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);il.Emit(System.Reflection.Emit.OpCodes.Newarr,game.GetType("MegaCrit.Sts2.Core.Models.AbstractModel")!);il.Emit(System.Reflection.Emit.OpCodes.Stind_Ref);il.Emit(System.Reflection.Emit.OpCodes.Ldarg_3);il.Emit(System.Reflection.Emit.OpCodes.Ret);
  var target=type.CreateType()!.GetMethod("Invoke")!;
  var h=new Harmony("output.production.regression");
  try
  {
   h.Patch(target,prefix:new(observer.GetMethod("HpPhasePrefix")!),postfix:new(observer.GetMethod("HpPhasePostfix")!),finalizer:new(observer.GetMethod("HpPhaseFinalizer")!));
   object?[] args={null,null,null,6m,Enum.ToObject(types[4],0),null,null,Enum.ToObject(types[7],1),null};
   var result=target.Invoke(null,args);
   check((decimal)result! ==6m&&args[8] is System.Collections.IEnumerable,"production HP observer preserves native-shaped out-modifiers list");
  }finally{h.UnpatchAll(h.Id);}
 }
}
