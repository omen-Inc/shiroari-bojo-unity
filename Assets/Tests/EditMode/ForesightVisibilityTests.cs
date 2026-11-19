using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShiroariBojo.Core;
using ShiroariBojo.Core.AI;
using static ShiroariBojo.Tests.T;

namespace ShiroariBojo.Tests
{
    /// <summary>
    /// 予知で見たカード(ForesightRevealed の CardIds)と手札に加えたカード(ForesightResolved の CardId)が、
    /// 相手向けの文に漏れていないかを確かめる。
    /// ・ForesightRevealed の CardId は使った予知カード自身(公開)。相手向け文に定型句として名前が1回入りうる
    /// ・ForesightResolved は ChooseForesight の Apply で出るので、同じ Apply に CardPlayed がない。
    ///   相手向け文の「予知で〜」は、直前の ForesightRevealed の予知カードの名前として1回まで許す
    /// ・めくったカード・加えたカードに同名カードがあり、名前が2回以上出たら漏れとみなす
    /// </summary>
    public class ForesightVisibilityTests
    {
        private const int MaxActions = 20000;
        private static readonly uint[] Seeds = Enumerable.Range(1, 20).Select(x => (uint)x).ToArray();

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

        private static bool IsForesight(GameEvent ev) =>
            ev.Type == GameEventType.ForesightRevealed || ev.Type == GameEventType.ForesightResolved;

        private static string Describe(GameEvent ev, Side opp, string where) =>
            $"{where}: 種類 {ev.Type} / ターン {ev.Turn} / Side {ev.Side.Label()} / Visibility {ev.Visibility} / " +
            $"CardId {ev.CardId ?? "null"} / CardIds [{string.Join(",", ev.CardIds)}] / 本人向け文「{ev.Text}」/ 相手({opp.Label()})向け文「{ev.TextFor(opp) ?? "null"}」";

        private sealed class Checker
        {
            public int Revealed, Resolved, HiddenForesight, CauseNameAllowed;
            public readonly List<string> Failures = new List<string>();

            // 陣営ごとに、解決待ちの予知で使った予知カードの名前(ForesightRevealed で記録し、ForesightResolved で使う)
            private readonly Dictionary<Side, string> _pendingCause = new Dictionary<Side, string>();

            /// <summary>1回の Apply で出たイベント(log[from..to))を確かめる</summary>
            public void CheckBatch(IReadOnlyList<GameEvent> log, int from, int to, string where)
            {
                // 同じ Apply の中で、その陣営が公開で使ったカードの名前(LogVisibilityTests と同じ考え方)
                var playedNames = new Dictionary<Side, HashSet<string>>
                {
                    [Side.Termite] = new HashSet<string>(),
                    [Side.Pest] = new HashSet<string>(),
                };

                for (int i = from; i < to; i++)
                {
                    var ev = log[i];
                    if (ev.Type == GameEventType.CardPlayed && ev.Visibility == EventVisibility.Public && ev.CardId != null)
                        playedNames[ev.Side].Add(Db.Get(ev.CardId).Name);

                    if (!IsForesight(ev)) continue;
                    var self = Owner(ev);
                    var opp = self.Opponent();

                    // 定型句として1回まで許す名前
                    var allowed = new HashSet<string>(playedNames[ev.Side]);
                    if (ev.Type == GameEventType.ForesightRevealed)
                    {
                        Revealed++;
                        if (ev.CardId != null)
                        {
                            var cause = Db.Get(ev.CardId).Name;
                            allowed.Add(cause);
                            _pendingCause[ev.Side] = cause;
                        }
                    }
                    else
                    {
                        Resolved++;
                        if (_pendingCause.TryGetValue(ev.Side, out var cause)) allowed.Add(cause);
                        else Failures.Add("ForesightRevealed より前に ForesightResolved が出た: " + Describe(ev, opp, where));
                        _pendingCause.Remove(ev.Side);
                    }

                    if (self != ev.Side)
                        Failures.Add("予知のイベントの見せる相手が予知した陣営と違う: " + Describe(ev, opp, where));

                    if (string.IsNullOrEmpty(ev.TextFor(self)))
                        Failures.Add("予知した側の文が空: " + Describe(ev, opp, where));

                    if (ev.VisibleTo(opp))
                    {
                        Failures.Add("予知のイベントが相手に見えている: " + Describe(ev, opp, where));
                        continue;
                    }
                    HiddenForesight++;

                    var oppText = ev.TextFor(opp);
                    if (oppText == null) continue;

                    var ids = new List<string>();
                    if (ev.CardId != null) ids.Add(ev.CardId);
                    ids.AddRange(ev.CardIds);
                    foreach (var name in ids.Select(id => Db.Get(id).Name).Distinct())
                    {
                        int count = CountOf(oppText, name);
                        if (count == 0) continue;
                        if (count == 1 && allowed.Contains(name))
                        {
                            CauseNameAllowed++;
                            continue;
                        }
                        Failures.Add($"非公開のカード名「{name}」が相手向け文に入っている: " + Describe(ev, opp, where));
                    }
                }
            }
        }

        private static void RunCpuGame(Side first, uint seed, Checker checker)
        {
            var s = Game(first, seed);
            var cpus = new ICpuPlayer[]
            {
                new CpuPlayer(unchecked(seed * 2654435761u + 1)),
                new CpuPlayer(unchecked(seed * 2246822519u + 2)),
            };
            string where = $"seed {seed} 先手 {first.Label()}";
            checker.CheckBatch(s.Log, 0, s.Log.Count, where);

            int n = 0;
            while (!s.IsOver)
            {
                if (++n > MaxActions)
                {
                    Assert.Fail($"{where}: 行動数が上限 {MaxActions} を超えた(無限ループの疑い)");
                    return;
                }
                int before = s.Log.Count;
                var side = s.Current;
                s = Engine.Apply(s, cpus[(int)side].ChooseAction(new PlayerView(s, side)));
                checker.CheckBatch(s.Log, before, s.Log.Count, where);
            }
        }

        [Test]
        public void CPU同士の対戦で予知で見たカードが相手向けの文に出ない()
        {
            var checker = new Checker();
            foreach (var first in new[] { Side.Termite, Side.Pest })
                foreach (var seed in Seeds)
                    RunCpuGame(first, seed, checker);

            TestContext.WriteLine($"対戦 {Seeds.Length * 2}戦 / ForesightRevealed {checker.Revealed}件 / ForesightResolved {checker.Resolved}件" +
                                  $" / 相手から見えない予知イベント {checker.HiddenForesight}件 / 予知カードの名前を定型句として許した件数 {checker.CauseNameAllowed}");

            Assert.Greater(checker.Revealed, 0, "ForesightRevealed が1件も出なかった(テストが何も確かめていない)");
            Assert.Greater(checker.Resolved, 0, "ForesightResolved が1件も出なかった(テストが何も確かめていない)");
            Assert.IsEmpty(checker.Failures, $"{checker.Failures.Count}件の問題:\n" + string.Join("\n", checker.Failures.Take(30)));
        }

        [TestCase(Side.Pest)]
        [TestCase(Side.Termite)]
        public void 予知で同名の予知カードをめくって手札に加えても相手向けの文に出ない(Side side)
        {
            // 山札の上3枚に予知を2枚入れ、そのうち1枚を手札に加える(定型句の「予知」と同名になる、いちばん紛らわしい形)
            var s = Game(side, 1);
            var hand = SetHand(s, side, "foresight");
            SetDeck(s, side, "foresight", "foresight", "disrupt", "scout");

            var checker = new Checker();
            int before = s.Log.Count;
            s = Play(s, hand[0]);
            checker.CheckBatch(s.Log, before, s.Log.Count, $"組み立て({side.Label()})・予知を使う");
            Assert.AreEqual(Phase.ForesightChoice, s.Phase);
            Assert.AreEqual(3, s.ForesightCards.Count);

            var take = s.ForesightCards.First(c => c.DefId == "foresight");
            var rest = s.ForesightCards.Where(c => c != take).Select(c => c.Uid).ToList();
            before = s.Log.Count;
            s = Engine.Apply(s, GameAction.ChooseForesight(take.Uid, rest));
            checker.CheckBatch(s.Log, before, s.Log.Count, $"組み立て({side.Label()})・予知を解決");

            Assert.AreEqual(1, checker.Revealed, "ForesightRevealed が1件出るはず");
            Assert.AreEqual(1, checker.Resolved, "ForesightResolved が1件出るはず");
            Assert.AreEqual(2, checker.HiddenForesight);
            Assert.IsEmpty(checker.Failures, $"{checker.Failures.Count}件の問題:\n" + string.Join("\n", checker.Failures));

            // 本人向けの文には、めくったカードと加えたカードの名前が入っている(相手向けと区別できている)
            var revealed = s.Log.Last(e => e.Type == GameEventType.ForesightRevealed);
            var resolved = s.Log.Last(e => e.Type == GameEventType.ForesightResolved);
            StringAssert.Contains(Db.Get("disrupt").Name, revealed.TextFor(side));
            StringAssert.Contains(Db.Get("foresight").Name, resolved.TextFor(side));
        }

        [Test]
        public void 確かめる仕組みが漏れを見つけられる()
        {
            // 相手向け文にカード名を入れたイベントを作り、Checker が漏れとして数えることを確かめる(テストの空振り防止)
            var log = new List<GameEvent>
            {
                new GameEvent
                {
                    Type = GameEventType.ForesightRevealed, Side = Side.Pest, CardId = "foresight",
                    CardIds = new[] { "disrupt", "foresight", "scout" }, Visibility = EventVisibility.PestOnly,
                    Text = "本人向け", TextForOpponent = $"予知で{Db.Get("disrupt").Name}を見た",
                },
                new GameEvent
                {
                    Type = GameEventType.ForesightResolved, Side = Side.Pest, CardId = "foresight",
                    Visibility = EventVisibility.PestOnly,
                    Text = "本人向け", TextForOpponent = "予知で「予知」を手札に加えた",
                },
            };
            var checker = new Checker();
            checker.CheckBatch(log, 0, 1, "作った漏れ1");
            checker.CheckBatch(log, 1, 2, "作った漏れ2");
            Assert.AreEqual(2, checker.Failures.Count, string.Join("\n", checker.Failures));
            StringAssert.Contains(Db.Get("disrupt").Name, checker.Failures[0]);
            StringAssert.Contains(Db.Get("foresight").Name, checker.Failures[1]);
        }
    }
}
