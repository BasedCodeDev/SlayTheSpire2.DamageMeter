using Godot;
namespace Based.Spires2DamageMeter.Hosting;

public partial class ReloadDriver : Node
{
    public override void _Process(double delta) => RuntimeHost.Pump(delta);
    public void RunDeferred(long id) => RuntimeHost.RunDeferred(id);
}
