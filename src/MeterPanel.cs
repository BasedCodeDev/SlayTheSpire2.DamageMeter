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
    private float expandedWidth=380f;
    private bool heightRefreshPending;
    private MeterHoverLabel resize = null!;
    private ScrollContainer scroll=null!;
    private Label empty=null!;
    private bool solo;
    private int Metric => metric.GetSelectedId();
    private OptionButton metric = null!;
    private Button scopeToggle = null!;
    private Button unattributedToggle = null!;
    private int scopeIndex;
    private bool showUnattributed;
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
        panel=new PanelContainer { Position=settings.GetValue("layout","position",new Vector2(20,160)).AsVector2(), CustomMinimumSize=new Vector2(280,0) }; AddChild(panel);
        panel.Theme=new Theme
        {
            DefaultFont=new SystemFont
            {
                FontNames=new[]{"ITC Avant Garde Gothic","Avant Garde","AvantGarde","DejaVu Sans","Segoe UI Light"}
            },
            DefaultFontSize=16
        };
        var panelStyle=new StyleBoxFlat
        {
            BgColor=new Color(.025f,.035f,.05f,.42f),
            BorderColor=new Color(.65f,.73f,.84f,.18f),
            ShadowColor=new Color(0,0,0,.2f)
        };
        panelStyle.SetContentMarginAll(5);
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetBorderWidthAll(1);
        panelStyle.ShadowSize=8;
        panel.AddThemeStyleboxOverride("panel",panelStyle);
        expandedWidth=Math.Max(280,settings.GetValue("layout","width",380f).AsSingle());
        panel.Size=new Vector2(expandedWidth,0);
        layout=new VBoxContainer(); layout.AddThemeConstantOverride("separation",3); panel.AddChild(layout);
        var header=new HBoxContainer(); header.AddThemeConstantOverride("separation",2); layout.AddChild(header);
        collapse=new Button { Text="-",TooltipText="Collapse / expand",CustomMinimumSize=new Vector2(24,26),FocusMode=Control.FocusModeEnum.None };header.AddChild(collapse);
        metric=new OptionButton {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,FitToLongestItem=false,ClipText=true,CustomMinimumSize=new Vector2(0,26)};header.AddChild(metric);
        resize=new MeterHoverLabel { Text="\u2194", TooltipText="Meter controls",HorizontalAlignment=HorizontalAlignment.Right,MouseFilter=Control.MouseFilterEnum.Stop,CustomMinimumSize=new Vector2(20,26) };header.AddChild(resize);
        resize.GuiInput+=e=> {
            if(e is InputEventMouseButton b&&b.ButtonIndex==MouseButton.Left){resizing=b.Pressed;dragging=false;}
            if(e is InputEventMouseMotion m){if(resizing)panel.Size=new Vector2(Math.Max(280,panel.Size.X+m.Relative.X),panel.Size.Y);}
        };
        grab=new Button { Text="\u2630",TooltipText="Left-drag to move",FocusMode=Control.FocusModeEnum.None,MouseDefaultCursorShape=Control.CursorShape.Drag,CustomMinimumSize=new Vector2(24,26) };header.AddChild(grab);
        grab.GuiInput+=e=> {
            if(e is InputEventMouseButton {ButtonIndex:MouseButton.Left} b){dragging=b.Pressed;resizing=false;if(b.Pressed)CloseTooltips();}
            if(dragging&&e is InputEventMouseMotion m)panel.Position+=m.Relative;
        };
        scopeIndex=Math.Clamp(settings.GetValue("layout","scope",0).AsInt32(),0,1);
        scopeToggle=new Button { CustomMinimumSize=new Vector2(26,26),FocusMode=Control.FocusModeEnum.None,TooltipText="Switch damage scope" };header.AddChild(scopeToggle);
        scopeToggle.Pressed+=()=>{scopeIndex=1-scopeIndex;UpdateScopeToggle();Save();};
        UpdateScopeToggle();
        header.MoveChild(scopeToggle,2);
        showUnattributed=settings.GetValue("layout","showUnattributed",false).AsBool();
        unattributedToggle=new Button { Text="?",ToggleMode=true,ButtonPressed=showUnattributed,CustomMinimumSize=new Vector2(22,26),FocusMode=Control.FocusModeEnum.None };
        unattributedToggle.Toggled+=value=>{showUnattributed=value;UpdateUnattributedToggle();Save();};
        header.AddChild(unattributedToggle);
        header.MoveChild(unattributedToggle,3);
        UpdateUnattributedToggle();
        body=new VBoxContainer();body.AddThemeConstantOverride("separation",2);layout.AddChild(body);collapse.Pressed+=()=>{ApplyCollapsedState(!collapsed);Save();};
        metric.AddSeparator("Damage");
        for(var id=0;id<9;id++) {if(id==8)metric.AddSeparator("Effective");if(id==5)metric.AddSeparator("Attribution");metric.AddItem(MeterTooltipModel.Labels[id],id);metric.GetPopup().SetItemTooltip(metric.ItemCount-1,MeterTooltipModel.Description(id));if(id is 5 or 6)metric.SetItemDisabled(metric.ItemCount-1,!NativeDamageCapture.Enabled);}
        var savedMetric=Math.Clamp(settings.GetValue("layout","metric",8).AsInt32(),0,8);
        if(savedMetric==7&&settings.GetValue("layout","attributionVersion",0).AsInt32()<1)savedMetric=0;
        metric.Select(metric.GetItemIndex(savedMetric));metric.ItemSelected+=_=>Save();
        empty=new Label {Text="No damage recorded yet"};body.AddChild(empty);
        scroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        rows=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill };rows.AddThemeConstantOverride("separation",0); scroll.AddChild(rows);
        ApplyCollapsedState(settings.GetValue("layout","collapsed",false).AsBool());
    }
    private void UpdateScopeToggle()
    {
        scopeToggle.Text=scopeIndex==0?"\u2694":"\u21bb";
        scopeToggle.TooltipText=scopeIndex==0?"Current Fight - click to show This Run":"This Run - click to show Current Fight";
    }
    private void UpdateUnattributedToggle()
    {
        unattributedToggle.TooltipText=showUnattributed?"Unattributed damage shown - click to hide":"Unattributed damage hidden - click to show";
    }
    private static Color Saturate(Color color,float amount)
    {
        var gray=.2126f*color.R+.7152f*color.G+.0722f*color.B;
        return new(
            Math.Clamp(gray+(color.R-gray)*amount,0,1),
            Math.Clamp(gray+(color.G-gray)*amount,0,1),
            Math.Clamp(gray+(color.B-gray)*amount,0,1),
            color.A);
    }
    private Vector2 TooltipBounds(Vector2 viewport)
    {
        var width=Math.Min(Math.Max(240,panel.Size.X*1.15f),Math.Max(160,viewport.X*.68f));
        var height=Math.Min(Math.Max(240,Math.Max(panel.Size.X,panel.Size.Y)*1.15f),Math.Max(120,viewport.Y*.7f));
        return new(width,height);
    }
    private void ApplyCollapsedState(bool value)
    {
        CloseTooltips();
        if(value&&!collapsed)expandedWidth=panel.Size.X;
        collapsed=value;body.Visible=!value;metric.Visible=!value;scopeToggle.Visible=!value;unattributedToggle.Visible=!value;resize.Visible=!value;grab.Visible=!value;
        collapse.Text=value?"+":"-";
        panel.CustomMinimumSize=new Vector2(value?0:280,0);
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
        settings.SetValue("layout","attributionVersion",1); settings.SetValue("layout","metric",Metric); settings.SetValue("layout","scope",scopeIndex); settings.SetValue("layout","showUnattributed",showUnattributed); settings.Save(SettingsPath);
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey { Pressed:true,Echo:false,Keycode:Key.F8 }) {CloseTooltips();dragging=resizing=false;panel.Visible=!panel.Visible;}
        if(e is InputEventMouseButton { Pressed:false }) { if(dragging||resizing) Save(); dragging=resizing=false; }
    }
    public override void _Process(double delta)
    {
        var viewport=GetViewport().GetVisibleRect().Size;
        var width=Math.Clamp(panel.Size.X,collapsed?0:Math.Min(280,viewport.X),viewport.X);
        if(!Mathf.IsEqualApprox(width,panel.Size.X)) panel.Size=new Vector2(width,0);
        panel.Position=new Vector2(Math.Clamp(panel.Position.X,0,Math.Max(0,viewport.X-panel.Size.X)),Math.Clamp(panel.Position.Y,0,Math.Max(0,viewport.Y-40)));
        var tooltipBounds=TooltipBounds(viewport);
        elapsed+=delta; if(elapsed<.1) return; elapsed=0;
        var sessionType=RunManager.Instance?.NetService?.Type.ToString();
        var detectedSolo=MeterTooltipModel.SoloForSession(sessionType);
        if(detectedSolo.HasValue)solo=detectedSolo.Value;
        var actual=scopeIndex==0?Probe.Meter.Rows:RunSession.RunRows();
        var effective=scopeIndex==0?Probe.Effective:RunSession.EffectiveRun();
        var ledger=Metric==8?effective.Ledger():scopeIndex==0?Probe.Credited:RunSession.CreditedRun();
        var transfers=scopeIndex==0?Probe.Meter.Transfers:RunSession.CreditedTransfers();
        var credited=ledger.Rows(Metric==8?null:transfers);foreach(var id in actual.Keys)credited.TryAdd(id,new());
        var data=Metric==7?actual:credited;
        decimal Value(Totals t)=>Metric switch {1=>t.Hp,2=>t.Block,3=>t.Poison,4=>t.Doom,5=>t.AmplifiedHp+t.AmplifiedBlock,6=>t.SupportGiven,7=>t.Hp+t.Block,_=>t.Hp+t.Block};
        var visibleData=data.Where(p=>showUnattributed||p.Key!=StatusOwnershipLedger.Unknown).ToArray();
        var ordered=visibleData.OrderByDescending(p=>actual.GetValueOrDefault(p.Key)?.Hp ?? 0).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray(); var max=ordered.Select(p=>Value(p.Value)).DefaultIfEmpty().Max(); var team=visibleData.Sum(p=>Value(p.Value));
        var hovered=panel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
        var visibleIds=visibleData.Select(p=>p.Key).ToHashSet(StringComparer.Ordinal);
        foreach(var id in controls.Keys.Except(visibleIds).ToArray()) { controls[id].Button.CloseTooltip();controls[id].Button.QueueFree(); controls.Remove(id); }
        var breakdowns=Metric==7?(scopeIndex==0?Probe.Meter.StructuredSources:RunSession.SourceBreakdowns()):ledger.Sources();
        var diagnostics=$"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\n{(scopeIndex==1&&RunSession.AttributionPartial?"Partial credited history; earlier actuals available in Actual Damage":"")}\nAttribution: {ledger.Provisional} provisional/unknown events\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (audit pending)\n{Probe.OwnershipStatus}\nEffective: {(EffectiveCapture.Enabled?"enabled":"unavailable")}, {(effective.Incomplete?"partial coverage":"observed coverage")}";
        var badge=MeterTooltipModel.Status((Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!NativeDamageCapture.Enabled||!Probe.OwnershipAvailable||RunSession.StorageIncomplete,ledger.Provisional>0||NativeDamageCapture.Unmatched>0||(scopeIndex==1&&(RunSession.AttributionPartial||RunSession.HistoryPartial)));
        resize.UpdateTooltip(new MeterTooltipModel($"Meter {Probe.Version}\nDrag arrow: resize; drag grab handle: move. Click the scope icon to switch between Current Fight and This Run.\nF8: show/hide. Bars scale to the largest player total.\nRows show rounded damage and, in multiplayer, share of the selected party metric.\n{(badge.Length>0?badge+": some statistics may be missing or unavailable.":"Capture status")}\nSelected: "+MeterTooltipModel.Description(Metric),Array.Empty<string>(),diagnostics){ShowSources=false},tooltipBounds);
        var metricTip=MeterTooltipModel.Description(Metric)+(badge.Length>0?$"\n{badge}: some statistics may be missing or unavailable. Hover the resize arrow for details.":"");
        if(metric.TooltipText!=metricTip)metric.TooltipText=metricTip;
        empty.Text=!showUnattributed&&data.ContainsKey(StatusOwnershipLedger.Unknown)?"No attributed damage yet":"No damage recorded yet";
        empty.Visible=ordered.Length==0;
        var index=0;
        foreach(var (id,total) in ordered)
        {
            if(!controls.TryGetValue(id,out var row))
            {
                var button=new MeterHoverButton { TooltipText="Player details",FocusMode=Control.FocusModeEnum.None, CustomMinimumSize=new Vector2(0,20) };
                button.AddThemeStyleboxOverride("normal",new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("pressed",new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("disabled",new StyleBoxEmpty());
                button.AddThemeStyleboxOverride("hover",new StyleBoxFlat{BgColor=new Color(1,1,1,.05f)});
                rows.AddChild(button);
                var line=new HBoxContainer { MouseFilter=Control.MouseFilterEnum.Ignore }; button.AddChild(line); line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var icon=new TextureRect { CustomMinimumSize=new Vector2(18,18),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(icon);
                var bar=new MeterBar { CustomMinimumSize=new Vector2(70,20),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(bar);
                controls[id]=row=(button,icon,bar);
            }
            if(!hovered) rows.MoveChild(row.Button,index); index++;
            row.Icon.Texture=Probe.CharacterIcons.GetValueOrDefault(id);
            row.Icon.Visible=row.Icon.Texture!=null;
            (Color Hp,Color Amp) colors=Probe.CharacterColors.GetValueOrDefault(id,(Colors.Gray,Colors.Gray));
            row.Bar.HpColour=Saturate(colors.Hp,1.2f);row.Bar.AmpColour=Saturate(colors.Amp,1.2f);
            var value=Value(total);
            row.Bar.PlayerName=Probe.Names.GetValueOrDefault(id,id);
            row.Bar.ShowPlayerName=!solo||id==StatusOwnershipLedger.Unknown||row.Icon.Visible;
            row.Bar.DamageText=Math.Round(value,MidpointRounding.AwayFromZero).ToString("0",System.Globalization.CultureInfo.CurrentCulture);
            row.Bar.ShareText=team>0?$"{100*value/team:0.#}%":"0%";
            row.Bar.ShowDetails=!solo;
            row.Bar.Orange=Metric switch{0 or 8=>total.AmplifiedHp+total.AmplifiedBlock,1=>total.AmplifiedHp,2=>total.AmplifiedBlock,5=>value,6=>value,7=>total.AmplifiedHp+total.AmplifiedBlock,3=>ledger.Credits.Where(c=>c.Player==id&&c.Kind=="Poison"&&c.Amplification).Sum(c=>c.Hp),_=>0};
            row.Bar.Block=Metric switch{0 or 8=>Math.Max(0,total.Block-total.AmplifiedBlock),2=>Math.Max(0,total.Block-total.AmplifiedBlock),7=>Math.Max(0,total.Block-total.AmplifiedBlock),_=>0};
            row.Bar.Hp=Math.Max(0,value-row.Bar.Block-row.Bar.Orange); row.Bar.Maximum=max; row.Bar.QueueRedraw();
            var affected=(Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!Probe.OwnershipAvailable||(Metric!=7&&(!NativeDamageCapture.Enabled||ledger.Provisional>0||NativeDamageCapture.Unmatched>0))||(scopeIndex==1&&(RunSession.StorageIncomplete||RunSession.HistoryPartial||(Metric!=7&&RunSession.AttributionPartial)));
            var card=MeterTooltipModel.BuildCard(id,row.Bar.PlayerName,Metric,scopeIndex==1,solo,value,team,total,effective.PendingFor(id),row.Bar.Orange,breakdowns,transfers,p=>Probe.Names.GetValueOrDefault(p,p),affected?"Data may be incomplete. Hover the resize arrow for details.":"");
            row.Button.UpdateTooltip(new("",Array.Empty<string>(),""){Card=card},tooltipBounds);

        }
        var overhead=panel.GetCombinedMinimumSize().Y-scroll.CustomMinimumSize.Y;
        var desired=Math.Min(Math.Max(0,viewport.Y-panel.Position.Y-overhead-8),Math.Max(0,controls.Count*20));
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
    private StyleBoxFlat? trackStyle,hpStyle,ampStyle,glossStyle;
    public override void _Draw()
    {
        trackStyle??=CreateStyle(new(.3f,.34f,.4f,.16f),6,0);
        if(hpStyle==null||hpStyle.BgColor!=WithAlpha(HpColour,.94f))hpStyle=CreateStyle(WithAlpha(HpColour,.94f),6,5);
        if(ampStyle==null||ampStyle.BgColor!=WithAlpha(AmpColour,.94f))ampStyle=CreateStyle(WithAlpha(AmpColour,.94f),6,5);
        glossStyle??=CreateStyle(new(1,1,1,.11f),5,0);
        var barRect=new Rect2(0,0,Size.X,Size.Y);
        DrawStyleBox(trackStyle,barRect);
        if(Maximum>0)
        {
            var hp=Math.Clamp(Size.X*(float)((Hp+Block)/Maximum),0,Size.X);
            var amp=Math.Clamp(Size.X*(float)(Orange/Maximum),0,Math.Max(0,Size.X-hp));
            if(hp>0)DrawStyleBox(hpStyle,new Rect2(0,0,hp,barRect.Size.Y));
            if(amp>0)DrawStyleBox(ampStyle,new Rect2(hp,0,amp,barRect.Size.Y));
            var filledWidth=hp+amp;
            if(filledWidth>12)DrawStyleBox(glossStyle,new Rect2(2,1,filledWidth-4,Math.Max(0,barRect.Size.Y*.32f)));
        }
        DrawRowText();
    }
    private static Color WithAlpha(Color color,float alpha)=>new(color.R,color.G,color.B,alpha);
    private static StyleBoxFlat CreateStyle(Color color,int radius,int glowSize)
    {
        var style=new StyleBoxFlat
        {
            BgColor=color,
            ShadowColor=new Color(color.R,color.G,color.B,.3f),
            ShadowSize=glowSize
        };
        style.SetCornerRadiusAll(radius);
        return style;
    }
    private void DrawRowText()
    {
        if(Size.X<=0)return;
        var font=GetThemeDefaultFont();var fontSize=Math.Clamp(GetThemeDefaultFontSize(),10,15);
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
