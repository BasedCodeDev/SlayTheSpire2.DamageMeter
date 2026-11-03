using System.Text.Json;
using Based.Spires2DamageMeter;

public static class PowerChangeChecks
{
    public static void Run(Action<bool, string> check)
    {
        var tracker = new PowerChangeTracker();
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "doom-applications.json")));
        foreach (var e in fixture.RootElement.EnumerateArray())
        {
            string S(string n) => e.GetProperty(n).GetString()!;
            tracker.Begin(S("operation"), S("target"), e.GetProperty("before").GetDecimal(), S("owner"), S("source"));
            var change = tracker.End(S("operation"), S("target"), e.GetProperty("after").GetDecimal(), S("owner"), S("source"));
            check(change.Quality == "confirmed" && change.Shares.All(s => s.Owner == "B"), "live fixture " + S("operation"));
        }
        var shares = tracker.Snapshot("enemy-2:Doom", 30);
        check(shares.Single(s => s.Source == "Deathbringer").Amount == 21 && shares.Single(s => s.Source == "BlightStrike").Amount == 9, "fixture preserves original 21 and additional 9 by source");
        check(tracker.Snapshot("enemy-3:Doom", 21).Sum(s => s.Amount) == 21, "target isolation");
        check(tracker.End("blightstrike", "enemy-2:Doom", 30, "B", "BlightStrike").Quality == "duplicate", "duplicate notification ignored");
        tracker.Begin("shared", "enemy-2:Doom", 30, "A", "apply");
        tracker.End("shared", "enemy-2:Doom", 40, "A", "apply");
        check(tracker.Snapshot("enemy-2:Doom", 40).Where(s => s.Owner == "A").Sum(s => s.Amount) == 10, "second provider preserved");
        tracker.Begin("decay", "enemy-2:Doom", 40, null, null); tracker.End("decay", "enemy-2:Doom", 20, null, null);
        check(tracker.Snapshot("enemy-2:Doom", 20).Where(s => s.Owner == "A").Sum(s => s.Amount) == 5, "tracker decay proportional");
        tracker.Begin("blocked", "empty", 0, "A", "apply"); tracker.EndScope("blocked");
        check(tracker.Snapshot("empty", 0).Count == 0, "failed application creates no credit");
        tracker.Begin("outer", "nested", 0, "A", "parent"); tracker.Begin("inner", "nested", 0, "B", "child");
        tracker.End("inner", "nested", 2, "B", "child"); tracker.End("outer", "nested", 5, "A", "parent");
        check(tracker.Snapshot("nested", 5).Single(s => s.Owner == "A").Amount == 3 && tracker.Snapshot("nested", 5).Single(s => s.Owner == "B").Amount == 2, "nested child mutation not counted twice");
        tracker.Begin("zero", "construction", 0, null, null); tracker.End("zero", "construction", 0, null, null);
        check(tracker.Snapshot("construction", 0).Count == 0, "construction-only zero creates no entry");
        tracker.Begin("partial", "partial", 8, "A", "new"); tracker.End("partial", "partial", 10, "A", "new");
        check(tracker.Snapshot("partial", 10).Single(s => s.Owner == "A").Amount == 2, "partial history retains confirmed delta");
        check(tracker.End("missing", "missing", 9, "B", "unpaired").Shares.All(s => s.Owner == StatusOwnershipLedger.Unknown), "unmatched addition cannot award player credit");
        tracker.Remove("nested"); tracker.Begin("again", "nested", 0, "C", "new lifetime"); tracker.End("again", "nested", 4, "C", "new lifetime");
        check(tracker.Snapshot("nested", 4).All(s => s.Owner == "C"), "removal and reapplication reset lifetime");
        tracker.Begin("cancel", "cancel", 0, "A", "cancel"); tracker.EndScope("cancel");
        check(tracker.End("cancel", "cancel", 3, "A", "late").Quality == "unmatched", "cancelled operation has no reusable pending observation");
        tracker.Clear(); check(tracker.Confirmed == 0 && tracker.Unknown == 0 && tracker.Unresolved == 0, "tracker combat reset");
    }
}
