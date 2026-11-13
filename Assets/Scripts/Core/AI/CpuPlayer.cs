// CPU の思考。sb-proto(index.html の「CPU」)の考え方をそのまま移植したもの。
//  - 手札の各カード × 対象ごとに点数をつけ、最も高い手を1つずつ打つ(点数が閾値以下ならターン終了)
//  - コストが高いカードは少し減点する
//  - 1枚も使わなかったターンは、手詰まり防止で価値の最も低いカードを捨てて引き直す
//  - 手札上限・予知は、カードの価値(CardValue)で選ぶ
// PlayerView だけを見るので、相手の手札・山札の中身を知らない。乱数は対戦とは別の CPU 専用のものを使う。

using System.Collections.Generic;
using System.Linq;

namespace ShiroariBojo.Core.AI
{
    public sealed class CpuOptions
    {
        /// <summary>点数に足すランダムな揺らぎの幅</summary>
        public double Noise = 0.5;
        /// <summary>これより高い点数の手がなければターンを終える</summary>
        public double PlayThreshold = 0.8;
        /// <summary>コスト1を超える分の減点(1あたり)</summary>
        public double CostPenalty = 1.5;

        public static CpuOptions Default => new CpuOptions();
    }

    public interface ICpuPlayer
    {
        /// <summary>view.IsMyTurn のときに呼ぶ。合法手を1つ返す</summary>
        GameAction ChooseAction(PlayerView view);
    }

    public sealed class CpuPlayer : ICpuPlayer
    {
        private readonly Rng _rng;
        private readonly CpuOptions _opt;

        public CpuPlayer(uint seed, CpuOptions options = null)
        {
            _rng = new Rng(seed);
            _opt = options ?? CpuOptions.Default;
        }

        public GameAction ChooseAction(PlayerView v)
        {
            switch (v.Phase)
            {
                case Phase.ForesightChoice: return ChooseForesight(v);
                case Phase.HandLimitDiscard: return ChooseHandLimitDiscard(v);
                case Phase.Main: return ChooseMain(v);
            }
            throw new System.InvalidOperationException("CPU が行動できる状態ではありません");
        }

        private GameAction ChooseMain(PlayerView v)
        {
            GameAction best = null;
            double bestScore = _opt.PlayThreshold;
            var seen = new HashSet<string>();
            foreach (var o in v.HandOptions())
            {
                // 同じカードは1枚だけ考える(sb-proto と同じ)
                if (!seen.Add(o.Def.Id) || !o.Usable) continue;
                double pen = _opt.CostPenalty * (o.Cost - 1);
                if (o.NeedsTarget)
                {
                    foreach (int i in o.Targets)
                    {
                        double sc = ScoreOption(v, o.Def, i) - pen + _rng.NextDouble() * _opt.Noise;
                        if (sc > bestScore) { bestScore = sc; best = GameAction.Play(o.Uid, i); }
                    }
                }
                else
                {
                    double sc = ScoreOption(v, o.Def, -1) - pen + _rng.NextDouble() * _opt.Noise;
                    if (sc > bestScore) { bestScore = sc; best = GameAction.Play(o.Uid); }
                }
            }
            if (best != null) return best;

            // ターン終了。1枚も使っていなければ、価値の最も低いカードを捨てて引き直す
            if (v.CanMulligan)
            {
                var worst = Lowest(v, v.MyHand);
                if (worst != null) return GameAction.EndTurn(worst.Uid);
            }
            return GameAction.EndTurn();
        }

        private GameAction ChooseForesight(PlayerView v)
        {
            // 価値の高い順(同点は元の順)。1枚目を手札へ、残りは sb-proto と同じ並びで山札の下へ
            var sorted = v.ForesightCards.Select((c, k) => (c, k)).OrderByDescending(x => CardValue(v, x.c.DefId)).ThenBy(x => x.k).Select(x => x.c).ToList();
            var bottom = sorted.Skip(1).Reverse().Select(c => c.Uid).ToList();
            return GameAction.ChooseForesight(sorted[0].Uid, bottom);
        }

        private GameAction ChooseHandLimitDiscard(PlayerView v)
        {
            var hand = v.MyHand.ToList();
            var picks = new List<int>();
            for (int n = 0; n < v.HandLimitDiscardCount; n++)
            {
                var c = Lowest(v, hand);
                hand.Remove(c);
                picks.Add(c.Uid);
            }
            return GameAction.DiscardForHandLimit(picks);
        }

        private static CardInstance Lowest(PlayerView v, IReadOnlyList<CardInstance> cards)
        {
            CardInstance min = null;
            double mv = double.MaxValue;
            foreach (var c in cards)
            {
                double x = CardValue(v, c.DefId);
                if (x < mv) { mv = x; min = c; }
            }
            return min;
        }

        /// <summary>カードを手札に持っておく価値(sb-proto の cardValue)</summary>
        public static double CardValue(PlayerView v, string defId)
        {
            var d = v.Cards.Get(defId);
            bool usable = d.User == CardUser.Neutral || (d.User == CardUser.Termite && v.Viewer == Side.Termite) || (d.User == CardUser.Pest && v.Viewer == Side.Pest);
            if (!usable) return -1;
            if (d.Effect == CardEffect.PlaceTermite)
            {
                bool any = false;
                for (int i = 0; i < Points.Count; i++)
                    if (Points.All[i].Allows(d.Species) && !v.Point(i).Destroyed) { any = true; break; }
                if (!any) return 0;
                return d.Caste == Caste.Winged ? 9 : d.Caste == Caste.Soldier ? 7 : 6;
            }
            switch (d.Effect)
            {
                case CardEffect.Scout: return 1;
                case CardEffect.Foresight: return 4;
                case CardEffect.Reinforce: return 3;
                case CardEffect.Disrupt: return 4;
                case CardEffect.Swarm: return 5;
                case CardEffect.ImportFurniture: return 5;
                case CardEffect.Barrier: return 6;
                case CardEffect.Bait: return 6;
                case CardEffect.Spray: return 8;
            }
            return 4;
        }

        /// <summary>その手の点数(sb-proto の scoreOption)。point = -1 は対象なし</summary>
        public static double ScoreOption(PlayerView v, CardDef d, int point)
        {
            double r = point < 0 ? (v.TreeFallen ? v.Rules.AfterTreeFallRequired : v.Rules.InfestationRequired) : v.RequiredAt(point);
            var p = point >= 0 ? v.Point(point) : null;
            int n = p?.Cards.Count ?? 0;
            switch (d.Effect)
            {
                case CardEffect.PlaceTermite:
                {
                    double after = n + 1;
                    double s = after / r * 10;
                    if (after >= r) s += 20;
                    if (Points.All[point].IsGardenTree && !v.TreeFallen) s += 3;
                    if (p.Swarm) s += 3;
                    if (d.Caste == Caste.Winged) s += 6 + (p.Swarm ? 4 : 0);
                    if (d.Caste == Caste.Soldier && n > 0) s += 1;
                    if (p.HasBait && d.Species != Species.Kanzai) s -= 4;
                    if (d.Species == Species.Ie) s -= 1;
                    return s;
                }
                case CardEffect.Swarm:
                    return n > 0 ? 4 + 6 * n / r + (p.Cards.Any(c => c.PromoteAtOwnTurn > 0) ? 3 : 0) : 0;
                case CardEffect.Spray:
                {
                    if (n == 0) return 0;
                    double s = 4 + 10 * n / r;
                    if (n >= r - 1) s += 15;
                    if (p.Cards.Any(c => c.PromoteAtOwnTurn > 0)) s += 6;
                    if (p.Cards.Any(c => c.GuardAvailable)) s -= 2;
                    return s;
                }
                case CardEffect.Barrier:
                    return n > 0 ? 3 + 8 * n / r : (Points.All[point].Zone == Zone.Wet ? 1.5 : 0.5);
                case CardEffect.Bait:
                {
                    int k = p.Cards.Count(c => v.Cards.Get(c.DefId).Species != Species.Kanzai);
                    return k > 0 ? 4 + 3 * k : (Points.All[point].Zone != Zone.Upper ? 1.5 : 0);
                }
                case CardEffect.ImportFurniture:
                {
                    for (int i = 0; i < Points.Count; i++)
                        if (Points.All[i].Allows(Species.Kanzai) && !v.Point(i).Destroyed) return 5;
                    return 1;
                }
                case CardEffect.Reinforce:
                    return v.Viewer == Side.Termite && v.MyDeckCount < 6 ? 0 : 4;
                case CardEffect.Foresight: return 3.5;
                case CardEffect.Disrupt: return 3;
                case CardEffect.Scout: return 0.5;
            }
            return 1;
        }
    }
}
