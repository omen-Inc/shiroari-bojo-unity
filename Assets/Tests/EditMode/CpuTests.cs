using System.Linq;
using NUnit.Framework;
using ShiroariBojo.Core;
using ShiroariBojo.Core.AI;
using ShiroariBojo.Core.Simulation;
using static ShiroariBojo.Tests.T;

namespace ShiroariBojo.Tests
{
    public class CpuTests
    {
        [Test]
        public void CPU同士200戦が例外なく終わる()
        {
            var res = new SimulationResult();
            for (uint g = 0; g < 200; g++)
            {
                var s = Simulator.RunGame(Db, 5000 + g);
                Assert.IsTrue(s.IsOver);
                Assert.AreNotEqual(WinReason.TurnLimit, s.WinReason, $"seed {5000 + g} が安全装置で終わった");
                Assert.AreEqual(103, TotalCards(s));
                Simulator.Accumulate(res, s);
            }
            Assert.AreEqual(200, res.TermiteWins + res.PestWins);
            Assert.Greater(res.TermiteWins, 0);
            Assert.Greater(res.PestWins, 0);
        }

        [Test]
        public void 同じシードなら同じ結果()
        {
            var a = Simulator.RunGame(Db, 77);
            var b = Simulator.RunGame(Db, 77);
            Assert.AreEqual(a.Turn, b.Turn);
            Assert.AreEqual(a.Winner, b.Winner);
            CollectionAssert.AreEqual(a.Log.Select(e => e.Text), b.Log.Select(e => e.Text));
        }

        [Test]
        public void CPUは相手の手札と山札の中身を見ない()
        {
            // 相手の手札・山札の中身(枚数は同じ)と自分の山札の並びだけが違う2つの状態で、CPU の選ぶ手が同じになる
            for (uint seed = 0; seed < 30; seed++)
            {
                var s1 = Engine.NewGame(Db, seed, Side.Termite);
                Place(s1, Underfloor, "worker-ie");
                var s2 = s1.Clone();
                var opp = s2.Player(Side.Pest);
                int nh = opp.Hand.Count, nd = opp.Deck.Count;
                SetHand(s2, Side.Pest, Enumerable.Repeat("bait", nh).ToArray());
                SetDeck(s2, Side.Pest, Enumerable.Repeat("barrier", nd).ToArray());
                s2.Player(Side.Termite).DeckList.Reverse();
                var a1 = new CpuPlayer(seed).ChooseAction(new PlayerView(s1, Side.Termite));
                var a2 = new CpuPlayer(seed).ChooseAction(new PlayerView(s2, Side.Termite));
                Assert.IsTrue(a1.SameAs(a2), $"seed {seed}: {a1} / {a2}");
            }
        }

        [Test]
        public void CPUは完全侵食に届く手を優先する()
        {
            var s = Game();
            Place(s, Underfloor, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Termite, "worker-ie");
            var a = new CpuPlayer(1).ChooseAction(new PlayerView(s, Side.Termite));
            Assert.AreEqual(ActionType.PlayCard, a.Type);
            Assert.AreEqual(Underfloor, a.Point);
        }

        [Test]
        public void CPUの防除側はあと1枚で完全侵食のポイントに薬剤散布する()
        {
            var s = Game(Side.Pest);
            Place(s, Underfloor, "worker-ie", "worker-ie");
            Place(s, Bath, "worker-ie");
            var h = SetHand(s, Side.Pest, "spray");
            var a = new CpuPlayer(1).ChooseAction(new PlayerView(s, Side.Pest));
            Assert.AreEqual(h[0].Uid, a.Uid);
            Assert.AreEqual(Underfloor, a.Point);
        }

        [Test]
        public void CPUは使えるカードがなければ手詰まり防止で引き直す()
        {
            var s = Game(Side.Pest);
            var h = SetHand(s, Side.Pest, "spray", "spray");
            var a = new CpuPlayer(1).ChooseAction(new PlayerView(s, Side.Pest));
            Assert.AreEqual(ActionType.EndTurn, a.Type);
            Assert.IsTrue(h.Any(c => c.Uid == a.MulliganUid));
        }

        [Test]
        public void シミュレーターのレポートに勝率と使用率が出る()
        {
            var r = Simulator.Run(Db, 20, 1);
            var text = r.ToReport(Db);
            StringAssert.Contains("シロアリ側の勝率", text);
            StringAssert.Contains("カード別使用率", text);
            Assert.AreEqual(20, r.Games);
        }
    }
}
