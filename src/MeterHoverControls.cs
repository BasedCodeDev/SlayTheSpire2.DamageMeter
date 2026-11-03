using Godot;
namespace Based.Spires2DamageMeter;
public static class MeterPalette
{
 public static Color Colour(TooltipTone tone)=>tone switch{TooltipTone.Hp=>new(1f,.45f,.5f),TooltipTone.Block=>new(.35f,.7f,1f),TooltipTone.Bonus=>new(1f,.65f,.3f),TooltipTone.MutedBlock=>new(.5f,.6f,.7f),_=>Colors.White};
}
public partial class MeterTooltipContent:VBoxContainer
{
 public MeterTooltipModel Model=new("",Array.Empty<string>(),""); public Vector2 AvailableSize;
 private ScrollContainer scroll=null!;private VBoxContainer content=null!;
 private string shape="";private float width;private Label title=null!,scope=null!,total=null!,share=null!,note=null!,warning=null!,more=null!,plain=null!,sourceHeading=null!;
 private readonly List<(Label Label,Label Value)> stats=new();
 private readonly List<(HBoxContainer Row,Label Name,Label[] Values)> sources=new();
 private readonly List<Label> textLabels=new();private readonly List<Label> headers=new();
 private Label Label(string text="",int size=0){var l=new Label{Text=text,MouseFilter=MouseFilterEnum.Ignore,AutowrapMode=TextServer.AutowrapMode.WordSmart};if(size>0)l.AddThemeFontSizeOverride("font_size",size);return l;}
 private void Set(Label label,string text,TooltipTone tone=TooltipTone.Neutral){if(label.Text!=text)label.Text=text;label.AddThemeColorOverride("font_color",MeterPalette.Colour(tone));}
 private HBoxContainer Row(){var row=new HBoxContainer{MouseFilter=MouseFilterEnum.Ignore};content.AddChild(row);return row;}
 private void Heading(string text){var label=Label(text);content.AddChild(label);textLabels.Add(label);}
 public override void _Ready()
 {
  scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,VerticalScrollMode=ScrollContainer.ScrollMode.Auto,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill};
  content=new VBoxContainer{SizeFlagsHorizontal=SizeFlags.ExpandFill};
  scroll.AddChild(content);AddChild(scroll);Refresh();
 }
 private void Build(TooltipCard? card)
 {
  foreach(var child in content.GetChildren()){content.RemoveChild(child);child.QueueFree();}stats.Clear();sources.Clear();headers.Clear();textLabels.Clear();
  if(card==null){plain=Label();content.AddChild(plain);return;}
  title=Label("",18);content.AddChild(title);scope=Label();content.AddChild(scope);
  var summary=Row();total=Label("",22);total.SizeFlagsHorizontal=SizeFlags.ExpandFill;summary.AddChild(total);share=Label();summary.AddChild(share);
  string section="";
  foreach(var stat in card.Stats){if(stat.Section!=section){content.AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});Heading(section=stat.Section);}var row=Row();var label=Label(stat.Label);label.SizeFlagsHorizontal=SizeFlags.ExpandFill;row.AddChild(label);var value=Label();value.HorizontalAlignment=HorizontalAlignment.Right;row.AddChild(value);stats.Add((label,value));}
  content.AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});sourceHeading=Label("Sources");content.AddChild(sourceHeading);
  var heading=Row();foreach(var text in card.Headers){var label=Label(text);heading.AddChild(label);headers.Add(label);}
  for(int i=0;i<card.Sources.Count;i++){var row=Row();var name=Label();row.AddChild(name);var values=new List<Label>();for(int c=1;c<card.Headers.Count;c++){var label=Label();label.HorizontalAlignment=HorizontalAlignment.Right;row.AddChild(label);values.Add(label);}sources.Add((row,name,values.ToArray()));}
  more=Label();content.AddChild(more);content.AddChild(new HSeparator{MouseFilter=MouseFilterEnum.Ignore});note=Label();content.AddChild(note);warning=Label();content.AddChild(warning);
 }
 public void Refresh()
 {
  if(!IsInsideTree())return;
  width=Math.Max(120,Math.Min(600,AvailableSize.X-16));var height=Math.Max(120,Math.Min(700,AvailableSize.Y-16));CustomMinimumSize=new(width+16,height+16);Size=new(width+16,height+16);scroll.CustomMinimumSize=new(width,height);content.CustomMinimumSize=new(width,0);MouseFilter=MouseFilterEnum.Ignore;
  var card=Model.Card;var key=card==null?"plain":string.Join("|",card.Stats.Select(s=>s.Section+":"+s.Label))+"/"+string.Join("|",card.Headers)+"/"+card.Sources.Count;
  if(shape!=key){shape=key;Build(card);}
  var font=GetThemeDefaultFont();var fontSize=GetThemeDefaultFontSize();
  if(card==null){Set(plain,Model.Render());plain.CustomMinimumSize=new(width,0);return;}
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
  for(int i=0;i<sources.Count;i++){
   var row=sources[i];var source=card.Sources[i];row.Row.Visible=true;Set(row.Name,source.Name);row.Name.CustomMinimumSize=new(nameWidth,0);row.Name.Size=new(nameWidth,0);
   for(int c=0;c<row.Values.Length;c++){Set(row.Values[c],source.Values[c].Text,source.Values[c].Tone);row.Values[c].CustomMinimumSize=new(numberWidth,0);row.Values[c].Size=new(numberWidth,0);
    int fitted=fontSize;while(fitted>10&&font.GetStringSize(source.Values[c].Text,HorizontalAlignment.Left,-1,fitted).X>numberWidth)fitted--;row.Values[c].AddThemeFontSizeOverride("font_size",fitted);}
  }
  Set(more,card.Sources.Count==0?"No matching damage recorded":"");more.Visible=more.Text.Length>0;
  content.QueueSort();
 }
}
public partial class MeterHoverButton:Button
{
 private MeterTooltipModel model=new("",Array.Empty<string>(),"");private Vector2 availableSize=new(420,480);private MeterTooltipContent? popup;
 public void UpdateTooltip(MeterTooltipModel value,Vector2 size){model=value;availableSize=size;if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()){popup.Model=value;popup.AvailableSize=size;popup.Refresh();}}
 public override GodotObject _MakeCustomTooltip(string forText){popup=new(){Model=model,AvailableSize=availableSize};return popup;}
 public void CloseTooltip(){if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()&&popup.GetWindow()!=GetWindow())popup.GetWindow().Hide();}
}
public partial class MeterHoverLabel:Label
{
 private MeterTooltipModel model=new("",Array.Empty<string>(),"");private Vector2 availableSize=new(420,480);private MeterTooltipContent? popup;
 public void UpdateTooltip(MeterTooltipModel value,Vector2 size){model=value;availableSize=size;if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()){popup.Model=value;popup.AvailableSize=size;popup.Refresh();}}
 public override GodotObject _MakeCustomTooltip(string forText){popup=new(){Model=model,AvailableSize=availableSize};return popup;}
 public void CloseTooltip(){if(GodotObject.IsInstanceValid(popup)&&popup!.IsInsideTree()&&popup.GetWindow()!=GetWindow())popup.GetWindow().Hide();}
}
