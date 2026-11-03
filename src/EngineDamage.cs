namespace Based.Spires2DamageMeter;

public sealed record EngineDamage(decimal Hp,decimal Block,decimal Overkill)
{
    public const int AccountingVersion = 2;
    // STS2 LoseHpInternal already caps UnblockedDamage to HP-before minus HP-after.
    public static EngineDamage Normalize(decimal unblockedDamage,decimal blockedDamage,decimal overkillDamage)
        =>new(Math.Max(0,unblockedDamage),Math.Max(0,blockedDamage),Math.Max(0,overkillDamage));
}

public sealed class TraceCoverage
{
    public Dictionary<string,int> Outgoing { get; }=new();
    public Dictionary<string,int> Incoming { get; }=new();
    public int Matched=>Outgoing.GetValueOrDefault("matched");
    public int Unmeasured=>Outgoing.Where(p=>p.Key!="matched").Sum(p=>p.Value);
    public void Record(bool enemyReceiver,string outcome)
    { var counts=enemyReceiver?Outgoing:Incoming;counts[outcome]=counts.GetValueOrDefault(outcome)+1; }
    public void Clear(){Outgoing.Clear();Incoming.Clear();}
    public static string IncomingOutcome(EngineDamage damage,bool redirected)
        =>redirected?"redirected":damage.Hp==0 && damage.Block==0?"prevention_or_redirection_unresolved":damage.Hp==0?"fully_blocked":"resolved_hp_loss";
}
