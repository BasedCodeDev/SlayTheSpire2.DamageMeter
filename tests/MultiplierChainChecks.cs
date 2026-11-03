using Based.Spires2DamageMeter;
static class MultiplierChainChecks
{
 public static void Run(Action<bool,string> check)
 {
  foreach(bool powered in new[]{false,true})foreach(bool target in new[]{false,true})foreach(bool dealer in new[]{false,true})
  {
   check(MultiplierChain.RootEligible(true,target,dealer,powered)==(powered&&target),"Vulnerable eligibility branch");
   check(MultiplierChain.RootEligible(false,target,dealer,powered)==(powered&&dealer),"Weak eligibility branch");
   check(MultiplierChain.ChildEligible("CrueltyPower",false,target,dealer,powered)==(powered&&!target),"Cruelty eligibility branch");
   check(MultiplierChain.ChildEligible("PaperPhrog",false,target,dealer,powered)==(powered&&!target),"Paper Phrog eligibility branch");
   check(MultiplierChain.ChildEligible("PaperKrane",true,target,dealer,powered)==(powered&&target),"Paper Krane eligibility branch");
   check(MultiplierChain.ChildEligible("DebilitatePower",false,target,dealer,powered)==(powered&&target),"Debilitate Vulnerable eligibility branch");
   check(MultiplierChain.ChildEligible("DebilitatePower",true,target,dealer,powered)==(powered&&dealer),"Debilitate Weak eligibility branch");
  }
  var steps=new[]{new MultiplierStep("phrog","PaperPhrog",MultiplierOperation.Add,.25m,false,true,Array.Empty<Share>(),1.5m,1.75m),new MultiplierStep("cruel","CrueltyPower",MultiplierOperation.Add,.2m,true,true,new[]{new Share("B","Cruelty",1)},1.75m,1.95m),new MultiplierStep("debil","DebilitatePower",MultiplierOperation.DoubleDeviation,0,true,true,new[]{new Share("C","Debilitate",1)},1.95m,2.9m)};
  var chain=new MultiplierChain("vuln",true,1.5m,steps);
  check(chain.Reconciles(2.9m)&&!chain.Reconciles(3),"nested chain validates child continuity and parent output");
  check(chain.Evaluate(new HashSet<string>{"vuln","cruel","debil"})==1.25m,"counterfactual retains relic baseline and captured eligibility");
  check(chain.Evaluate(new HashSet<string>{"cruel"})==2.5m,"Debilitate reevaluates changed child input");
  var modifier=new DamageModifier("vuln",ModifierKind.Multiply,2.9m,true,new[]{new Share("A","Vulnerable",1)}){Chain=chain};
  var r=Amplification.Calculate(10,100,0,new[]{modifier});
  check(r.Actual.Hp==29&&r.Baseline.Hp==12&&r.OrangeHp==17&&r.HpProviders.Select(p=>p.Owner).Distinct().Count()==3,"nested contributors receive separate conserved provider credit");
  var account=new Accounting();account.AddResolved(new(new object(),"Direct",29,0,new[]{new Share("A","hit",29)}));account.ApplyAmplification("A",r);
  check(account.Rows.Values.Sum(t=>t.Hp-t.SupportReceived+t.SupportGiven)==29&&account.Transfers.All(t=>t.Provider!="A"),"nested self/team support preserves Contribution");
  foreach(var hp in new[]{1m,20m,100m})foreach(var block in new[]{0m,5m,100m})
  {var capped=Amplification.Calculate(10,hp,block,new[]{modifier});check(capped.Red+capped.OrangeHp==capped.Actual.Hp&&capped.Blue+capped.OrangeBlock==capped.Actual.Block,"nested lethal/block budget reconciles");}
  var weak=new MultiplierChain("weak",true,.75m,new[]{new MultiplierStep("krane","PaperKrane",MultiplierOperation.Add,-.15m,false,true,Array.Empty<Share>(),.75m,.6m),new MultiplierStep("debil","DebilitatePower",MultiplierOperation.DoubleDeviation,0,true,true,Array.Empty<Share>(),.6m,.2m)});
  weak=weak with{Family="Weak"};
  var reduced=Amplification.Calculate(10,100,0,new[]{new DamageModifier("weak",ModifierKind.Multiply,.2m,true,Array.Empty<Share>()){Chain=weak}});
  check(weak.Reconciles(.2m)&&reduced.Actual.Hp==2&&reduced.OrangeHp==0,"Weak nested reductions never earn positive support");
  check(!new MultiplierChain("x",true,1.5m,steps.Reverse().ToArray()).Reconciles(2.9m),"out-of-order nested snapshots rejected");
  check(new MultiplierChain("x",false,1.5m,Array.Empty<MultiplierStep>()).Evaluate()==1,"ineligible root is neutral");
  foreach(var amount in new[]{-20m,0m,20m})
  {
   var step=new MultiplierStep("c","CrueltyPower",MultiplierOperation.Add,amount/100,true,true,Array.Empty<Share>(),1.5m,0);
   check(step.Apply(1.5m)==1.5m+amount/100&&(step with{Eligible=false}).Apply(1.5m)==1.5m,"Cruelty signed/zero amounts and ineligible passthrough");
  }
  check(new MultiplierChain("v",true,1.5m,Array.Empty<MultiplierStep>()).Reconciles(1.5m),"absent children preserve root multiplier");
  var json=System.Text.Json.JsonSerializer.Serialize(new{schemaVersion=6,accountingVersion=2,amplificationVersion=3,session="chain",combat=1,sequence=1,role="Host",kind="damage_trace",data=new{eventId="hit",Modifiers=new[]{modifier},calculated=r,normalized=new{Hp=29,Block=0}}});
  var report=new TraceReport();report.Read("synthetic-chain",new[]{json});
  check(report.ParseFailures.Count==0&&report.Groups.Single().Failures.Count==0&&report.Groups.Single().Modifiers.Contains("CrueltyPower"),"schema-6 analyser reads child provenance and validates chains");
  var bad=new TraceReport();bad.Read("synthetic-corrupt",new[]{json.Replace("\"Input\":1.75","\"Input\":9.75")});
  check(bad.Groups.Single().Failures.Any(f=>f.Contains("chain continuity")),"analyser rejects corrupted child continuity");
 }
}
