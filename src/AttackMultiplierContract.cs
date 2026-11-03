using System.Reflection;
namespace Based.Spires2DamageMeter;
public static class AttackMultiplierContract
{
 public static IReadOnlyList<string> Missing(Assembly game)
 {
  const BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  var errors=new List<string>();
  if(Enum.GetName(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.PileType")!,5)!="Play"||Enum.GetName(game.GetType("MegaCrit.Sts2.Core.Entities.Cards.CardType")!,1)!="Attack")errors.Add("Card/pile enum changed");
  var lethal=game.GetType("MegaCrit.Sts2.Core.Models.Powers.LethalityPower")!;
  var predicate=lethal.GetMethods(f|BindingFlags.DeclaredOnly).SingleOrDefault(m=>m.Name=="<ModifyDamageMultiplicative>b__4_0");
  if(predicate==null||Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(predicate.GetMethodBody()!.GetILAsByteArray()!))!="6622B7B75243C9AD26724E5139DFBA244A3EDB733C7B1D28A9041308E8005472")errors.Add("Lethality history predicate changed");
  var giant=game.GetType("MegaCrit.Sts2.Core.Models.Powers.GigantificationPower")!;
  if(giant.GetNestedType("Data",f)?.GetField("commandToModify",f)?.FieldType.GetProperty("ModelSource",f)==null)errors.Add("Gigantification command source missing");
  foreach(var name in new[]{"ShrinkPower","SlowPower"})
   if(game.GetType("MegaCrit.Sts2.Core.Models.Powers."+name)?.GetProperty("DynamicVars",f)?.PropertyType.GetProperty("Item",new[]{typeof(string)})?.PropertyType.GetProperty("BaseValue",f)==null)errors.Add(name+" dynamic accessor missing");
  return errors;
 }
}
