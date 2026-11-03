using Godot;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Migrations;
using Based.Spires2DamageMeter;
using HarmonyLib;
public class FixtureStore:DispatchProxy
{
 public readonly Dictionary<string,string> Files=new();public int Mutations;
 protected override object? Invoke(MethodInfo? method,object?[]? args){var name=method!.Name;var path=args?.FirstOrDefault() as string??"";
 if(name=="ReadFile")return Files[path];if(name=="FileExists")return Files.ContainsKey(path);if(name=="GetFullPath")return "fixture://"+path;
 if(name=="GetLastModifiedTime")return DateTimeOffset.UnixEpoch;
 if(name=="RenameFile"){Mutations++;Files[(string)args![1]!]=Files[path];Files.Remove(path);return null;}
 if(name=="WriteFile"){Mutations++;Files[path]=args![1] is byte[] b?System.Text.Encoding.UTF8.GetString(b):(string)args[1]!;return null;}
 if(name=="DeleteFile"){Mutations++;Files.Remove(path);return null;}
 throw new InvalidOperationException("Unexpected fixture I/O: "+name);
 }
}
public partial class Gate:Node
{
 public override void _Ready(){var report=OS.GetCmdlineUserArgs()[0];File.WriteAllText(report,"Native node entered; fixture not yet run");try{Run(report);}catch(Exception e){File.WriteAllText(report,e.ToString());GetTree().Quit(1);}}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 private void Run(string report){var checks=new List<string>();try{
 if(!Engine.GetVersionInfo()["string"].ToString().StartsWith("4.5.1"))throw new Exception("Wrong engine runtime");
 if(OS.GetUserDataDir().Contains("SlayTheSpire2",StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe user-data directory");
 var store=DispatchProxy.Create<ISaveStore,FixtureStore>();var fixture=(FixtureStore)(object)store;
 var manager=new MigrationManager(store);var asm=typeof(SerializableRun).Assembly;
 RunSession.Register();
 foreach(var typeName in new[]{"ProfileSave","PrefsSave","SerializableProgress"}){
 var type=asm.GetTypes().Single(t=>t.Name==typeName);
 var version=(int)typeof(MigrationManager).GetMethod("GetLatestVersion")!.MakeGenericMethod(type).Invoke(manager,null)!;
 var create=typeof(MigrationManager).GetMethod("CreateNewSave")!.MakeGenericMethod(type).Invoke(manager,null)!;
 var util=asm.GetType("MegaCrit.Sts2.Core.Saves.JsonSerializationUtility")!;
 var info=(System.Text.Json.Serialization.Metadata.JsonTypeInfo)util.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name=="GetTypeInfo"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0).MakeGenericMethod(type).Invoke(null,null)!;
 var json=JsonSerializer.Serialize(create,info);fixture.Files["fixture-"+typeName+".save"]=json;
 var result=typeof(MigrationManager).GetMethod("LoadSave")!.MakeGenericMethod(type).Invoke(manager,new object[]{"fixture-"+typeName+".save"})!;
 if(!(bool)result.GetType().GetProperty("Success")!.GetValue(result)!)throw new Exception("Native load failed: "+typeName);
 if(fixture.Mutations!=0)throw new Exception("Native schema load modified fixture files");
 checks.Add($"PASS native {typeName} schema {version}; no mutation");
 }
 checks.Add("LIMIT: fixture schema gate only; populated progress, production runtime, restart and gameplay certification remain pending");
 File.WriteAllLines(report,checks);GetTree().Quit();
 }catch(Exception e){File.WriteAllText(report,e.ToString());GD.PrintErr(e);GetTree().Quit(1);}finally{new Harmony("save-safety.fixture.RunStorage").UnpatchAll("save-safety.fixture.RunStorage");}}
}
