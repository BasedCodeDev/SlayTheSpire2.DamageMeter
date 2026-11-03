using Based.Spires2DamageMeter.Reload;
using Godot;
namespace Based.Spires2DamageMeter.Runtime;

public sealed class RuntimeModule : IRuntimeModule
{
    public int ApiVersion => RuntimeContract.ApiVersion;
    public int StateVersion => RuntimeContract.StateVersion;
    public void Prepare() { }
    public object? Invoke(string operation, object?[] arguments)
    {
        if (operation == "calculate") return (int)arguments[0]! + 7;
        if (operation == "ui")
        {
            var control = new VBoxContainer();
            control.AddChild(new Label { Text = "Reload " + arguments[0] });
            return control;
        }
        throw new ArgumentException(operation);
    }
    public void Dispose() { }
}
