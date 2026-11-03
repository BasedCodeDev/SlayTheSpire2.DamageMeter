namespace Based.Spires2DamageMeter;
/// <summary>Shared, non-reloadable classification used by persistence observers.</summary>
public static class RunSavePolicy
{
 public static bool IsRunSave(string? path)
 {
  if(string.IsNullOrWhiteSpace(path))return false;
  var normalized=path.Replace('\\','/');
  if(normalized.Split('/').Any(part=>part is "." or ".."))return false;
  var name=normalized.Split('/').Last();
  return name is "current_run.save" or "current_run.save.backup" or "current_run_mp.save" or "current_run_mp.save.backup";
 }
}
