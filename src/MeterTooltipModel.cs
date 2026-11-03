namespace Based.Spires2DamageMeter;
public sealed record MeterTooltipModel(string Summary, IReadOnlyList<string> Entries, string Diagnostics)
{
    public TooltipCard? Card { get; init; }

    public static TooltipCard BuildCard(string player, string name, int metric, bool run, bool solo, decimal value, decimal team, Totals t, decimal pending, decimal poisonBonus, IEnumerable<SourceTotals> sources, IEnumerable<SupportTransfer> transfers, Func<string, string> names, string warning) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<TooltipCard>("MeterTooltipModel.BuildCard", player, name, metric, run, solo, value, team, t, pending, poisonBonus, sources, transfers, names, warning);
    public static bool? SoloForSession(string? session) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool?>("MeterTooltipModel.SoloForSession", session);
    public static string RowSummary(string player, int metric, bool run, decimal value, decimal team, bool solo, bool unattributed = false) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("MeterTooltipModel.RowSummary", player, metric, run, value, team, solo, unattributed);
    public bool ShowSources { get; init; } = true;

    public static string[] Labels => Reload.RuntimeCalls.Call<string[]>("MeterTooltipModel.Labels");
    public static string Description(int metric) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("MeterTooltipModel.Description", metric);
    public static string Detail(int metric, Totals t) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("MeterTooltipModel.Detail", metric, t);
    public string Render(int limit = 12) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("MeterTooltipModel.Render", this, limit);
    public static string Status(bool incomplete, bool partial) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("MeterTooltipModel.Status", incomplete, partial);
    public static IReadOnlyList<string> Sources(string player, int metric, IEnumerable<SourceTotals> sources, IEnumerable<SupportTransfer> transfers, Func<string, string> name) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<string>>("MeterTooltipModel.Sources", player, metric, sources, transfers, name);
}

public enum TooltipTone
{
    Neutral,
    Hp,
    Block,
    Bonus,
    MutedBlock
}

public sealed record TooltipNumber(decimal Value, TooltipTone Tone)
{
    public string Text => Reload.RuntimeCalls.Call<string>("TooltipNumber.Text", this);
}

public sealed record TooltipStat(string Label, TooltipNumber Number, string Section);
public sealed record TooltipSource(string Name, IReadOnlyList<TooltipNumber> Values);
public sealed record TooltipCard(string Title, string Scope, TooltipNumber Total, decimal? PartyShare, IReadOnlyList<TooltipStat> Stats, IReadOnlyList<string> Headers, IReadOnlyList<TooltipSource> Sources, string Note, string Warning);
