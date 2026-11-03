using Based.Spires2DamageMeter;
static class ResultRegistryChecks
{
    public static void Run(Action<bool,string> check)
    {
        var registry=new ResultRegistry<object>();var a=new object();var b=new object();var first=new object();var overflow=new object();
        registry.Bind(first,a);registry.Bind(overflow,a);registry.Bind(first,a);
        check(registry.Count==2&&ReferenceEquals(registry.Find(first),registry.Find(overflow)),"redirect and overflow keep a shared resolution after repeated binding");
        registry.Bind(first,b);check(registry.Conflicted(first)&&registry.Find(first)==null,"conflicting result ownership fails closed");
        var nested=new ResultRegistry<object>();nested.Bind(overflow,b);
        check(ReferenceEquals(nested.Find(overflow),b)&&ReferenceEquals(registry.Find(overflow),a),"nested command registry is isolated");
        registry.Clear();check(registry.Count==0&&!registry.Conflicted(first),"completion/reset releases identities and conflicts");
        check(registry.Find(new object())==null,"unbound result never guesses by target or amount");
    }
}
