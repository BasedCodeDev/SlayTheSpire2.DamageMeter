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
    private HBoxContainer legend=null!; private Label empty=null!,partyHeading=null!;
    private bool solo;
    private int Metric => metric.GetSelectedId();
    private OptionButton metric = null!;
    private TabBar scope = null!;
    private readonly Dictionary<string, (MeterHoverButton Button, Label Name, MeterBar Bar, Label Total)> controls = new();
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
        partyHeading=new Label {Text="Party %",TooltipText="Share of party total for the selected metric."};headings.AddChild(partyHeading);
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
        partyHeading.Visible=!solo;
        var actual=scope.CurrentTab==0?Probe.Meter.Rows:RunSession.RunRows();
        var effective=scope.CurrentTab==0?Probe.Effective:RunSession.EffectiveRun();
        var ledger=Metric==8?effective.Ledger():scope.CurrentTab==0?Probe.Credited:RunSession.CreditedRun();
        var transfers=scope.CurrentTab==0?Probe.Meter.Transfers:RunSession.CreditedTransfers();
        var credited=ledger.Rows(Metric==8?null:transfers);foreach(var id in actual.Keys)credited.TryAdd(id,new());
        var data=Metric==7?actual:credited;
        decimal Value(Totals t)=>Metric switch {1=>t.Hp,2=>t.Block,3=>t.Poison,4=>t.Doom,5=>t.AmplifiedHp+t.AmplifiedBlock,6=>t.SupportGiven,7=>t.Hp+t.Block,_=>t.Hp+t.Block};
        var ordered=data.OrderByDescending(p=>Value(p.Value)).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray(); var max=ordered.Select(p=>Value(p.Value)).DefaultIfEmpty().Max(); var team=data.Values.Sum(Value);
        var hovered=panel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
        foreach(var id in controls.Keys.Except(data.Keys).ToArray()) { controls[id].Button.CloseTooltip();controls[id].Button.QueueFree(); controls.Remove(id); }
        var breakdowns=Metric==7?(scope.CurrentTab==0?Probe.Meter.StructuredSources:RunSession.SourceBreakdowns()):ledger.Sources();
        var diagnostics=$"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\n{(scope.CurrentTab==1&&RunSession.AttributionPartial?"Partial credited history; earlier actuals available in Actual Damage":"")}\nAttribution: {ledger.Provisional} provisional/unknown events\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (audit pending)\n{Probe.OwnershipStatus}\nEffective: {(EffectiveCapture.Enabled?"enabled":"unavailable")}, {(effective.Incomplete?"partial coverage":"observed coverage")}";
        var badge=MeterTooltipModel.Status((Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!NativeDamageCapture.Enabled||!Probe.OwnershipAvailable||RunSession.StorageIncomplete,ledger.Provisional>0||NativeDamageCapture.Unmatched>0||(scope.CurrentTab==1&&(RunSession.AttributionPartial||RunSession.HistoryPartial)));
        resize.UpdateTooltip(new MeterTooltipModel($"Meter {Probe.Version}\nDrag arrow: resize; drag grab handle: move.\nF8: show/hide. Bars scale to the largest player total.\n{(solo?"":"Party % is share of the selected party metric.\n")}{(badge.Length>0?badge+": some statistics may be missing or unavailable.":"Capture status")}\nSelected: "+MeterTooltipModel.Description(Metric),Array.Empty<string>(),diagnostics){ShowSources=false});
        var metricTip=MeterTooltipModel.Description(Metric)+(badge.Length>0?$"\n{badge}: some statistics may be missing or unavailable. Hover the resize arrow for details.":"");
        if(metric.TooltipText!=metricTip)metric.TooltipText=metricTip;
        UpdateLegend();empty.Visible=data.Count==0;
        var index=0;
        foreach(var (id,total) in ordered)
        {
            if(!controls.TryGetValue(id,out var row))
            {
                var button=new MeterHoverButton { TooltipText="Player details",FocusMode=Control.FocusModeEnum.None, CustomMinimumSize=new Vector2(0,32) }; rows.AddChild(button);
                var line=new HBoxContainer { MouseFilter=Control.MouseFilterEnum.Ignore }; button.AddChild(line); line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var name=new Label { CustomMinimumSize=new Vector2(105,0),ClipText=true,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(name);
                var bar=new MeterBar { CustomMinimumSize=new Vector2(70,24),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(bar);
                var number=new Label { CustomMinimumSize=new Vector2(62,0),HorizontalAlignment=HorizontalAlignment.Right,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(number);
                controls[id]=row=(button,name,bar,number);
            }
            if(!hovered) rows.MoveChild(row.Button,index); index++;
            row.Name.Text=Probe.Names.GetValueOrDefault(id,id);
            row.Name.Visible=!solo||id==StatusOwnershipLedger.Unknown;row.Total.Visible=!solo;
            var value=Value(total);
            row.Total.Text=$"{(team>0?100*value/team:0):0.#}%";
            row.Bar.Orange=Metric switch{0 or 8=>total.AmplifiedHp+total.AmplifiedBlock,1=>total.AmplifiedHp,2=>total.AmplifiedBlock,5=>value,6=>value,7=>total.AmplifiedHp+total.AmplifiedBlock,3=>ledger.Credits.Where(c=>c.Player==id&&c.Kind=="Poison"&&c.Amplification).Sum(c=>c.Hp),_=>0};
            row.Bar.Block=Metric switch{0 or 8=>Math.Max(0,total.Block-total.AmplifiedBlock),2=>Math.Max(0,total.Block-total.AmplifiedBlock),7=>Math.Max(0,total.Block-total.AmplifiedBlock),_=>0};
            row.Bar.Hp=Math.Max(0,value-row.Bar.Block-row.Bar.Orange); row.Bar.Maximum=max; row.Bar.QueueRedraw();
            var summary=MeterTooltipModel.RowSummary(row.Name.Text,Metric,scope.CurrentTab==1,value,team,solo,id==StatusOwnershipLedger.Unknown)+MeterTooltipModel.Detail(Metric,total);
            if(Metric==8)summary+=$"\nQualified block: {total.Block:0.##}; pending block (excluded): {effective.PendingFor(id):0.##}\n"+MeterTooltipModel.Description(8);
            if(Metric==3)summary+=$"\nBonus HP included: {row.Bar.Orange:0.##}";
            var affected=(Metric==8&&(!EffectiveCapture.Enabled||effective.Incomplete))||Probe.Health.StartsWith("INCOMPLETE")||!Probe.OwnershipAvailable||(Metric!=7&&(!NativeDamageCapture.Enabled||ledger.Provisional>0||NativeDamageCapture.Unmatched>0))||(scope.CurrentTab==1&&(RunSession.StorageIncomplete||RunSession.HistoryPartial||(Metric!=7&&RunSession.AttributionPartial)));
            row.Button.UpdateTooltip(new(summary,MeterTooltipModel.Sources(id,Metric,breakdowns,transfers,p=>Probe.Names.GetValueOrDefault(p,p)),affected?"Data may be incomplete. Hover the resize arrow for details.":""));

        }
        var overhead=panel.GetCombinedMinimumSize().Y-scroll.CustomMinimumSize.Y;
        var desired=Math.Min(Math.Max(0,viewport.Y-panel.Position.Y-overhead-8),Math.Max(0,controls.Count*36));
        scroll.CustomMinimumSize=new Vector2(0,desired);
        QueueHeightRefresh();
    }
    private int legendMetric=-1;
    private void UpdateLegend()
    {
        if(legendMetric==Metric)return;legendMetric=Metric;
        foreach(var child in legend.GetChildren()){legend.RemoveChild(child);child.QueueFree();}
        void Swatch(string text,Color color){var item=new Label{Text=text};item.AddThemeColorOverride("font_color",color);legend.AddChild(item);}
        if(Metric is 0 or 2 or 7 or 8)Swatch("■ Block",new(.35f,.7f,1f));
        if(Metric is 0 or 1 or 3 or 4 or 7 or 8)Swatch("■ HP",new(1f,.45f,.5f));
        if(Metric!=4)Swatch("■ Bonus",new(1f,.65f,.3f));
    }
    private void CloseTooltips(){if(resize!=null)resize.CloseTooltip();foreach(var row in controls.Values)row.Button.CloseTooltip();}
    public override void _ExitTree() { CloseTooltips();Save(); Probe.Flush(); instance=null; }
}

public partial class MeterBar : Control
{
    public decimal Hp,Block,Orange,Maximum;
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color(.12f,.12f,.15f)); if(Maximum<=0){DrawValue(0,0,Size.X);return;}
        var b=Size.X*(float)(Block/Maximum); var h=Size.X*(float)(Hp/Maximum); var o=Size.X*(float)(Orange/Maximum);
        DrawRect(new Rect2(0,3,b,Math.Max(0,Size.Y-6)),new Color(.2f,.55f,.95f));
        DrawRect(new Rect2(b,3,h,Math.Max(0,Size.Y-6)),new Color(.85f,.25f,.3f));
        DrawRect(new Rect2(b+h,3,o,Math.Max(0,Size.Y-6)),new Color(1f,.55f,.12f));
        DrawValue(Hp+Block+Orange,0,Size.X);
    }
    private void DrawValue(decimal value,float left,float width)
    {
        if(width<=0) return;
        var font=GetThemeDefaultFont(); var fontSize=Math.Clamp(GetThemeDefaultFontSize(),12,18);
        var text=value.ToString("0.##",System.Globalization.CultureInfo.CurrentCulture);
        var measured=font.GetStringSize(text,HorizontalAlignment.Left,-1,fontSize);
        while(measured.X+8>width&&fontSize>8){fontSize--;measured=font.GetStringSize(text,HorizontalAlignment.Left,-1,fontSize);}
        if(measured.X+8>width || font.GetHeight(fontSize)>Size.Y) return;
        var position=new Vector2(left+(width-measured.X)/2,(Size.Y-font.GetHeight(fontSize))/2+font.GetAscent(fontSize));
        DrawStringOutline(font,position,text,HorizontalAlignment.Left,-1,fontSize,3,Colors.Black);
        DrawString(font,position,text,HorizontalAlignment.Left,-1,fontSize,Colors.White);
    }
}
