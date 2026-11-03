using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;
namespace Based.Spires2DamageMeter;
public static class NativeAdditiveCapture
{
 private static readonly Lazy<IReadOnlyList<string>> missing=new(()=>AdditiveContract.Missing(typeof(PowerModel).Assembly));
 public static AdditiveSnapshot Capture(PowerModel power,object[] args)
 {
  if(missing.Value.Count>0)throw new InvalidOperationException(string.Join(", ",missing.Value));
  var dealer=args[3] as Creature;var target=args[0] as Creature;var card=args[4] as CardModel;var model=power.GetType().Name;
  bool powered=((ValueProp)args[2]).IsPoweredAttack(),owner=ReferenceEquals(dealer,power.Owner);
  object? command=null;int prior=0;
  if(model=="VigorPower"&&owner&&powered)
  {
   var data=Data(power);command=data.GetType().GetField("commandToModify",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(data);
  }
  bool tagged=card?.Tags.Contains(CardTag.Shiv)==true;
  if(model=="PhantomBladesPower"&&owner&&powered&&tagged)
   prior=AdditiveSnapshot.CountPrior(CombatManager.Instance.History.CardPlaysFinished.Select(e=>new AdditiveHistoryEntry(e.HappenedThisTurn(power.CombatState),e.CardPlay.Card.Tags.Contains(CardTag.Shiv),ReferenceEquals(e.CardPlay.Card.Owner.Creature,power.Owner))));
  return new(model,power.Amount,powered,dealer!=null,owner,ReferenceEquals(target,power.Owner),dealer!=null&&dealer.Side==power.Owner.Side,card!=null,tagged,dealer?.Monster is Osty,ReferenceEquals(dealer?.PetOwner?.Creature,power.Owner),command!=null,card!=null&&ReferenceEquals(card,Probe.Get(command,"ModelSource")),ReferenceEquals(dealer,Probe.Get(command,"Attacker")),prior,Probe.PowerOwnership(power));
 }
 private static object Data(PowerModel power)
 {
  var type=power.GetType().GetNestedType("Data",BindingFlags.Public|BindingFlags.NonPublic)??throw new InvalidOperationException("Missing Vigor data type");
  var getter=typeof(PowerModel).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(m=>m.Name=="GetInternalData"&&m.IsGenericMethodDefinition);
  return getter.MakeGenericMethod(type).Invoke(power,null)??throw new InvalidOperationException("Missing Vigor data");
 }
}
