using Based.Spires2DamageMeter;
using System.Reflection;
using System.Text.Json;
static class AmplificationV2Checks
{
 public static void Run(Action<bool,string> check)
 {
  DamageModifier Add(string id,string owner,decimal amount)=>new(id,ModifierKind.Add,amount,true,new[]{new Share(owner,id,1)});
  var cap=new DamageModifier("cap",ModifierKind.Cap,2,true,Array.Empty<Share>());
  var r=Amplification.Calculate(1,100,0,new[]{Add("a","A",2),Add("b","B",2),cap});
  check(r.OrangeHp==1&&r.HpProviders.All(s=>s.Amount==.5m)&&r.HpProviders.All(s=>s.Owner!=StatusOwnershipLedger.Unknown),"capped redundant buffs retain known equal provider weights");
  r=Amplification.Calculate(1,100,0,new[]{Add("a","A",2),Add("b","B",4),cap});
  check(r.HpProviders.Single(s=>s.Owner=="B").Amount>r.HpProviders.Single(s=>s.Owner=="A").Amount&&r.HpProviders.Sum(s=>s.Amount)==1,"unequal capped providers retain proportional credit and exact residual");
  check(r.Weights.All(w=>w.Basis=="uncapped_unrounded_potential"),"allocation records potential-weight fallback basis");
  r=Amplification.Calculate(1,2,0,new[]{Add("a","A",2),Add("b","B",4)},new[]{new HpLossRule("intangible",HpLossRuleKind.Cap,2)});
  check(r.OrangeHp==1&&r.HpProviders.All(s=>s.Owner!=StatusOwnershipLedger.Unknown),"HP-loss prevention caps do not erase provider provenance");
  var dir=Path.Combine(AppContext.BaseDirectory,"amp-migration-"+Guid.NewGuid().ToString("N"));
  try
  {
   var run=new RunIdentity("p","m","s","seed","A");var store=new RunStatisticsStore(dir);
   store.Completed["fight"]=new("fight",new(){["A"]=new(){Hp=12,Block=3,Poison=2,Doom=4,AmplifiedHp=5,SupportGiven=5}},new());store.Save(run,"hash");
   var file=Directory.GetFiles(dir,"*.json",SearchOption.AllDirectories).Single();
   var old=File.ReadAllText(file).Replace("\"Schema\":7","\"Schema\":3");File.WriteAllText(file,old);
   store.Restore(run,"hash");var row=store.Completed["fight"].Players["A"];
   check(store.Partial&&row.Hp==12&&row.Block==3&&row.Poison==2&&row.Doom==4&&row.AmplifiedHp==0&&row.SupportGiven==0,"legacy derived history excluded while actual damage retained");
   check(File.ReadAllText(file)==old,"restoration does not rewrite original checkpoint");
   store.Save(run,"hash");store.Restore(run,"hash");check(store.Partial&&store.Completed["fight"].Players["A"].Hp==12,"migration partial marker survives subsequent save");
  }finally{Directory.Delete(dir,true);}
 }
 public static void Installed(Assembly assembly,Action<bool,string> check)
 {
  var entries=ModifierManifest.Build(assembly.GetType("MegaCrit.Sts2.Core.Models.AbstractModel")!);
  check(ModifierContracts.MissingStateAccessors(assembly).Count==0,"installed HP-loss snapshot accessors are available");
  check(entries.Count==ModifierContracts.Entries.Count,"installed catalogue has one pinned contract per effective method");
  foreach(var type in assembly.GetTypes())foreach(var m in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>ModifierManifest.Slots.Contains(m.Name)&&ModifierContracts.Entries.ContainsKey(type.FullName+"."+m.Name)))
   check(ModifierContracts.Supported(m),"installed modifier contract: "+type.Name+"."+m.Name);
 }
}
