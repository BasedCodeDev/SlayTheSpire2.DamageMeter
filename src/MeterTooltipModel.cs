namespace Based.Spires2DamageMeter;
public sealed record MeterTooltipModel(string Summary,IReadOnlyList<string> Entries,string Diagnostics)
{
 public bool ShowSources { get; init; }=true;
 public string Render(int limit=12)
 {
  limit=Math.Clamp(limit,0,12);var shown=Entries.Take(limit).ToArray();var more=Entries.Count-shown.Length;
  var entries=shown.Length==0?(more>0?"":"No matching damage recorded"):string.Join("\n",shown);
  if(more>0)entries+=(entries.Length>0?"\n":"")+$"+{more} more";
  return Summary+(ShowSources?"\n\n"+entries:"")+"\n\n"+Diagnostics;
 }
 public static string Status(bool incomplete,bool partial)=>incomplete?"Incomplete":partial?"Partial":"";
 public static IReadOnlyList<string> Sources(string player,int metric,IEnumerable<SourceTotals> sources,IEnumerable<SupportTransfer> transfers,Func<string,string> name)
 {
  if(metric==6)return transfers.Where(t=>t.Provider==player).GroupBy(t=>(t.Recipient,t.Source)).OrderByDescending(g=>g.Sum(t=>t.Hp+t.Block)).Select(g=>$"To {name(g.Key.Recipient)} ({g.Key.Source}): HP {g.Sum(t=>t.Hp):0.##}, block {g.Sum(t=>t.Block):0.##}").ToArray();
  return sources.Where(s=>s.Player==player).GroupBy(s=>s.Source).Select(g=>new{Name=g.Key.DisplayName,Value=g.Sum(t=>metric switch{1=>t.Hp,2=>t.Block,3=>t.Source.Kind=="Poison"?t.Hp:0,4=>t.Source.Kind=="Doom"?t.Hp:0,5=>t.AmplifiedHp+t.AmplifiedBlock,_=>t.Hp+t.Block})}).Where(s=>s.Value>0).OrderByDescending(s=>s.Value).Select(s=>$"{s.Name}: {s.Value:0.##}").ToArray();
 }
}
