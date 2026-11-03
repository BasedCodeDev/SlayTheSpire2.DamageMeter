namespace Based.Spires2DamageMeter.Reload;

/// <summary>Stable state objects call the currently active implementation synchronously.</summary>
public static class RuntimeCalls
{
    public static Func<string, object?[], object?>? Handler { get; set; }
    public static T Call<T>(string operation, params object?[] arguments)
        => (T)Call(operation, arguments)!;
    public static object? Call(string operation, params object?[] arguments)
        => (Handler ?? throw new InvalidOperationException("Damage meter runtime has not been loaded."))(operation, arguments);
}
