namespace Based.Spires2DamageMeter;

public static class HpLossAdapterAudit
{
    public const string ModuleId="97f10687-c306-4798-ab75-8b9f23f34dfb";
    public static readonly IReadOnlyDictionary<string,string> Hashes=new Dictionary<string,string>
    {
        ["BufferPower"]="1701B5877D9240FE16E250492B4C5ACBC4944BCBE4CB722CF88C3E7C61C53122",
        ["HardenedShellPower"]="7574C69A65F0874D62CBB3A4D4CB4E58F13BE3FAD00A70362C4270A9EB964967",
        ["IntangiblePower"]="ADE66A430EA257681939460A33BC20BBA007D3EB0B928D3A25EE37893601CBFA",
        ["SlipperyPower"]="A7FB63FC30F7C10230406FA7008AC4E1957DF128E72CE1526504BAA4F286A8DB",
        ["BeatingRemnant"]="E65F62045DCDDE4B50CE415D40ED20F7A1B64BAB185102C9F9D57816B61249D1",
        ["TheBoot"]="B3672FFFB27AEEDE83027F8A8F25353C23C76F0C93AA2792A02144E9CEF5BDA9",
        ["TungstenRod"]="BF80866BB94FC14A4A8D84785AD7A3EC68FFD5A7F8B1699C9CDA8DB8A226ECE5"
    };
}

public enum HpLossRuleKind { Cap, CapNonzero, Zero, Subtract, MinimumAtLeastOne }
public sealed record HpLossRule(string Id,HpLossRuleKind Kind,decimal Value)
{
    public decimal Apply(decimal input)=>Kind switch
    {
        HpLossRuleKind.Cap=>Math.Min(input,Value),
        HpLossRuleKind.CapNonzero=>input==0?0:Math.Min(input,Value),
        HpLossRuleKind.Zero=>0,
        HpLossRuleKind.Subtract=>Math.Max(0,input-Value),
        HpLossRuleKind.MinimumAtLeastOne=>input<1?input:Math.Max(input,Value),
        _=>throw new ArgumentOutOfRangeException()
    };
}
