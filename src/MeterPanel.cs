using Godot;
namespace Based.Spires2DamageMeter;

public partial class MeterPanel : CanvasLayer
{
    private static MeterPanel? instance;
    private PanelContainer panel = null!;
    private VBoxContainer rows = null!, body = null!;
    private VBoxContainer layout = null!;
    private Button collapse = null!;
    private bool heightRefreshPending;
    private MeterHoverLabel status = null!, title = null!, info = null!;
    private ScrollContainer scroll=null!;
    private HBoxContainer legend=null!; private Label empty=null!;
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
        panel.Size=new Vector2(settings.GetValue("layout","width",440f).AsSingle(),0);
        layout=new VBoxContainer(); panel.AddChild(layout);
        var header=new HBoxContainer(); layout.AddChild(header);
        title=new MeterHoverLabel { TooltipText="Diagnostics", Text="Damage Meter", SizeFlagsHorizontal=Control.SizeFlags.ExpandFill, ClipText=true, CustomMinimumSize=new Vector2(65,0), MouseFilter=Control.MouseFilterEnum.Stop }; header.AddChild(title);
        title.GuiInput+=e=> { if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Left) dragging=b.Pressed; if(dragging && e is InputEventMouseMotion m) panel.Position+=m.Relative; };
        status=new MeterHoverLabel { TooltipText="Status details",MouseFilter=Control.MouseFilterEnum.Stop };header.AddChild(status);
        info=new MeterHoverLabel {Text="Info",TooltipText="Meter help",MouseFilter=Control.MouseFilterEnum.Stop};header.AddChild(info);
        collapse=new Button { Text="-" }; header.AddChild(collapse);
        body=new VBoxContainer(); layout.AddChild(body); collapse.Pressed+=()=> { ApplyCollapsedState(!collapsed); Save(); };
        metric=new OptionButton(); body.AddChild(metric);
        metric.AddSeparator("Damage");
        for(var id=0;id<8;id++) {if(id==5)metric.AddSeparator("Attribution");metric.AddItem(MeterTooltipModel.Labels[id],id);metric.GetPopup().SetItemTooltip(metric.ItemCount-1,MeterTooltipModel.Description(id));if(id is 5 or 6)metric.SetItemDisabled(metric.ItemCount-1,!NativeDamageCapture.Enabled);}
        var savedMetric=Math.Clamp(settings.GetValue("layout","metric",0).AsInt32(),0,7);
        if(savedMetric==7&&settings.GetValue("layout","attributionVersion",0).AsInt32()<1)savedMetric=0;
        metric.Select(metric.GetItemIndex(savedMetric));metric.ItemSelected+=_=>Save();
        scope=new TabBar(); scope.AddTab("Current Fight"); scope.AddTab("This Run"); scope.CurrentTab=Math.Clamp(settings.GetValue("layout","scope",0).AsInt32(),0,1); var tabs=new HBoxContainer(); body.AddChild(tabs); scope.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill; tabs.AddChild(scope);
        scope.TabChanged+=_=>Save();
        var headings=new HBoxContainer();body.AddChild(headings);
        legend=new HBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};headings.AddChild(legend);
        headings.AddChild(new Label {Text="Party %",TooltipText="Share of party total for the selected metric."});
        empty=new Label {Text="No damage recorded yet"};body.AddChild(empty);
        scroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        rows=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
        var resize=new Label { Text="↔", TooltipText="Drag to resize width", HorizontalAlignment=HorizontalAlignment.Right, MouseFilter=Control.MouseFilterEnum.Stop }; body.AddChild(resize);
        resize.GuiInput+=e=> { if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Left) resizing=b.Pressed; if(resizing && e is InputEventMouseMotion m) panel.Size=new Vector2(Math.Max(320,panel.Size.X+m.Relative.X),panel.Size.Y); };
        ApplyCollapsedState(settings.GetValue("layout","collapsed",false).AsBool());
    }
    private void ApplyCollapsedState(bool value)
    {
        CloseTooltips();collapsed=value; body.Visible=!value; collapse.Text=value?"+":"-";
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
            panel.Size=new Vector2(panel.Size.X,0);
        }).CallDeferred();
    }
    private void Save()
    {
        settings.SetValue("layout","position",panel.Position); settings.SetValue("layout","width",panel.Size.X); settings.SetValue("layout","collapsed",collapsed);
        settings.SetValue("layout","attributionVersion",1); settings.SetValue("layout","metric",Metric); settings.SetValue("layout","scope",scope.CurrentTab); settings.Save(SettingsPath);
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey { Pressed:true,Echo:false,Keycode:Key.F8 }) {CloseTooltips();panel.Visible=!panel.Visible;}
        if(e is InputEventMouseButton { Pressed:false }) { if(dragging||resizing) Save(); dragging=resizing=false; }
    }
    public override void _Process(double delta)
    {
        var viewport=GetViewport().GetVisibleRect().Size;
        var width=Math.Clamp(panel.Size.X,Math.Min(320,viewport.X),viewport.X);
        if(!Mathf.IsEqualApprox(width,panel.Size.X)) panel.Size=new Vector2(width,0);
        panel.Position=new Vector2(Math.Clamp(panel.Position.X,0,Math.Max(0,viewport.X-panel.Size.X)),Math.Clamp(panel.Position.Y,0,Math.Max(0,viewport.Y-40)));
        elapsed+=delta; if(elapsed<.1) return; elapsed=0;
        var actual=scope.CurrentTab==0?Probe.Meter.Rows:RunSession.RunRows();
        var ledger=scope.CurrentTab==0?Probe.Credited:RunSession.CreditedRun();
        var transfers=scope.CurrentTab==0?Probe.Meter.Transfers:RunSession.CreditedTransfers();
        var credited=ledger.Rows(transfers);foreach(var id in actual.Keys)credited.TryAdd(id,new());
        var data=Metric==7?actual:credited;
        decimal Value(Totals t)=>Metric switch {1=>t.Hp,2=>t.Block,3=>t.Poison,4=>t.Doom,5=>t.AmplifiedHp+t.AmplifiedBlock,6=>t.SupportGiven,7=>t.Hp+t.Block,_=>t.Hp+t.Block};
        var ordered=data.OrderByDescending(p=>Value(p.Value)).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray(); var max=ordered.Select(p=>Value(p.Value)).DefaultIfEmpty().Max(); var team=data.Values.Sum(Value);
        var hovered=panel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
        foreach(var id in controls.Keys.Except(data.Keys).ToArray()) { controls[id].Button.CloseTooltip();controls[id].Button.QueueFree(); controls.Remove(id); }
        var breakdowns=Metric==7?(scope.CurrentTab==0?Probe.Meter.StructuredSources:RunSession.SourceBreakdowns()):ledger.Sources();
        var diagnostics=$"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\n{(scope.CurrentTab==1&&RunSession.AttributionPartial?"Partial credited history; earlier actuals available in Actual Damage":"")}\nAttribution: {ledger.Provisional} provisional/unknown events\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (audit pending)\n{Probe.OwnershipStatus}";
        var badge=MeterTooltipModel.Status(Probe.Health.StartsWith("INCOMPLETE")||!NativeDamageCapture.Enabled||!Probe.OwnershipAvailable||RunSession.StorageIncomplete,ledger.Provisional>0||NativeDamageCapture.Unmatched>0||(scope.CurrentTab==1&&(RunSession.AttributionPartial||RunSession.HistoryPartial)));
        status.Text=badge;status.Visible=badge.Length>0;
        var globalTip=new MeterTooltipModel("Meter status\nPartial: some history or attribution is missing.\nIncomplete: capture or storage is unavailable.\nOlder actual history may remain in Actual Damage.",Array.Empty<string>(),diagnostics){ShowSources=false};status.UpdateTooltip(globalTip);title.UpdateTooltip(globalTip);
        info.UpdateTooltip(new MeterTooltipModel($"Damage Meter {Probe.Version}\nF8: show/hide; drag title: move; drag corner: resize.\nBars scale to the largest player total. Party % is share of the selected party metric.\nAmplification: self and teammate bonuses. Support: teammate bonuses only, already included in Damage.\nActual Damage: ownership before bonus sharing.\nSelected: "+MeterTooltipModel.Description(Metric),Array.Empty<string>(),diagnostics){ShowSources=false});
        metric.TooltipText=MeterTooltipModel.Description(Metric);
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
            row.Name.Text=Probe.Names.GetValueOrDefault(id,id); var value=Value(total);
            row.Total.Text=$"{(team>0?100*value/team:0):0.#}%";
            row.Bar.Orange=Metric switch{0=>total.AmplifiedHp+total.AmplifiedBlock,1=>total.AmplifiedHp,2=>total.AmplifiedBlock,5=>value,6=>value,7=>total.AmplifiedHp+total.AmplifiedBlock,3=>ledger.Credits.Where(c=>c.Player==id&&c.Kind=="Poison"&&c.Amplification).Sum(c=>c.Hp),_=>0};
            row.Bar.Block=Metric switch{0=>Math.Max(0,total.Block-total.AmplifiedBlock),2=>Math.Max(0,total.Block-total.AmplifiedBlock),7=>Math.Max(0,total.Block-total.AmplifiedBlock),_=>0};
            row.Bar.Hp=Math.Max(0,value-row.Bar.Block-row.Bar.Orange); row.Bar.Maximum=max; row.Bar.QueueRedraw();
            var summary=$"{row.Name.Text} - {MeterTooltipModel.Labels[Metric]} ({(scope.CurrentTab==0?"Current Fight":"This Run")})\nTotal: {value:0.##} | Party share: {(team>0?100*value/team:0):0.#}%\n"+MeterTooltipModel.Detail(Metric,total);
            if(Metric==3)summary+=$"\nBonus HP included: {row.Bar.Orange:0.##}";
            var affected=Probe.Health.StartsWith("INCOMPLETE")||!Probe.OwnershipAvailable||(Metric!=7&&(!NativeDamageCapture.Enabled||ledger.Provisional>0||NativeDamageCapture.Unmatched>0))||(scope.CurrentTab==1&&(RunSession.StorageIncomplete||RunSession.HistoryPartial||(Metric!=7&&RunSession.AttributionPartial)));
            row.Button.UpdateTooltip(new(summary,MeterTooltipModel.Sources(id,Metric,breakdowns,transfers,p=>Probe.Names.GetValueOrDefault(p,p)),affected?"Data may be incomplete. Hover the header status for details.":""));

        }
        var desired=Math.Min(Math.Max(0,viewport.Y-panel.Position.Y-180),Math.Max(0,controls.Count*36));
        scroll.CustomMinimumSize=new Vector2(0,desired);
        QueueHeightRefresh();
    }
    private int legendMetric=-1;
    private void UpdateLegend()
    {
        if(legendMetric==Metric)return;legendMetric=Metric;
        foreach(var child in legend.GetChildren()){legend.RemoveChild(child);child.QueueFree();}
        void Swatch(string text,Color color){var item=new Label{Text=text};item.AddThemeColorOverride("font_color",color);legend.AddChild(item);}
        if(Metric is 0 or 2 or 7)Swatch("■ Block",new(.35f,.7f,1f));
        if(Metric is 0 or 1 or 3 or 4 or 7)Swatch("■ HP",new(1f,.45f,.5f));
        if(Metric!=4)Swatch("■ Bonus",new(1f,.65f,.3f));
    }
    private void CloseTooltips(){if(title!=null)title.CloseTooltip();if(info!=null)info.CloseTooltip();if(status!=null)status.CloseTooltip();foreach(var row in controls.Values)row.Button.CloseTooltip();}
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
