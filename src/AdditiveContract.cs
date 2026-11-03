using System.Reflection;
namespace Based.Spires2DamageMeter;
public static class AdditiveContract
{
 public static IReadOnlyList<string> Missing(Assembly game)
 {
  var errors=new List<string>();const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  if(Enum.GetName(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardTag")!,5)!="Shiv")errors.Add("Shiv tag changed");
  var vigor=game.GetType("MegaCrit.Sts2.Core.Models.Powers.VigorPower")!;
  var command=vigor.GetNestedType("Data",flags)?.GetField("commandToModify",flags)?.FieldType;
  if(command?.GetProperty("ModelSource",flags)==null||command.GetProperty("Attacker",flags)==null)errors.Add("Vigor command accessors missing");
  var phantom=game.GetType("MegaCrit.Sts2.Core.Models.Powers.PhantomBladesPower")!;
  var predicate=phantom.GetMethods(flags|BindingFlags.DeclaredOnly).SingleOrDefault(m=>m.Name=="<ModifyDamageAdditive>b__8_0");
  if(predicate==null||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(predicate.GetMethodBody()!.GetILAsByteArray()!))!="83694B506D8C9F87F32266908C28D111C9C888AF7340229F11AD4BA32195EC22")errors.Add("Phantom history predicate changed");
  return errors;
 }
}
