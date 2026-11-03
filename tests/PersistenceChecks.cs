using Based.Spires2DamageMeter;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using HarmonyLib;
static class PersistenceChecks
{
 static string Json(int hp)=>"{\"game_mode\":1,\"start_time\":123,\"rng\":{\"seed\":\"seed\"},\"players\":[{\"net_id\":2,\"hp\":"+hp+"}]}";
 public static void Run(Action<bool,string> check){
 var p=new PersistencePayload();p.Capture("run.save",Json(20));var original=p.Hash;
 check(original==RunStatisticsStore.PayloadFingerprint(Json(20)),"payload fingerprint binds actual saved JSON");
 check(original!=RunStatisticsStore.PayloadFingerprint(Json(19)),"changed gameplay cannot match payload checkpoint");
 check(RunStatisticsStore.PayloadFingerprint("{\"a\":1,\"b\":2}")==RunStatisticsStore.PayloadFingerprint("{\"b\":2,\"a\":1}"),"payload key order is canonical");
 check(RunStatisticsStore.PayloadFingerprint("{\"save_time\":1}")!=RunStatisticsStore.PayloadFingerprint("{\"save_time\":2}"),"v2 payload preserves all saved values");
 check(PersistencePayload.Identity("p",Json(20))!=PersistencePayload.Identity("q",Json(20)),"payload identities isolate profiles");
 var dir=Path.Combine(Path.GetTempPath(),"payload-check-"+Guid.NewGuid().ToString("N"));
 try{
 var run=PersistencePayload.Identity("profile",Json(20));var store=new RunStatisticsStore(dir);
 var credits=new CreditedDamage();credits.Add("hit","Direct",9,0,new[]{new Share("2","Strike",9)},new("card","strike","Strike"),null);
 var effective=new EffectiveDamageLedger();effective.Add("hit","enemy",credits.Credits,true);
 store.Completed["floor1"]=new("floor1",credits.Rows(),new()){Attribution=credits.Snapshot(),Effective=effective.Snapshot(),StructuredSources=credits.Sources()};
 store.Save(run,original!);var restarted=new RunStatisticsStore(dir);restarted.Restore(run,RunStatisticsStore.PayloadFingerprint(Json(20)));
 check(restarted.Completed["floor1"].Attribution!.Credits.Sum(c=>c.Hp)==9&&restarted.Completed["floor1"].Effective!.Credits.Sum(c=>c.Hp)==9,"payload restart restores credited/effective totals and sources");
 restarted.Restore(run,RunStatisticsStore.PayloadFingerprint(Json(19)));check(restarted.Partial&&restarted.Completed.Count==0,"unmatched payload cannot borrow later statistics");
 }finally{Directory.Delete(dir,true);}

 }
 public static void Production(Assembly game,Action<bool,string> check){
 var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.godot/mono/temp/bin/Release/Based.Spires2DamageMeter.dll"));
 var mod=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);var session=mod.GetType("Based.Spires2DamageMeter.RunSession")!;
 object? Call(string method,params object?[] args)=>session.GetMethod(method)!.Invoke(null,args);
 var run=RuntimeHelpers.GetUninitializedObject(game.GetType("MegaCrit.Sts2.Core.Saves.SerializableRun")!);
 object?[] start={run,null};Call("SavePrefix",start);
 try{
 var bytes=System.Text.Encoding.UTF8.GetBytes(Json(20));Call("WritePayload","run.save",bytes);Array.Fill(bytes,(byte)32);
 var field=session.GetField("saving",BindingFlags.NonPublic|BindingFlags.Static)!;var local=field.GetValue(null)!;var op=local.GetType().GetProperty("Value")!.GetValue(local)!;
 var payload=op.GetType().GetField("Payload")!.GetValue(op)!;
 check((string)payload.GetType().GetProperty("Json")!.GetValue(payload)! == Json(20),"production observer freezes bytes before caller mutation");
 var failure=new InvalidOperationException("native failure");object?[] finish={Task.FromException(failure),start[1]};Call("SavePostfix",finish);
 try{((Task)finish[0]!).GetAwaiter().GetResult();check(false,"save failure must propagate");}catch(InvalidOperationException ex){check(ReferenceEquals(ex,failure),"production save wrapper preserves native exception identity");}
 }finally{Call("SaveFinalizer",null,start[1]);}
 object?[] outer={null};Call("LoadPrefix",outer);object?[] inner={null};Call("LoadPrefix",inner);
 check(inner[0]!=null,"nested production load shares one operation");Call("LoadFinalizer",null,inner[0]);Call("LoadFinalizer",null,outer[0]);
 var probe=mod.GetType("Based.Spires2DamageMeter.Probe")!;
 try{Call("Register");
 var migration=game.GetType("MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager")!;
 var generic=migration.GetMethods(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Single(m=>m.Name=="LoadSaveFromPath"&&m.IsGenericMethodDefinition);
 foreach(var typeName in new[]{"MegaCrit.Sts2.Core.Saves.SerializableRun","MegaCrit.Sts2.Core.Saves.SerializableProgress","MegaCrit.Sts2.Core.Saves.ProfileSave"}){
 var nativeType=game.GetType(typeName);if(nativeType==null)continue;
 var info=Harmony.GetPatchInfo(generic.MakeGenericMethod(nativeType));
 check(info==null||!info.Owners.Contains((string)probe.GetField("Id")!.GetValue(null)!+".RunStorage"),"production persistence does not patch generic loader: "+nativeType.Name);
 }
 object?[] scope={null};Call("LoadPrefix",scope);
 Call("ReadPayload","profile.save","{\"schema_version\":2}");Call("ReadPayload","progress.save","{\"schema_version\":21}");
 var loading=session.GetField("loading",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;var context=loading.GetType().GetProperty("Value")!.GetValue(loading)!;
 check(context.GetType().GetField("Accepted")!.GetValue(context)==null,"profile and progress reads never bind run checkpoints");
 Call("ReadPayload","current_run_mp.save.backup",Json(20));check(context.GetType().GetField("Accepted")!.GetValue(context)!=null,"non-generic I/O captures accepted run backup payload");Call("LoadFinalizer",null,scope[0]);
 check(session.GetField("storageError",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)==null,"production save/load observers register against installed game");}
 finally{var id=(string)probe.GetField("Id")!.GetValue(null)!;new Harmony(id+".RunStorage").UnpatchAll(id+".RunStorage");}
 var gen=session.GetField("generation",BindingFlags.NonPublic|BindingFlags.Static)!;
 object?[] pendingStart={run,null};Call("SavePrefix",pendingStart);var pending=new TaskCompletionSource();object?[] pendingFinish={pending.Task,pendingStart[1]};Call("SavePostfix",pendingFinish);
 var generation=(int)gen.GetValue(null)!;gen.SetValue(null,generation+1);pending.SetResult();((Task)pendingFinish[0]!).GetAwaiter().GetResult();check(session.GetField("store",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)==null,"production stale run completion cannot publish a checkpoint");gen.SetValue(null,generation);
 var published=session.GetField("publishedOrder",BindingFlags.NonPublic|BindingFlags.Static)!;var previous=(long)published.GetValue(null)!;
 object?[] oldStart={run,null};Call("SavePrefix",oldStart);var delayed=new TaskCompletionSource();object?[] oldFinish={delayed.Task,oldStart[1]};Call("SavePostfix",oldFinish);published.SetValue(null,long.MaxValue);delayed.SetResult();((Task)oldFinish[0]!).GetAwaiter().GetResult();check(session.GetField("store",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)==null,"production older completion cannot replace newer publication");published.SetValue(null,previous);
 var store=game.GetType("MegaCrit.Sts2.Core.Saves.CloudSaveStore")!;
 check(store.GetMethod("WriteFileAsync",new[]{typeof(string),typeof(byte[])})!.ReturnType==typeof(Task)&&store.GetMethod("ReadFile",new[]{typeof(string)})!.ReturnType==typeof(string),"installed payload I/O signatures validated");
 }
}

