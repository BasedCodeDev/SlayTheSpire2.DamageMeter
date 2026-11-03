using Godot;
namespace Based.Spires2DamageMeter;

public partial class ProbePanel : CanvasLayer
{
    private static ProbePanel? instance;
    private Label label = null!;
    private PanelContainer panel = null!;
    private OptionButton player = null!;
    private Label details = null!;
    private double elapsed;
    private bool dragging;
    public static void Ensure()
    {
        if (GodotObject.IsInstanceValid(instance)) return;
        instance = new(); ((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, instance);
    }
    public override void _Ready()
    {
        Layer = 100; panel = new PanelContainer { Position = new Vector2(20, 160) }; AddChild(panel);
        var layout = new VBoxContainer(); panel.AddChild(layout);
        label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore }; layout.AddChild(label);
        player = new OptionButton(); layout.AddChild(player);
        details = new Label { MouseFilter = Control.MouseFilterEnum.Ignore }; layout.AddChild(details);
        panel.GuiInput += e => { if (e is InputEventMouseButton b && b.ButtonIndex == MouseButton.Left) dragging = b.Pressed; if (dragging && e is InputEventMouseMotion m) panel.Position += m.Relative; };
    }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 }) panel.Visible = !panel.Visible;
        if (e is InputEventMouseButton { Pressed: false }) dragging = false;
    }
    public override void _Process(double delta)
    {
        elapsed += delta; if (elapsed < .1) return; elapsed = 0;
        label.Text = $"Based diagnostic {Probe.Version} — {Probe.Combat} — {Probe.Role}\n{Probe.Health}\n{Probe.OwnershipStatus}\nFight only | Poison/Doom are subsets of HP | F8 hide\nPlayer: HP / Block / Poison HP / Doom HP\n" + string.Join("\n", Probe.Meter.Rows.Select(kv => $"{Probe.Names.GetValueOrDefault(kv.Key, kv.Key)}: {kv.Value.Hp:0.##} / {kv.Value.Block:0.##} / {kv.Value.Poison:0.##} / {kv.Value.Doom:0.##}"));
        var keys = Probe.Meter.Rows.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var selection = player.Selected >= 0 ? player.GetItemMetadata(player.Selected).AsString() : "";
        if (player.ItemCount != keys.Length || Enumerable.Range(0, player.ItemCount).Any(i => player.GetItemMetadata(i).AsString() != keys[i]))
        {
            player.Clear(); foreach (var key in keys) { player.AddItem(Probe.Names.GetValueOrDefault(key, key)); player.SetItemMetadata(player.ItemCount - 1, key); }
            player.Select(Math.Max(0, Array.IndexOf(keys, selection)));
        }
        var selected = player.Selected >= 0 ? player.GetItemMetadata(player.Selected).AsString() : "";
        details.Text = "HP by application source (rounded):\n" + (Probe.Meter.Sources.TryGetValue(selected, out var sources) ? string.Join("\n", sources.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}: {s.Value:0.##}")) : "No HP damage yet");
    }
    public override void _ExitTree() { Probe.Flush(); instance = null; }
}
