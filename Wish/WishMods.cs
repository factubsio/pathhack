namespace Pathhack.Wish;

public record struct WishMods
{
    public int? Count;
    public int? Potency;
    public BUC? Buc;
    public int? Charges;
    public List<(string Name, int? Quality)>? Runes;
}
