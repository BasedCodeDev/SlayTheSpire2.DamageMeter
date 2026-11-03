namespace Based.Spires2DamageMeter;
public sealed record MeterTooltipModel(string Summary,IReadOnlyList<string> Entries,string Diagnostics)
{
 public TooltipCard? Card {get;init;}
 public static TooltipCard BuildCard(string player,string name,int metric,bool run,bool solo,decimal value,decimal team,Totals t,decimal pending,decimal poisonBonus,IEnumerable<SourceTotals> sources,IEnumerable<SupportTransfer> transfers,Func<string,string> names,string warning)
 {
  var tone=metric==2?TooltipTone.Block:metric is 5 or 6?TooltipTone.Bonus:metric is 1 or 3 or 4?TooltipTone.Hp:TooltipTone.Neutral;
  var stats=new List<TooltipStat>();
  void Stat(string label,decimal n,TooltipTone colour,string section="Breakdown")=>stats.Add(new(label,new(n,colour),section));
  if(metric is 0 or 7 or 8){Stat("HP damage",t.Hp,TooltipTone.Hp);Stat(metric==8?"Qualified block":"Block damage",t.Block,TooltipTone.Block);}
  if(metric==5){Stat("Bonus HP",t.AmplifiedHp,TooltipTone.Bonus);Stat("Bonus block",t.AmplifiedBlock,TooltipTone.Bonus);Stat("Self bonus",Math.Max(0,t.AmplifiedHp+t.AmplifiedBlock-t.SupportGiven),TooltipTone.Bonus,"Already included");Stat("Teammate bonus",t.SupportGiven,TooltipTone.Bonus,"Already included");}
  else if(metric==6){Stat("Bonus HP",t.SupportGivenHp,TooltipTone.Bonus);Stat("Bonus block",t.SupportGivenBlock,TooltipTone.Bonus);}
  else{
   if(metric is 0 or 1 or 7 or 8)Stat("Bonus HP",t.AmplifiedHp,TooltipTone.Bonus,"Already included");
   if(metric is 0 or 2 or 7 or 8)Stat("Bonus block",t.AmplifiedBlock,TooltipTone.Bonus,"Already included");
   if(metric==3)Stat("Bonus HP",poisonBonus,TooltipTone.Bonus,"Already included");
  }
  if(metric==8)Stat("Pending block - excluded",pending,TooltipTone.MutedBlock,"Not counted");
  var combined=metric is 0 or 7 or 8;var support=metric==6;
  var headers=support?new[]{"Recipient / Source","Bonus HP","Bonus Block","Total"}:combined?new[]{"Source","HP","Block","Total"}:new[]{"Source","Value"};
  var rows=new List<TooltipSource>();
  if(support)foreach(var g in transfers.Where(t=>t.Provider==player).GroupBy(t=>(t.Recipient,t.Source))){var hp=g.Sum(t=>t.Hp);var block=g.Sum(t=>t.Block);rows.Add(new($"{names(g.Key.Recipient)} / {g.Key.Source}",new[]{new TooltipNumber(hp,TooltipTone.Bonus),new(block,TooltipTone.Bonus),new(hp+block,TooltipTone.Bonus)}));}
  else foreach(var g in sources.Where(s=>s.Player==player).GroupBy(s=>s.Source)){
   var hp=g.Sum(s=>s.Hp);var block=g.Sum(s=>s.Block);var amount=metric switch{1=>hp,2=>block,3=>g.Key.Kind=="Poison"?hp:0,4=>g.Key.Kind=="Doom"?hp:0,5=>g.Sum(s=>s.AmplifiedHp+s.AmplifiedBlock),_=>hp+block};
   if(amount<=0)continue;
   rows.Add(new(g.Key.DisplayName,combined?new[]{new TooltipNumber(hp,TooltipTone.Hp),new(block,TooltipTone.Block),new(amount,TooltipTone.Neutral)}:new[]{new TooltipNumber(amount,tone)}));
  }
  rows=rows.OrderByDescending(r=>r.Values.Last().Value).ThenBy(r=>r.Name,StringComparer.Ordinal).ToList();
  return new((!solo||player==StatusOwnershipLedger.Unknown?name+" - ":"")+Labels[metric],run?"This Run":"Current Fight",new(value,tone),solo?null:team>0?100*value/team:0,stats,headers,rows,Description(metric),warning);
 }
 public static bool? SoloForSession(string? session)=>session switch {"Singleplayer"=>true,"Host" or "Client" or "FakeMultiplayer"=>false,_=>null};
 public static string RowSummary(string player,int metric,bool run,decimal value,decimal team,bool solo,bool unattributed=false)
 {
  var identity=!solo||unattributed?player+" - ":"";
  var share=solo?"":$" | Party share: {(team>0?100*value/team:0):0.#}%";
  return $"{identity}{Labels[metric]} ({(run?"This Run":"Current Fight")})\nTotal: {value:0.##}{share}\n";
 }
 public bool ShowSources { get; init; }=true;
 public static readonly string[] Labels={"Damage (HP + Block)","HP Only","Block Only","Poison HP","Doom HP","Amplification","Support Damage","Actual Damage","Effective Damage"};
 public static string Description(int metric)=>metric switch{
  8=>"HP plus block from shields breached by a later block-respecting HP hit. Pending block is excluded.",
  5=>"Bonuses provided to yourself and teammates; already included in Damage.",
  6=>"Bonuses provided to teammates only; already included in Damage.",
  7=>"Observed ownership before bonuses are reassigned to providers.",
  3=>"Actual poison HP loss, with bonuses credited to their providers.",
  4=>"HP removed by Doom executions only; applying Doom earns no damage.",
  _=>"Base damage belongs to its owner; effective bonuses belong to their providers."};
 public static string Detail(int metric,Totals t)=>metric switch{
  1=>$"Bonus HP included: {t.AmplifiedHp:0.##}",
  2=>$"Bonus block included: {t.AmplifiedBlock:0.##}",
  3 or 4=>Description(metric),
  5=>$"Self bonus: {Math.Max(0,t.AmplifiedHp+t.AmplifiedBlock-t.SupportGiven):0.##}; teammate bonus: {t.SupportGiven:0.##}\nBonus HP: {t.AmplifiedHp:0.##}; block: {t.AmplifiedBlock:0.##}\nAlready included in Damage.",
  6=>$"Bonus HP: {t.SupportGivenHp:0.##}; block: {t.SupportGivenBlock:0.##}\nAlready included in Damage. Sources list recipients.",
  _=>$"HP: {t.Hp:0.##}; block: {t.Block:0.##}\nBonus included: HP {t.AmplifiedHp:0.##}; block {t.AmplifiedBlock:0.##}"};
 public string Render(int limit=12)
 {
  limit=Math.Clamp(limit,0,12);var shown=Entries.Take(limit).ToArray();var more=Entries.Count-shown.Length;
  var entries=shown.Length==0?(more>0?"":"No matching damage recorded"):string.Join("\n",shown);
  if(more>0)entries+=(entries.Length>0?"\n":"")+$"+{more} more";
  return Summary+(ShowSources?"\n\n"+entries:"")+(Diagnostics.Length>0?"\n\n"+Diagnostics:"");
 }
 public static string Status(bool incomplete,bool partial)=>incomplete?"Incomplete":partial?"Partial":"";
 public static IReadOnlyList<string> Sources(string player,int metric,IEnumerable<SourceTotals> sources,IEnumerable<SupportTransfer> transfers,Func<string,string> name)
 {
  if(metric==6)return transfers.Where(t=>t.Provider==player).GroupBy(t=>(t.Recipient,t.Source)).OrderByDescending(g=>g.Sum(t=>t.Hp+t.Block)).Select(g=>$"To {name(g.Key.Recipient)} ({g.Key.Source}): HP {g.Sum(t=>t.Hp):0.##}, block {g.Sum(t=>t.Block):0.##}").ToArray();
  return sources.Where(s=>s.Player==player).GroupBy(s=>s.Source).Select(g=>new{Name=g.Key.DisplayName,Value=g.Sum(t=>metric switch{1=>t.Hp,2=>t.Block,3=>t.Source.Kind=="Poison"?t.Hp:0,4=>t.Source.Kind=="Doom"?t.Hp:0,5=>t.AmplifiedHp+t.AmplifiedBlock,_=>t.Hp+t.Block})}).Where(s=>s.Value>0).OrderByDescending(s=>s.Value).Select(s=>$"{s.Name}: {s.Value:0.##}").ToArray();
 }
}

public enum TooltipTone {Neutral,Hp,Block,Bonus,MutedBlock}
public sealed record TooltipNumber(decimal Value,TooltipTone Tone){public string Text=>Value.ToString("0.##");}
public sealed record TooltipStat(string Label,TooltipNumber Number,string Section);
public sealed record TooltipSource(string Name,IReadOnlyList<TooltipNumber> Values);
public sealed record TooltipCard(string Title,string Scope,TooltipNumber Total,decimal? PartyShare,IReadOnlyList<TooltipStat> Stats,IReadOnlyList<string> Headers,IReadOnlyList<TooltipSource> Sources,string Note,string Warning);
