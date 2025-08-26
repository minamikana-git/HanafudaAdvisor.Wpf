using System.Collections.Generic;
using System.Linq;

namespace HanafudaAdvisor.Wpf.Models
{
    public sealed class GameState
    {
        public List<Card> MyHand { get; } = new();   // ©•ª‚ÌèD
        public List<Card> OppKnown { get; } = new(); // ‘Šè‚ÌŒöŠJî•ñi”»–¾Dj
        public List<Card> Field { get; } = new();    // ê
        public List<Card> MyPile { get; } = new();   // ©•ª‚ªæ‚Á‚½D
        public List<Card> OppPile { get; } = new();  // ‘Šè‚ªæ‚Á‚½D

        public IEnumerable<Card> Seen =>
            MyHand.Concat(OppKnown).Concat(Field).Concat(MyPile).Concat(OppPile);

        public IEnumerable<Card> Unknown =>
            Deck.Full.Except(Seen);

        public int RemainingOfMonth(int month) =>
            Deck.Full.Count(c => c.Month == month) - Seen.Count(c => c.Month == month);

        public GameState CloneShallow()
        {
            var g = new GameState();
            g.MyHand.AddRange(MyHand);
            g.OppKnown.AddRange(OppKnown);
            g.Field.AddRange(Field);
            g.MyPile.AddRange(MyPile);
            g.OppPile.AddRange(OppPile);
            return g;
        }
    }
}
