using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.PowerChangeTracker;

namespace Based.Spires2DamageMeter.Runtime;
internal static class PowerChangeTrackerLogic
{
    public static void Begin(PowerChangeTracker self, string operation, string key, decimal before, string? owner, string? source)
    {
        self.pending.Add(new(operation, key, Math.Max(0, before), owner, source));
    }

    public static PowerChange End(PowerChangeTracker self, string operation, string key, decimal after, string? owner, string? source)
    {
        after = Math.Max(0, after);
        var index = self.pending.FindLastIndex(p => p.Operation == operation && p.Key == key);
        if (index < 0)
        {
            var quality = self.observed.TryGetValue(key, out var previous) && previous == after ? "duplicate" : "unmatched";
            if (quality == "unmatched")
            {
                self.Unresolved++;
                self.Unknown++;
                self.ledger.Change(key, self.observed.GetValueOrDefault(key), after, null, null);
            }

            self.observed[key] = after;
            return new(operation, key, previous, after, null, null, quality, self.ledger.Snapshot(key, after));
        }

        var frame = self.pending[index];
        self.pending.RemoveAt(index);
        var before = Math.Max(0, frame.Before + frame.Accounted);
        var delta = after - before;
        foreach (var parent in self.pending.Where(p => p.Key == key))
            parent.Accounted += delta;
        var provider = owner ?? frame.Owner;
        var application = source ?? frame.Source;
        self.ledger.Change(key, before, after, provider, application);
        self.observed[key] = after;
        if (delta != 0)
        {
            self.Confirmed++;
            if (delta > 0 && provider == null)
                self.Unknown++;
        }

        return new(operation, key, before, after, provider, application, delta == 0 ? "no_change" : provider == null && delta > 0 ? "unknown_provider" : "confirmed", self.ledger.Snapshot(key, after));
    }

    public static IReadOnlyList<Share> Snapshot(PowerChangeTracker self, string key, decimal amount) => self.ledger.Snapshot(key, amount);
    public static void EndScope(PowerChangeTracker self, string operation)
    {
        self.Unresolved += self.pending.RemoveAll(p => p.Operation == operation);
    }

    public static void Remove(PowerChangeTracker self, string key)
    {
        self.pending.RemoveAll(p => p.Key == key);
        self.ledger.Change(key, self.observed.GetValueOrDefault(key), 0, null, null);
        self.observed.Remove(key);
    }

    public static void Clear(PowerChangeTracker self)
    {
        self.pending.Clear();
        self.observed.Clear();
        self.ledger.Clear();
        self.Confirmed = self.Unknown = self.Unresolved = 0;
    }
}
