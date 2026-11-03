namespace Based.Spires2DamageMeter;

public sealed class ResultRegistry<T> where T:class
{
    private readonly Dictionary<object,T> bindings=new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> conflicts=new(ReferenceEqualityComparer.Instance);
    public void Bind(object result,T context)
    { if(bindings.TryGetValue(result,out var previous)&&!ReferenceEquals(previous,context))conflicts.Add(result);else bindings[result]=context; }
    public T? Find(object result)=>conflicts.Contains(result)?null:bindings.GetValueOrDefault(result);
    public bool Conflicted(object result)=>conflicts.Contains(result);
    public int Count=>bindings.Count;
    public object[] Identities=>bindings.Keys.ToArray();
    public bool Contains(object result)=>bindings.ContainsKey(result);
    public void Clear(){bindings.Clear();conflicts.Clear();}
}
