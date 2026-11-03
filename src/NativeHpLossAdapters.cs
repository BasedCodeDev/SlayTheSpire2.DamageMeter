using System.Reflection;
using System.Security.Cryptography;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace Based.Spires2DamageMeter;

// Read-only state snapshots. Never call gameplay modifier methods during counterfactual replay.
public static class NativeHpLossAdapters
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodInfo,bool> audited=new();
    public static bool Audited(MethodInfo method)=>audited.GetOrAdd(method,m=>m.Module.ModuleVersionId.ToString()==HpLossAdapterAudit.ModuleId&&HpLossAdapterAudit.Hashes.TryGetValue(m.DeclaringType!.Name,out var hash)&&m.DeclaringType.Namespace?.StartsWith("MegaCrit.Sts2.Core.Models.",StringComparison.Ordinal)==true&&Convert.ToHexString(SHA256.HashData(m.GetMethodBody()!.GetILAsByteArray()!))==hash);
    public static HpLossRule? Snapshot(AbstractModel model,MethodInfo method,object[] args)
    {
        if(!Audited(method))throw new InvalidOperationException("Unaudited HP-loss callback: "+method);
        var name=model.GetType().Name;var target=(Creature)args[0];var dealer=args[3] as Creature;
        var power=model as PowerModel;var relic=model as RelicModel;
        var owner=power?.Owner??relic?.Owner.Creature;
        if(name=="TheBoot")
        {
            if(relic==null||ReferenceEquals(target,owner)||(!ReferenceEquals(dealer,owner)&&!ReferenceEquals(dealer,relic.Owner.Osty))||!((ValueProp)args[2]).IsPoweredAttack())return null;
            return new(name,HpLossRuleKind.MinimumAtLeastOne,Dynamic(model,"DamageMinimum"));
        }
        if(!ReferenceEquals(target,owner))return null;
        if(name is "IntangiblePower" or "BeatingRemnant" && !CombatManager.Instance.IsInProgress)return null;
        return name switch
        {
            "BufferPower"=>new(name,HpLossRuleKind.Zero,0),
            "IntangiblePower" or "SlipperyPower"=>new(name,HpLossRuleKind.Cap,1),
            "TungstenRod"=>new(name,HpLossRuleKind.Subtract,Dynamic(model,"HpLossReduction")),
            "BeatingRemnant"=>new(name,HpLossRuleKind.Cap,Dynamic(model,"MaxHpLoss")-Convert.ToDecimal(Probe.Get(model,"DamageReceivedThisTurn")??throw new InvalidOperationException("Missing turn damage"))),
            "HardenedShellPower"=>new(name,HpLossRuleKind.CapNonzero,power!.Amount-ShellDamage(model)),
            _=>throw new InvalidOperationException("Missing HP-loss adapter")
        };
    }
    private static decimal Dynamic(AbstractModel model,string key)
    {
        var vars=Probe.Get(model,"DynamicVars")??throw new InvalidOperationException("Missing dynamic vars");
        var value=vars.GetType().GetProperty("Item",new[]{typeof(string)})!.GetValue(vars,new object[]{key});
        return Convert.ToDecimal(Probe.Get(value,"BaseValue")??throw new InvalidOperationException("Missing dynamic value "+key));
    }
    private static decimal ShellDamage(AbstractModel model)
    {
        var dataType=model.GetType().GetNestedType("Data",BindingFlags.NonPublic|BindingFlags.Public)??throw new InvalidOperationException("Missing shell data type");
        var getter=typeof(PowerModel).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name=="GetInternalData"&&m.IsGenericMethodDefinition);
        var data=getter.MakeGenericMethod(dataType).Invoke(model,null);
        return Convert.ToDecimal(dataType.GetField("damageReceivedThisTurn",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(data));
    }
}
