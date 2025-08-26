using HanafudaAdvisor.Wpf.Models;

public sealed class GameState
{
    // Public info at any time
    public List<Card> MyHand { get; } = new(); // 8
    public List<Card> OppKnown { get; } = new(); // if any revealed
    public List<Card> Field { get; } = new(); // 8 at start
    public List<Card> MyPile { get; } = new();
    public List<Card> OppPile { get; } = new();
    public HashSet<Card> Discarded { get; } = new(); // dead/killed if variant


    public IEnumerable<Card> Seen => MyHand.Concat(OppKnown).Concat(Field).Concat(MyPile).Concat(OppPile).Concat(Discarded);
    public IEnumerable<Card> Unknown => Deck.Full.Except(Seen);


    public int RemainingOfMonth(int month)
    => Deck.Full.Count(c => c.Month == month) - Seen.Count(c => c.Month == month);


    public GameState CloneShallow()
    {
        var g = new GameState();
        g.MyHand.AddRange(MyHand);
        g.OppKnown.AddRange(OppKnown);
        g.Field.AddRange(Field);
        g.MyPile.AddRange(MyPile);
        g.OppPile.AddRange(OppPile);
        g.Discarded.UnionWith(Discarded);
        return g;
    }
}