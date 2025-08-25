using System.Collections.Generic;
using System.Linq;

namespace HanafudaAdvisor.Wpf.Models
{
    public static class Deck
    {
        public static IReadOnlyList<Card> Full { get; } = Build();
        public static IEnumerable<int> Months => Enumerable.Range(1, 12);
        public static IEnumerable<CardType> Types => new[] { CardType.Hikari, CardType.Tane, CardType.Tanzaku, CardType.Kasu };

        private static List<Card> Build()
        {
            var list = new List<Card>(48);

            // Base 4 cards per month
            for (int m = 1; m <= 12; m++)
            {
                list.Add(new Card(m, CardType.Tanzaku, $"M{m} Tanzaku"));
                list.Add(new Card(m, CardType.Tane, $"M{m} Tane"));
                list.Add(new Card(m, CardType.Kasu, $"M{m} Kasu1"));
                list.Add(new Card(m, CardType.Kasu, $"M{m} Kasu2"));
            }

            // Named/specials & flags
            Replace(1, CardType.Hikari, name: "Pine-Crane", redPoem: true);
            Replace(2, CardType.Tanzaku, name: "Plum Ribbon", redPoem: true);
            Replace(3, CardType.Hikari, name: "Cherry Curtain", redPoem: true);
            Replace(6, CardType.Tanzaku, name: "Peony Blue Ribbon", blueRibbon: true);
            Replace(6, CardType.Tane, name: "Butterfly", isButterfly: true);
            Replace(7, CardType.Tane, name: "Boar", isBoar: true);
            Replace(8, CardType.Hikari, name: "Susuki & Moon");
            Replace(9, CardType.Tane, name: "Chrysanthemum Cup", isCup: true);
            Replace(9, CardType.Tanzaku, name: "Chrysanthemum Blue", blueRibbon: true);
            Replace(10, CardType.Tane, name: "Deer", isDeer: true);
            Replace(10, CardType.Tanzaku, name: "Maple Blue Ribbon", blueRibbon: true);
            Replace(11, CardType.Hikari, name: "Willow (Rain)", isRain: true);
            Replace(12, CardType.Hikari, name: "Paulownia & Phoenix");
            return list;

            // Local helper to swap names/flags
            void Replace(int month, CardType type, string? name = null, bool isRain = false, bool redPoem = false, bool blueRibbon = false,
                         bool isBoar = false, bool isDeer = false, bool isButterfly = false, bool isCup = false)
            {
                var idx = list.FindIndex(c => c.Month == month && c.Type == type);
                if (idx >= 0)
                {
                    var c = list[idx];
                    list[idx] = new Card(month, type, name ?? c.Name, isRain, redPoem, blueRibbon, isBoar, isDeer, isButterfly, isCup);
                }
            }
        }
    }
}
