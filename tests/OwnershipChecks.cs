using Based.Spires2DamageMeter;

public static class OwnershipChecks
{
    public static void Run(Action<bool, string> check)
    {
        var ledger = new StatusOwnershipLedger(); var effect = new object();
        ledger.Change(effect, 0, 6, "A", "card A"); ledger.Change(effect, 6, 10, "B", "card B");
        var snapshot = ledger.Snapshot(effect, 10);
        var split = StatusOwnershipLedger.Allocate(snapshot, 10);
        check(split.Single(s => s.Owner == "A").Amount == 6 && split.Single(s => s.Owner == "B").Amount == 4, "shared 6/4 damage");
        ledger.Change(effect, 10, 9, null, null);
        split = StatusOwnershipLedger.Allocate(ledger.Snapshot(effect, 9), 10);
        check(split[0].Amount == 6 && split[1].Amount == 4, "decay preserves proportions");
        check(snapshot.Sum(s => s.Amount) == 10, "snapshot immutable after decay");
        ledger.Clear(); ledger.Change(effect, 0, 5, "A", "apply"); ledger.Change(effect, 5, 10, "B", "double");
        split = StatusOwnershipLedger.Allocate(ledger.Snapshot(effect, 10), 8);
        check(split.All(s => s.Amount == 4), "Doom remaining HP split after doubling");
        var meter = new Accounting(); var id = new object();
        check(meter.AddResolved(new(id, "Doom", 8, 0, split)) && !meter.AddResolved(new(id, "Doom", 8, 0, split)), "allocated event deduplication");
        check(meter.Rows.Values.Sum(r => r.Hp) == 8 && meter.Sources.Values.Sum(v => v.Values.Sum()) == 8, "player/source totals reconcile");
        ledger.Clear(); ledger.Change(effect, 6, 10, "B", "add");
        split = StatusOwnershipLedger.Allocate(ledger.Snapshot(effect, 10), 5);
        check(split.Single(s => s.Owner == "B").Amount == 2 && split.Single(s => s.Owner == StatusOwnershipLedger.Unknown).Amount == 3, "known share retained with partial history and overkill cap");
        ledger.Change(effect, 10, 0, null, null); ledger.Change(effect, 0, 2, "C", "reapply");
        check(ledger.Snapshot(effect, 2).Single().Owner == "C", "removal/reapplication resets lifetime");
        ledger.Clear(); ledger.Change(effect, 0, 3, "A", "one"); ledger.Change(effect, 3, 6, "B", "two"); ledger.Change(effect, 6, 9, "C", "three");
        split = StatusOwnershipLedger.Allocate(ledger.Snapshot(effect, 9), 1);
        check(split.Sum(s => s.Amount) == 1 && split.SequenceEqual(StatusOwnershipLedger.Allocate(ledger.Snapshot(effect, 9).Reverse().ToArray(), 1)), "fractional residual exact and deterministic");
        var target = new object(); ledger.Transfer(effect, 9, target, 0, 3);
        check(ledger.Snapshot(effect, 6).Sum(s => s.Amount) == 6 && ledger.Snapshot(target, 3).Select(s => s.Owner).Distinct().Count() == 3, "explicit transfer preserves source ownership");
        check(ledger.Snapshot(target, 5).Any(s => s.Owner == StatusOwnershipLedger.Unknown && s.Amount == 2), "upward drift becomes unknown");
        check(ledger.Snapshot(target, 2).Sum(s => s.Amount) == 2, "downward drift reconciles");
        ledger.Clear(); check(ledger.Snapshot(effect, 0).Count == 0, "combat reset clears ledger");
        meter.Reset(); meter.AddResolved(new(new object(), "Poison", 1, 0, split)); meter.AddResolved(new(new object(), "Poison", 1, 0, split));
        check(meter.Rows.Values.Sum(v => v.Poison) == 2 && meter.Rows.Values.Sum(v => v.Hp) == 2, "repeated ticks count once each as HP subsets");
    }
}
