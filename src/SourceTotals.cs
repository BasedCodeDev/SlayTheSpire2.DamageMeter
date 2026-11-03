namespace Based.Spires2DamageMeter;

public sealed record SourceIdentity(string Kind,string ModelId,string DisplayName);
public sealed class SourceTotals
{
    public required string Player { get; init; }
    public required SourceIdentity Source { get; init; }
    public decimal Hp { get; set; }
    public decimal Block { get; set; }
    public decimal AmplifiedHp { get; set; }
    public decimal AmplifiedBlock { get; set; }
    public decimal SupportGivenHp { get; set; }
    public decimal SupportGivenBlock { get; set; }
    public decimal SupportReceivedHp { get; set; }
    public decimal SupportReceivedBlock { get; set; }
}
