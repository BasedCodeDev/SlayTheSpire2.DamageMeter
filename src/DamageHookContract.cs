using System.Reflection;
namespace Based.Spires2DamageMeter;

public static class DamageHookContract
{
    public static IReadOnlyDictionary<string,MethodInfo> Resolve(Assembly game)
    {
        const string c="MegaCrit.Sts2.Core.Entities.Creatures.Creature";
        const string p="MegaCrit.Sts2.Core.ValueProps.ValueProp";
        const string card="MegaCrit.Sts2.Core.Models.CardModel";
        const string model="System.Collections.Generic.IEnumerable`1[MegaCrit.Sts2.Core.Models.AbstractModel]&";
        var shapes=new Dictionary<string,string[]>
        {
            ["ModifyDamage"]=new[]{"MegaCrit.Sts2.Core.Runs.IRunState","MegaCrit.Sts2.Core.Combat.ICombatState",c,c,"System.Decimal",p,card,"MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType","MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode",model},
            ["ModifyHpLost"]=new[]{"MegaCrit.Sts2.Core.Runs.IRunState","MegaCrit.Sts2.Core.Combat.ICombatState",c,"System.Decimal",p,c,card,"MegaCrit.Sts2.Core.Hooks.HpLossHookPhase",model},
            ["ModifyUnblockedDamageTarget"]=new[]{"MegaCrit.Sts2.Core.Combat.ICombatState",c,"System.Decimal",p,c}
        };
        var hook=game.GetType("MegaCrit.Sts2.Core.Hooks.Hook")??throw new InvalidOperationException("Missing Hook API");
        return shapes.ToDictionary(s=>s.Key,s=>
        {
            var matches=hook.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name==s.Key&&m.GetParameters().Select(p=>p.ParameterType.ToString()).SequenceEqual(s.Value)).ToArray();
            if(matches.Length!=1 || matches[0].ReturnType.FullName!=(s.Key=="ModifyUnblockedDamageTarget"?c:"System.Decimal"))throw new InvalidOperationException("Unsupported damage hook shape: "+s.Key);
            return matches[0];
        });
    }
}
