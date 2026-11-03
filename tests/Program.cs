using Based.Spires2DamageMeter;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
OwnershipChecks.Run(Check);
NormalizationChecks.Run(Check);
OutParameterChecks.Run(Check);
AdditiveChecks.Run(Check);
AttackMultiplierChecks.Run(Check);
MultiplierChainChecks.Run(Check);
AmplificationV2Checks.Run(Check);
TraceV5Checks.Run(Check);
ResultRegistryChecks.Run(Check);
TraceReportChecks.Run(Check);
ManifestChecks.Run(Check);
ReleaseEvidenceChecks.Run(Check);
HpLossChecks.Run(Check);
Checks.MilestoneModelChecks.Run(Check);
PowerChangeChecks.Run(Check);
RegistrationChecks.Run(Check);
var a = new Accounting();
var hit = new object();
a.Add(hit, "A", 10, 0, 10, "Direct");
Check(a.Rows["A"].Hp == 10, "overkill capped");
Check(!a.Add(hit, "A", 10, 0, 10, "Direct"), "duplicate identity rejected");
a.Reset(); a.Add(new(), "A", 10, 5, 5, "Direct");
Check(a.Rows["A"].Hp == 10 && a.Rows["A"].Block == 5, "HP and block separate");
a.Add(new(), "A", 10, 5, 5, "Direct"); Check(a.Rows["A"].Hp == 20, "identical distinct hits retained");
a.Reset(); a.Add(new(), "Unattributed", 5, 0, 7, "Poison"); a.Add(new(), "Unattributed", 3, 0, 0, "Poison");
Check(a.Rows["Unattributed"].Hp == 8 && a.Rows["Unattributed"].Poison == 8, "poison ticks are HP subsets and retain unknown owner");
a.Add(new(), "A", 8, 0, 0, "Doom"); Check(a.Rows["A"].Doom == 8 && a.Rows["A"].Hp == 8, "Doom execution remaining HP");
a.Add(new(), "A", 0, 10, 0, "Direct"); Check(a.Rows["A"].Hp == 8 && a.Rows["A"].Block == 10, "fully blocked hit");
var json = JsonSerializer.Serialize(a.Rows); using var doc = JsonDocument.Parse(json); Check(doc.RootElement.GetProperty("A").GetProperty("Hp").GetDecimal() == 8, "decimal JSON round trip");
a.Reset(); Check(a.Rows.Count == 0 && a.Add(hit, "A", 1, 0, 0, "Direct"), "combat reset clears deduplication");

if (args.Length > 0)
{
    var dir = Path.GetFullPath(args[0]);
    AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(dir, name.Name + ".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir, name.Name + ".dll")) : null;
    var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir, "sts2.dll"));
    HpLossChecks.Installed(asm,Check);
    OutParameterChecks.Production(asm,Check);
    DispatchChecks.Installed(asm,Check);
    AdditiveChecks.Installed(asm,Check);
    AttackMultiplierChecks.Installed(asm,Check);
    AmplificationV2Checks.Installed(asm,Check);
    Check(DamageHookContract.Resolve(asm).Count==3,"installed damage, HP-loss and redirection signatures validated before patching");
            Check(ModifierCatalogue.Discover(asm.GetType("MegaCrit.Sts2.Core.Models.PowerModel")!).SequenceEqual(ModifierCatalogue.DeclaredPowers.OrderBy(n=>n,StringComparer.Ordinal)), "modifier discovery inventory matches installed assembly");
    var targets = OwnershipHooks.ResolveGame(asm);
    foreach(var type in asm.GetTypes().Where(t=>asm.GetType("MegaCrit.Sts2.Core.Models.PowerModel")!.IsAssignableFrom(t) && !t.IsAbstract))
    foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>m.Name.StartsWith("ModifyDamage",StringComparison.Ordinal)))
        Check(ModifierAudit.InputIndependent(method), "frozen-output input audit: "+type.Name+"."+method.Name);
    Check(targets.Select(t => t.Name).SequenceEqual(new[] { "BeforePowerAmountChanged", "AfterPowerAmountChanged", "Apply", "ModifyAmount", "Remove" }), "installed-game ownership targets validated");
    foreach (var target in targets) Console.WriteLine("VALIDATED " + target);
}
