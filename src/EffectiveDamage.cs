namespace Based.Spires2DamageMeter;
public sealed record EffectiveSnapshot(int Version,List<DamageCredit> Credits,Dictionary<string,List<DamageCredit>> Pending,bool Incomplete);
public sealed class EffectiveDamageLedger
{
 public const int Version=1;
 public List<DamageCredit> Credits {get;}=new();
 public Dictionary<string,List<DamageCredit>> Pending {get;}=new();
 private readonly HashSet<string> seen=new();
 public bool Incomplete {get;set;}
 public void Clear(){Credits.Clear();Pending.Clear();seen.Clear();Incomplete=false;}
 public void Discard(string target)=>Pending.Remove(target);
 public void Finish()=>Pending.Clear();
 public void Add(string id,string? target,IReadOnlyList<DamageCredit> parts,bool? breakthrough)
 {
  if(!seen.Add(id))return;
  Credits.AddRange(parts.Where(c=>c.Hp>0).Select(c=>c with{Block=0}));
  if(target==null||breakthrough==null){Incomplete=true;return;}
  if(!Pending.TryGetValue(target,out var pending))Pending[target]=pending=new();
  pending.AddRange(parts.Where(c=>c.Block>0).Select(c=>c with{Hp=0}));
  if(breakthrough==true){Credits.AddRange(pending);Pending.Remove(target);}
 }
 public EffectiveSnapshot Snapshot()=>new(Version,Credits.ToList(),Pending.ToDictionary(p=>p.Key,p=>p.Value.ToList()),Incomplete);
 public void Merge(EffectiveSnapshot s){if(s.Version!=Version){Incomplete=true;return;}Credits.AddRange(s.Credits);foreach(var p in s.Pending)Pending[p.Key]=p.Value.ToList();Incomplete|=s.Incomplete;}
 public CreditedDamage Ledger(){var ledger=new CreditedDamage();ledger.Merge(new(CreditedDamage.Version,Credits.ToList(),0));return ledger;}
 public decimal PendingFor(string player)=>Pending.Values.SelectMany(p=>p).Where(c=>c.Player==player).Sum(c=>c.Block);
}
