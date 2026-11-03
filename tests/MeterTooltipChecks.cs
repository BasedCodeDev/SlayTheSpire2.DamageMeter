using Based.Spires2DamageMeter;

static class MeterTooltipChecks
{
 public static void Run(Action<bool,string> check)
 {
  check(MeterTooltipModel.SoloForSession("Singleplayer")==true,"native single-player session selects solo presentation");
  check(MeterTooltipModel.SoloForSession("Host")==false&&MeterTooltipModel.SoloForSession("Client")==false,"multiplayer presentation does not depend on player count");
  check(MeterTooltipModel.SoloForSession(null)==null&&MeterTooltipModel.SoloForSession("unknown")==null,"unknown session preserves previous presentation");
  var solo=MeterTooltipModel.RowSummary("Character",8,false,9,9,true);
  check(!solo.Contains("Character")&&!solo.Contains("%")&&!solo.Contains("Party")&&solo.Contains("Total: 9"),"solo summary hides identity and percentages but retains total");
  var multiplayer=MeterTooltipModel.RowSummary("Player",8,true,3,9,false);
  check(multiplayer.Contains("Player")&&multiplayer.Contains("Party share:")&&multiplayer.Contains("%")&&multiplayer.Contains("This Run"),"multiplayer summary keeps identity scope and share");
  check(MeterTooltipModel.RowSummary("Unattributed",0,false,0,0,true,true).Contains("Unattributed"),"solo unknown ownership remains identifiable");
  var detail=new Totals{Hp=9,Block=2,AmplifiedHp=3,AmplifiedBlock=1,SupportGiven=3,SupportGivenHp=2,SupportGivenBlock=1,Poison=8,Doom=4};
  check(MeterTooltipModel.Labels.Length==9&&MeterTooltipModel.Labels[7]=="Actual Damage","grouped metrics retain legacy IDs");
  check(MeterTooltipModel.Detail(5,detail).Contains("Self bonus: 1; teammate bonus: 3"),"amplification details distinguish self and team credit");
  check(MeterTooltipModel.Detail(6,detail).Contains("Bonus HP: 2; block: 1"),"support tooltip uses outgoing destinations");
  check(!MeterTooltipModel.Detail(1,detail).Contains("block")&&!MeterTooltipModel.Detail(4,detail).Contains("Poison"),"focused tooltip omits unrelated metrics");
  check(MeterTooltipModel.Description(7).Contains("before bonuses")&&MeterTooltipModel.Description(6).Contains("already included"),"advanced metrics explain ownership and subset semantics");
  check(new MeterTooltipModel("Focused",new[]{"Strike: 6"},"").Render()=="Focused\n\nStrike: 6","healthy player tooltip omits diagnostic footer");
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
  TooltipCard Card(int metric,bool solo=false)=>MeterTooltipModel.BuildCard("A","Player",metric,false,solo,9,18,detail,7,1,sources,transfers,s=>s,"Coverage warning");
  var card=Card(0);
  check(card.Headers.SequenceEqual(new[]{"Source","HP","Block","Total"}),"combined tooltip has destination table columns");
  check(card.Sources.First().Values.Select(n=>n.Value).SequenceEqual(new[]{6m,2m,8m}),"source table separates HP block and conserved total");
  check(card.Sources.First().Values.Select(n=>n.Tone).SequenceEqual(new[]{TooltipTone.Hp,TooltipTone.Block,TooltipTone.Neutral}),"source destination values use semantic colours");
  check(card.Total.Tone==TooltipTone.Neutral&&card.PartyShare==50,"combined total and party share remain distinct");
  check(card.Stats.Where(r=>r.Section=="Already included").All(r=>r.Number.Tone==TooltipTone.Bonus),"bonus subset is separately labelled and orange");
  check(Card(2).Headers.Count==2&&Card(2).Sources.Single().Values.Single().Tone==TooltipTone.Block,"block view uses one blue value column");
  check(Card(3).Sources.Single().Name=="Poison"&&Card(3).Total.Tone==TooltipTone.Hp,"poison HP retains red destination semantics");
  check(Card(4).Sources.Single().Name=="Doom"&&Card(4).Total.Tone==TooltipTone.Hp,"Doom HP retains red destination semantics");
  check(Card(5).Sources.Single().Values.Single().Value==1&&Card(5).Total.Tone==TooltipTone.Bonus,"amplification table filters base damage and colours bonuses");
  check(Card(6).Sources.Single().Values.Select(n=>n.Value).SequenceEqual(new[]{2m,1m,3m})&&Card(6).Sources.Single().Values.All(n=>n.Tone==TooltipTone.Bonus),"support recipient table retains bonus destinations");
  check(Card(8).Stats.Single(s=>s.Section=="Not counted").Number==new TooltipNumber(7,TooltipTone.MutedBlock),"pending block is separated and muted");
  check(Card(0,true).PartyShare==null&&!Card(0,true).Title.Contains("Player"),"structured solo card omits identity and share");
  check(card.Warning=="Coverage warning"&&card.Note.Length>0,"card retains explanatory note and warning independently");
  var emptyCard=MeterTooltipModel.BuildCard("absent","Unknown",0,false,true,0,0,new(),0,0,sources,transfers,s=>s,"");
  check(emptyCard.Sources.Count==0&&emptyCard.Total.Value==0,"structured card supports zero values and empty sources");

 }
}
