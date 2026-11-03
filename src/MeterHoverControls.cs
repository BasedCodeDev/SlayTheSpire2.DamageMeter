using Godot;
namespace Based.Spires2DamageMeter;

public static class MeterPalette
{
    public static Color Colour(TooltipTone tone) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Color>("MeterPalette.Colour", tone);
}
public partial class MeterTooltipContent : VBoxContainer
{
    public MeterTooltipModel Model = new("", Array.Empty<string>(), "");
    public Vector2 AvailableSize;
    internal ScrollContainer scroll = null!;
    internal VBoxContainer content = null!;
    internal string shape = "";
    internal float width;
    internal Label title = null!, scope = null!, total = null!, share = null!, note = null!, warning = null!, more = null!, plain = null!, sourceHeading = null!;
    internal readonly List<(Label Label, Label Value)> stats = new();
    internal readonly List<(HBoxContainer Row, Label Name, Label[] Values)> sources = new();
    internal readonly List<Label> textLabels = new();
    internal readonly List<Label> headers = new();
    internal Label Label(string text = "", int size = 0) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Label>("MeterTooltipContent.Label", this, text, size);
    internal void Set(Label label, string text, TooltipTone tone = TooltipTone.Neutral) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterTooltipContent.Set", this, label, text, tone);
    internal HBoxContainer Row() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<HBoxContainer>("MeterTooltipContent.Row", this);
    internal void Heading(string text) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterTooltipContent.Heading", this, text);
    public override void _Ready() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterTooltipContent._Ready", this);
    internal void Build(TooltipCard? card) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterTooltipContent.Build", this, card);
    public void Refresh() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterTooltipContent.Refresh", this);
}
public partial class MeterHoverButton : Button
{
    internal MeterTooltipModel model = new("", Array.Empty<string>(), "");
    internal Vector2 availableSize = new(420, 480);
    internal MeterTooltipContent? popup;
    public void UpdateTooltip(MeterTooltipModel value, Vector2 size) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterHoverButton.UpdateTooltip", this, value, size);
    public override GodotObject _MakeCustomTooltip(string forText) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<GodotObject>("MeterHoverButton._MakeCustomTooltip", this, forText);
    public void CloseTooltip() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterHoverButton.CloseTooltip", this);
}
public partial class MeterHoverLabel : Label
{
    internal MeterTooltipModel model = new("", Array.Empty<string>(), "");
    internal Vector2 availableSize = new(420, 480);
    internal MeterTooltipContent? popup;
    public void UpdateTooltip(MeterTooltipModel value, Vector2 size) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterHoverLabel.UpdateTooltip", this, value, size);
    public override GodotObject _MakeCustomTooltip(string forText) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<GodotObject>("MeterHoverLabel._MakeCustomTooltip", this, forText);
    public void CloseTooltip() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("MeterHoverLabel.CloseTooltip", this);
}
