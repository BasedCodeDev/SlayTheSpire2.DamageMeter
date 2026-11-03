namespace Based.Spires2DamageMeter;
public sealed record StartedAttack(bool ThisTurn,bool Attack,bool OwnerMatches);
public sealed record AttackMultiplierSnapshot(string Model,decimal Amount,bool Powered,bool HasCard,bool DealerOwner,bool DealerPet,bool CardOwner,bool TargetOwner,bool Restricted,bool SourceMatches,bool InPlay,int PlayIndex,int StartedCount,bool TargetWeak,bool DealerApplier,decimal DynamicValue,IReadOnlyList<Share> Providers)
{
 public static readonly string[] Models={"DoubleDamagePower","LethalityPower","GigantificationPower","ShrinkPower","SlowPower","TrackingPower","KnockdownPower"};
 public static int CountStarted(IEnumerable<StartedAttack> entries)=>entries.Count(e=>e.ThisTurn&&e.Attack&&e.OwnerMatches);
 public decimal Expected=>!Powered?1:Model switch
 {
  "DoubleDamagePower"=>HasCard&&(DealerOwner||DealerPet)?2:1,
  "LethalityPower"=>HasCard&&CardOwner&&(!InPlay||PlayIndex<=0)&&StartedCount<=(InPlay?1:0)?1+Amount/100:1,
  "GigantificationPower"=>HasCard&&CardOwner&&(!Restricted||SourceMatches)?3:1,
  "ShrinkPower"=>DealerOwner?(100-DynamicValue)/100:1,
  "SlowPower"=>TargetOwner?1+.1m*DynamicValue:1,
  "TrackingPower"=>HasCard&&(DealerOwner||DealerPet)&&TargetWeak?Amount:1,
  "KnockdownPower"=>TargetOwner&&!DealerApplier?Amount:1,
  _=>throw new InvalidOperationException("Unknown multiplier model")
 };
}
