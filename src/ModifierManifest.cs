using System.Reflection;
using System.Security.Cryptography;
namespace Based.Spires2DamageMeter;

public sealed record ModifierManifestEntry(string Method,string ModuleId,string IlSha256,string Classification,bool ReadsIncomingAmount,IReadOnlyList<string> Dependencies,IReadOnlyList<string> ConcreteListeners,IReadOnlyList<string> RequiredCases)
{
    public string ValidationStatus => "pending";
    public string Adapter=>ModuleId==HpLossAdapterAudit.ModuleId && HpLossAdapterAudit.Hashes.Values.Contains(IlSha256)?"state_snapshot_with_callback_output_check":Classification=="redirect_adapter_required"?"actual_totals_only_amplification_unmeasured":"frozen_output_capture_requires_interaction_review";
}
public static class ModifierManifest
{
    public static readonly string[] Slots={"ModifyWeakMultiplier","ModifyVulnerableMultiplier","ModifyDamageAdditive","ModifyDamageMultiplicative","ModifyDamageCap","EnchantDamageAdditive","EnchantDamageMultiplicative","ModifyHpLostBeforeOsty","ModifyHpLostBeforeOstyLate","ModifyHpLostAfterOsty","ModifyHpLostAfterOstyLate","ModifyUnblockedDamageTarget"};
    // Resolve effective methods on concrete listeners, including inherited implementations.
    // The manifest is an auditable inventory: dependencies are direct IL references, not a transitive proof.
    public static IReadOnlyList<ModifierManifestEntry> Build(Type root)
    {
        var listeners=root.Assembly.GetTypes().Where(t=>root.IsAssignableFrom(t)&&!t.IsAbstract&&!t.ContainsGenericParameters).ToArray();
        return listeners.SelectMany(t=>t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>Slots.Contains(m.Name)&&!m.IsAbstract).Select(m=>(Type:t,Method:m)))
            .GroupBy(p=>(p.Method.Module,p.Method.MetadataToken)).OrderBy(g=>g.First().Method.DeclaringType!.FullName,StringComparer.Ordinal).ThenBy(g=>g.First().Method.Name,StringComparer.Ordinal)
            .Select(g=>
            {
                var m=g.First().Method;var body=m.GetMethodBody()?.GetILAsByteArray();var reads=!ModifierAudit.InputIndependent(m);
                var classification=m.DeclaringType==root?"base_default_review_required":m.Name=="ModifyUnblockedDamageTarget"?"redirect_adapter_required":m.Name.StartsWith("ModifyHpLost",StringComparison.Ordinal)?"hp_loss_adapter_required":reads?"input_adapter_required":"frozen_output_candidate";
                return new ModifierManifestEntry(m.DeclaringType!.FullName+"."+m, m.Module.ModuleVersionId.ToString(),body==null?"unavailable":Convert.ToHexString(SHA256.HashData(body)),classification,reads,ModifierAudit.Dependencies(m),g.Select(p=>p.Type.FullName!).OrderBy(n=>n,StringComparer.Ordinal).ToArray(),new[]{"active","inactive","reduction_and_cap","lethal_and_block","shared_provider","native_host_guest"});
            }).ToArray();
    }
}
