using Godot;
namespace Based.Spires2DamageMeter;
public static class MeterPalette
{
 public static Color Colour(TooltipTone tone)=>tone switch{TooltipTone.Hp=>new(1f,.45f,.5f),TooltipTone.Block=>new(.35f,.7f,1f),TooltipTone.Bonus=>new(1f,.65f,.3f),TooltipTone.MutedBlock=>new(.5f,.6f,.7f),_=>Colors.White};
}
public partial class MeterTooltipContent:VBoxContainer
{
 public MeterTooltipModel Model=new("",Array.Empty<string>(),""); public Vector2 AvailableSize;
 private string shape="";private float width;private Label title=null!,scope=null!,total=null!,share=null!,note=null!,warning=null!,more=null!,plain=null!,sourceHeading=null!;
 private readonly List<(Label Label,Label Value)> stats=new();
 private readonly List<(HBoxContainer Row,Label Name,Label[] Values)> sources=new();
 private readonly List<Label> textLabels=new();private readonly List<Label> headers=new();
 private Label Label(string text="",int size=0){var l=new Label{Text=text,MouseFilter=MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart};if(size>0)l.AddThemeFontSizeOverride("font_size",size);return l;}
 private void Set(Label label,string text,TooltipTone tone=TooltipTone.Neutral){if(label.Text!=text)label.Text=text;label.AddThemeColorOverride("font_color",MeterPalette.Colour(tone));}
 private HBoxContainer Row(){var row=new HBoxContainer{MouseFilter=MouseFilterEnum.Ignore};AddChild(row);return row;}
 private void Heading(string text){var label=Label(text);AddChild(label);textLabels.Add(label);}
 private void Build(TooltipCard? card)
 {
  foreach(var child in GetChildren()){RemoveChild(child);child.QueueFree();}stats.Clear();sources.Clear();headers.Clear();textLabels.Clear();
  if(card==null){plain=Label();AddChild(plain);return;}
  title=Label("",18);AddChild(title);scope=Label();AddChild(scope);
  var summary=Row();total=Label("",22);total.SizeFlagsHorizontal=SizeFlags.ExpandFill;summary.AddChild(total);share=Label();summary.AddChild(share);
  string section="";
  foreach(var stat in card.Stats){if(stat.Section!=section){AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});Heading(section=stat.Section);}var row=Row();var label=Label(stat.Label);label.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(label);var value=Label();value.HorizontalAlignment=HorizontalAlignment.Right;row.AddChild(value);stats.Add((label,value));}
  AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});sourceHeading=Label("Sources");AddChild(sourceHeading);
  var heading=Row();foreach(var text in card.Headers){var label=Label(text);heading.AddChild(label);headers.Add(label);}
  for(int i=0;i<12;i++){var row=Row();var name=Label();row.AddChild(name);var values=new List<Label>();for(int c=1;c<card.Headers.Count;c++){var label=Label();label.HorizontalAlignment=HorizontalAlignment.Right;row.AddChild(label);values.Add(label);}sources.Add((row,name,values.ToArray()));}
  more=Label();AddChild(more);AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});note=Label();AddChild(note);warning=Label();AddChild(warning);
 }
 public void Refresh()
 {
  if(!IsInsideTree())return;
  width=Math.Max(40,Math.Min(420,AvailableSize.X-32));CustomMinimumSize=new(width,0);Size=new(width,0);MouseFilter=MouseFilterEnum.Ignore;
  var card=Model.Card;var key=card==null?"plain":string.Join("|",card.Stats.Select(s=>s.Section+":"+s.Label))+"/"+string.Join("|",card.Headers);
  if(shape!=key){shape=key;Build(card);}
  var font=GetThemeDefaultFont();var fontSize=GetThemeDefaultFontSize();
  if(card==null){int limit=12;while(limit>0&&font.GetMultilineStringSize(Model.Render(limit),HorizontalAlignment.Left,width,fontSize).Y>AvailableSize.Y-48)limit--;Set(plain,Model.Render(limit));plain.CustomMinimumSize=new(width,0);return;}
  Set(title,card.Title);Set(scope,card.Scope);Set(total,card.Total.Text,card.Total.Tone);share.Visible=card.PartyShare.HasValue;Set(share,card.PartyShare.HasValue?$"{card.PartyShare:0.#}% of party":"");
  share.CustomMinimumSize=new(font.GetStringSize(share.Text,HorizontalAlignment.Left,-1,fontSize).X,0);
  for(int i=0;i<stats.Count;i++){
   Set(stats[i].Value,card.Stats[i].Number.Text,card.Stats[i].Number.Tone);
   stats[i].Value.CustomMinimumSize=new(Math.Min(width*.45f,font.GetStringSize(stats[i].Value.Text,HorizontalAlignment.Left,-1,fontSize).X),0);
  }
  Set(note,card.Note);Set(warning,card.Warning);warning.Visible=card.Warning.Length>0;
  var numberWidth=Math.Max(45,headers.Skip(1).Select(l=>font.GetStringSize(l.Text,HorizontalAlignment.Left,-1,fontSize).X+8).DefaultIfEmpty(45).Max());
  foreach(var row in card.Sources.Take(12))foreach(var value in row.Values)numberWidth=Math.Max(numberWidth,font.GetStringSize(value.Text,HorizontalAlignment.Left,-1,fontSize).X+8);
  numberWidth=Math.Min(numberWidth,width*.65f/(headers.Count-1));
  var nameWidth=Math.Max(20,width-(headers.Count-1)*(numberWidth+4));
  for(int i=0;i<headers.Count;i++){headers[i].CustomMinimumSize=new(i==0?nameWidth:numberWidth,0);headers[i].Size=new(i==0?nameWidth:numberWidth,0);headers[i].HorizontalAlignment=i==0?HorizontalAlignment.Left:HorizontalAlignment.Right;}
  float Measure(string text,float w,int size)=>font.GetMultilineStringSize(text,HorizontalAlignment.Left,w,size).Y;
  var used=Measure(card.Title,width,18)+Measure(card.Scope,width,fontSize)+font.GetHeight(22)+Measure(card.Note,width,fontSize)+(warning.Visible?Measure(card.Warning,width,fontSize):0)+(stats.Count+textLabels.Count+3)* (font.GetHeight(fontSize)+4)+60;
  int shown=0;var count=Math.Min(12,card.Sources.Count);
  for(int i=0;i<sources.Count;i++){
   var row=sources[i];row.Row.Visible=false;if(i>=count)continue;var source=card.Sources[i];var height=Math.Max(font.GetHeight(fontSize),Measure(source.Name,nameWidth,fontSize))+4;
   if(used+height>AvailableSize.Y-48)break;
   used+=height;shown++;row.Row.Visible=true;Set(row.Name,source.Name);row.Name.CustomMinimumSize=new(nameWidth,0);row.Name.Size=new(nameWidth,0);
   for(int c=0;c<row.Values.Length;c++){Set(row.Values[c],source.Values[c].Text,source.Values[c].Tone);row.Values[c].CustomMinimumSize=new(numberWidth,0);row.Values[c].Size=new(numberWidth,0);
    int fitted=fontSize;while(fitted>10&&font.GetStringSize(source.Values[c].Text,HorizontalAlignment.Left,-1,fitted).X>numberWidth)fitted--;row.Values[c].AddThemeFontSizeOverride("font_size",fitted);}
  }
  // Keep previously visible trailing rows hidden after a live update reduces capacity.
  for(int i=shown;i<sources.Count;i++)sources[i].Row.Visible=false;
  Set(more,card.Sources.Count>shown?$"+{card.Sources.Count-shown} more":card.Sources.Count==0?"No matching damage recorded":"");more.Visible=more.Text.Length>0;
  QueueSort();
 }
 public override void _Ready()=>Refresh();
}
public partial class MeterHoverButton:Button
{
 private MeterTooltipModel model=new("",Array.Empty<string>(),"");private MeterTooltipContent? popup;
 public void UpdateTooltip(MeterTooltipModel value){model=value;if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()){popup.Model=value;popup.AvailableSize=GetViewportRect().Size;popup.Refresh();}}
 public override GodotObject _MakeCustomTooltip(string forText){popup=new(){Model=model,AvailableSize=GetViewportRect().Size};return popup;}
 public void CloseTooltip(){if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()&&popup.GetWindow()!=GetWindow())popup.GetWindow().Hide();}
}
public partial class MeterHoverLabel:Label
{
 private MeterTooltipModel model=new("",Array.Empty<string>(),"");private MeterTooltipContent? popup;
 public void UpdateTooltip(MeterTooltipModel value){model=value;if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()){popup.Model=value;popup.AvailableSize=GetViewportRect().Size;popup.Refresh();}}
 public override GodotObject _MakeCustomTooltip(string forText){popup=new(){Model=model,AvailableSize=GetViewportRect().Size};return popup;}
 public void CloseTooltip(){if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()&&popup.GetWindow()!=GetWindow())popup.GetWindow().Hide();}
}
