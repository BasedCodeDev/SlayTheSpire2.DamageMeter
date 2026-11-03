using Based.Spires2DamageMeter;
static class ManifestChecks
{
    public static void Run(Action<bool,string> check)
    {
        var manifest=ModifierManifest.Build(typeof(Model));
        var inherited=manifest.Single(e=>e.Method.Contains(nameof(Parent)+"."));
        check(inherited.ConcreteListeners.Any(n=>n.EndsWith("Child")) && inherited.ConcreteListeners.Any(n=>n.EndsWith("Parent")),"manifest inventories inherited effective listeners");
        check(!inherited.ReadsIncomingAmount && inherited.Classification=="frozen_output_candidate" && inherited.ValidationStatus=="pending","input independence alone cannot mark a modifier validated");
        var dependent=manifest.Single(e=>e.Method.Contains(nameof(Dependent)+"."));
        check(dependent.ReadsIncomingAmount && dependent.Classification=="input_adapter_required","input-dependent modifiers require explicit adapters");
        check(manifest.All(e=>e.IlSha256.Length==64 && e.RequiredCases.Contains("active") && e.RequiredCases.Contains("inactive")),"manifest records method hashes and active/inactive obligations");
        check(!ModifierAudit.InputIndependent(typeof(MissingParameter).GetMethod(nameof(MissingParameter.ModifyDamageAdditive))!),"missing input parameter cannot silently pass the input audit");
    }
    abstract class Model{public virtual decimal ModifyDamageAdditive(decimal amount)=>0;}
    class Parent:Model{public override decimal ModifyDamageAdditive(decimal amount)=>decimal.One;}
    class Child:Parent{}
    class Dependent:Model{public override decimal ModifyDamageAdditive(decimal amount)=>amount/2;}
    class MissingParameter{public decimal ModifyDamageAdditive()=>0;}
}
