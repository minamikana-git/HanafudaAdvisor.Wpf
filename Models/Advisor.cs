using System;
using System.Collections.Generic;
using System.Linq;

namespace HanafudaAdvisor.Wpf.Models
{
    public sealed class Advisor
    {
        private readonly Random _rng;
        public Advisor(int? seed = null) => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

        public sealed record Suggestion(Card Play, double Ev, string Reason);
        public sealed record OpponentPrediction(int Month, double Probability, string Reason);

        public static string DecideFirstPlayer(Card mySelected, Card oppSelected)
        {
            if (mySelected.Month < oppSelected.Month) return "You_First";
            if (mySelected.Month > oppSelected.Month) return "Opponent_First";
            return "Tie";
        }

        public IEnumerable<Suggestion> BestMoves(GameState g, int simulations = 800)
        {
            foreach (var play in g.MyHand)
            {
                double ev = MonteCarloEV(g, play, simulations);
                var matches = g.Field.Where(f => f.Month == play.Month).ToList();
                var reason = matches.Count > 0
                    ? $"場に同月{matches.Count}枚。即時成立チャンス↑"
                    : "場に同月なし。山依存";
                yield return new Suggestion(play, ev, reason);
            }
        }

        public IEnumerable<OpponentPrediction> PredictOpponentNext(GameState g, int simulations = 1000)
        {
            var monthCount = Enumerable.Range(1, 12).ToDictionary(m => m, _ => 0);
            for (int s = 0; s < simulations; s++)
            {
                var unknown = g.Unknown.ToList();
                Shuffle(unknown);
                int oppNeed = Math.Max(0, 8 - g.OppKnown.Count);
                var oppHand = g.OppKnown.Concat(unknown.Take(oppNeed)).ToList();
                var choice = OpponentGreedyChoice(g, oppHand);
                if (choice != null) monthCount[choice.Month]++;
            }
            int total = Math.Max(1, monthCount.Values.Sum());
            return monthCount.Where(kv => kv.Value > 0)
                             .OrderByDescending(kv => kv.Value)
                             .Select(kv => new OpponentPrediction(
                                 kv.Key,
                                 (double)kv.Value / total,
                                 kv.Value / (double)total > 0.2
                                     ? "高確率：場一致/役進捗"
                                     : "低〜中：捨て候補"));
        }

        private Card? OpponentGreedyChoice(GameState g, List<Card> oppHand)
        {
            Card? best = null; double bestScore = double.NegativeInfinity;
            foreach (var c in oppHand)
            {
                double s = 0.0;
                bool canCapture = g.Field.Any(f => f.Month == c.Month);
                s += canCapture ? 0.6 : 0.0;

                var tmp = g.OppPile.ToList();
                if (canCapture)
                {
                    tmp.Add(c);
                    tmp.Add(g.Field.First(f => f.Month == c.Month));
                }
                s += Scoring.ScorePile(tmp) * 0.3 + Scoring.ProgressSignal(tmp) * 0.2;
                if (s > bestScore) { bestScore = s; best = c; }
            }
            return best ?? oppHand.FirstOrDefault();
        }

        private double MonteCarloEV(GameState g0, Card play, int sims)
        {
            double sum = 0;
            for (int s = 0; s < sims; s++)
            {
                var g = g0.CloneShallow();
                var unknown = g.Unknown.ToList();
                Shuffle(unknown);
                int oppNeed = Math.Max(0, 8 - g.OppKnown.Count);
                var oppHand = g.OppKnown.Concat(unknown.Take(oppNeed)).ToList();
                var drawPile = unknown.Skip(oppNeed).ToList();

                SimulatePlay(g, play, drawPile, myTurn: true);

                var oppChoice = OpponentGreedyChoice(g, oppHand);
                if (oppChoice != null) SimulatePlay(g, oppChoice, drawPile, myTurn: false);

                int myScore = Scoring.ScorePile(g.MyPile);
                int oppScore = Scoring.ScorePile(g.OppPile);
                double prog = Scoring.ProgressSignal(g.MyPile) - Scoring.ProgressSignal(g.OppPile);
                sum += (myScore - oppScore) + 0.5 * prog;
            }
            return sum / Math.Max(1, sims);
        }

        private void SimulatePlay(GameState g, Card play, List<Card> drawPile, bool myTurn)
        {
            if (myTurn) g.MyHand.Remove(play);
            var matches = g.Field.Where(f => f.Month == play.Month).ToList();
            if (matches.Count > 0)
            {
                var bestField = matches.OrderByDescending(m => CaptureValue(myTurn ? g.MyPile : g.OppPile, play, m)).First();
                g.Field.Remove(bestField);
                if (myTurn) { g.MyPile.Add(play); g.MyPile.Add(bestField); }
                else { g.OppPile.Add(play); g.OppPile.Add(bestField); }
            }
            else g.Field.Add(play);

            if (drawPile.Count > 0)
            {
                var drawn = drawPile[0]; drawPile.RemoveAt(0);
                var m2 = g.Field.Where(f => f.Month == drawn.Month).ToList();
                if (m2.Count > 0)
                {
                    var bestField2 = m2.OrderByDescending(m => CaptureValue(myTurn ? g.MyPile : g.OppPile, drawn, m)).First();
                    g.Field.Remove(bestField2);
                    if (myTurn) { g.MyPile.Add(drawn); g.MyPile.Add(bestField2); }
                    else { g.OppPile.Add(drawn); g.OppPile.Add(bestField2); }
                }
                else g.Field.Add(drawn);
            }
        }

        private double CaptureValue(List<Card> pile, Card a, Card b)
        {
            var tmp = pile.ToList(); tmp.Add(a); tmp.Add(b);
            int pts = Scoring.ScorePile(tmp) - Scoring.ScorePile(pile);
            double prog = Scoring.ProgressSignal(tmp) - Scoring.ProgressSignal(pile);
            return pts + 0.6 * prog;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
