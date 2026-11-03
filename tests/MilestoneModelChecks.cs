using Based.Spires2DamageMeter;
namespace Checks;

public static class MilestoneModelChecks
{
    public static void Run(Action<bool, string> check)
    {
        var modifiers = new[] { new DamageModifier("vulnerable-1", ModifierKind.Multiply, 1.5m, true, new[] { new Share("support", "Vulnerable", 1) }) };
        var hit = Amplification.Calculate(10, 100, 0, modifiers);
        check(hit.Red == 10 && hit.OrangeHp == 5 && hit.Blue == 0, "orange replaces effective HP portion");
        hit = Amplification.Calculate(10, 8, 0, modifiers);
        check(hit.Actual.Hp == 8 && hit.OrangeHp == 0, "lethal cap gives no unnecessary amplification credit");
        hit = Amplification.Calculate(10, 100, 12, modifiers);
        check(hit.Blue == 10 && hit.OrangeBlock == 2 && hit.OrangeHp == 3 && hit.Red == 0, "amplification preserves HP/block destination");
        var transfers = Amplification.SupportTransfers("dealer", hit.HpProviders.Concat(hit.BlockProviders).ToArray());
        check(transfers.Values.Sum() == 0 && transfers["support"] == 5, "support transfers conserve team contribution");
        var accounting=new Accounting();var source=new SourceIdentity("card","strike","Strike");
        accounting.AddResolved(new(new object(),"Direct",hit.Actual.Hp,hit.Actual.Block,new[]{new Share("dealer","Strike",hit.Actual.Hp)}){Source=source});
        accounting.ApplyAmplification("dealer",hit,source);
        check(accounting.Rows["dealer"].SupportReceivedHp==3 && accounting.Rows["support"].SupportGivenBlock==2,"support preserves HP/block destination");
        check(accounting.Rows.Values.Sum(t=>t.Hp+t.Block-t.SupportReceived+t.SupportGiven)==15,"integrated contribution conservation");
        check(accounting.StructuredSources.Sum(s=>s.Hp+s.Block)==15 && accounting.StructuredSources.Sum(s=>s.SupportReceivedHp+s.SupportReceivedBlock)==5,"structured source totals reconcile");
        var shared=new Accounting();var stackOwners=new[]{new Share("A","poison A",6),new Share("B","poison B",4)};
        shared.AddResolved(new(new object(),"Poison",10,0,stackOwners));
        shared.ApplySharedAmplification("Poison",stackOwners,new(new(10,0),new(5,0),new[]{new Share("C","amplifier",5)},Array.Empty<Share>()));
        check(shared.Rows["A"].SupportReceivedHp==3 && shared.Rows["B"].SupportReceivedHp==2 && shared.Rows["C"].SupportGivenHp==5,"shared status amplification debits stack owners, not null dealer");
        check(Amplification.SupportTransfers("support", hit.HpProviders).Count == 0, "self amplification is not teammate support");
        var reduced = modifiers.Append(new DamageModifier("weak", ModifierKind.Multiply, .75m, true, Array.Empty<Share>())).ToArray();
        hit = Amplification.Calculate(10, 100, 0, reduced);
        check(hit.Actual.Hp == 11 && hit.Baseline.Hp == 7 && hit.OrangeHp == 4, "reductions and flooring remain in counterfactual");
        var capped = modifiers.Append(new DamageModifier("cap", ModifierKind.Cap, 1, true, Array.Empty<Share>())).ToArray();
        check(Amplification.Calculate(10, 100, 0, capped).OrangeHp == 0, "caps preserved");
        var combined = modifiers.Append(new DamageModifier("strength", ModifierKind.Add, 4, true, new[] { new Share("other", "Strength", 1) })).ToArray();
        hit = Amplification.Calculate(10, 100, 5, combined);
        check(hit.Blue + hit.Red + hit.OrangeHp + hit.OrangeBlock == hit.Actual.Hp + hit.Actual.Block, "overlapping modifier portions reconcile");
        var run = new RunIdentity("profile1-modded", "multiplayer", "start-1", "seed", "A,B");
        var hash = RunStatisticsStore.Fingerprint("{\"save_time\":1,\"num_reloads\":0,\"players\":[{\"hp\":20}],\"rng\":4}");
        check(hash == RunStatisticsStore.Fingerprint("{\"rng\":4,\"players\":[{\"hp\":20}],\"num_reloads\":4,\"save_time\":2}"), "checkpoint ignores incidental time/reload fields and key order");
        check(hash != RunStatisticsStore.Fingerprint("{\"players\":[{\"hp\":19}],\"rng\":4}"), "checkpoint gameplay changes cannot match");
        var directory = Path.Combine(AppContext.BaseDirectory, "checkpoint-checks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new RunStatisticsStore(directory);
            store.Completed["floor-1"] = new("floor-1", new() { ["A"] = new() { Hp = 10, AmplifiedHp = 5, SupportReceived = 5 } }, new() { ["A"] = new() { ["Strike"] = 10 } }) { Transfers=new(){new("B","A","Vulnerable",5,0)}, StructuredSources=new(){new(){Player="A",Source=source,Hp=10,SupportReceivedHp=5}} };
            store.Save(run, hash); store.Restore(run, hash);
            check(!store.Partial && store.Completed["floor-1"].Players["A"].AmplifiedHp == 5, "matching checkpoint restores exact accounting");
            check(store.Completed["floor-1"].Transfers.Single().Hp==5 && store.Completed["floor-1"].StructuredSources.Single().SupportReceivedHp==5,"checkpoint restores provider-recipient and structured source records");
            store.Save(run, hash);
            var file = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Single(); File.WriteAllText(file, "broken"); store.Restore(run, hash);
            check(store.Partial && store.Completed.Count == 1, "corrupt primary restores backup and warns");
            store.Restore(run with { Started = "start-2" }, hash); check(store.Partial && store.Completed.Count == 0, "same seed new run cannot inherit history");
            store.Restore(run, "other-checkpoint"); check(store.Partial && store.Completed.Count == 0, "later or unmatched checkpoint never restored");
        }
        finally
        {
            if (Path.GetFullPath(directory).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
