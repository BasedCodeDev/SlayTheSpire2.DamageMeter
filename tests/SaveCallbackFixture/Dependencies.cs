namespace Based.Spires2DamageMeter;
// Dependency substitutes only: callbacks and runtime dispatch compile from production source.
public record RunIdentity;
public record Attribution(int Version);
public record EncounterSnapshot(Attribution? Attribution);
public class RunStatisticsStore {public bool Partial;public string? Warning;}
public record SourceTotals;
public record SupportTransfer;
public class Totals;
public class CreditedDamage {public const int Version=1;}
public class EffectiveDamageLedger;
public class PersistencePayload;
public static class Probe {public const string Id="save-safety.fixture";public static void Emit(string key,object data){} }
