using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShiroariBojo.Core;
using ShiroariBojo.Core.AI;
using static ShiroariBojo.Tests.T;

namespace ShiroariBojo.Tests
{
    /// <summary>
    /// CPU 同士の対戦のログ全体で、非公開の情報が相手向けの文に漏れていないかを確かめる。
    /// 「自分」= そのイベントを見てよい側(TermiteOnly → シロアリ側、PestOnly → 防除業者側、Public → ev.Side)。
    /// 「相手」= その反対。非公開のイベントは Engine 側で必ず OnlyFor(ev.Side) で作られるので、
    /// 非公開のイベントでは「自分」は ev.Side と一致する。
    /// </summary>
    public class LogVisibilityTests
    {
        private const int MaxActions = 20000;
        private static readonly uint[] Seeds = { 1, 7, 42, 1234, 9001 };

        private static Side Owner(GameEvent ev) => ev.Visibility switch
        {
            EventVisibility.TermiteOnly => Side.Termite,
            EventVisibility.PestOnly => Side.Pest,
            _ => ev.Side,
        };

        private static int CountOf(string text, string word)
        {
            int n = 0, i = 0;
            while ((i = text.IndexOf(word, i, System.StringComparison.Ordinal)) >= 0) { n++; i += word.Length; }
            return n;
        }

        private static string Describe(GameEvent ev, Side opp, uint seed, Side first) =>
            $"seed {seed} 先手 {first.Label()}: 種類 {ev.Type} / ターン {ev.Turn} / Side {ev.Side.Label()} / Visibility {ev.Visibility} / " +
            $"CardId {ev.CardId ?? "null"} / CardIds [{string.Join(",", ev.CardIds)}] / 本人向け文「{ev.Text}」/ 相手({opp.Label()})向け文「{ev.TextFor(opp) ?? "null"}」";

        private sealed class Tally
        {
            public int Games, Events, Hidden, HiddenWithCards, CauseNameAllowed;
            public readonly Dictionary<GameEventType, int> HiddenByType = new Dictionary<GameEventType, int>();
        }

        /// <summary>CPU 同士で1戦回し、1回の Apply で出たイベントごとに確かめる</summary>
        private static void RunAndCheck(Side first, uint seed, Tally tally, List<string> failures)
        {
            var s = Game(first, seed);
            var cpus = new ICpuPlayer[]
            {
                new CpuPlayer(unchecked(seed * 2654435761u + 1)),
                new CpuPlayer(unchecked(seed * 2246822519u + 2)),
            };

            // NewGame で出たイベント(Apply の外)も確かめる
            CheckBatch(s.Log, 0, s.Log.Count, seed, first, tally, failures);

            int n = 0;
            while (!s.IsOver)
            {
                if (++n > MaxActions)
                {
                    Assert.Fail($"seed {seed} 先手 {first.Label()}: 行動数が上限 {MaxActions} を超えた(無限ループの疑い)");
                    return;
                }
                int before = s.Log.Count;
                var side = s.Current;
                s = Engine.Apply(s, cpus[(int)side].ChooseAction(new PlayerView(s, side)));
                CheckBatch(s.Log, before, s.Log.Count, seed, first, tally, failures);
            }
            tally.Games++;
        }

        private static void CheckBatch(IReadOnlyList<GameEvent> log, int from, int to, uint seed, Side first, Tally tally, List<string> failures)
        {
            // 同じ Apply の中で、その陣営が公開で使ったカードの名前(例:「増援」を使った → 「増援:2枚引いた」)。
            // 相手向けの文の定型句にこの名前が入るため、引いたカードに同じ名前があると誤検出になる。
            // 定型句に入るのは1回だけなので、2回以上出たら漏れとみなす。
            var playedNames = new Dictionary<Side, HashSet<string>>
            {
                [Side.Termite] = new HashSet<string>(),
                [Side.Pest] = new HashSet<string>(),
            };

            for (int i = from; i < to; i++)
            {
                var ev = log[i];
                tally.Events++;
                var self = Owner(ev);
                var opp = self.Opponent();

                var selfText = ev.TextFor(self);
                if (string.IsNullOrEmpty(selfText))
                    failures.Add("本人向け文が空: " + Describe(ev, opp, seed, first));

                if (ev.Type == GameEventType.CardPlayed && ev.Visibility == EventVisibility.Public && ev.CardId != null)
                    playedNames[ev.Side].Add(Db.Get(ev.CardId).Name);

                if (ev.VisibleTo(opp)) continue;
                tally.Hidden++;
                tally.HiddenByType[ev.Type] = (tally.HiddenByType.TryGetValue(ev.Type, out var c) ? c : 0) + 1;
                if (ev.CardIds.Count > 0) tally.HiddenWithCards++;

                var oppText = ev.TextFor(opp);
                if (oppText == null) continue;

                foreach (var name in ev.CardIds.Select(id => Db.Get(id).Name).Distinct())
                {
                    int count = CountOf(oppText, name);
                    if (count == 0) continue;
                    if (count == 1 && playedNames[ev.Side].Contains(name))
                    {
                        tally.CauseNameAllowed++;
                        continue;
                    }
                    failures.Add($"非公開のカード名「{name}」が相手向け文に入っている: " + Describe(ev, opp, seed, first));
                }
            }
        }

        [Test]
        public void CPU同士の対戦で相手向けのログに非公開のカード名が出ない()
        {
            var tally = new Tally();
            var failures = new List<string>();
            foreach (var first in new[] { Side.Termite, Side.Pest })
                foreach (var seed in Seeds)
                    RunAndCheck(first, seed, tally, failures);

            TestContext.WriteLine($"対戦 {tally.Games}戦 / イベント {tally.Events}件 / 相手から見えないイベント {tally.Hidden}件(うち CardIds あり {tally.HiddenWithCards}件)" +
                                  $" / 使ったカード名の定型句として許した件数 {tally.CauseNameAllowed}");
            foreach (var kv in tally.HiddenByType.OrderBy(x => x.Key))
                TestContext.WriteLine($"  {kv.Key}: {kv.Value}");

            Assert.AreEqual(Seeds.Length * 2, tally.Games);
            Assert.Greater(tally.HiddenWithCards, 0, "非公開のカードを含むイベントが1件も出なかった(テストが何も確かめていない)");
            Assert.IsEmpty(failures, $"{failures.Count}件の問題:\n" + string.Join("\n", failures.Take(30)));
        }
    }
}
