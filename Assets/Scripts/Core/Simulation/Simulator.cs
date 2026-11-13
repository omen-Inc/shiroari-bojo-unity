// CPU 同士の対戦を回して集計する。ファイル I/O はしない(結果は文字列で返す)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShiroariBojo.Core.AI;

namespace ShiroariBojo.Core.Simulation
{
    public sealed class SimulationResult
    {
        public int Games;
        public int TermiteWins;
        public int PestWins;
        public int Draws;
        /// <summary>先攻がシロアリ側だった対戦数と、そのときのシロアリ側の勝ち数</summary>
        public int TermiteFirstGames, TermiteFirstWins;
        public long TotalTurns;
        public long TotalDestroyed;
        public int TreeFellGames;
        public long QueensPromoted, SprayRemoved, SprayBlocked, BaitRemoved;
        public long[] Mulligans = new long[2];
        public long[] HandLimitDiscards = new long[2];
        public int MinTurns = int.MaxValue, MaxTurns;
        /// <summary>[陣営] カードID → 手札に来た回数 / 使った回数</summary>
        public Dictionary<string, long>[] Drawn = { new Dictionary<string, long>(), new Dictionary<string, long>() };
        public Dictionary<string, long>[] Played = { new Dictionary<string, long>(), new Dictionary<string, long>() };

        public double TermiteWinRate => Games == 0 ? 0 : (double)TermiteWins / Games;
        public double AverageTurns => Games == 0 ? 0 : (double)TotalTurns / Games;

        public sealed class CardUsage
        {
            public Side Side;
            public string CardId;
            public string Name;
            public long Drawn;
            public long Played;
            /// <summary>使用率 = 使った回数 / 手札に来た回数</summary>
            public double Rate => Drawn == 0 ? 0 : (double)Played / Drawn;
        }

        public List<CardUsage> Usage(CardDatabase db)
        {
            var list = new List<CardUsage>();
            foreach (Side side in new[] { Side.Termite, Side.Pest })
                foreach (var def in db.All)
                {
                    Drawn[(int)side].TryGetValue(def.Id, out long dr);
                    Played[(int)side].TryGetValue(def.Id, out long pl);
                    if (dr == 0 && pl == 0) continue;
                    list.Add(new CardUsage { Side = side, CardId = def.Id, Name = def.Name, Drawn = dr, Played = pl });
                }
            return list;
        }

        public string ToReport(CardDatabase db, string title = null)
        {
            var ci = CultureInfo.InvariantCulture;
            string Pct(double x) => (x * 100).ToString("0.0", ci) + "%";
            string Avg(long v) => Games == 0 ? "0" : ((double)v / Games).ToString("0.00", ci);
            var sb = new StringBuilder();
            sb.AppendLine(title ?? $"シロアリボージョ CPU 同士の対戦 {Games}戦");
            sb.AppendLine($"シロアリ側の勝率: {Pct(TermiteWinRate)}({TermiteWins}勝)");
            sb.AppendLine($"防除業者側の勝率: {Pct(Games == 0 ? 0 : (double)PestWins / Games)}({PestWins}勝)");
            sb.AppendLine($"引き分け(安全装置): {Draws}");
            int pf = Games - TermiteFirstGames;
            sb.AppendLine($"シロアリ側の勝率(シロアリ先攻 {TermiteFirstGames}戦): {Pct(TermiteFirstGames == 0 ? 0 : (double)TermiteFirstWins / TermiteFirstGames)}" +
                          $" / (防除先攻 {pf}戦): {Pct(pf == 0 ? 0 : (double)(TermiteWins - TermiteFirstWins) / pf)}");
            sb.AppendLine($"平均ターン数(両者の手番の合計): {AverageTurns.ToString("0.0", ci)}(最短 {(Games == 0 ? 0 : MinTurns)} / 最長 {MaxTurns})");
            sb.AppendLine($"平均完全侵食ポイント: {Avg(TotalDestroyed)}");
            sb.AppendLine($"庭の枯れ木が倒れた割合: {Pct(Games == 0 ? 0 : (double)TreeFellGames / Games)}");
            sb.AppendLine($"1戦あたり: 女王アリ {Avg(QueensPromoted)} / 薬剤散布で除去 {Avg(SprayRemoved)} / 兵アリが耐えた {Avg(SprayBlocked)} / ベイトで除去 {Avg(BaitRemoved)}");
            sb.AppendLine($"1戦あたり: 手詰まり防止 シロアリ {Avg(Mulligans[0])} / 防除 {Avg(Mulligans[1])}、手札上限で捨てた シロアリ {Avg(HandLimitDiscards[0])} / 防除 {Avg(HandLimitDiscards[1])}");
            sb.AppendLine();
            sb.AppendLine("カード別使用率(使った回数 / 手札に来た回数。低い順)");
            sb.AppendLine("陣営\tカード\t使用率\t使った\t手札に来た\t1戦あたり使った");
            foreach (var u in Usage(db).OrderBy(x => x.Rate))
                sb.AppendLine($"{(u.Side == Side.Termite ? "シロアリ" : "防除")}\t{u.Name}\t{Pct(u.Rate)}\t{u.Played}\t{u.Drawn}\t{Avg(u.Played)}");
            return sb.ToString();
        }
    }

    public static class Simulator
    {
        /// <summary>CPU 同士の1戦。行動の数が上限を超えたら例外(無限ループの検出)</summary>
        public static GameState RunGame(CardDatabase db, uint seed, GameRules rules = null, CpuOptions termiteCpu = null, CpuOptions pestCpu = null, int maxActions = 20000)
        {
            var r = rules?.Clone() ?? GameRules.Default;
            var s = Engine.NewGame(db, seed, null, r);
            var cpus = new ICpuPlayer[]
            {
                new CpuPlayer(unchecked(seed * 2654435761u + 1), termiteCpu),
                new CpuPlayer(unchecked(seed * 2246822519u + 2), pestCpu),
            };
            int n = 0;
            while (!s.IsOver)
            {
                if (++n > maxActions) throw new InvalidOperationException($"行動数が上限 {maxActions} を超えた(seed {seed})");
                var side = s.Current;
                var action = cpus[(int)side].ChooseAction(new PlayerView(s, side));
                s = Engine.Apply(s, action);
            }
            return s;
        }

        public static SimulationResult Run(CardDatabase db, int games, uint baseSeed = 1, GameRules rules = null, Action<int> progress = null)
        {
            var r = rules?.Clone() ?? GameRules.Default;
            r.KeepLog = false;
            var res = new SimulationResult();
            for (int g = 0; g < games; g++)
            {
                var s = RunGame(db, unchecked(baseSeed + (uint)g), r);
                Accumulate(res, s);
                progress?.Invoke(g + 1);
            }
            return res;
        }

        public static void Accumulate(SimulationResult res, GameState s)
        {
            res.Games++;
            if (s.Winner == Side.Termite) res.TermiteWins++;
            else if (s.Winner == Side.Pest) res.PestWins++;
            else res.Draws++;
            if (s.First == Side.Termite)
            {
                res.TermiteFirstGames++;
                if (s.Winner == Side.Termite) res.TermiteFirstWins++;
            }
            res.TotalTurns += s.Turn;
            res.MinTurns = Math.Min(res.MinTurns, s.Turn);
            res.MaxTurns = Math.Max(res.MaxTurns, s.Turn);
            res.TotalDestroyed += s.DestroyedCount;
            if (s.TreeFallen) res.TreeFellGames++;
            res.QueensPromoted += s.Stats.QueensPromoted;
            res.SprayRemoved += s.Stats.SprayRemoved;
            res.SprayBlocked += s.Stats.SprayBlocked;
            res.BaitRemoved += s.Stats.BaitRemoved;
            for (int k = 0; k < 2; k++)
            {
                res.Mulligans[k] += s.Stats.Mulligans[k];
                res.HandLimitDiscards[k] += s.Stats.HandLimitDiscards[k];
                foreach (var kv in s.Stats.Drawn[k]) res.Drawn[k][kv.Key] = (res.Drawn[k].TryGetValue(kv.Key, out var a) ? a : 0) + kv.Value;
                foreach (var kv in s.Stats.Played[k]) res.Played[k][kv.Key] = (res.Played[k].TryGetValue(kv.Key, out var b) ? b : 0) + kv.Value;
            }
        }
    }
}
