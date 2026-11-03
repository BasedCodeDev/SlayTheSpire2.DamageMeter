namespace Based.Spires2DamageMeter;

public sealed class SourceContext<T> where T : class
{
    private readonly AsyncLocal<T?> slot = new();
    public T? Current { get => slot.Value; set => slot.Value = value; }
    public async Task Complete(Task task, T? current, T? previous)
    { Current = current; try { await task; } finally { Current = previous; } }
    public async Task<TResult> Complete<TResult>(Task<TResult> task, T? current, T? previous)
    { Current = current; try { return await task; } finally { Current = previous; } }
}
