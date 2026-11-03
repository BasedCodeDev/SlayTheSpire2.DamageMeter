using Based.Spires2DamageMeter.Reload;
namespace Based.Spires2DamageMeter.Runtime;

public sealed class RuntimeModule : IRuntimeModule
{
    public int ApiVersion => RuntimeContract.ApiVersion;
    public int StateVersion => RuntimeContract.StateVersion;
    public void Prepare() { }
    public object? Invoke(string operation, object?[] arguments)
    {
        if (operation == "MeterTooltipModel.Labels") return MeterTooltipModelLogic.GetLabels();
        if (operation == "TooltipNumber.Text") return ((TooltipNumber)arguments[0]!).Value.ToString("0.##");
        if (operation == "AdditiveSnapshot.Eligible") return AdditiveSnapshotLogic.Eligible((AdditiveSnapshot)arguments[0]!);
        if (operation == "AdditiveSnapshot.Expected") return AdditiveSnapshotLogic.Expected((AdditiveSnapshot)arguments[0]!);
        if (operation == "AttackMultiplierSnapshot.Expected") return AttackMultiplierSnapshotLogic.Expected((AttackMultiplierSnapshot)arguments[0]!);
        if (operation == "Ui.Prepare")
        {
            var panel = new MeterPanel();
            try
            {
                panel._Ready();
                var layout = (MeterPresentation)arguments[0]!;
                panel.metric.Select(panel.metric.GetItemIndex(layout.Metric));
                panel.scopeIndex = layout.Scope; panel.UpdateScopeToggle();
                panel.showUnattributed = layout.ShowUnattributed; panel.unattributedToggle.ButtonPressed = layout.ShowUnattributed; panel.UpdateUnattributedToggle();
                panel.expandedWidth = layout.Width;
                panel.panel.Size = new Godot.Vector2(layout.Width, 0);
                panel.ApplyCollapsedState(layout.Collapsed);
                panel.panel.Position = layout.Position;
                panel.panel.Visible = layout.Visible;
                panel.solo = layout.Solo;
                return panel;
            }
            catch { panel.Free(); throw; }
        }
        return Operations.Invoke(operation, arguments);
    }
    public void Dispose() { }
}
