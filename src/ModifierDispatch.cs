using System.Reflection;
namespace Based.Spires2DamageMeter;
public enum ModifierHandler { Generic, Chain, Additive, AttackMultiplier, Unsupported }
public static class ModifierDispatch
{
 public static ModifierHandler Select(Type model,MethodBase method)
 {
  var name=model.Name;
  var handler=name is "WeakPower" or "VulnerablePower"?ModifierHandler.Chain:AdditiveSnapshot.Models.Contains(name)?ModifierHandler.Additive:AttackMultiplierSnapshot.Models.Contains(name)?ModifierHandler.AttackMultiplier:ModifierHandler.Generic;
  if(handler==ModifierHandler.Generic)return handler;
  var expected=handler==ModifierHandler.Additive?"ModifyDamageAdditive":"ModifyDamageMultiplicative";
  // Unrelated inherited callbacks, including the four-argument cap, are generic.
  if(method.Name!=expected)return ModifierHandler.Generic;
  var shape=new[]{"MegaCrit.Sts2.Core.Entities.Creatures.Creature","System.Decimal","MegaCrit.Sts2.Core.ValueProps.ValueProp","MegaCrit.Sts2.Core.Entities.Creatures.Creature","MegaCrit.Sts2.Core.Models.CardModel"};
  if(method is not MethodInfo m||m.DeclaringType!=model||m.ReturnType!=typeof(decimal)||!m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(shape)||!ModifierContracts.Supported(m))return ModifierHandler.Unsupported;
  return handler;
 }
}
