using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
namespace Based.Spires2DamageMeter;
public static class NativeAttackMultiplierCapture
{
 private static readonly Lazy<IReadOnlyList<string>> missing=new(()=>AttackMultiplierContract.Missing(typeof(PowerModel).Assembly));
 public static AttackMultiplierSnapshot Capture(PowerModel power,object[] args)
 {
  if(missing.Value.Count>0)throw new InvalidOperationException(string.Join(", ",missing.Value));
  var model=power.GetType().Name;var dealer=args[3] as Creature;var target=args[0] as Creature;var card=args[4] as CardModel;
  bool powered=((ValueProp)args[2]).IsPoweredAttack(),owner=ReferenceEquals(dealer,power.Owner),cardOwner=false,pet=false,targetOwner=ReferenceEquals(target,power.Owner),inPlay=false,restricted=false,source=false,weak=false,applier=false;
  int index=0,count=0;decimal value=0;
  if(powered)
  {
   if(model is "DoubleDamagePower" or "TrackingPower" && card!=null)pet=!owner&&power.Owner.Pets.Contains(dealer!);
   if(model is "LethalityPower" or "GigantificationPower" && card!=null)cardOwner=ReferenceEquals(card.Owner.Creature,power.Owner);
   if(model=="LethalityPower"&&cardOwner)
   {
    inPlay=card!.Pile?.Type==PileType.Play;index=card.CurrentPlayIndex;
    if(!inPlay||index<=0)count=AttackMultiplierSnapshot.CountStarted(CombatManager.Instance.History.CardPlaysStarted.Select(e=>new StartedAttack(e.HappenedThisTurn(power.CombatState),e.CardPlay.Card.Type==CardType.Attack,ReferenceEquals(e.CardPlay.Card.Owner.Creature,power.Owner))));
   }
   if(model=="GigantificationPower"&&cardOwner)
   {
    var dataType=power.GetType().GetNestedType("Data",BindingFlags.Public|BindingFlags.NonPublic)!;
    var getter=typeof(PowerModel).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name=="GetInternalData"&&m.IsGenericMethodDefinition);
    var data=getter.MakeGenericMethod(dataType).Invoke(power,null);
    var command=dataType.GetField("commandToModify",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(data);
    restricted=command!=null;source=ReferenceEquals(card,Probe.Get(command,"ModelSource"));
   }
   if(model=="ShrinkPower"&&owner)value=Dynamic(power,"DamageDecrease");
   if(model=="SlowPower"&&targetOwner)value=Dynamic(power,"SlowAmount");
   if(model=="TrackingPower"&&card!=null&&(owner||pet))weak=target?.HasPower<WeakPower>()==true;
   if(model=="KnockdownPower"&&targetOwner)applier=ReferenceEquals(dealer,power.Applier);
  }
  return new(model,power.Amount,powered,card!=null,owner,pet,cardOwner,targetOwner,restricted,source,inPlay,index,count,weak,applier,value,Probe.PowerOwnership(power));
 }
 private static decimal Dynamic(PowerModel power,string key)
 {
  var vars=Probe.Get(power,"DynamicVars")!;var item=vars.GetType().GetProperty("Item",new[]{typeof(string)})!.GetValue(vars,new object[]{key});
  return Convert.ToDecimal(Probe.Get(item,"BaseValue")??throw new InvalidOperationException("Missing dynamic value "+key));
 }
}
