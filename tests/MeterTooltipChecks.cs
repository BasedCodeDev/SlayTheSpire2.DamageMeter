using Based.Spires2DamageMeter;

static class MeterTooltipChecks
{
 public static void Run(Action<bool,string> check)
 {
  check(MeterTooltipModel.Status(true,true)=="Incomplete","tooltip status gives incomplete precedence");
  check(MeterTooltipModel.Status(false,true)=="Partial"&&MeterTooltipModel.Status(false,false)=="","tooltip status hides healthy badge");
  var model=new MeterTooltipModel("A: 0 (0%)",Enumerable.Range(1,15).Select(i=>$"Source {i}").ToArray(),"INCOMPLETE: capture failure");
  var text=model.Render();
  check(text.Contains("Source 12\n+3 more")&&!text.Contains("Source 13"),"tooltip bounds source list at twelve entries");
  check(model.Render(2).Contains("Source 2\n+13 more")&&model.Render(0).Contains("+15 more"),"tooltip adapts entries to available height");
  check(model.Render(0).EndsWith("INCOMPLETE: capture failure"),"tooltip trimming retains diagnostics");
  check(new MeterTooltipModel("A: 0 (0%)",Array.Empty<string>(),"Ready").Render().Contains("No matching damage recorded"),"tooltip handles zero damage");
  check(!(model with{ShowSources=false}).Render().Contains("Source"),"header diagnostics omit source section");
  var sources=new[]{
   new SourceTotals{Player="A",Source=new("Direct","strike","Strike"),Hp=6,Block=2},
   new SourceTotals{Player="A",Source=new("Poison","poison","Poison"),Hp=3,AmplifiedHp=1},
   new SourceTotals{Player="A",Source=new("Doom","doom","Doom"),Hp=4},
   new SourceTotals{Player="B",Source=new("Direct","strike","Strike"),Hp=100}};
  IReadOnlyList<string> Entries(int metric)=>MeterTooltipModel.Sources("A",metric,sources,Array.Empty<SupportTransfer>(),s=>s);
  check(Entries(0).Count==3&&Entries(0)[0]=="Strike: 8","tooltip filters player and sorts damage sources");
  check(Entries(1)[0]=="Strike: 6"&&Entries(2).SequenceEqual(new[]{"Strike: 2"}),"tooltip separates HP and block sources");
  check(Entries(3).SequenceEqual(new[]{"Poison: 3"})&&Entries(4).SequenceEqual(new[]{"Doom: 4"}),"tooltip filters poison and Doom sources");
  check(Entries(5).SequenceEqual(new[]{"Poison: 1"}),"tooltip amplification excludes base damage");
  var transfers=new[]{new SupportTransfer("A","B","Vulnerable",2,0),new SupportTransfer("A","B","Vulnerable",0,1),new SupportTransfer("B","A","Vulnerable",9,0)};
  var support=MeterTooltipModel.Sources("A",6,sources,transfers,id=>id=="B"?"Teammate":id);
  check(support.SequenceEqual(new[]{"To Teammate (Vulnerable): HP 2, block 1"}),"tooltip groups outgoing support by recipient and source");
 }
}
