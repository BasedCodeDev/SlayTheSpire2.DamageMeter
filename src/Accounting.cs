namespace Based.Spires2DamageMeter;

public sealed class Totals
{
    public decimal Hp { get; set; }
    public decimal Block { get; set; }
    public decimal Poison { get; set; }
    public decimal Doom { get; set; }
    public decimal AmplifiedHp { get; set; }
    public decimal AmplifiedBlock { get; set; }
    public decimal SupportGiven { get; set; }
    public decimal SupportReceived { get; set; }
    public decimal SupportGivenHp { get; set; }
    public decimal SupportGivenBlock { get; set; }
    public decimal SupportReceivedHp { get; set; }
    public decimal SupportReceivedBlock { get; set; }
}

public sealed record SupportTransfer(string Provider,string Recipient,string Source,decimal Hp,decimal Block);

public sealed class Accounting
{
    private readonly HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
    public Dictionary<string, Totals> Rows { get; } = new();
    public Dictionary<string, Dictionary<string, decimal>> Sources { get; } = new();
    public List<SupportTransfer> Transfers { get; } = new();
    public List<SourceTotals> StructuredSources { get; } = new();
    private SourceTotals SourceRow(string player,SourceIdentity source)
    {
        var row=StructuredSources.FirstOrDefault(s=>s.Player==player && s.Source==source);
        if(row==null){row=new(){Player=player,Source=source};StructuredSources.Add(row);}return row;
    }
    public void Reset() { seen.Clear(); Rows.Clear(); Sources.Clear(); Transfers.Clear(); StructuredSources.Clear(); }
    public void ApplyAmplification(string dealer,AmplificationResult result,SourceIdentity? source=null)
    {
        if(!Rows.TryGetValue(dealer,out var row)) Rows[dealer]=row=new();
        var dealt=SourceRow(dealer,source??new("unknown","unknown","Unknown source"));
        dealt.AmplifiedHp+=result.OrangeHp;dealt.AmplifiedBlock+=result.OrangeBlock;
        row.AmplifiedHp+=result.OrangeHp; row.AmplifiedBlock+=result.OrangeBlock;
        void Transfer(IReadOnlyList<Share> allocations,bool hp)
        {
            foreach(var share in allocations.Where(s=>s.Owner!=dealer && s.Amount>0))
            {
                if(!Rows.TryGetValue(share.Owner,out var provider))Rows[share.Owner]=provider=new();
                provider.SupportGiven+=share.Amount; row.SupportReceived+=share.Amount;
                var provided=SourceRow(share.Owner,new("power",share.Source,share.Source));
                if(hp){provided.SupportGivenHp+=share.Amount;dealt.SupportReceivedHp+=share.Amount;}else{provided.SupportGivenBlock+=share.Amount;dealt.SupportReceivedBlock+=share.Amount;}
                if(hp){provider.SupportGivenHp+=share.Amount;row.SupportReceivedHp+=share.Amount;}else{provider.SupportGivenBlock+=share.Amount;row.SupportReceivedBlock+=share.Amount;}
                Transfers.Add(new(share.Owner,dealer,share.Source,hp?share.Amount:0,hp?0:share.Amount));
            }
        }
        Transfer(result.HpProviders,true); Transfer(result.BlockProviders,false);
    }
    public void ApplySharedAmplification(string kind,IReadOnlyList<Share> damageShares,AmplificationResult result)
    {
        // Status damage belongs to stack providers, which may differ from the damage command's null dealer.
        foreach(var share in StatusOwnershipLedger.Allocate(damageShares,result.OrangeHp))
        {
            var allocated=StatusOwnershipLedger.Allocate(result.HpProviders,share.Amount);
            var partial=new AmplificationResult(new(share.Amount,0),new(0,0),allocated,Array.Empty<Share>());
            ApplyAmplification(share.Owner,partial,new(kind,share.Source,share.Source));
        }
    }
    public bool AddResolved(ResolvedDamage damage)
    {
        if (damage.Shares.Sum(s => s.Amount) != damage.Hp) throw new InvalidOperationException("Damage allocation does not reconcile");
        if (!seen.Add(damage.Identity)) return false;
        foreach (var s in damage.Shares)
        {
            if (!Rows.TryGetValue(s.Owner, out var row)) Rows[s.Owner] = row = new();
            row.Hp += s.Amount;
            SourceRow(s.Owner,damage.Source??new(damage.Kind,s.Source,s.Source)).Hp+=s.Amount;
            if (damage.Kind == "Poison") row.Poison += s.Amount;
            if (damage.Kind == "Doom") row.Doom += s.Amount;
            if (!Sources.TryGetValue(s.Owner, out var sources)) Sources[s.Owner] = sources = new();
            var key = damage.Kind + ": " + s.Source;
            sources[key] = sources.GetValueOrDefault(key) + s.Amount;
        }
        if (damage.Block > 0)
        {
            var owner = damage.Shares.FirstOrDefault()?.Owner ?? StatusOwnershipLedger.Unknown;
            if (!Rows.TryGetValue(owner, out var row)) Rows[owner] = row = new();
            row.Block += damage.Block;
            SourceRow(owner,damage.Source??new(damage.Kind,damage.Shares.FirstOrDefault()?.Source??"unknown",damage.Kind)).Block+=damage.Block;
        }
        return true;
    }
    public bool Add(object identity, string owner, decimal unblocked, decimal blocked, decimal overkill, string kind)
    {
        var normalized=EngineDamage.Normalize(unblocked,blocked,overkill);
        return AddResolved(new(identity,kind,normalized.Hp,normalized.Block,new[]{new Share(owner,"Unknown source",normalized.Hp)}));
    }
}
