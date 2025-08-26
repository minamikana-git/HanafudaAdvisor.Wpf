using System.Collections.Generic;
using System.Linq;

namespace HanafudaAdvisor.Wpf.Models
{
    public sealed class GameState
    {
        public List<Card> MyHand { get; } = new();   // �����̎�D
        public List<Card> OppKnown { get; } = new(); // ����̌��J���i�����D�j
        public List<Card> Field { get; } = new();    // ��
        public List<Card> MyPile { get; } = new();   // ������������D
        public List<Card> OppPile { get; } = new();  // ���肪������D

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
