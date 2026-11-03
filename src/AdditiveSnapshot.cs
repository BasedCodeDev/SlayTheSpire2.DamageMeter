namespace Based.Spires2DamageMeter;
public sealed record AdditiveHistoryEntry(bool ThisTurn,bool Tagged,bool OwnerMatches);
public sealed record AdditiveSnapshot(string Model,decimal Amount,bool Powered,bool HasDealer,bool DealerOwner,bool TargetOwner,bool SameSide,bool HasCard,bool Tagged,bool IsOsty,bool PetOwnerMatches,bool RestrictedCommand,bool SourceMatches,bool AttackerMatches,int PriorFinished,IReadOnlyList<Share> Providers)
{
 public bool Eligible=>Powered&&(Model switch
 {
  "StrengthPower"=>DealerOwner,
  "AccuracyPower"=>DealerOwner&&HasCard&&Tagged,
  "VigorPower"=>DealerOwner&&(!RestrictedCommand||(HasCard&&SourceMatches&&AttackerMatches)),
  "CalcifyPower"=>IsOsty&&PetOwnerMatches,
  "LeadershipPower"=>HasDealer&&!DealerOwner&&SameSide,
  "PhantomBladesPower"=>DealerOwner&&HasCard&&Tagged&&PriorFinished==0,
  "TaintedPower"=>TargetOwner,
  _=>throw new InvalidOperationException("Unknown additive model")
 });
 public decimal Expected=>Eligible?Amount:0;
 public static int CountPrior(IEnumerable<AdditiveHistoryEntry> entries)=>entries.Count(e=>e.ThisTurn&&e.Tagged&&e.OwnerMatches);
 public static readonly string[] Models={"StrengthPower","AccuracyPower","VigorPower","CalcifyPower","LeadershipPower","PhantomBladesPower","TaintedPower"};
}
