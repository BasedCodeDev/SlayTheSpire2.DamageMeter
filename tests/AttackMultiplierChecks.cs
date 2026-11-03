using Based.Spires2DamageMeter;
using System.Reflection;
using System.Text.Json;
static class AttackMultiplierChecks
{
 static AttackMultiplierSnapshot Base(string model)=>new(model,2,true,true,true,false,true,true,false,true,false,0,0,true,false,20,new[]{new Share("A","power",1)});
 public static void Run(Action<bool,string> check)
 {
  var expected=new Dictionary<string,decimal>{{"DoubleDamagePower",2},{"LethalityPower",1.02m},{"GigantificationPower",3},{"ShrinkPower",.8m},{"SlowPower",3},{"TrackingPower",2},{"KnockdownPower",2}};
  foreach(var model in AttackMultiplierSnapshot.Models)
  {
   var s=Base(model);check(s.Expected==expected[model],model+" eligible formula");check((s with{Powered=false}).Expected==1,model+" non-powered neutral");
  }
  foreach(var model in new[]{"DoubleDamagePower","TrackingPower"})
  {
   var s=Base(model);check((s with{DealerOwner=false,DealerPet=true}).Expected==2,model+" owned pet");check((s with{DealerOwner=false,DealerPet=false}).Expected==1,model+" unrelated dealer/pet");check((s with{HasCard=false}).Expected==1,model+" missing card");
  }
  check((Base("TrackingPower") with{TargetWeak=false}).Expected==1,"Tracking requires current target Weak presence");
  foreach(var amount in new[]{-2m,0m,5m})
  {
   check((Base("DoubleDamagePower") with{Amount=amount}).Expected==2,"Double Damage fixed factor ignores Amount");
   check((Base("GigantificationPower") with{Amount=amount}).Expected==3,"Gigantification fixed factor ignores Amount");
   check((Base("TrackingPower") with{Amount=amount}).Expected==amount,"Tracking preserves signed/zero Amount");
   check((Base("KnockdownPower") with{Amount=amount}).Expected==amount,"Knockdown preserves signed/zero Amount");
   check((Base("LethalityPower") with{Amount=amount}).Expected==1+amount/100,"Lethality percentage Amount");
  }
  var giant=Base("GigantificationPower");check((giant with{Restricted=true,SourceMatches=false}).Expected==1,"Gigantification mismatching restricted source");check((giant with{Restricted=true}).Expected==3,"Gigantification matching restricted source");
  foreach(var model in new[]{"GigantificationPower","LethalityPower"}){check((Base(model) with{HasCard=false}).Expected==1,model+" card required");check((Base(model) with{CardOwner=false}).Expected==1,model+" card owner differs");}
  var lethal=Base("LethalityPower");check((lethal with{StartedCount=1}).Expected==1,"Lethality outside-play excludes prior started attack");check((lethal with{InPlay=true,StartedCount=1}).Expected==1.02m,"Lethality current started attack included once");check((lethal with{InPlay=true,StartedCount=2}).Expected==1,"Lethality second started attack excluded");check((lethal with{InPlay=true,PlayIndex=1}).Expected==1,"Lethality repeated current play index excluded");check((lethal with{InPlay=false,PlayIndex=1}).Expected==1.02m,"Lethality index guard only applies in play pile");
  var history=new[]{new StartedAttack(false,true,true),new StartedAttack(true,false,true),new StartedAttack(true,true,false)};
  check(AttackMultiplierSnapshot.CountStarted(history)==0&&AttackMultiplierSnapshot.CountStarted(history.Append(new(true,true,true)))==1,"Lethality filters turn, attack type and owner");
  check((Base("ShrinkPower") with{Amount=999,DynamicValue=30}).Expected==.7m,"Shrink reads dynamic percentage not Amount");check((Base("ShrinkPower") with{DealerOwner=false}).Expected==1,"Shrink owner exclusion");
  check((Base("SlowPower") with{Amount=999,DynamicValue=3}).Expected==1.3m,"Slow reads dynamic value not Amount");check((Base("SlowPower") with{TargetOwner=false}).Expected==1,"Slow target ownership");
  check((Base("KnockdownPower") with{DealerApplier=true}).Expected==1&&(Base("KnockdownPower") with{TargetOwner=false}).Expected==1,"Knockdown excludes applier and unrelated target, including equal-null identities");
  foreach(var hp in new[]{1m,100m})foreach(var block in new[]{0m,8m})
  {
   var r=Amplification.Calculate(6,hp,block,new[]{new DamageModifier("double",ModifierKind.Multiply,2,true,new[]{new Share("B","double",1)}),new DamageModifier("strength",ModifierKind.Add,4,true,new[]{new Share("A","strength",1)})});
   var a=new Accounting();a.AddResolved(new(new object(),"Direct",r.Actual.Hp,r.Actual.Block,new[]{new Share("A","hit",r.Actual.Hp)}));a.ApplyAmplification("A",r);
   check(r.Red+r.OrangeHp==r.Actual.Hp&&r.Blue+r.OrangeBlock==r.Actual.Block&&a.Rows.Values.Sum(t=>t.Hp+t.Block-t.SupportReceived+t.SupportGiven)==r.Actual.Hp+r.Actual.Block,"multipliers conserve capped source/Contribution budgets");
  }
  var json=JsonSerializer.Serialize(new{schemaVersion=6,accountingVersion=2,amplificationVersion=3,session="test",combat=1,sequence=1,role="Host",kind="multiplicative_snapshot",data=new{snapshot=giant,expected=3,observed=2}});
  var report=new TraceReport();report.Read("synthetic",new[]{json});check(report.Groups.Single().Failures.Any(f=>f.Contains("multiplicative snapshot")),"analyser rejects incorrect native multiplier");
 }
 public static void Installed(Assembly a,Action<bool,string> check)=>check(AttackMultiplierContract.Missing(a).Count==0,"attack multiplier history/enum/command/dynamic accessors validated");
}
