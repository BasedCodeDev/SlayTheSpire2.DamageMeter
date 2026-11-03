using Godot;
using static Based.Spires2DamageMeter.MeterHoverLabel;
using static Godot.Control;
namespace Based.Spires2DamageMeter.Runtime;

internal static class MeterHoverLabelLogic
{
    public static void UpdateTooltip(MeterHoverLabel self, MeterTooltipModel value, Vector2 size) { self.model = value; self.availableSize = size; if (GodotObject.IsInstanceValid(self.popup) && self.popup!.IsInsideTree()) { self.popup.Model = value; self.popup.AvailableSize = size; self.popup.Refresh(); } }
    public static GodotObject _MakeCustomTooltip(MeterHoverLabel self, string forText) { self.popup = new() { Model = self.model, AvailableSize = self.availableSize }; return self.popup; }
    public static void CloseTooltip(MeterHoverLabel self) { if (GodotObject.IsInstanceValid(self.popup) && self.popup!.IsInsideTree() && self.popup.GetWindow() != self.GetWindow()) self.popup.GetWindow().Hide(); }
}