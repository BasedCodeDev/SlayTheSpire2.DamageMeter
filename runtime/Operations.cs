using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Saves;
using static Based.Spires2DamageMeter.NativeDamageCapture;
using static Based.Spires2DamageMeter.NativeMultiplierCapture;
using static Based.Spires2DamageMeter.RunSession;

namespace Based.Spires2DamageMeter.Runtime;

internal static class Operations
{
    public static object? Invoke(string operation, object?[] values)
    {
        switch (operation)
        {
            case "MeterPanel.Ensure":
                MeterPanelLogic.Ensure();
                return null;
            case "MeterPanel._Ready":
                MeterPanelLogic._Ready((MeterPanel)values[0]!);
                return null;
            case "MeterPanel.UpdateScopeToggle":
                MeterPanelLogic.UpdateScopeToggle((MeterPanel)values[0]!);
                return null;
            case "MeterPanel.UpdateUnattributedToggle":
                MeterPanelLogic.UpdateUnattributedToggle((MeterPanel)values[0]!);
                return null;
            case "MeterPanel.Saturate":
                return MeterPanelLogic.Saturate((Color)values[0]!, (float)values[1]!);
            case "MeterPanel.TooltipBounds":
                return MeterPanelLogic.TooltipBounds((MeterPanel)values[0]!, (Vector2)values[1]!);
            case "MeterPanel.ApplyCollapsedState":
                MeterPanelLogic.ApplyCollapsedState((MeterPanel)values[0]!, (bool)values[1]!);
                return null;
            case "MeterPanel.QueueHeightRefresh":
                MeterPanelLogic.QueueHeightRefresh((MeterPanel)values[0]!);
                return null;
            case "MeterPanel.Save":
                MeterPanelLogic.Save((MeterPanel)values[0]!);
                return null;
            case "MeterPanel._Input":
                MeterPanelLogic._Input((MeterPanel)values[0]!, (InputEvent)values[1]!);
                return null;
            case "MeterPanel._Process":
                MeterPanelLogic._Process((MeterPanel)values[0]!, (double)values[1]!);
                return null;
            case "MeterPanel.CloseTooltips":
                MeterPanelLogic.CloseTooltips((MeterPanel)values[0]!);
                return null;
            case "MeterPanel._ExitTree":
                MeterPanelLogic._ExitTree((MeterPanel)values[0]!);
                return null;
            case "MeterBar._Draw":
                MeterBarLogic._Draw((MeterBar)values[0]!);
                return null;
            case "MeterBar.WithAlpha":
                return MeterBarLogic.WithAlpha((Color)values[0]!, (float)values[1]!);
            case "MeterBar.CreateStyle":
                return MeterBarLogic.CreateStyle((Color)values[0]!, (int)values[1]!, (int)values[2]!);
            case "MeterBar.DrawRowText":
                MeterBarLogic.DrawRowText((MeterBar)values[0]!);
                return null;
            case "MeterBar.DrawOutlined":
                MeterBarLogic.DrawOutlined((MeterBar)values[0]!, (Font)values[1]!, (Vector2)values[2]!, (string)values[3]!, (int)values[4]!);
                return null;
            case "MeterPalette.Colour":
                return MeterPaletteLogic.Colour((TooltipTone)values[0]!);
            case "MeterTooltipContent.Label":
                return MeterTooltipContentLogic.Label((MeterTooltipContent)values[0]!, (string)values[1]!, (int)values[2]!);
            case "MeterTooltipContent.Set":
                MeterTooltipContentLogic.Set((MeterTooltipContent)values[0]!, (Label)values[1]!, (string)values[2]!, (TooltipTone)values[3]!);
                return null;
            case "MeterTooltipContent.Row":
                return MeterTooltipContentLogic.Row((MeterTooltipContent)values[0]!);
            case "MeterTooltipContent.Heading":
                MeterTooltipContentLogic.Heading((MeterTooltipContent)values[0]!, (string)values[1]!);
                return null;
            case "MeterTooltipContent._Ready":
                MeterTooltipContentLogic._Ready((MeterTooltipContent)values[0]!);
                return null;
            case "MeterTooltipContent.Build":
                MeterTooltipContentLogic.Build((MeterTooltipContent)values[0]!, (TooltipCard?)values[1]!);
                return null;
            case "MeterTooltipContent.Refresh":
                MeterTooltipContentLogic.Refresh((MeterTooltipContent)values[0]!);
                return null;
            case "MeterHoverButton.UpdateTooltip":
                MeterHoverButtonLogic.UpdateTooltip((MeterHoverButton)values[0]!, (MeterTooltipModel)values[1]!, (Vector2)values[2]!);
                return null;
            case "MeterHoverButton._MakeCustomTooltip":
                return MeterHoverButtonLogic._MakeCustomTooltip((MeterHoverButton)values[0]!, (string)values[1]!);
            case "MeterHoverButton.CloseTooltip":
                MeterHoverButtonLogic.CloseTooltip((MeterHoverButton)values[0]!);
                return null;
            case "MeterHoverLabel.UpdateTooltip":
                MeterHoverLabelLogic.UpdateTooltip((MeterHoverLabel)values[0]!, (MeterTooltipModel)values[1]!, (Vector2)values[2]!);
                return null;
            case "MeterHoverLabel._MakeCustomTooltip":
                return MeterHoverLabelLogic._MakeCustomTooltip((MeterHoverLabel)values[0]!, (string)values[1]!);
            case "MeterHoverLabel.CloseTooltip":
                MeterHoverLabelLogic.CloseTooltip((MeterHoverLabel)values[0]!);
                return null;
            case "Accounting.SourceRow":
                {
                    var result = AccountingLogic.SourceRow((Accounting)values[0]!, (string)values[1]!, (SourceIdentity)values[2]!);
                    return result;
                }

            case "Accounting.Reset":
                {
                    AccountingLogic.Reset((Accounting)values[0]!);
                    return null;
                }

            case "Accounting.ApplyAmplification":
                {
                    AccountingLogic.ApplyAmplification((Accounting)values[0]!, (string)values[1]!, (AmplificationResult)values[2]!, (SourceIdentity?)values[3]!);
                    return null;
                }

            case "Accounting.ApplySharedAmplification":
                {
                    AccountingLogic.ApplySharedAmplification((Accounting)values[0]!, (string)values[1]!, (IReadOnlyList<Share>)values[2]!, (AmplificationResult)values[3]!);
                    return null;
                }

            case "Accounting.AddResolved":
                {
                    var result = AccountingLogic.AddResolved((Accounting)values[0]!, (ResolvedDamage)values[1]!);
                    return result;
                }

            case "Accounting.Add":
                {
                    var result = AccountingLogic.Add((Accounting)values[0]!, (object)values[1]!, (string)values[2]!, (decimal)values[3]!, (decimal)values[4]!, (decimal)values[5]!, (string)values[6]!);
                    return result;
                }

            case "AdditiveSnapshot.CountPrior":
                {
                    var result = AdditiveSnapshotLogic.CountPrior((IEnumerable<AdditiveHistoryEntry>)values[0]!);
                    return result;
                }

            case "Amplification.Potential":
                {
                    var result = AmplificationLogic.Potential((decimal)values[0]!, (IReadOnlyList<DamageModifier>)values[1]!, (string?)values[2]!);
                    return result;
                }

            case "Amplification.Resolve":
                {
                    var result = AmplificationLogic.Resolve((decimal)values[0]!, (decimal)values[1]!, (decimal)values[2]!, (IReadOnlyList<DamageModifier>)values[3]!, (ISet<string>?)values[4]!, (IReadOnlyList<HpLossRule>?)values[5]!, (bool)values[6]!);
                    return result;
                }

            case "Amplification.Calculate":
                {
                    var result = AmplificationLogic.Calculate((decimal)values[0]!, (decimal)values[1]!, (decimal)values[2]!, (IReadOnlyList<DamageModifier>)values[3]!, (IReadOnlyList<HpLossRule>?)values[4]!, (bool)values[5]!);
                    return result;
                }

            case "Amplification.SupportTransfers":
                {
                    var result = AmplificationLogic.SupportTransfers((string)values[0]!, (IReadOnlyList<Share>)values[1]!);
                    return result;
                }

            case "AttackMultiplierSnapshot.CountStarted":
                {
                    var result = AttackMultiplierSnapshotLogic.CountStarted((IEnumerable<StartedAttack>)values[0]!);
                    return result;
                }

            case "CreditedDamage.Clear":
                {
                    CreditedDamageLogic.Clear((CreditedDamage)values[0]!);
                    return null;
                }

            case "CreditedDamage.Snapshot":
                {
                    var result = CreditedDamageLogic.Snapshot((CreditedDamage)values[0]!);
                    return result;
                }

            case "CreditedDamage.Merge":
                {
                    CreditedDamageLogic.Merge((CreditedDamage)values[0]!, (AttributionSnapshot)values[1]!);
                    return null;
                }

            case "CreditedDamage.Add":
                {
                    var result = CreditedDamageLogic.Add((CreditedDamage)values[0]!, (string)values[1]!, (string)values[2]!, (decimal)values[3]!, (decimal)values[4]!, (IReadOnlyList<Share>)values[5]!, (SourceIdentity?)values[6]!, (AmplificationResult?)values[7]!, (bool)values[8]!);
                    return result;
                }

            case "CreditedDamage.Rows":
                {
                    var result = CreditedDamageLogic.Rows((CreditedDamage)values[0]!, (IEnumerable<SupportTransfer>?)values[1]!);
                    return result;
                }

            case "CreditedDamage.Sources":
                {
                    var result = CreditedDamageLogic.Sources((CreditedDamage)values[0]!);
                    return result;
                }

            case "EffectiveCapture.Key":
                {
                    var result = EffectiveCaptureLogic.Key((Creature)values[0]!);
                    return result;
                }

            case "EffectiveCapture.Before":
                {
                    int arg1;
                    EffectiveCaptureLogic.Before((Creature)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "EffectiveCapture.Gained":
                {
                    EffectiveCaptureLogic.Gained((Creature)values[0]!, (int)values[1]!);
                    return null;
                }

            case "EffectiveCapture.AfterClearCompleted":
                {
                    EffectiveCaptureLogic.AfterClearCompleted((Creature)values[0]!, (int)values[1]!);
                    return null;
                }

            case "EffectiveDamageLedger.Clear":
                {
                    EffectiveDamageLedgerLogic.Clear((EffectiveDamageLedger)values[0]!);
                    return null;
                }

            case "EffectiveDamageLedger.Discard":
                {
                    EffectiveDamageLedgerLogic.Discard((EffectiveDamageLedger)values[0]!, (string)values[1]!);
                    return null;
                }

            case "EffectiveDamageLedger.Finish":
                {
                    EffectiveDamageLedgerLogic.Finish((EffectiveDamageLedger)values[0]!);
                    return null;
                }

            case "EffectiveDamageLedger.Add":
                {
                    EffectiveDamageLedgerLogic.Add((EffectiveDamageLedger)values[0]!, (string)values[1]!, (string?)values[2]!, (IReadOnlyList<DamageCredit>)values[3]!, (bool?)values[4]!);
                    return null;
                }

            case "EffectiveDamageLedger.Snapshot":
                {
                    var result = EffectiveDamageLedgerLogic.Snapshot((EffectiveDamageLedger)values[0]!);
                    return result;
                }

            case "EffectiveDamageLedger.Merge":
                {
                    EffectiveDamageLedgerLogic.Merge((EffectiveDamageLedger)values[0]!, (EffectiveSnapshot)values[1]!);
                    return null;
                }

            case "EffectiveDamageLedger.Ledger":
                {
                    var result = EffectiveDamageLedgerLogic.Ledger((EffectiveDamageLedger)values[0]!);
                    return result;
                }

            case "EffectiveDamageLedger.PendingFor":
                {
                    var result = EffectiveDamageLedgerLogic.PendingFor((EffectiveDamageLedger)values[0]!, (string)values[1]!);
                    return result;
                }

            case "EngineDamage.Normalize":
                {
                    var result = EngineDamageLogic.Normalize((decimal)values[0]!, (decimal)values[1]!, (decimal)values[2]!);
                    return result;
                }

            case "TraceCoverage.Record":
                {
                    TraceCoverageLogic.Record((TraceCoverage)values[0]!, (bool)values[1]!, (string)values[2]!);
                    return null;
                }

            case "TraceCoverage.Clear":
                {
                    TraceCoverageLogic.Clear((TraceCoverage)values[0]!);
                    return null;
                }

            case "TraceCoverage.IncomingOutcome":
                {
                    var result = TraceCoverageLogic.IncomingOutcome((EngineDamage)values[0]!, (bool)values[1]!);
                    return result;
                }

            case "HpLossRule.Apply":
                {
                    var result = HpLossRuleLogic.Apply((HpLossRule)values[0]!, (decimal)values[1]!);
                    return result;
                }

            case "MeterTooltipModel.BuildCard":
                {
                    var result = MeterTooltipModelLogic.BuildCard((string)values[0]!, (string)values[1]!, (int)values[2]!, (bool)values[3]!, (bool)values[4]!, (decimal)values[5]!, (decimal)values[6]!, (Totals)values[7]!, (decimal)values[8]!, (decimal)values[9]!, (IEnumerable<SourceTotals>)values[10]!, (IEnumerable<SupportTransfer>)values[11]!, (Func<string, string>)values[12]!, (string)values[13]!);
                    return result;
                }

            case "MeterTooltipModel.SoloForSession":
                {
                    var result = MeterTooltipModelLogic.SoloForSession((string?)values[0]!);
                    return result;
                }

            case "MeterTooltipModel.RowSummary":
                {
                    var result = MeterTooltipModelLogic.RowSummary((string)values[0]!, (int)values[1]!, (bool)values[2]!, (decimal)values[3]!, (decimal)values[4]!, (bool)values[5]!, (bool)values[6]!);
                    return result;
                }

            case "MeterTooltipModel.Description":
                {
                    var result = MeterTooltipModelLogic.Description((int)values[0]!);
                    return result;
                }

            case "MeterTooltipModel.Detail":
                {
                    var result = MeterTooltipModelLogic.Detail((int)values[0]!, (Totals)values[1]!);
                    return result;
                }

            case "MeterTooltipModel.Render":
                {
                    var result = MeterTooltipModelLogic.Render((MeterTooltipModel)values[0]!, (int)values[1]!);
                    return result;
                }

            case "MeterTooltipModel.Status":
                {
                    var result = MeterTooltipModelLogic.Status((bool)values[0]!, (bool)values[1]!);
                    return result;
                }

            case "MeterTooltipModel.Sources":
                {
                    var result = MeterTooltipModelLogic.Sources((string)values[0]!, (int)values[1]!, (IEnumerable<SourceTotals>)values[2]!, (IEnumerable<SupportTransfer>)values[3]!, (Func<string, string>)values[4]!);
                    return result;
                }

            case "ModifierCapturePatch.Prefix":
                {
                    object? arg3;
                    ModifierCapturePatchLogic.Prefix((AbstractModel)values[0]!, (MethodBase)values[1]!, (object[])values[2]!, out arg3);
                    values[3] = arg3;
                    return null;
                }

            case "ModifierCapturePatch.Postfix":
                {
                    ModifierCapturePatchLogic.Postfix((AbstractModel)values[0]!, (MethodBase)values[1]!, (decimal)values[2]!, (object?)values[3]!);
                    return null;
                }

            case "ModifierCapturePatch.Finalizer":
                {
                    var result = ModifierCapturePatchLogic.Finalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "ModifierDispatch.Select":
                {
                    var result = ModifierDispatchLogic.Select((Type)values[0]!, (MethodBase)values[1]!);
                    return result;
                }

            case "MultiplierChain.Evaluate":
                {
                    var result = MultiplierChainLogic.Evaluate((MultiplierChain)values[0]!, (ISet<string>?)values[1]!);
                    return result;
                }

            case "MultiplierChain.Reconciles":
                {
                    var result = MultiplierChainLogic.Reconciles((MultiplierChain)values[0]!, (decimal)values[1]!);
                    return result;
                }

            case "MultiplierChain.RootEligible":
                {
                    var result = MultiplierChainLogic.RootEligible((bool)values[0]!, (bool)values[1]!, (bool)values[2]!, (bool)values[3]!);
                    return result;
                }

            case "MultiplierChain.ChildEligible":
                {
                    var result = MultiplierChainLogic.ChildEligible((string)values[0]!, (bool)values[1]!, (bool)values[2]!, (bool)values[3]!, (bool)values[4]!);
                    return result;
                }

            case "NativeDamageCapture.Observe":
                {
                    string arg1;
                    var result = NativeDamageCaptureLogic.Observe((object)values[0]!, out arg1);
                    values[1] = arg1;
                    return result;
                }

            case "NativeDamageCapture.Bypasses":
                {
                    var result = NativeDamageCaptureLogic.Bypasses((DamageResult)values[0]!);
                    return result;
                }

            case "NativeDamageCapture.Reset":
                {
                    NativeDamageCaptureLogic.Reset();
                    return null;
                }

            case "NativeDamageCapture.DamagePrefix":
                {
                    object? arg0;
                    NativeDamageCaptureLogic.DamagePrefix(out arg0);
                    values[0] = arg0;
                    return null;
                }

            case "NativeDamageCapture.BlockPrefix":
                {
                    object? arg1;
                    NativeDamageCaptureLogic.BlockPrefix((Creature)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "NativeDamageCapture.BlockPostfix":
                {
                    NativeDamageCaptureLogic.BlockPostfix((decimal)values[0]!, (object?)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.ResultConstructed":
                {
                    NativeDamageCaptureLogic.ResultConstructed((DamageResult)values[0]!);
                    return null;
                }

            case "NativeDamageCapture.HpReturned":
                {
                    NativeDamageCaptureLogic.HpReturned((DamageResult)values[0]!);
                    return null;
                }

            case "NativeDamageCapture.Bind":
                {
                    NativeDamageCaptureLogic.Bind((DamageResult)values[0]!);
                    return null;
                }

            case "NativeDamageCapture.DamageFinalizer":
                {
                    var result = NativeDamageCaptureLogic.DamageFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "NativeDamageCapture.ModifyPrefix":
                {
                    object? arg1;
                    NativeDamageCaptureLogic.ModifyPrefix((object[])values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "NativeDamageCapture.ModifyPostfix":
                {
                    NativeDamageCaptureLogic.ModifyPostfix((decimal)values[0]!, (object?)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.ModifyFinalizer":
                {
                    var result = NativeDamageCaptureLogic.ModifyFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "NativeDamageCapture.MarkUnsupported":
                {
                    NativeDamageCaptureLogic.MarkUnsupported((string)values[0]!);
                    return null;
                }

            case "NativeDamageCapture.RecordMultiplier":
                {
                    NativeDamageCaptureLogic.RecordMultiplier((AttackMultiplierSnapshot)values[0]!, (decimal)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.RecordAdditive":
                {
                    NativeDamageCaptureLogic.RecordAdditive((AdditiveSnapshot)values[0]!, (decimal)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.ModifierPostfix":
                {
                    NativeDamageCaptureLogic.ModifierPostfix((AbstractModel)values[0]!, (MethodBase)values[1]!, (decimal)values[2]!, (object?)values[3]!, (AdditiveSnapshot?)values[4]!, (AttackMultiplierSnapshot?)values[5]!);
                    return null;
                }

            case "NativeDamageCapture.Pending":
                {
                    var result = NativeDamageCaptureLogic.Pending((Creature)values[0]!);
                    return result;
                }

            case "NativeDamageCapture.HpPhasePrefix":
                {
                    object? arg1;
                    NativeDamageCaptureLogic.HpPhasePrefix((Creature)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "NativeDamageCapture.HpPhasePostfix":
                {
                    NativeDamageCaptureLogic.HpPhasePostfix((Creature)values[0]!, (decimal)values[1]!, (HpLossHookPhase)values[2]!, (decimal)values[3]!, (object?)values[4]!);
                    return null;
                }

            case "NativeDamageCapture.HpPhaseFinalizer":
                {
                    var result = NativeDamageCaptureLogic.HpPhaseFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "NativeDamageCapture.HpModifierPrefix":
                {
                    object? arg3;
                    NativeDamageCaptureLogic.HpModifierPrefix((AbstractModel)values[0]!, (MethodBase)values[1]!, (object[])values[2]!, out arg3);
                    values[3] = arg3;
                    return null;
                }

            case "NativeDamageCapture.HpModifierPostfix":
                {
                    NativeDamageCaptureLogic.HpModifierPostfix((decimal)values[0]!, (object?)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.RedirectPostfix":
                {
                    NativeDamageCaptureLogic.RedirectPostfix((object[])values[0]!, (Creature)values[1]!);
                    return null;
                }

            case "NativeDamageCapture.Resolve":
                {
                    var result = NativeDamageCaptureLogic.Resolve((Creature)values[0]!, (DamageResult)values[1]!, (string)values[2]!);
                    return result;
                }

            case "NativeMultiplierCapture.Postfix":
                {
                    NativeMultiplierCaptureLogic.Postfix((AbstractModel)values[0]!, (MethodBase)values[1]!, (decimal)values[2]!, (object?)values[3]!);
                    return null;
                }

            case "NativeMultiplierCapture.Prefix":
                {
                    object? arg3;
                    NativeMultiplierCaptureLogic.Prefix((AbstractModel)values[0]!, (MethodBase)values[1]!, (object[])values[2]!, out arg3);
                    values[3] = arg3;
                    return null;
                }

            case "NativeMultiplierCapture.Add":
                {
                    NativeMultiplierCaptureLogic.Add((Frame)values[0]!, (AbstractModel?)values[1]!);
                    return null;
                }

            case "NativeMultiplierCapture.Dynamic":
                {
                    var result = NativeMultiplierCaptureLogic.Dynamic((PowerModel)values[0]!, (string)values[1]!);
                    return result;
                }

            case "NativeMultiplierCapture.Finish":
                {
                    var result = NativeMultiplierCaptureLogic.Finish((object?)values[0]!, (decimal)values[1]!);
                    return result;
                }

            case "NativeMultiplierCapture.Finalizer":
                {
                    var result = NativeMultiplierCaptureLogic.Finalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "NativeMultiplierCapture.ChildPrefix":
                {
                    object? arg3;
                    NativeMultiplierCaptureLogic.ChildPrefix((AbstractModel)values[0]!, (MethodInfo)values[1]!, (object[])values[2]!, out arg3);
                    values[3] = arg3;
                    return null;
                }

            case "NativeMultiplierCapture.ChildPostfix":
                {
                    NativeMultiplierCaptureLogic.ChildPostfix((decimal)values[0]!, (object?)values[1]!);
                    return null;
                }

            case "PersistencePayload.Capture":
                {
                    PersistencePayloadLogic.Capture((PersistencePayload)values[0]!, (string)values[1]!, (string)values[2]!);
                    return null;
                }

            case "PersistencePayload.Identity":
                {
                    var result = PersistencePayloadLogic.Identity((string)values[0]!, (string)values[1]!);
                    return result;
                }

            case "PowerChangeTracker.Begin":
                {
                    PowerChangeTrackerLogic.Begin((PowerChangeTracker)values[0]!, (string)values[1]!, (string)values[2]!, (decimal)values[3]!, (string?)values[4]!, (string?)values[5]!);
                    return null;
                }

            case "PowerChangeTracker.End":
                {
                    var result = PowerChangeTrackerLogic.End((PowerChangeTracker)values[0]!, (string)values[1]!, (string)values[2]!, (decimal)values[3]!, (string?)values[4]!, (string?)values[5]!);
                    return result;
                }

            case "PowerChangeTracker.Snapshot":
                {
                    var result = PowerChangeTrackerLogic.Snapshot((PowerChangeTracker)values[0]!, (string)values[1]!, (decimal)values[2]!);
                    return result;
                }

            case "PowerChangeTracker.EndScope":
                {
                    PowerChangeTrackerLogic.EndScope((PowerChangeTracker)values[0]!, (string)values[1]!);
                    return null;
                }

            case "PowerChangeTracker.Remove":
                {
                    PowerChangeTrackerLogic.Remove((PowerChangeTracker)values[0]!, (string)values[1]!);
                    return null;
                }

            case "PowerChangeTracker.Clear":
                {
                    PowerChangeTrackerLogic.Clear((PowerChangeTracker)values[0]!);
                    return null;
                }

            case "Probe.Get":
                {
                    var result = ProbeLogic.Get((object?)values[0]!, (string)values[1]!);
                    return result;
                }

            case "Probe.Arg":
                {
                    var result = ProbeLogic.Arg((MethodBase)values[0]!, (object[])values[1]!, (string)values[2]!);
                    return result;
                }

            case "Probe.Owner":
                {
                    var result = ProbeLogic.Owner((object?)values[0]!);
                    return result;
                }

            case "Probe.Entity":
                {
                    var result = ProbeLogic.Entity((object?)values[0]!);
                    return result;
                }

            case "Probe.HookPrefix":
                {
                    ProbeLogic.HookPrefix((MethodBase)values[0]!, (object[])values[1]!);
                    return null;
                }

            case "Probe.HandleHook":
                {
                    ProbeLogic.HandleHook((MethodBase)values[0]!, (object[])values[1]!);
                    return null;
                }

            case "Probe.EndCombatCompleted":
                {
                    ProbeLogic.EndCombatCompleted();
                    return null;
                }

            case "Probe.PowerOwnership":
                {
                    var result = ProbeLogic.PowerOwnership((PowerModel)values[0]!);
                    return result;
                }

            case "Probe.Ownership":
                {
                    var result = ProbeLogic.Ownership((PowerModel)values[0]!);
                    return result;
                }

            case "Probe.PowerKey":
                {
                    var result = ProbeLogic.PowerKey((PowerModel)values[0]!);
                    return result;
                }

            case "Probe.BeforePowerPrefix":
                {
                    ProbeLogic.BeforePowerPrefix((PowerModel)values[0]!, (Creature)values[1]!, (Creature?)values[2]!, (CardModel?)values[3]!);
                    return null;
                }

            case "Probe.AfterPowerPrefix":
                {
                    ProbeLogic.AfterPowerPrefix((PowerModel)values[0]!, (Creature?)values[1]!, (CardModel?)values[2]!);
                    return null;
                }

            case "Probe.SourcePrefix":
                {
                    object? arg2;
                    ProbeLogic.SourcePrefix((MethodBase)values[0]!, (object[])values[1]!, out arg2);
                    values[2] = arg2;
                    return null;
                }

            case "Probe.SourceFinalizer":
                {
                    var result = ProbeLogic.SourceFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "Probe.RemovePrefix":
                {
                    string arg1;
                    ProbeLogic.RemovePrefix((PowerModel)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "Probe.DoomPrefix":
                {
                    object arg1;
                    ProbeLogic.DoomPrefix((IEnumerable<Creature>)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "Probe.CleanDoom":
                {
                    ProbeLogic.CleanDoom((object)values[0]!);
                    return null;
                }

            case "Probe.DoomFinalizer":
                {
                    var result = ProbeLogic.DoomFinalizer((Exception?)values[0]!, (object)values[1]!);
                    return result;
                }

            case "Probe.PoisonPrefix":
                {
                    PowerModel? arg1;
                    ProbeLogic.PoisonPrefix((PoisonPower)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "Probe.PoisonFinalizer":
                {
                    var result = ProbeLogic.PoisonFinalizer((Exception?)values[0]!, (PowerModel?)values[1]!);
                    return result;
                }

            case "RunSession.AttachState":
                {
                    RunSessionLogic.AttachState((object?)values[0]!);
                    return null;
                }

            case "RunSession.StateKey":
                {
                    var result = RunSessionLogic.StateKey((object)values[0]!);
                    return result;
                }

            case "RunSession.VisibleEncounters":
                {
                    var result = RunSessionLogic.VisibleEncounters();
                    return result;
                }

            case "RunSession.SourceBreakdowns":
                {
                    var result = RunSessionLogic.SourceBreakdowns();
                    return result;
                }

            case "RunSession.SupportTransfers":
                {
                    var result = RunSessionLogic.SupportTransfers();
                    return result;
                }

            case "RunSession.CreditedRun":
                {
                    var result = RunSessionLogic.CreditedRun();
                    return result;
                }

            case "RunSession.EffectiveRun":
                {
                    var result = RunSessionLogic.EffectiveRun();
                    return result;
                }

            case "RunSession.CreditedTransfers":
                {
                    var result = RunSessionLogic.CreditedTransfers();
                    return result;
                }

            case "RunSession.Capture":
                {
                    var result = RunSessionLogic.Capture();
                    return result;
                }

            case "RunSession.Commit":
                {
                    RunSessionLogic.Commit();
                    return null;
                }

            case "RunSession.RunSources":
                {
                    var result = RunSessionLogic.RunSources();
                    return result;
                }

            case "RunSession.RunRows":
                {
                    var result = RunSessionLogic.RunRows();
                    return result;
                }

            case "RunSession.Error":
                {
                    RunSessionLogic.Error((Exception)values[0]!);
                    return null;
                }

            case "RunSession.SavePrefix":
                {
                    object? arg1;
                    RunSessionLogic.SavePrefix((SerializableRun)values[0]!, out arg1);
                    values[1] = arg1;
                    return null;
                }

            case "RunSession.WritePayload":
                {
                    RunSessionLogic.WritePayload((string)values[0]!, (byte[])values[1]!);
                    return null;
                }

            case "RunSession.SaveFinalizer":
                {
                    var result = RunSessionLogic.SaveFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "RunSession.Publish":
                {
                    RunSessionLogic.Publish((SaveOperation)values[0]!);
                    return null;
                }

            case "RunSession.LoadPrefix":
                {
                    object? arg0;
                    RunSessionLogic.LoadPrefix(out arg0);
                    values[0] = arg0;
                    return null;
                }

            case "RunSession.ReadPayload":
                {
                    RunSessionLogic.ReadPayload((string)values[0]!, (string)values[1]!);
                    return null;
                }

            case "RunSession.LoadPostfix":
                {
                    RunSessionLogic.LoadPostfix((ReadSaveResult<SerializableRun>)values[0]!, (object?)values[1]!);
                    return null;
                }

            case "RunSession.LoadFinalizer":
                {
                    var result = RunSessionLogic.LoadFinalizer((Exception?)values[0]!, (object?)values[1]!);
                    return result;
                }

            case "RunStatisticsStore.MarkPartial":
                {
                    RunStatisticsStoreLogic.MarkPartial((RunStatisticsStore)values[0]!, (string)values[1]!);
                    return null;
                }

            case "RunStatisticsStore.Hash":
                {
                    var result = RunStatisticsStoreLogic.Hash((string)values[0]!);
                    return result;
                }

            case "RunStatisticsStore.PayloadFingerprint":
                {
                    var result = RunStatisticsStoreLogic.PayloadFingerprint((string)values[0]!);
                    return result;
                }

            case "RunStatisticsStore.Fingerprint":
                {
                    var result = (values.Length == 1 ? RunStatisticsStoreLogic.Fingerprint((string)values[0]!) : RunStatisticsStoreLogic.Fingerprint((string)values[0]!, (bool)values[1]!));
                    return result;
                }

            case "RunStatisticsStore.PathFor":
                {
                    var result = RunStatisticsStoreLogic.PathFor((RunStatisticsStore)values[0]!, (RunIdentity)values[1]!, (string)values[2]!);
                    return result;
                }

            case "RunStatisticsStore.Save":
                {
                    RunStatisticsStoreLogic.Save((RunStatisticsStore)values[0]!, (RunIdentity)values[1]!, (string)values[2]!);
                    return null;
                }

            case "RunStatisticsStore.Restore":
                {
                    RunStatisticsStoreLogic.Restore((RunStatisticsStore)values[0]!, (RunIdentity)values[1]!, (string)values[2]!);
                    return null;
                }

            case "StatusOwnershipLedger.Clear":
                {
                    StatusOwnershipLedgerLogic.Clear((StatusOwnershipLedger)values[0]!);
                    return null;
                }

            case "StatusOwnershipLedger.Snapshot":
                {
                    var result = StatusOwnershipLedgerLogic.Snapshot((StatusOwnershipLedger)values[0]!, (object)values[1]!, (decimal)values[2]!);
                    return result;
                }

            case "StatusOwnershipLedger.Change":
                {
                    StatusOwnershipLedgerLogic.Change((StatusOwnershipLedger)values[0]!, (object)values[1]!, (decimal)values[2]!, (decimal)values[3]!, (string?)values[4]!, (string?)values[5]!);
                    return null;
                }

            case "StatusOwnershipLedger.Transfer":
                {
                    StatusOwnershipLedgerLogic.Transfer((StatusOwnershipLedger)values[0]!, (object)values[1]!, (decimal)values[2]!, (object)values[3]!, (decimal)values[4]!, (decimal)values[5]!);
                    return null;
                }

            case "StatusOwnershipLedger.Reconcile":
                {
                    StatusOwnershipLedgerLogic.Reconcile((StatusOwnershipLedger)values[0]!, (object)values[1]!, (decimal)values[2]!);
                    return null;
                }

            case "StatusOwnershipLedger.Allocate":
                {
                    var result = StatusOwnershipLedgerLogic.Allocate((IReadOnlyList<Share>)values[0]!, (decimal)values[1]!);
                    return result;
                }

            default:
                throw new System.MissingMethodException(operation);
        }
    }
}
