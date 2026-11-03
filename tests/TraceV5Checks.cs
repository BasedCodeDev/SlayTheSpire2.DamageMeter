using Based.Spires2DamageMeter;
using System.Text.Json;
using System.Text.Json.Nodes;
static class TraceV5Checks
{
 public static void Run(Action<bool,string> check)
 {
  var mods=new[]{new DamageModifier("a",ModifierKind.Add,2,true,new[]{new Share("A","a",1)}),new DamageModifier("b",ModifierKind.Add,2,true,new[]{new Share("B","b",1)}),new DamageModifier("cap",ModifierKind.Cap,2,true,Array.Empty<Share>())};
  var r=Amplification.Calculate(1,100,0,mods);
  var json=JsonSerializer.Serialize(new{schemaVersion=5,accountingVersion=2,amplificationVersion=2,session="test",combat=1,sequence=1,role="Host",kind="damage_trace",data=new{eventId="event",Modifiers=mods,calculated=r,normalized=new{Hp=2,Block=0}}});
  var good=new TraceReport();good.Read("good",new[]{json});check(good.ParseFailures.Count==0&&good.Groups.Single().Failures.Count==0,"schema-5 provider diagnostics reconcile");
  var node=JsonNode.Parse(json)!;node["data"]!["calculated"]!["HpProviders"]![0]!["Amount"]=100;
  var bad=new TraceReport();bad.Read("bad",new[]{node.ToJsonString()});check(bad.Groups.Single().Failures.Any(f=>f.Contains("provider allocations")),"schema-5 analyser detects corrupted provider allocation");
 }
}
