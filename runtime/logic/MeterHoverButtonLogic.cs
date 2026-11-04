using Godot;
using static Based.Spires2DamageMeter.MeterHoverButton;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime;

internal static class MeterHoverButtonLogic
{
    private static void CopyPalette(MeterHoverButton self)
    {
        foreach (var key in new[] { "bar_hp", "bar_bonus" })
        {
            if (self.HasMeta(key)) self.popup!.SetMeta(key, self.GetMeta(key));
            else if (self.popup!.HasMeta(key)) self.popup.RemoveMeta(key);
        }
    }
    public static void UpdateTooltip(MeterHoverButton self, MeterTooltipModel value, Vector2 size) { self.model = value; self.availableSize = size; if (GodotObject.IsInstanceValid(self.popup) && self.popup!.IsInsideTree()) { self.popup.Model = value; if (!self.popup.HasMeta("interactive")) self.popup.AvailableSize = size; CopyPalette(self); self.popup.Refresh(); } }
    public static MeterTooltipContent CreateContent(MeterHoverButton self) { self.popup = new() { Model = self.model, AvailableSize = self.availableSize }; CopyPalette(self); return self.popup; }
    // Godot calls this virtual even for empty tooltip text. Suppress that native popup.
    public static GodotObject? _MakeCustomTooltip(MeterHoverButton self, string forText)
        => string.IsNullOrEmpty(forText) ? null : CreateContent(self);
    public static void CloseTooltip(MeterHoverButton self) { PlayerTooltips.CloseFor(self); if (GodotObject.IsInstanceValid(self.popup) && self.popup!.IsInsideTree() && self.popup.GetWindow() != self.GetWindow()) self.popup.GetWindow().Hide(); }
}
