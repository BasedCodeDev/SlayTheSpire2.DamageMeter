using System.Reflection;
namespace Based.Spires2DamageMeter;

/// <summary>Discovery inventory, not a claim of implemented native adapters.</summary>
public static class ModifierCatalogue
{
    public const string GameVersion = "v0.107.1";
    public const string CoverageStatus = "Native trace connected; full modifier-interaction validation pending";
    public static readonly string[] DeclaredPowers = {
        "AccuracyPower", "CalcifyPower", "ColossusPower", "ConquerorPower", "CoveredPower", "DiamondDiademPower", "DoubleDamagePower", "FlankingPower", "FlutterPower", "GigantificationPower", "GuardedPower", "HangPower", "HardToKillPower", "IntangiblePower", "InterceptPower", "KnockdownPower", "LeadershipPower", "LethalityPower", "PhantomBladesPower", "ShrinkPower", "SlowPower", "SoarPower", "StrengthPower", "SurroundedPower", "TaintedPower", "TankPower", "TrackingPower", "VigorPower", "VulnerablePower", "WeakPower", "MockRevivePower"
    };
    public static string[] Discover(Type power) => power.Assembly.GetTypes().Where(t => power.IsAssignableFrom(t) && !t.IsAbstract && t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Any(m => m.Name.StartsWith("ModifyDamage", StringComparison.Ordinal))).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
}
