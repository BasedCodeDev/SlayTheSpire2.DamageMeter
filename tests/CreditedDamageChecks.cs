using Based.Spires2DamageMeter;
using System.Text.Json;
static class CreditedDamageChecks
{
 public static void Run(Action<bool,string> check)
 {
  var source=new SourceIdentity("card","Strike","Strike");
  DamageModifier Vuln(params Share[] providers)=>new("vulnerable",ModifierKind.Multiply,1.5m,true,providers);
  var providers=new[]{new Share("A","Bash",1)};
  CreditedDamage Hit(decimal hp,decimal block,DamageModifier modifier,string dealer="B",string kind="Direct")
  {var r=Amplification.Calculate(6,hp,block,new[]{modifier});var l=new CreditedDamage();l.Add("hit",kind,r.Actual.Hp,r.Actual.Block,new[]{new Share(dealer,"Strike",r.Actual.Hp)},source,r);return l;}
  var basic=Hit(100,0,Vuln(providers));var rows=basic.Rows();
  check(rows["B"].Hp==6&&rows["A"].Hp==3&&rows.Values.Sum(t=>t.Hp)==9,"6 to 9 splits attacker 6 and provider 3");
  check(basic.Sources().Single(s=>s.Player=="A").Source.DisplayName.Contains("Bash")&&basic.Credits.All(c=>c.Trigger==source),"provider source distinct from triggering attack");
  var shared=Hit(100,0,Vuln(new Share("A","Bash",2),new Share("C","Beam",1))).Rows();check(Math.Abs(shared["A"].Hp-2)<.000000000000000000000001m&&Math.Abs(shared["C"].Hp-1)<.000000000000000000000001m,"shared active stacks split amplification 2 to 1");
  var self=Hit(100,0,Vuln(new Share("B","Bash",1))).Rows();check(self.Count==1&&self["B"].Hp==9&&self["B"].AmplifiedHp==3,"self provider retains all damage and orange subset");
  var lethal=Hit(4,0,Vuln(providers)).Rows();check(lethal["B"].Hp==4&&!lethal.ContainsKey("A"),"already lethal base earns no unnecessary provider credit");
  var blocked=Hit(100,20,Vuln(providers)).Rows();check(blocked["B"].Block==6&&blocked["A"].Block==3,"fully blocked bonus belongs to provider");
  var mixed=Hit(100,7,Vuln(providers)).Rows();check(mixed["B"].Block==6&&mixed["A"].Block==1&&mixed["A"].Hp==2,"mixed destinations preserve effective block and HP bonus");
  var poison=new CreditedDamage();poison.Add("p","Poison",9,0,new[]{new Share("A","poison",6),new Share("B","poison",3)},null,new(new(9,0),new(6,0),new[]{new Share("C","buff",3)},Array.Empty<Share>()));
  check(poison.Rows()["C"].Poison==3&&Math.Abs(poison.Rows()["A"].Poison-4)<.000000000000000000000001m&&Math.Abs(poison.Rows()["B"].Poison-2)<.000000000000000000000001m,"poison base owners and buff provider share consistent effect view");
  var doom=new CreditedDamage();doom.Add("d","Doom",7,0,new[]{new Share("A","Doom",7)},null,null);check(doom.Rows()["A"].Doom==7,"Doom execution ownership unchanged");
  check(basic.Add("hit","Direct",9,0,new[]{new Share("B","Strike",9)},source,null).Count==0&&basic.Rows()["B"].Hp==6,"credited event identity deduplicates without double credit");
  var support=basic.Rows(new[]{new SupportTransfer("A","B","Bash",3,0)});check(support["A"].Hp==3&&support["A"].SupportGiven==3&&support.Values.Sum(t=>t.Hp)==9,"Support is subset and not added to credited damage again");
  var unknown=Hit(100,0,Vuln(new Share(StatusOwnershipLedger.Unknown,"missing",1)));check(unknown.Rows()[StatusOwnershipLedger.Unknown].Hp==3&&unknown.Provisional==1,"unknown amplification credited explicitly");
  var rawFallback=new CreditedDamage();rawFallback.Add("f","Direct",9,0,new[]{new Share("B","Strike",9)},source,null,false);check(rawFallback.Rows()["B"].Hp==9&&rawFallback.Provisional==1,"unmeasured event preserves actual ownership provisionally");
  var round=JsonSerializer.Deserialize<AttributionSnapshot>(JsonSerializer.Serialize(basic.Snapshot()))!;var restored=new CreditedDamage();restored.Merge(round);check(restored.Rows()["A"].Hp==3&&restored.Rows()["B"].Hp==6,"credited checkpoint payload roundtrip");
  var dir=Path.Combine(AppContext.BaseDirectory,"credit-check-"+Guid.NewGuid().ToString("N"));
  try
  {
   var store=new RunStatisticsStore(dir);var run=new RunIdentity("p","m","s","seed","A,B");
   store.Completed["fight"]=new("fight",new(){["B"]=new(){Hp=9}},new()){Attribution=basic.Snapshot()};store.Save(run,"hash");store.Restore(run,"hash");
   check(store.Completed["fight"].Attribution!.Credits.Sum(c=>c.Hp)==9,"schema-5 checkpoint restores credited history");
   var file=Directory.GetFiles(dir,"*.json",SearchOption.AllDirectories).Single();var legacy=File.ReadAllText(file).Replace("\"Schema\":7","\"Schema\":4");
   using(var doc=JsonDocument.Parse(legacy))
   {var old=new RunCheckpoint(4,run,"hash",new(){new("fight",new(){["B"]=new(){Hp=9}},new())}){AccountingVersion=2,AmplificationVersion=3};File.WriteAllText(file,JsonSerializer.Serialize(old));}
   store.Restore(run,"hash");check(store.Completed["fight"].Players["B"].Hp==9&&store.Completed["fight"].Attribution==null,"legacy actual history retained without inventing credited history");
  }finally{Directory.Delete(dir,true);}
  var json=JsonSerializer.Serialize(new{schemaVersion=7,accountingVersion=2,amplificationVersion=3,attributionVersion=1,session="s",role="Host",combat=1,sequence=1,kind="credited_damage",data=new{eventId="hit",hp=9,block=0,credits=basic.Credits}});
  var report=new TraceReport();report.Read("synthetic",new[]{json});check(report.ParseFailures.Count==0&&report.Groups.Single().Failures.Count==0,"analyser reconciles credited event");
  var broken=new TraceReport();broken.Read("corrupt",new[]{json.Replace("\"hp\":9","\"hp\":10")});check(broken.Groups.Single().Failures.Count==1,"analyser detects missing credited amount");
  string Line(string kind,object data)=>JsonSerializer.Serialize(new{schemaVersion=7,accountingVersion=2,amplificationVersion=3,attributionVersion=1,session="fight",role="Host",combat=1,sequence=1,kind,data});
  var complete=new[]{Line("damage",new{eventId="hit",hp=9,rawUnblocked=9,blocked=0,enemy=true}),Line("damage_allocation",new{eventId="hit",hp=9,shares=new[]{new Share("B","hit",9)}}),Line("damage_binding",new{eventId="hit",status="bound"}),Line("damage_trace_missing",new{eventId="hit"}),Line("credited_damage",new{eventId="hit",hp=9,block=0,credits=basic.Credits}),Line("combat_summary",new{totals=new{B=new{Hp=9,Block=0,SupportGiven=0,SupportReceived=0}},creditedTotals=basic.Rows()})};
  var completed=new TraceReport();completed.Read("complete",complete);check(completed.ParseFailures.Count==0&&completed.Groups.Single().Failures.Count==0,"analyser reconciles complete raw and credited fight summaries");
  var omitted=new TraceReport();omitted.Read("missing-credit",complete.Where(line=>!line.Contains("\"kind\":\"credited_damage\"")));check(omitted.Groups.Single().Failures.Count>0,"schema-7 complete fight cannot silently omit all credit events");
 }
}
