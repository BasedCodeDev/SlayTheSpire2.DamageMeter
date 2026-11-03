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
    private Label status = null!, details = null!;
    private OptionButton metric = null!;
    private TabBar scope = null!;
    private readonly Dictionary<string, (Button Button, Label Name, MeterBar Bar, Label Total)> controls = new();
    private string selected = "";
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
        var title=new Label { Text=$"Based Damage Meter {Probe.Version}", SizeFlagsHorizontal=Control.SizeFlags.ExpandFill, MouseFilter=Control.MouseFilterEnum.Stop }; header.AddChild(title);
        title.GuiInput+=e=> { if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Left) dragging=b.Pressed; if(dragging && e is InputEventMouseMotion m) panel.Position+=m.Relative; };
        collapse=new Button { Text="−" }; header.AddChild(collapse);
        body=new VBoxContainer(); layout.AddChild(body); collapse.Pressed+=()=> { ApplyCollapsedState(!collapsed); Save(); };
        metric=new OptionButton(); body.AddChild(metric);
        foreach(var name in new[]{"HP + Block","HP Only","Block Only","Poison HP","Doom HP","Amplification","Support Damage","Contribution"}) metric.AddItem(name);
        for(var i=5;i<8;i++) metric.SetItemDisabled(i,!NativeDamageCapture.Enabled);
        metric.Selected=Math.Clamp(settings.GetValue("layout","metric",0).AsInt32(),0,7); metric.ItemSelected+=_=>Save();
        scope=new TabBar(); scope.AddTab("Current Fight"); scope.AddTab("This Run"); scope.CurrentTab=Math.Clamp(settings.GetValue("layout","scope",0).AsInt32(),0,1); body.AddChild(scope); scope.TabChanged+=_=>Save();
        body.AddChild(new Label { Text="Blue: block | Red: HP | Orange: amplification" });
        var scroll=new ScrollContainer { CustomMinimumSize=new Vector2(0,130) }; body.AddChild(scroll);
        rows=new VBoxContainer { SizeFlagsHorizontal=Control.SizeFlags.ExpandFill }; scroll.AddChild(rows);
        details=new Label { Text="Click a player for sources", AutowrapMode=TextServer.AutowrapMode.WordSmart }; body.AddChild(details);
        status=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart }; body.AddChild(status);
        var resize=new Label { Text="◢", HorizontalAlignment=HorizontalAlignment.Right, MouseFilter=Control.MouseFilterEnum.Stop }; body.AddChild(resize);
        resize.GuiInput+=e=> { if(e is InputEventMouseButton b && b.ButtonIndex==MouseButton.Left) resizing=b.Pressed; if(resizing && e is InputEventMouseMotion m) panel.Size=new Vector2(Math.Max(320,panel.Size.X+m.Relative.X),panel.Size.Y); };
        ApplyCollapsedState(settings.GetValue("layout","collapsed",false).AsBool());
    }
    private void ApplyCollapsedState(bool value)
    {
        collapsed=value; body.Visible=!value; collapse.Text=value?"+":"−";
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
        settings.SetValue("layout","metric",metric.Selected); settings.SetValue("layout","scope",scope.CurrentTab); settings.Save(SettingsPath);
    }
    public override void _Input(InputEvent e)
    {
        if(e is InputEventKey { Pressed:true,Echo:false,Keycode:Key.F8 }) panel.Visible=!panel.Visible;
        if(e is InputEventMouseButton { Pressed:false }) { if(dragging||resizing) Save(); dragging=resizing=false; }
    }
    public override void _Process(double delta)
    {
        var viewport=GetViewport().GetVisibleRect().Size;
        var width=Math.Clamp(panel.Size.X,Math.Min(320,viewport.X),viewport.X);
        if(!Mathf.IsEqualApprox(width,panel.Size.X)) panel.Size=new Vector2(width,0);
        panel.Position=new Vector2(Math.Clamp(panel.Position.X,0,Math.Max(0,viewport.X-panel.Size.X)),Math.Clamp(panel.Position.Y,0,Math.Max(0,viewport.Y-40)));
        elapsed+=delta; if(elapsed<.1) return; elapsed=0;
        var data=scope.CurrentTab==0?Probe.Meter.Rows:RunSession.RunRows();
        decimal Value(Totals t)=>metric.Selected switch {1=>t.Hp,2=>t.Block,3=>t.Poison,4=>t.Doom,5=>t.AmplifiedHp+t.AmplifiedBlock,6=>t.SupportGiven,7=>t.Hp+t.Block-t.SupportReceived+t.SupportGiven,_=>t.Hp+t.Block};
        var ordered=data.OrderByDescending(p=>Value(p.Value)).ThenBy(p=>p.Key,StringComparer.Ordinal).ToArray(); var max=ordered.Select(p=>Value(p.Value)).DefaultIfEmpty().Max(); var team=data.Values.Sum(Value);
        var hovered=panel.GetGlobalRect().HasPoint(GetViewport().GetMousePosition());
        foreach(var id in controls.Keys.Except(data.Keys).ToArray()) { controls[id].Button.QueueFree(); controls.Remove(id); }
        var index=0;
        foreach(var (id,total) in ordered)
        {
            if(!controls.TryGetValue(id,out var row))
            {
                var button=new Button { CustomMinimumSize=new Vector2(0,32) }; rows.AddChild(button); button.Pressed+=()=>selected=id;
                var line=new HBoxContainer { MouseFilter=Control.MouseFilterEnum.Ignore }; button.AddChild(line); line.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var name=new Label { CustomMinimumSize=new Vector2(105,0),ClipText=true,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(name);
                var bar=new MeterBar { CustomMinimumSize=new Vector2(70,24),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(bar);
                var number=new Label { CustomMinimumSize=new Vector2(62,0),HorizontalAlignment=HorizontalAlignment.Right,MouseFilter=Control.MouseFilterEnum.Ignore }; line.AddChild(number);
                controls[id]=row=(button,name,bar,number);
            }
            if(!hovered) rows.MoveChild(row.Button,index); index++;
            row.Name.Text=Probe.Names.GetValueOrDefault(id,id); var value=Value(total);
            row.Total.Text=$"{(team>0?100*value/team:0):0.#}%";
                        var selfHp=Math.Max(0,total.AmplifiedHp-total.SupportReceivedHp);var selfBlock=Math.Max(0,total.AmplifiedBlock-total.SupportReceivedBlock);
            row.Bar.Orange=metric.Selected switch{0=>total.AmplifiedHp+total.AmplifiedBlock,1=>total.AmplifiedHp,2=>total.AmplifiedBlock,5=>value,6=>value,7=>selfHp+selfBlock+total.SupportGiven,_=>0};
            row.Bar.Block=metric.Selected switch{0=>Math.Max(0,total.Block-total.AmplifiedBlock),2=>Math.Max(0,total.Block-total.AmplifiedBlock),7=>Math.Max(0,total.Block-total.SupportReceivedBlock-selfBlock),_=>0};
            row.Bar.Hp=Math.Max(0,value-row.Bar.Block-row.Bar.Orange); row.Bar.Maximum=max; row.Bar.QueueRedraw();
            row.Button.TooltipText=$"{row.Name.Text}\nHP: {total.Hp:0.##}; Block: {total.Block:0.##}\nPoison HP: {total.Poison:0.##}; Doom HP: {total.Doom:0.##}\nAmplification includes reconciled traces only";
        }
                var breakdowns=scope.CurrentTab==0?Probe.Meter.StructuredSources:RunSession.SourceBreakdowns();
        var transfers=scope.CurrentTab==0?Probe.Meter.Transfers:RunSession.SupportTransfers();
        var sources=scope.CurrentTab==0?Probe.Meter.Sources:RunSession.RunSources();
        details.Text=sources.TryGetValue(selected,out var leaves)?string.Join("\n",leaves.OrderByDescending(p=>p.Value).Take(12).Select(p=>$"{p.Key}: {p.Value:0.##}")):"Click a player for sources";
        if(metric.Selected is 6 or 7)
            details.Text=string.Join("\n",transfers.Where(t=>t.Provider==selected || (metric.Selected==7 && t.Recipient==selected)).Select(t=>$"{Probe.Names.GetValueOrDefault(t.Provider,t.Provider)} → {Probe.Names.GetValueOrDefault(t.Recipient,t.Recipient)} ({t.Source}): HP {t.Hp:0.##}, Block {t.Block:0.##}").Take(12));
        else if(breakdowns.Any(s=>s.Player==selected))
            details.Text=string.Join("\n",breakdowns.Where(s=>s.Player==selected).GroupBy(s=>s.Source).Select(g=>new {Name=g.Key.DisplayName,Value=g.Sum(t=>metric.Selected switch{1=>t.Hp,2=>t.Block,3=>t.Source.Kind=="Poison"?t.Hp:0,4=>t.Source.Kind=="Doom"?t.Hp:0,5=>t.AmplifiedHp+t.AmplifiedBlock,_=>t.Hp+t.Block})}).Where(s=>s.Value>0).OrderByDescending(s=>s.Value).Take(12).Select(s=>$"{s.Name}: {s.Value:0.##}"));
        status.Text=$"{Probe.Combat} | {Probe.Health}\n{RunSession.Status}\nEnemy amplification: {NativeDamageCapture.Matched} matched, {NativeDamageCapture.Unmatched} unmeasured (coverage audit pending)\n{Probe.OwnershipStatus}";
        QueueHeightRefresh();
    }
    public override void _ExitTree() { Save(); Probe.Flush(); instance=null; }
}

public partial class MeterBar : Control
{
    public decimal Hp,Block,Orange,Maximum;
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero,Size),new Color(.12f,.12f,.15f)); if(Maximum<=0)return;
        var b=Size.X*(float)(Block/Maximum); var h=Size.X*(float)(Hp/Maximum); var o=Size.X*(float)(Orange/Maximum);
        DrawRect(new Rect2(0,3,b,Math.Max(0,Size.Y-6)),new Color(.2f,.55f,.95f));
        DrawRect(new Rect2(b,3,h,Math.Max(0,Size.Y-6)),new Color(.85f,.25f,.3f));
        DrawRect(new Rect2(b+h,3,o,Math.Max(0,Size.Y-6)),new Color(1f,.55f,.12f));
        DrawValue(Orange,b+h,o);
        DrawValue(Block,0,b);
        DrawValue(Hp,b,h);
    }
    private void DrawValue(decimal value,float left,float width)
    {
        if(value<=0 || width<=0) return;
        var font=GetThemeDefaultFont(); var fontSize=Math.Clamp(GetThemeDefaultFontSize(),12,18);
        var text=value.ToString("0.##",System.Globalization.CultureInfo.CurrentCulture);
        var measured=font.GetStringSize(text,HorizontalAlignment.Left,-1,fontSize);
        if(measured.X+8>width || font.GetHeight(fontSize)>Size.Y) return;
        var position=new Vector2(left+(width-measured.X)/2,(Size.Y-font.GetHeight(fontSize))/2+font.GetAscent(fontSize));
        DrawStringOutline(font,position,text,HorizontalAlignment.Left,-1,fontSize,3,Colors.Black);
        DrawString(font,position,text,HorizontalAlignment.Left,-1,fontSize,Colors.White);
    }
}
