using Based.Spires2DamageMeter;
static class RunSavePolicyChecks{
 public static void Run(Action<bool,string> check){
 foreach(var path in new[]{"current_run.save","current_run.save.backup","modded/profile1/saves/current_run_mp.save","user://steam/test/modded/profile1/saves/current_run_mp.save.backup"})check(RunSavePolicy.IsRunSave(path),"run payload path accepted: "+path);
 foreach(var path in new[]{"profile.save","prefs.save","progress.save","progress.save.backup","settings.save","current_run.save.corrupt","../current_run.save","current_run.save/","unrelated.json",""})check(!RunSavePolicy.IsRunSave(path),"unrelated or unsafe payload path excluded: "+path);
 check(!RunSavePolicy.IsRunSave(null),"missing payload path excluded");
 }
}
