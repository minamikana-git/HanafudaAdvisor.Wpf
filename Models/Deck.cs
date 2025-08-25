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

            // 月ごとの基本4枚
            for (int m = 1; m <= 12; m++)
            {
                list.Add(new Card(m, CardType.Tanzaku, $"{m}月 短冊"));
                list.Add(new Card(m, CardType.Tane, $"{m}月 タネ"));
                list.Add(new Card(m, CardType.Kasu, $"{m}月 カス1"));
                list.Add(new Card(m, CardType.Kasu, $"{m}月 カス2"));
            }

            // 名前・フラグの上書き
            Replace(1, CardType.Hikari, name: "松に鶴", redPoem: true);
            Replace(2, CardType.Tanzaku, name: "梅に短冊", redPoem: true);
            Replace(3, CardType.Hikari, name: "桜に幕", redPoem: true);
            Replace(6, CardType.Tanzaku, name: "牡丹に青短", blueRibbon: true);
            Replace(6, CardType.Tane, name: "蝶", isButterfly: true);
            Replace(7, CardType.Tane, name: "猪", isBoar: true);
            Replace(8, CardType.Hikari, name: "芒に月");
            Replace(9, CardType.Tane, name: "菊に盃", isCup: true);
            Replace(9, CardType.Tanzaku, name: "菊に青短", blueRibbon: true);
            Replace(10, CardType.Tane, name: "鹿", isDeer: true);
            Replace(10, CardType.Tanzaku, name: "紅葉に青短", blueRibbon: true);
            Replace(11, CardType.Hikari, name: "柳に小野道風", isRain: true);
            Replace(12, CardType.Hikari, name: "桐に鳳凰");
            return list;

            // ローカル: 札の差し替え
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
