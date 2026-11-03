using Godot;
using MegaCrit.Sts2.Core.Runs;
namespace Based.Spires2DamageMeter;

public partial class MeterPanel : CanvasLayer
{
    public MeterPanel() => Hosting.RuntimeHost.TrackUi(this);
    internal bool Initialized;
    internal MeterPresentation CapturePresentation() => new(panel.Position, collapsed ? expandedWidth : panel.Size.X,
        collapsed, panel.Visible, Metric, scopeIndex, solo, showUnattributed);

    internal void DestroyTooltips()
    {
        static void Destroy(MeterTooltipContent? content)
        {
            if (!GodotObject.IsInstanceValid(content)) return;
            if (content!.IsInsideTree())
            {
                var window = content.GetWindow();
                if (window != ((SceneTree)Engine.GetMainLoop()).Root)
                { window.Hide(); window.QueueFree(); return; }
            }
            content.Free();
        }
        if (resize != null) Destroy(resize.popup);
        foreach (var row in controls.Values) Destroy(row.Button.popup);
    }

    internal static MeterPanel? instance;
    internal PanelContainer panel = null!;
    internal VBoxContainer rows = null!, body = null!;
    internal VBoxContainer layout = null!;
    internal Button collapse = null!, grab = null!;
    internal float expandedWidth = 380f;
    internal bool heightRefreshPending;
    internal MeterHoverLabel resize = null!;
    internal ScrollContainer scroll = null!;
    internal Label empty = null!;
    internal bool solo;
    internal int Metric => metric.GetSelectedId();

    internal OptionButton metric = null!;
    internal Button scopeToggle = null!;
    internal Button unattributedToggle = null!;
    internal int scopeIndex;
    internal bool showUnattributed;
    internal readonly Dictionary<string, (MeterHoverButton Button, TextureRect Icon, MeterBar Bar)> controls = new();
    internal bool dragging, resizing, collapsed;
    internal double elapsed;
    internal readonly ConfigFile settings = new();
    internal string SettingsPath => "user://" + Probe.Id + "/meter-layout.cfg";

    public static void Ensure() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.Ensure");
    public override void _Ready()
    {
        if (Initialized)
            return;
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel._Ready", this);
        Initialized = true;
    }

    internal void UpdateScopeToggle() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.UpdateScopeToggle", this);
    internal void UpdateUnattributedToggle() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.UpdateUnattributedToggle", this);
    internal static Color Saturate(Color color, float amount) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Color>("MeterPanel.Saturate", color, amount);
    internal Vector2 TooltipBounds(Vector2 viewport) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Vector2>("MeterPanel.TooltipBounds", this, viewport);
    internal void ApplyCollapsedState(bool value) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.ApplyCollapsedState", this, value);
    internal void QueueHeightRefresh() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.QueueHeightRefresh", this);
    internal void Save() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.Save", this);
    public override void _Input(InputEvent e) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel._Input", this, e);
    public override void _Process(double delta) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel._Process", this, delta);
    internal void CloseTooltips() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel.CloseTooltips", this);
    public override void _ExitTree() { if (Initialized) global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterPanel._ExitTree", this); else if (ReferenceEquals(instance, this)) instance = null; }
}
public partial class MeterBar : Control
{
    public decimal Hp, Block, Orange, Maximum;
    public Color HpColour = Colors.Gray, AmpColour = Colors.Gray;
    public string PlayerName = "", DamageText = "", ShareText = "";
    public bool ShowPlayerName, ShowDetails;
    internal StyleBoxFlat? trackStyle, hpStyle, ampStyle, glossStyle;
    public override void _Draw() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterBar._Draw", this);
    internal static Color WithAlpha(Color color, float alpha) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Color>("MeterBar.WithAlpha", color, alpha);
    internal static StyleBoxFlat CreateStyle(Color color, int radius, int glowSize) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<StyleBoxFlat>("MeterBar.CreateStyle", color, radius, glowSize);
    internal void DrawRowText() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterBar.DrawRowText", this);
    internal void DrawOutlined(Font font, Vector2 position, string text, int fontSize) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterBar.DrawOutlined", this, font, position, text, fontSize);
}

public sealed record MeterPresentation(Vector2 Position, float Width, bool Collapsed, bool Visible, int Metric, int Scope, bool Solo, bool ShowUnattributed = false);
