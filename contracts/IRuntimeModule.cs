namespace Based.Spires2DamageMeter.Reload;

/// <summary>The permanent boundary. Implementations must not own background work.</summary>
public interface IRuntimeModule : IDisposable
{
    int ApiVersion { get; }
    int StateVersion { get; }
    void Prepare();
    object? Invoke(string operation, object?[] arguments);
}

public static class RuntimeContract
{
    public const int ApiVersion = 1;
    public const int StateVersion = 1;
}
