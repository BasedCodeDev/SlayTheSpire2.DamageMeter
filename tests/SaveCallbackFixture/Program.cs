using Based.Spires2DamageMeter;
using Based.Spires2DamageMeter.Reload;
if(args.Contains("--inspect")){
 var asm=typeof(MegaCrit.Sts2.Core.Saves.SerializableRun).Assembly;
 foreach(var typeName in new[]{"MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager","MegaCrit.Sts2.Core.Saves.ISaveStore","MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager"}){
 var type=asm.GetType(typeName)!;foreach(var ctor in type.GetConstructors())Console.WriteLine(type.Name+" "+ctor);
 foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly))Console.WriteLine(type.Name+" "+method);
 }return;
}
int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
RuntimeCalls.Handler=null;
var previousSave=new RunSession.SaveOperation();RunSession.saving.Value=previousSave;
RunSession.SavePrefix(null!,out var state);Check(ReferenceEquals(state,previousSave),"missing runtime preserves prior save scope");
var task=Task.CompletedTask;RunSession.SavePostfix(ref task,state);Check(ReferenceEquals(task,Task.CompletedTask),"missing runtime does not publish a parent save operation");
var nativeFailure=new InvalidOperationException("native failure");Check(ReferenceEquals(RunSession.SaveFinalizer(nativeFailure,state),nativeFailure),"save finalizer preserves native exception");
var previousLoad=new RunSession.LoadOperation();RunSession.loading.Value=previousLoad;
RunSession.LoadPrefix(out state);Check(ReferenceEquals(state,previousLoad),"missing runtime preserves prior load scope");
Check(ReferenceEquals(RunSession.LoadFinalizer(nativeFailure,state),nativeFailure),"load finalizer preserves native exception");
int calls=0;RuntimeCalls.Handler=(name,args)=>{calls++;throw new InvalidOperationException("observer failure");};
foreach(var path in new[]{"profile.save","prefs.save","progress.save","settings.save"}){RunSession.WritePayload(path,Array.Empty<byte>());RunSession.ReadPayload(path,"{}");}
Check(calls==0,"non-run I/O never enters runtime");
RunSession.WritePayload("current_run_mp.save",Array.Empty<byte>());RunSession.ReadPayload("current_run_mp.save.backup","{}");Check(calls==2,"observer failures are contained");
Check(RunSession.storageError=="observer failure","tracking failure becomes statistics status");
RunSession.LoadPostfix(null!,previousLoad);Check(ReferenceEquals(RunSession.loading.Value,previousLoad),"load scope restored after observer exception");
var op=new RunSession.SaveOperation();await RunSession.Saved(Task.CompletedTask,op);Check(RunSession.storageError=="observer failure","successful native save survives publication failure");
try{await RunSession.Saved(Task.FromException(nativeFailure),op);throw new Exception("failure swallowed");}catch(InvalidOperationException e){Check(ReferenceEquals(e,nativeFailure),"failed native save preserves exception identity");}
var cancelled=Task.FromCanceled(new CancellationToken(true));try{await RunSession.Saved(cancelled,op);throw new Exception("cancellation swallowed");}catch(OperationCanceledException){Check(true,"native save cancellation preserved");}
RuntimeCalls.Handler=null;Console.WriteLine($"PASS {passed} production callback isolation checks; native game execution NOT certified");
