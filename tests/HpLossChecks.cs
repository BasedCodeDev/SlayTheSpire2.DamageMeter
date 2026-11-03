using Based.Spires2DamageMeter;
using System.Reflection;
using System.Security.Cryptography;
static class HpLossChecks
{
    public static void Run(Action<bool,string> check)
    {
        var amplifier=new[]{new DamageModifier("strength",ModifierKind.Add,4,true,new[]{new Share("A","Strength",1)})};
        var rules=new[]{new HpLossRule("Intangible",HpLossRuleKind.Cap,1),new HpLossRule("Rod",HpLossRuleKind.Subtract,1)};
        check(Amplification.Calculate(10,100,0,amplifier,rules).Actual.Hp==0,"HP-loss callbacks execute after block in observed order");
        var cap=new[]{new HpLossRule("shell",HpLossRuleKind.CapNonzero,8)};
        var hit=Amplification.Calculate(6,100,0,amplifier,cap);
        check(hit.Actual.Hp==8 && hit.Baseline.Hp==6 && hit.OrangeHp==2,"stateful cap is reevaluated for amplified and baseline amounts");
        check(Amplification.Calculate(6,100,9,amplifier,new[]{new HpLossRule("buffer",HpLossRuleKind.Zero,0)}).Actual==new EffectiveDamage(0,9),"Buffer prevents HP loss while preserving consumed block");
        var boot=new HpLossRule("boot",HpLossRuleKind.MinimumAtLeastOne,5);
        check(boot.Apply(0)==0 && boot.Apply(.5m)==.5m && boot.Apply(1)==5 && boot.Apply(8)==8,"Boot minimum does not create damage from zero or fractional HP loss");
        check(new HpLossRule("shell",HpLossRuleKind.CapNonzero,-1).Apply(0)==0,"depleted Hardened Shell preserves zero-input special case");
        check(Amplification.Calculate(6,100,20,amplifier,bypassBlock:true).Actual==new EffectiveDamage(10,0),"block bypass leaves block unconsumed");
        check(Amplification.Resolve(1.5m,100,0,Array.Empty<DamageModifier>(),hpLoss:new[]{new HpLossRule("fraction",HpLossRuleKind.Subtract,.75m)}).Hp==0,"HP-loss rules run before engine integer truncation");
        foreach(var kind in Enum.GetValues<HpLossRuleKind>())
        foreach(var hp in new decimal[]{0,1,8,100})
        foreach(var block in new decimal[]{0,3,20})
        {
            var result=Amplification.Calculate(6,hp,block,amplifier,new[]{new HpLossRule("matrix",kind,3)});
            if(result.Red<0||result.Blue<0||result.OrangeHp<0||result.OrangeBlock<0||result.Red+result.OrangeHp!=result.Actual.Hp||result.Blue+result.OrangeBlock!=result.Actual.Block)throw new Exception("HP-loss matrix reconciliation failure");
        }
        check(true,"HP-loss matrix conserves segments across lethal, block, cap and prevention cases");
    }
    public static void Installed(Assembly assembly,Action<bool,string> check)
    {
        foreach(var entry in HpLossAdapterAudit.Hashes)
        {
            var type=assembly.GetTypes().Single(t=>t.Name==entry.Key);
            var method=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Single(m=>m.Name.StartsWith("ModifyHpLost",StringComparison.Ordinal));
            check(method.Module.ModuleVersionId.ToString()==HpLossAdapterAudit.ModuleId && Convert.ToHexString(SHA256.HashData(method.GetMethodBody()!.GetILAsByteArray()!))==entry.Value,"installed HP-loss adapter IL contract: "+entry.Key);
        }
    }
}
