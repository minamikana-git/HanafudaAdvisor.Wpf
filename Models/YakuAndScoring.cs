using System.Collections.Generic;
using System.Linq;

namespace HanafudaAdvisor.Wpf.Models
{
    public static class YakuCatalog
    {
        public const int Gokou = 10;      // 5 Hikari
        public const int Yonkou = 8;      // 4 Hikari
        public const int AmeYonkou = 7;   // 4 Hikari incl. Rain
        public const int Sankou = 5;      // 3 Hikari (no Rain)
        public const int Inoshikacho = 5; // Boar-Deer-Butterfly
        public const int Akatan = 5;      // Red ribbons set
        public const int Aotan = 5;       // Blue ribbons set
        public const int Hanami = 5;      // Cherry Hikari + Cup
        public const int Tsukimi = 5;     // Moon Hikari + Cup

        // Count-based yakus
        public const int KasuBase = 1;    // 10 Kasu +1 per extra
        public const int TanzakuBase = 1; // 5 Tanzaku +1 per extra
        public const int TaneBase = 1;    // 5 Tane +1 per extra

        // Deal-only yakus (initial hand)
        public const int Teshi = 6;       // Four of the same month in the initial hand
        public const int Kuttsuki = 6;    // Four pairs (two of same month) in the initial hand
    }

    public static class Scoring
    {
        // -------- In-play pile scoring (captures) --------
        public static int ScorePile(IReadOnlyCollection<Card> pile)
        {
            int points = 0;
            int kasu = pile.Count(c => c.Type == CardType.Kasu);
            int tanzaku = pile.Count(c => c.Type == CardType.Tanzaku);
            int tane = pile.Count(c => c.Type == CardType.Tane);
            int hikari = pile.Count(c => c.Type == CardType.Hikari);
            bool hasRain = pile.Any(c => c.Type == CardType.Hikari && c.IsRain);

            if (kasu >= 10) points += YakuCatalog.KasuBase + (kasu - 10);
            if (tanzaku >= 5) points += YakuCatalog.TanzakuBase + (tanzaku - 5);
            if (tane >= 5) points += YakuCatalog.TaneBase + (tane - 5);

            if (pile.Count(c => c.IsRedPoem) == 3) points += YakuCatalog.Akatan;
            if (pile.Count(c => c.IsBlueRibbon) == 3) points += YakuCatalog.Aotan;

            if (pile.Any(c => c.IsBoar) && pile.Any(c => c.IsDeer) && pile.Any(c => c.IsButterfly))
                points += YakuCatalog.Inoshikacho;

            if (pile.Any(c => c.IsCup) && pile.Any(c => c.Type == CardType.Hikari && c.Month == 3)) points += YakuCatalog.Hanami;
            if (pile.Any(c => c.IsCup) && pile.Any(c => c.Type == CardType.Hikari && c.Month == 8)) points += YakuCatalog.Tsukimi;

            if (hikari >= 5) points += YakuCatalog.Gokou;
            else if (hikari == 4) points += hasRain ? YakuCatalog.AmeYonkou : YakuCatalog.Yonkou;
            else if (hikari == 3 && !hasRain) points += YakuCatalog.Sankou;

            return points;
        }

        // 完成役（取り札の山）を日本語で列挙
        public static IEnumerable<(string name, int points)> ListCompletedYaku(IReadOnlyCollection<Card> pile)
        {
            int kasu = pile.Count(c => c.Type == CardType.Kasu);
            int tanzaku = pile.Count(c => c.Type == CardType.Tanzaku);
            int tane = pile.Count(c => c.Type == CardType.Tane);
            int hikari = pile.Count(c => c.Type == CardType.Hikari);
            bool hasRain = pile.Any(c => c.Type == CardType.Hikari && c.IsRain);

            if (kasu >= 10) yield return ("カス", YakuCatalog.KasuBase + (kasu - 10));
            if (tanzaku >= 5) yield return ("短冊", YakuCatalog.TanzakuBase + (tanzaku - 5));
            if (tane >= 5) yield return ("タネ", YakuCatalog.TaneBase + (tane - 5));
            if (pile.Count(c => c.IsRedPoem) == 3) yield return ("赤短", YakuCatalog.Akatan);
            if (pile.Count(c => c.IsBlueRibbon) == 3) yield return ("青短", YakuCatalog.Aotan);
            if (pile.Any(c => c.IsBoar) && pile.Any(c => c.IsDeer) && pile.Any(c => c.IsButterfly)) yield return ("猪鹿蝶", YakuCatalog.Inoshikacho);
            if (pile.Any(c => c.IsCup) && pile.Any(c => c.Type == CardType.Hikari && c.Month == 3)) yield return ("花見で一杯", YakuCatalog.Hanami);
            if (pile.Any(c => c.IsCup) && pile.Any(c => c.Type == CardType.Hikari && c.Month == 8)) yield return ("月見で一杯", YakuCatalog.Tsukimi);
            if (hikari >= 5) yield return ("五光", YakuCatalog.Gokou);
            else if (hikari == 4) yield return (!hasRain ? ("四光", YakuCatalog.Yonkou) : ("雨四光", YakuCatalog.AmeYonkou));
            else if (hikari == 3 && !hasRain) yield return ("三光", YakuCatalog.Sankou);
        }

        // 配札役（初期手札）を日本語で列挙
        public static IEnumerable<(string name, int points)> ListDealYaku(IReadOnlyCollection<Card> hand)
        {
            if (hand.GroupBy(c => c.Month).Any(g => g.Count() == 4))
                yield return ("手四", YakuCatalog.Teshi);

            int pairMonths = hand.GroupBy(c => c.Month).Count(g => g.Count() >= 2);
            if (pairMonths >= 4)
                yield return ("くっつき", YakuCatalog.Kuttsuki);
        }


        // Progress signalは従来どおり
        public static double ProgressSignal(IReadOnlyCollection<Card> pile)
        {
            double s = 0;
            int hikari = pile.Count(c => c.Type == CardType.Hikari);
            bool hasRain = pile.Any(c => c.Type == CardType.Hikari && c.IsRain);
            if (hikari < 3 || (hikari == 3 && hasRain)) s += 1.0 * hikari * 0.3;
            int tanzaku = pile.Count(c => c.Type == CardType.Tanzaku);
            if (tanzaku < 5) s += 0.4 * tanzaku * 0.1;
            int tane = pile.Count(c => c.Type == CardType.Tane);
            if (tane < 5) s += 0.4 * tane * 0.1;
            int reds = pile.Count(c => c.IsRedPoem);
            if (reds < 3) s += 0.8 * reds * 0.2;
            int blues = pile.Count(c => c.IsBlueRibbon);
            if (blues < 3) s += 0.8 * blues * 0.2;
            int trio = (pile.Any(c => c.IsBoar) ? 1 : 0) + (pile.Any(c => c.IsDeer) ? 1 : 0) + (pile.Any(c => c.IsButterfly) ? 1 : 0);
            if (trio < 3) s += 1.1 * trio * 0.3;
            return s;
        }
    }
}
