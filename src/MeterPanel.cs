using Godot;
using MegaCrit.Sts2.Core.Runs;
namespace Based.Spires2DamageMeter;

public partial class MeterPanel : CanvasLayer
{
    private static MeterPanel? instance;
    private PanelContainer panel = null!;
    private VBoxContainer rows = null!, body = null!;
    private VBoxContainer layout = null!;
    private Button collapse = null!, grab = null!;
    private float expandedWidth=440f;
    private bool heightRefreshPending;
    private MeterHoverLabel resize = null!;
    private ScrollContainer scroll=null!;
    private HBoxContainer legend=null!; private Label empty=null!;
    private bool solo;
    private int Metric => metric.GetSelectedId();
    private OptionButton metric = null!;
    private TabBar scope = null!;
    private readonly Dictionary<string, (MeterHoverButton Button, TextureRect Icon, MeterBar Bar)> controls = new();
    private bool dragging, resizing, collapsed;
    private double elapsed;
    private readonly ConfigFile settings = new();
    private string SettingsPath => "user://" + Probe.Id + "/meter-layout.cfg";
    public static void Ensure()
    {
        if (GodotObject.IsInstanceValid(instance)) return;
        instance = new(); ((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, instance);
    }
    public override void _Ready()
    {
        Layer=100; settings.Load(SettingsPath);
        panel=new PanelContainer { Position=settings.GetValue("layout","position",new Vector2(20,160)).AsVector2(), CustomMinimumSize=new Vector2(320,0) }; AddChild(panel);
        expandedWidth=Math.Max(320,settings.GetValue("layout","width",440f).AsSingle());
        panel.Size=new Vector2(expandedWidth,0);
        layout=new VBoxContainer(); panel.AddChild(layout);
        var header=new HBoxContainer(); layout.AddChild(header);
        collapse=new Button { Text="-",TooltipText="Collapse / expand" };header.AddChild(collapse);
        metric=new OptionButton {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,FitToLongestItem=false,ClipText=true};header.AddChild(metric);
        resize=new MeterHoverLabel { Text="\u2194", TooltipText="Meter controls",HorizontalAlignment=HorizontalAlignment.Right,MouseFilter=Control.MouseFilterEnum.Stop };header.AddChild(resize);
        resize.GuiInput+=e=> {
            if(e is InputEventMouseButton b&&b.ButtonIndex==MouseButton.Left){resizing=b.Pressed;dragging=false;}
            if(e is InputEventMouseMotion m){if(resizing)panel.Size=new Vector2(Math.Max(320,panel.Size.X+m.Relative.X),panel.Size.Y);}
        };
        grab=new Button { Text="\u2630",TooltipText="Left-drag to move",FocusMode=Control.FocusModeEnum.None,MouseDefaultCursorShape=Control.CursorShape.Drag,CustomMinimumSize=new Vector2(28,0) };header.AddChild(grab);
        grab.GuiInput+=e=> {
            if(e is InputEventMouseButton {ButtonIndex:MouseButton.Left} b){dragging=b.Pressed;resizing=false;if(b.Pressed)CloseTooltips();}
            if(dragging&&e is InputEventMouseMotion m)panel.Position+=m.Relative;
        };
        body=new VBoxContainer();layout.AddChild(body);collapse.Pressed+=()=>{ApplyCollapsedState(!collapsed);Save();};
        metric.AddSeparator("Damage");
        for(var id=0;id<9;id++) {if(id==8)metric.AddSeparator("Effective");if(id==5)metric.AddSeparator("Attribution");metric.AddItem(MeterTooltipModel.Labels[id],id);metric.GetPopup().SetItemTooltip(metric.ItemCount-1,MeterTooltipModel.Description(id));if(id is 5 or 6)metric.SetItemDisabled(metric.ItemCount-1,!NativeDamageCapture.Enabled);}
        var savedMetric=Math.Clamp(settings.GetValue("layout","metric",8).AsInt32(),0,8);
        if(savedMetric==7&&settings.GetValue("layout","attributionVersion",0).AsInt32()<1)savedMetric=0;
        metric.Select(metric.GetItemIndex(savedMetric));metric.ItemSelected+=_=>Save();
        scope=new TabBar(); scope.AddTab("Current Fight"); scope.AddTab("This Run"); scope.CurrentTab=Math.Clamp(settings.GetValue("layout","scope",0).AsInt32(),0,1); var tabs=new HBoxContainer(); body.AddChild(tabs); scope.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill; tabs.AddChild(scope);
        scope.TabChanged+=_=>Save();
        var headings=new HBoxContainer();body.AddChild(headings);
        legend=new HBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};headings.AddChild(legend);
        legend.AddChild(new Label {Text="HP / Amp / %"});
        empty=new Label {Text="No damage recorded yet"};body.AddChild(empty);
        scroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        rows=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
        ApplyCollapsedState(settings.GetValue("layout","collapsed",false).AsBool());
    }
    private void ApplyCollapsedState(bool value)
    {
        CloseTooltips();
        if(value&&!collapsed)expandedWidth=panel.Size.X;
        collapsed=value;body.Visible=!value;metric.Visible=!value;resize.Visible=!value;grab.Visible=!value;
        collapse.Text=value?"+":"-";
        panel.CustomMinimumSize=new Vector2(value?0:320,0);
        panel.Size=new Vector2(value?0:expandedWidth,0);
        dragging=resizing=false;
        QueueHeightRefresh();
    }
    private void QueueHeightRefresh()
    {
        if(heightRefreshPending) return;
        heightRefreshPending=true;
        // Hidden children leave the minimum-size calculation after the container's queued layout pass.
        layout.QueueSort();
        Callable.From(() =>
        {
            heightRefreshPending=false;
            if(!GodotObject.IsInstanceValid(panel)) return;
            panel.Size=collapsed?Vector2.Zero:new Vector2(panel.Size.X,0);
        }).CallDeferred();
    }
    private void Save()
    {
        settings.SetValue("layout","position",panel.Position); settings.SetValue("layout","width",collapsed?expandedWidth:panel.Size.X); settings.SetValue("layout","collapsed",collapsed);
        settings.SetValue("layout","attributionVersion",1); settings.SetValue("layout","metric",Metric); settings.SetValue("layout","scope",scope.CurrentTab); settings.Save(SettingsPath);
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey { Pressed:true,Echo:false,Keycode:Key.F8 }) {CloseTooltips();dragging=resizing=false;panel.Visible=!panel.Visible;}
        if(e is InputEventMouseButton { Pressed:false }) { if(dragging||resizing) Save(); dragging=resizing=false; }
    }
    public override void _Process(double delta)
    {
        var viewport=GetViewport().GetVisibleRect().Size;
        var width=Math.Clamp(panel.Size.X,collapsed?0:Math.Min(320,viewport.X),viewport.X);
        if(!Mathf.IsEqualApprox(width,panel.Size.X)) panel.Size=new Vector2(width,0);
        panel.Position=new Vector2(Math.Clamp(panel.Position.X,0,Math.Max(0,viewport.X-panel.Size.X)),Math.Clamp(panel.Position.Y,0,Math.Max(0,viewport.Y-40)));
        elapsed+=delta; if(elapsed<.1) return; elapsed=0;
        var sessionType=RunManager.Instance?.NetService?.Type.ToString();
        var detectedSolo=MeterTooltipModel.SoloForSession(sessionType);
        if(detectedSolo.HasValue)solo=detectedSolo.Value;
        var actual=scope.CurrentTab==0?Probe.Meter.Rows:RunSession.RunRows();
        var effective=scope.CurrentTab==0?Probe.Effective:RunSession.EffectiveRun();
        var ledger=Metric==8?effective.Ledger():scope.CurrentTab==0?Probe.Credited:RunSession.CreditedRun();
        var transfers=scope.CurrentTab==0?Probe.Meter.Transfers:RunSession.CreditedTransfers();
        var credited=ledger.Rows(Metric==8?null:transfers);foreach(var id in actual.Keys)credited.TryAdd(id,new());
        var data=Metric==7?actual:credited;
        decimal Value(Totals t)=>Metric switch {1=>t.Hp,2=>t.Block,3=>t.Poison,4=>t.Doom,5=>t.AmplifiedHp+t.AmplifiedBlock,6=>t.SupportGiven,7=>t.Hp+t.Block,_=>t.Hp+t.Block};
        var ordered=data.OrderByDescending(p=>actual.GetValueOrDefault(p.Key)?.Hp ?? 0).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray(); var max=ordered.Select(p=>Value(p.Value)).DefaultIfEmpty().Max(); var team=data.Values.Sum(Value);
        var hovered=panel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
        foreach(var id in controls.Keys.Except(data.Keys).ToArray()) { controls[id].Button.CloseTooltip();controls[id].Button.QueueFree(); controls.Remove(id); }
        var breakdowns=Metric==7?(scope.CurrentTab==0?Probe.Meter.StructuredSources:RunSession.SourceBreakdowns()):ledger.Sources();
        var diagnostics=$"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\n{(scope.CurrentTab==1&&RunSession.AttributionPartial?"Partial credited history; earlier actuals available in Actual Damage":"")}\nAttribution: {ledger.Provisional} provisional/unknown events\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (audit pending)\n{Probe.OwnershipStatus}\nEffective: {(EffectiveCapture.Enabled?"enabled":"unavailable")}, {(effective.Incomplete?"partial coverage":"observed coverage")}";
        var badge=MeterTooltipModel.Status((Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!NativeDamageCapture.Enabled||!Probe.OwnershipAvailable||RunSession.StorageIncomplete,ledger.Provisional>0||NativeDamageCapture.Unmatched>0||(scope.CurrentTab==1&&(RunSession.AttributionPartial||RunSession.HistoryPartial)));
        resize.UpdateTooltip(new MeterTooltipModel($"Meter {Probe.Version}\nDrag arrow: resize; drag grab handle: move.\nF8: show/hide. Bars scale to the largest player total.\nRows show rounded damage and, in multiplayer, share of the selected party metric.\n{(badge.Length>0?badge+": some statistics may be missing or unavailable.":"Capture status")}\nSelected: "+MeterTooltipModel.Description(Metric),Array.Empty<string>(),diagnostics){ShowSources=false});
        var metricTip=MeterTooltipModel.Description(Metric)+(badge.Length>0?$"\n{badge}: some statistics may be missing or unavailable. Hover the resize arrow for details.":"");
        if(metric.TooltipText!=metricTip)metric.TooltipText=metricTip;
        empty.Visible=data.Count==0;
        var index=0;
        foreach(var (id,total) in ordered)
        {
            if(!controls.TryGetValue(id,out var row))
            {
                var button=new MeterHoverButton { TooltipText="Player details",FocusMode=Control.FocusModeEnum.None, CustomMinimumSize=new Vector2(0,32) }; rows.AddChild(button);
                var line=new HBoxContainer { MouseFilter=Control.MouseFilterEnum.Ignore }; button.AddChild(line); line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var icon=new TextureRect { CustomMinimumSize=new Vector2(20,20),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(icon);
                var bar=new MeterBar { CustomMinimumSize=new Vector2(70,24),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(bar);
                controls[id]=row=(button,icon,bar);
            }
            if(!hovered) rows.MoveChild(row.Button,index); index++;
            row.Icon.Texture=Probe.CharacterIcons.GetValueOrDefault(id);
            row.Icon.Visible=!solo&&row.Icon.Texture!=null;
            (Color Hp,Color Amp) colors=Probe.CharacterColors.GetValueOrDefault(id,(Colors.Gray,Colors.Gray));
            row.Bar.HpColour=colors.Hp;row.Bar.AmpColour=colors.Amp;
            var value=Value(total);
            row.Bar.PlayerName=Probe.Names.GetValueOrDefault(id,id);
            row.Bar.ShowPlayerName=!solo||id==StatusOwnershipLedger.Unknown;
            row.Bar.DamageText=Math.Round(value,MidpointRounding.AwayFromZero).ToString("0",System.Globalization.CultureInfo.CurrentCulture);
            row.Bar.ShareText=team>0?$"{100*value/team:0.#}%":"0%";
            row.Bar.ShowDetails=!solo;
            row.Bar.Orange=Metric switch{0 or 8=>total.AmplifiedHp+total.AmplifiedBlock,1=>total.AmplifiedHp,2=>total.AmplifiedBlock,5=>value,6=>value,7=>total.AmplifiedHp+total.AmplifiedBlock,3=>ledger.Credits.Where(c=>c.Player==id&&c.Kind=="Poison"&&c.Amplification).Sum(c=>c.Hp),_=>0};
            row.Bar.Block=Metric switch{0 or 8=>Math.Max(0,total.Block-total.AmplifiedBlock),2=>Math.Max(0,total.Block-total.AmplifiedBlock),7=>Math.Max(0,total.Block-total.AmplifiedBlock),_=>0};
            row.Bar.Hp=Math.Max(0,value-row.Bar.Block-row.Bar.Orange); row.Bar.Maximum=max; row.Bar.QueueRedraw();
            var affected=(Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!Probe.OwnershipAvailable||(Metric!=7&&(!NativeDamageCapture.Enabled||ledger.Provisional>0||NativeDamageCapture.Unmatched>0))||(scope.CurrentTab==1&&(RunSession.StorageIncomplete||RunSession.HistoryPartial||(Metric!=7&&RunSession.AttributionPartial)));
            var card=MeterTooltipModel.BuildCard(id,row.Bar.PlayerName,Metric,scope.CurrentTab==1,solo,value,team,total,effective.PendingFor(id),row.Bar.Orange,breakdowns,transfers,p=>Probe.Names.GetValueOrDefault(p,p),affected?"Data may be incomplete. Hover the resize arrow for details.":"");
            row.Button.UpdateTooltip(new("",Array.Empty<string>(),""){Card=card});

        }
        var overhead=panel.GetCombinedMinimumSize().Y-scroll.CustomMinimumSize.Y;
        var desired=Math.Min(Math.Max(0,viewport.Y-panel.Position.Y-overhead-8),Math.Max(0,controls.Count*36));
        scroll.CustomMinimumSize=new Vector2(0,desired);
        QueueHeightRefresh();
    }
    private void CloseTooltips(){if(resize!=null)resize.CloseTooltip();foreach(var row in controls.Values)row.Button.CloseTooltip();}
    public override void _ExitTree() { CloseTooltips();Save(); Probe.Flush(); instance=null; }
}

public partial class MeterBar : Control
{
    public decimal Hp,Block,Orange,Maximum;
    public Color HpColour=Colors.Gray,AmpColour=Colors.Gray;
    public string PlayerName="",DamageText="",ShareText="";
    public bool ShowPlayerName,ShowDetails;
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color(.12f,.12f,.15f));
        if(Maximum>0)
        {
            var hp=Size.X*(float)((Hp+Block)/Maximum); var amp=Size.X*(float)(Orange/Maximum);
            DrawRect(new Rect2(0,3,hp,Math.Max(0,Size.Y-6)),HpColour);
            DrawRect(new Rect2(hp,3,amp,Math.Max(0,Size.Y-6)),AmpColour);
        }
        DrawRowText();
    }
    private void DrawRowText()
    {
        if(Size.X<=0)return;
        var font=GetThemeDefaultFont();var fontSize=Math.Clamp(GetThemeDefaultFontSize(),10,16);
        var summary=ShowDetails?$"{DamageText} | {ShareText}":DamageText;
        var summarySize=font.GetStringSize(summary,HorizontalAlignment.Left,-1,fontSize);
        var name=ShowPlayerName?PlayerName:"";
        var nameWidth=Math.Max(0,Size.X-summarySize.X-20);
        while(name.Length>0&&font.GetStringSize(name,HorizontalAlignment.Left,-1,fontSize).X>nameWidth)
            name=name.Length>1?name[..^2]+"…":"";
        var y=(Size.Y-font.GetHeight(fontSize))/2+font.GetAscent(fontSize);
        if(name.Length>0)DrawOutlined(font,new Vector2(4,y),name,fontSize);
        if(summary.Length>0&&summarySize.X+8<=Size.X)
            DrawOutlined(font,new Vector2(Size.X-summarySize.X-4,y),summary,fontSize);
    }
    private void DrawOutlined(Font font,Vector2 position,string text,int fontSize)
    {
        DrawStringOutline(font,position,text,HorizontalAlignment.Left,-1,fontSize,3,Colors.Black);
        DrawString(font,position,text,HorizontalAlignment.Left,-1,fontSize,Colors.White);
    }
}
