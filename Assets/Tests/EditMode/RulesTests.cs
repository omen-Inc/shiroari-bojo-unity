using System.Linq;
using NUnit.Framework;
using ShiroariBojo.Core;
using static ShiroariBojo.Tests.T;

namespace ShiroariBojo.Tests
{
    public class CardDataTests
    {
        [Test]
        public void 山札は陣営ごとに50枚_没は読み込まない()
        {
            Assert.AreEqual(50, Db.DeckSize(Side.Termite));
            Assert.AreEqual(50, Db.DeckSize(Side.Pest));
            Assert.IsFalse(Db.All.Any(d => d.Status == "没"));
            Assert.IsEmpty(Db.Warnings);
        }

        [Test]
        public void 女王アリは種類ごとに1枚_山札外_コストなし()
        {
            foreach (var sp in new[] { Species.Yamato, Species.Ie, Species.Kanzai })
            {
                var q = Db.QueenOf(sp);
                Assert.IsNotNull(q);
                Assert.AreEqual(1, q.CountOutside);
                Assert.AreEqual(0, q.CountTermiteDeck + q.CountPestDeck);
                Assert.IsNull(q.Cost);
                Assert.AreEqual(CardUser.Nobody, q.User);
            }
        }

        [Test]
        public void 区分と使える陣営()
        {
            Assert.AreEqual(CardUser.Termite, Db.Get("worker-ie").User);
            Assert.AreEqual(CardUser.Termite, Db.Get("swarm").User);
            Assert.AreEqual(CardUser.Termite, Db.Get("import-furniture").User);
            Assert.AreEqual(CardUser.Pest, Db.Get("spray").User);
            Assert.AreEqual(CardUser.Pest, Db.Get("barrier").User);
            Assert.AreEqual(CardUser.Neutral, Db.Get("reinforce").User);
            Assert.AreEqual(Species.Kanzai, Db.Get("soldier-kanzai").Species);
            Assert.AreEqual(Caste.Winged, Db.Get("winged-yamato").Caste);
            Assert.AreEqual(2, Db.Get("bait").Cost);
        }

        [Test]
        public void MiniJson_エスケープと数値()
        {
            var o = (System.Collections.Generic.Dictionary<string, object>)MiniJson.Parse("{\"a\":\"\\u3042\\n\",\"b\":[1,-2.5e1,true,null]}");
            Assert.AreEqual("あ\n", o["a"]);
            var b = (System.Collections.Generic.List<object>)o["b"];
            Assert.AreEqual(1.0, b[0]);
            Assert.AreEqual(-25.0, b[1]);
            Assert.AreEqual(true, b[2]);
            Assert.IsNull(b[3]);
        }
    }

    public class SetupTests
    {
        [Test]
        public void 初期手札5枚_行動力3_ターン1()
        {
            var s = Game();
            Assert.AreEqual(5, s.Player(Side.Termite).Hand.Count);
            Assert.AreEqual(5, s.Player(Side.Pest).Hand.Count);
            Assert.AreEqual(45, s.Player(Side.Termite).Deck.Count);
            Assert.AreEqual(45, s.Player(Side.Pest).Deck.Count);
            Assert.AreEqual(3, s.CurrentPlayer.Ap);
            Assert.AreEqual(1, s.Turn);
            Assert.AreEqual(3, s.QueenReserve.Count);
            Assert.AreEqual(103, TotalCards(s));
        }

        [Test]
        public void 陣営の山札には自陣営と中立のカードだけが入る()
        {
            var s = Game();
            Assert.IsTrue(s.Player(Side.Termite).Deck.Concat(s.Player(Side.Termite).Hand).All(c => s.Cards.Get(c.DefId).User != CardUser.Pest));
            Assert.IsTrue(s.Player(Side.Pest).Deck.Concat(s.Player(Side.Pest).Hand).All(c => s.Cards.Get(c.DefId).User != CardUser.Termite));
        }

        [Test]
        public void 同じシードなら同じ対戦_違うシードなら違う山札()
        {
            var a = Engine.NewGame(Db, 42);
            var b = Engine.NewGame(Db, 42);
            var c = Engine.NewGame(Db, 43);
            Assert.AreEqual(a.First, b.First);
            CollectionAssert.AreEqual(a.Player(Side.Termite).Deck.Select(x => x.DefId), b.Player(Side.Termite).Deck.Select(x => x.DefId));
            CollectionAssert.AreNotEqual(a.Player(Side.Termite).Deck.Select(x => x.DefId), c.Player(Side.Termite).Deck.Select(x => x.DefId));
        }

        [Test]
        public void 先攻はコイントスで両方出る()
        {
            var firsts = Enumerable.Range(0, 40).Select(i => Engine.NewGame(Db, (uint)i).First).Distinct().Count();
            Assert.AreEqual(2, firsts);
        }

        [Test]
        public void Applyは元の状態を変更しない()
        {
            var s = Game();
            var c = SetHand(s, Side.Termite, "worker-ie", "reinforce")[0];
            int deck = s.Player(Side.Termite).Deck.Count;
            var s2 = Play(s, c, Underfloor);
            Assert.AreEqual(0, s.Point(Underfloor).Cards.Count);
            Assert.AreEqual(2, s.CurrentPlayer.Hand.Count);
            Assert.AreEqual(3, s.CurrentPlayer.Ap);
            Assert.AreEqual(1, s2.Point(Underfloor).Cards.Count);
            Assert.AreEqual(deck, s.Player(Side.Termite).Deck.Count);
        }

        [Test]
        public void 不正な行動は例外()
        {
            var s = Game();
            var c = SetHand(s, Side.Termite, "worker-yamato")[0];
            Assert.Throws<System.InvalidOperationException>(() => Engine.Apply(s, GameAction.Play(c.Uid, Underfloor)));
            Assert.Throws<System.InvalidOperationException>(() => Engine.Apply(s, GameAction.Play(9999, Bath)));
        }
    }

    public class CostTests
    {
        [Test]
        public void コストを行動力から払い_足りなければ使えない()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "soldier-ie", "worker-ie", "worker-ie");
            s = Play(s, h[0], Underfloor); // 2
            Assert.AreEqual(1, s.CurrentPlayer.Ap);
            s = Play(s, h[1], Entrance); // 1
            Assert.AreEqual(0, s.CurrentPlayer.Ap);
            var opt = Engine.HandOptions(s, Side.Termite).Single();
            Assert.IsFalse(opt.Usable);
            Assert.AreEqual("行動力が足りません", opt.Reason);
        }

        [Test]
        public void 余った行動力は持ち越さない()
        {
            var s = Game();
            s = End(s); // シロアリ側は何もしない
            s = End(s); // 防除側
            Assert.AreEqual(Side.Termite, s.Current);
            Assert.AreEqual(3, s.CurrentPlayer.Ap);
        }

        [Test]
        public void 枚数の上限はなく行動力の限り使える()
        {
            var s = Game(Side.Pest);
            Place(s, Underfloor, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Pest, "spray", "spray", "spray");
            s = Play(s, h[0], Underfloor);
            s = Play(s, h[1], Underfloor); // 同じポイントへの連続使用も可
            Assert.AreEqual(0, s.Point(Underfloor).Cards.Count);
            Assert.AreEqual(1, s.CurrentPlayer.Ap);
        }
    }

    public class PlacementTests
    {
        [Test]
        public void 置ける種類の対応表()
        {
            var s = Game();
            CollectionAssert.AreEquivalent(new[] { Bath, Kitchen }, Engine.TargetsFor(s, Side.Termite, Db.Get("worker-yamato")));
            CollectionAssert.AreEquivalent(new[] { Beam, Attic }, Engine.TargetsFor(s, Side.Termite, Db.Get("worker-kanzai")));
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 8), Engine.TargetsFor(s, Side.Termite, Db.Get("worker-ie")));
        }

        [Test]
        public void シロアリは同じポイントへ1ターン1枚_次のターンはまた置ける()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "worker-ie", "worker-ie", "worker-ie");
            s = Play(s, h[0], Underfloor);
            AssertIllegal(s, GameAction.Play(h[1].Uid, Underfloor));
            s = Play(s, h[1], Entrance);
            s = End(End(s));
            var h2 = SetHand(s, Side.Termite, "worker-ie");
            s = Play(s, h2[0], Underfloor);
            Assert.AreEqual(2, s.Point(Underfloor).Cards.Count);
        }

        [Test]
        public void 防除側はシロアリカードを使えない_シロアリ側は防除カードを使えない()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "spray", "reinforce");
            var opts = Engine.HandOptions(s, Side.Termite);
            Assert.IsFalse(opts[0].Usable);
            Assert.AreEqual("相手の陣営のカードです", opts[0].Reason);
            Assert.IsTrue(opts[1].Usable);
            s = End(s);
            SetHand(s, Side.Pest, "worker-ie");
            Assert.IsFalse(Engine.HandOptions(s, Side.Pest)[0].Usable);
        }

        [Test]
        public void 女王アリは手札から使えない()
        {
            var s = Game();
            SetHand(s, Side.Termite, "queen-ie");
            Assert.IsFalse(Engine.HandOptions(s, Side.Termite)[0].Usable);
        }
    }

    public class InfestationTests
    {
        [Test]
        public void 完全侵食は3枚_潰れたポイントはすべてのカードの対象外()
        {
            var s = Game();
            Place(s, Underfloor, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Termite, "worker-ie");
            s = Play(s, h[0], Underfloor);
            Assert.IsTrue(s.Point(Underfloor).Destroyed);
            Assert.AreEqual(1, s.DestroyedCount);
            foreach (var id in new[] { "worker-ie", "swarm" })
                Assert.IsFalse(Engine.TargetsFor(s, Side.Termite, Db.Get(id)).Contains(Underfloor));
            foreach (var id in new[] { "spray", "barrier", "bait" })
                Assert.IsFalse(Engine.TargetsFor(s, Side.Pest, Db.Get(id)).Contains(Underfloor));
        }

        [Test]
        public void 庭の枯れ木は4枚で倒れ_全ポイントが2枚で完全侵食になる()
        {
            var s = Game();
            Assert.AreEqual(4, s.RequiredAt(Garden));
            Assert.AreEqual(3, s.RequiredAt(Bath));
            Place(s, Garden, "worker-ie", "worker-ie", "worker-ie");
            Place(s, Kitchen, "worker-yamato", "worker-yamato"); // 2枚あるポイントは、倒れた時点で完全侵食
            Place(s, Beam, "worker-kanzai");
            var h = SetHand(s, Side.Termite, "worker-ie");
            s = Play(s, h[0], Garden);
            Assert.IsTrue(s.Point(Garden).Destroyed);
            Assert.IsTrue(s.TreeFallen);
            Assert.IsTrue(s.Point(Kitchen).Destroyed);
            Assert.IsFalse(s.Point(Beam).Destroyed);
            Assert.AreEqual(2, s.RequiredAt(Beam));
            Assert.AreEqual(2, s.DestroyedCount);
            Assert.IsTrue(s.Log.Any(e => e.Type == GameEventType.GardenTreeFell));
        }

        [Test]
        public void 枯れ木は3枚では倒れない()
        {
            var s = Game();
            Place(s, Garden, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Termite, "worker-ie");
            s = Play(s, h[0], Garden);
            Assert.IsFalse(s.Point(Garden).Destroyed);
            Assert.IsFalse(s.TreeFallen);
        }

        [Test]
        public void 完全侵食5ポイントでシロアリ側の勝利()
        {
            var s = Game();
            foreach (var i in new[] { Bath, Kitchen, Entrance, Deck }) { Place(s, i, "worker-ie", "worker-ie", "worker-ie"); s.PointStates[i].Destroyed = true; }
            Place(s, Underfloor, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Termite, "worker-ie", "worker-ie");
            s = Play(s, h[0], Underfloor);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(Side.Termite, s.Winner);
            Assert.AreEqual(WinReason.Infestation, s.WinReason);
            Assert.IsEmpty(Engine.LegalActions(s));
        }
    }

    public class SoldierAndSprayTests
    {
        [Test]
        public void 薬剤散布は最後に置かれたカードを捨て札へ()
        {
            var s = Game(Side.Pest);
            Place(s, Bath, "worker-ie", "worker-yamato");
            var h = SetHand(s, Side.Pest, "spray");
            s = Play(s, h[0], Bath);
            CollectionAssert.AreEqual(new[] { "worker-ie" }, PointIds(s, Bath));
            Assert.AreEqual("worker-yamato", s.Player(Side.Termite).Discard.Last().DefId);
            Assert.AreEqual("spray", s.Player(Side.Pest).Discard.Last().DefId);
        }

        [Test]
        public void シロアリカードのないポイントには撒けない()
        {
            var s = Game(Side.Pest);
            var h = SetHand(s, Side.Pest, "spray");
            Assert.IsFalse(Engine.HandOptions(s, Side.Pest)[0].Usable);
        }

        [Test]
        public void 兵アリは1枚につき1回薬剤散布を無効化する()
        {
            var s = Game(Side.Pest);
            Place(s, Underfloor, "soldier-ie", "soldier-ie");
            var h = SetHand(s, Side.Pest, "spray", "spray", "spray");
            s = Play(s, h[0], Underfloor);
            Assert.AreEqual(2, s.Point(Underfloor).Cards.Count);
            s = Play(s, h[1], Underfloor);
            Assert.AreEqual(2, s.Point(Underfloor).Cards.Count);
            s = Play(s, h[2], Underfloor);
            Assert.AreEqual(1, s.Point(Underfloor).Cards.Count);
            Assert.AreEqual(2, s.Log.Count(e => e.Type == GameEventType.SprayBlocked));
        }

        [Test]
        public void 兵アリはベイト工法を防げない()
        {
            var s = Game(Side.Pest);
            Place(s, Underfloor, "soldier-ie", "worker-ie");
            var h = SetHand(s, Side.Pest, "bait");
            s = Play(s, h[0], Underfloor);
            s = End(End(s)); // 防除側の次のターン(1回目)
            Assert.AreEqual(2, s.Point(Underfloor).Cards.Count);
            s = End(End(s)); // 2回目の自分のターン開始時に発動
            Assert.AreEqual(0, s.Point(Underfloor).Cards.Count);
        }
    }

    public class WingedQueenTests
    {
        [Test]
        public void 羽アリは次の自分のターン開始時に女王アリになり_同種の職アリを2枚手札へ()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "worker-yamato", "reinforce", "worker-yamato", "worker-yamato");
            var h = SetHand(s, Side.Termite, "winged-yamato", "reinforce", "reinforce", "reinforce", "reinforce", "reinforce");
            s = Play(s, h[0], Kitchen);
            s = Engine.Apply(s, GameAction.EndTurn()); // 手札5枚なので補充なし
            s = End(s); // 防除側
            Assert.AreEqual(Side.Termite, s.Current);
            Assert.AreEqual("queen-yamato", s.Point(Kitchen).Cards.Single().DefId);
            Assert.AreEqual(1, s.Point(Kitchen).Cards.Count); // 1枚分のまま
            Assert.AreEqual(2, s.CurrentPlayer.Hand.Count(c => c.DefId == "worker-yamato"));
            CollectionAssert.AreEqual(new[] { "worker-ie", "reinforce", "worker-yamato" }, s.CurrentPlayer.Deck.Select(c => c.DefId));
            Assert.IsTrue(s.CurrentPlayer.Discard.Any(c => c.DefId == "winged-yamato"));
            Assert.IsFalse(s.QueenReserve.ContainsKey(Species.Yamato));
            Assert.AreEqual(1, s.Stats.QueensPromoted);
        }

        [Test]
        public void 山札に同種の職アリがなければ空振り()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "worker-ie");
            var h = SetHand(s, Side.Termite, "winged-kanzai", "reinforce", "reinforce", "reinforce", "reinforce", "reinforce");
            s = Play(s, h[0], Beam);
            s = End(End(s));
            Assert.AreEqual("queen-kanzai", s.Point(Beam).Cards.Single().DefId);
            Assert.AreEqual(2, s.CurrentPlayer.Deck.Count);
            Assert.IsTrue(s.Log.Any(e => e.Type == GameEventType.DeckSearched && e.Count == 0));
        }

        [Test]
        public void 女王アリになる前に駆除されたら女王アリにならない()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "winged-ie");
            s = Play(s, h[0], Attic);
            s = End(s);
            var p = SetHand(s, Side.Pest, "spray");
            s = Play(s, p[0], Attic);
            s = End(s);
            Assert.AreEqual(0, s.Point(Attic).Cards.Count);
            Assert.IsTrue(s.QueenReserve.ContainsKey(Species.Ie));
            Assert.AreEqual(0, s.Stats.QueensPromoted);
        }

        [Test]
        public void 同じ種類の女王アリが場にいれば2枚目の羽アリは女王アリにならない()
        {
            var s = Game();
            s.QueenReserve.Remove(Species.Ie);
            var h = SetHand(s, Side.Termite, "winged-ie");
            s = Play(s, h[0], Attic);
            s = End(End(s));
            Assert.AreEqual("winged-ie", s.Point(Attic).Cards.Single().DefId);
            Assert.IsTrue(s.Log.Any(e => e.Type == GameEventType.QueenUnavailable));
        }

        [Test]
        public void 女王アリも薬剤散布で除去できる()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "winged-ie");
            s = Play(s, h[0], Attic);
            s = End(End(s));
            s = End(s);
            var p = SetHand(s, Side.Pest, "spray");
            s = Play(s, p[0], Attic);
            Assert.AreEqual(0, s.Point(Attic).Cards.Count);
        }
    }

    public class ControlCardTests
    {
        [Test]
        public void バリア工法は3ターンの間シロアリカードを置けなくする()
        {
            var s = Game(Side.Pest);
            var h = SetHand(s, Side.Pest, "barrier");
            s = Play(s, h[0], Underfloor);
            Assert.AreEqual(3, s.BarrierRemaining(Underfloor));
            for (int k = 0; k < 3; k++)
            {
                s = End(s); // → シロアリ側
                Assert.IsFalse(Engine.TargetsFor(s, Side.Termite, Db.Get("worker-ie")).Contains(Underfloor), $"{k + 1}ターン目");
                s = End(s); // → 防除側
            }
            // 防除側の3回目の次のターン開始時に切れている
            Assert.IsFalse(s.Point(Underfloor).HasBarrier);
            s = End(s);
            Assert.IsTrue(Engine.TargetsFor(s, Side.Termite, Db.Get("worker-ie")).Contains(Underfloor));
        }

        [Test]
        public void バリア工法は重ねて置けない_ほかのポイントには置ける()
        {
            var s = Game(Side.Pest);
            var h = SetHand(s, Side.Pest, "barrier");
            s = Play(s, h[0], Underfloor);
            Assert.IsFalse(Engine.CanTarget(s, Side.Pest, Db.Get("barrier"), Underfloor));
            Assert.IsTrue(Engine.CanTarget(s, Side.Pest, Db.Get("barrier"), Entrance));
        }

        [Test]
        public void ベイト工法は2回目の自分のターン開始時にヤマトとイエをすべて除去_カンザイは残る()
        {
            var s = Game(Side.Pest);
            Place(s, Attic, "worker-ie", "worker-kanzai", "soldier-ie");
            var h = SetHand(s, Side.Pest, "bait");
            s = Play(s, h[0], Attic);
            Assert.AreEqual(2, s.BaitRemaining(Attic));
            Assert.IsFalse(Engine.CanTarget(s, Side.Pest, Db.Get("bait"), Attic)); // 重ね置き不可
            s = End(End(s));
            Assert.AreEqual(3, s.Point(Attic).Cards.Count);
            Assert.AreEqual(1, s.BaitRemaining(Attic));
            s = End(End(s));
            CollectionAssert.AreEqual(new[] { "worker-kanzai" }, PointIds(s, Attic));
            Assert.IsFalse(s.Point(Attic).HasBait);
            Assert.AreEqual(2, s.Stats.BaitRemoved);
        }

        [Test]
        public void 大量発生はシロアリ側の次のターンまで防除駆除カードを置けなくする()
        {
            var s = Game();
            Place(s, Underfloor, "worker-ie");
            var h = SetHand(s, Side.Termite, "swarm", "worker-ie");
            s = Play(s, h[0], Underfloor);
            Assert.IsTrue(s.Point(Underfloor).Swarm);
            s = Play(s, h[1], Underfloor); // シロアリ側は置ける
            s = End(s);
            foreach (var id in new[] { "spray", "barrier", "bait" })
                Assert.IsFalse(Engine.CanTarget(s, Side.Pest, Db.Get(id), Underfloor), id);
            s = End(s); // シロアリ側のターン開始時に切れる
            Assert.IsFalse(s.Point(Underfloor).Swarm);
            Assert.IsTrue(Engine.CanTarget(s, Side.Pest, Db.Get("spray"), Underfloor));
        }

        [Test]
        public void 輸入家具購入は山札で最初に見つかったカンザイを手札へ_なければ空振り()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "soldier-kanzai", "worker-kanzai");
            var h = SetHand(s, Side.Termite, "import-furniture", "import-furniture");
            s = Play(s, h[0]);
            Assert.AreEqual("soldier-kanzai", s.CurrentPlayer.Hand.Last().DefId);
            CollectionAssert.AreEqual(new[] { "worker-ie", "worker-kanzai" }, s.CurrentPlayer.Deck.Select(c => c.DefId));
            SetDeck(s, Side.Termite, "worker-ie");
            s = Play(s, h[1]);
            Assert.AreEqual(1, s.CurrentPlayer.Deck.Count);
            Assert.IsTrue(s.Log.Last().Type == GameEventType.DeckSearched && s.Log.Last().Count == 0);
        }
    }

    public class NeutralCardTests
    {
        [Test]
        public void 増援は自分の山札から2枚引く()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "swarm", "worker-yamato");
            var h = SetHand(s, Side.Termite, "reinforce");
            s = Play(s, h[0]);
            CollectionAssert.AreEqual(new[] { "worker-ie", "swarm" }, s.CurrentPlayer.Hand.Select(c => c.DefId));
            Assert.AreEqual(1, s.CurrentPlayer.Deck.Count);
        }

        [Test]
        public void 偵察は相手の手札を見る_相手の手札がなければ使えない()
        {
            var s = Game();
            SetHand(s, Side.Pest, "spray", "bait");
            var h = SetHand(s, Side.Termite, "scout");
            s = Play(s, h[0]);
            var e = s.Log.Last(x => x.Type == GameEventType.Scouted);
            CollectionAssert.AreEqual(new[] { "spray", "bait" }, e.CardIds);
            Assert.IsTrue(e.VisibleTo(Side.Termite));
            Assert.IsFalse(e.VisibleTo(Side.Pest));
            SetHand(s, Side.Pest);
            SetHand(s, Side.Termite, "scout", "disrupt");
            Assert.IsTrue(Engine.HandOptions(s, Side.Termite).All(o => !o.Usable && o.Reason == "相手の手札がありません"));
        }

        [Test]
        public void 妨害は相手の手札をランダムに1枚捨てさせる()
        {
            var s = Game();
            SetHand(s, Side.Pest, "spray", "bait", "barrier");
            var h = SetHand(s, Side.Termite, "disrupt");
            int disc = s.Player(Side.Pest).Discard.Count;
            s = Play(s, h[0]);
            Assert.AreEqual(2, s.Player(Side.Pest).Hand.Count);
            Assert.AreEqual(disc + 1, s.Player(Side.Pest).Discard.Count);
        }

        [Test]
        public void 予知は山札の上3枚から1枚を手札へ_残りは好きな順で山札の下へ()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "swarm", "worker-yamato", "reinforce");
            var h = SetHand(s, Side.Termite, "foresight");
            s = Play(s, h[0]);
            Assert.AreEqual(Phase.ForesightChoice, s.Phase);
            Assert.AreEqual(3, s.ForesightCards.Count);
            Assert.AreEqual(6, Engine.LegalActions(s).Count);
            AssertIllegal(s, GameAction.EndTurn());
            var fc = s.ForesightCards;
            var take = fc[1]; // swarm
            s = Engine.Apply(s, GameAction.ChooseForesight(take.Uid, new[] { fc[2].Uid, fc[0].Uid }));
            Assert.AreEqual(Phase.Main, s.Phase);
            CollectionAssert.AreEqual(new[] { "swarm" }, s.CurrentPlayer.Hand.Select(c => c.DefId));
            CollectionAssert.AreEqual(new[] { "reinforce", "worker-yamato", "worker-ie" }, s.CurrentPlayer.Deck.Select(c => c.DefId));
        }

        [Test]
        public void 予知は残りを全部指定しないと不正()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "swarm", "worker-yamato");
            var h = SetHand(s, Side.Termite, "foresight");
            s = Play(s, h[0]);
            AssertIllegal(s, GameAction.ChooseForesight(s.ForesightCards[0].Uid, new[] { s.ForesightCards[1].Uid }));
        }

        [Test]
        public void 山札がなければ増援と予知は使えない()
        {
            var s = Game();
            SetDeck(s, Side.Termite);
            SetHand(s, Side.Termite, "reinforce", "foresight", "import-furniture");
            Assert.IsTrue(Engine.HandOptions(s, Side.Termite).All(o => !o.Usable));
        }
    }

    public class TurnEndTests
    {
        [Test]
        public void ターンの終わりに5枚まで補充_ターン開始時のドローはない()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "worker-ie", "worker-ie");
            s = Play(s, h[0], Underfloor);
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.AreEqual(5, s.Player(Side.Termite).Hand.Count);
            Assert.AreEqual(Side.Pest, s.Current);
            Assert.AreEqual(5, s.Player(Side.Pest).Hand.Count); // ターン開始時に引かない
        }

        [Test]
        public void 手札が5枚を超えていれば引かない()
        {
            var s = Game();
            SetHand(s, Side.Termite, "worker-ie", "worker-ie", "worker-ie", "worker-ie", "worker-ie", "worker-ie");
            int deck = s.CurrentPlayer.Deck.Count;
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.AreEqual(6, s.Player(Side.Termite).Hand.Count);
            Assert.AreEqual(deck, s.Player(Side.Termite).Deck.Count);
        }

        [Test]
        public void 手詰まり防止は1枚も使わなかったターンだけ_1枚捨てて1枚引く()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "worker-yamato", "worker-ie", "worker-ie", "worker-ie", "worker-ie");
            Assert.IsTrue(Engine.CanMulligan(s));
            var s2 = Engine.Apply(s, GameAction.EndTurn(h[0].Uid));
            Assert.AreEqual(5, s2.Player(Side.Termite).Hand.Count);
            Assert.IsFalse(s2.Player(Side.Termite).Hand.Any(c => c.Uid == h[0].Uid));
            Assert.AreEqual("worker-yamato", s2.Player(Side.Termite).Discard.Last().DefId);
            Assert.AreEqual(1, s2.Stats.Mulligans[0]);

            var s3 = Play(s, h[1], Underfloor);
            Assert.IsFalse(Engine.CanMulligan(s3));
            AssertIllegal(s3, GameAction.EndTurn(h[2].Uid));
        }

        [Test]
        public void 手札上限7枚_8枚以上なら7枚になるまで好きなカードを捨てる()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, Enumerable.Repeat("worker-ie", 9).ToArray());
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.AreEqual(Phase.HandLimitDiscard, s.Phase);
            Assert.AreEqual(2, s.HandLimitDiscardCount);
            Assert.AreEqual(Side.Termite, s.Current);
            AssertIllegal(s, GameAction.DiscardForHandLimit(new[] { h[0].Uid }));
            AssertIllegal(s, GameAction.DiscardForHandLimit(new[] { h[0].Uid, h[0].Uid }));
            int disc = s.Player(Side.Termite).Discard.Count;
            s = Engine.Apply(s, GameAction.DiscardForHandLimit(new[] { h[3].Uid, h[5].Uid }));
            Assert.AreEqual(7, s.Player(Side.Termite).Hand.Count);
            Assert.AreEqual(disc + 2, s.Player(Side.Termite).Discard.Count);
            Assert.AreEqual(Side.Pest, s.Current);
        }

        [Test]
        public void 山札が尽きても捨て札を混ぜ直さない()
        {
            var s = Game(Side.Pest);
            SetDeck(s, Side.Pest, "reinforce");
            SetHand(s, Side.Pest);
            s.Player(Side.Pest).DiscardList.Add(New(s, "spray"));
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.AreEqual(1, s.Player(Side.Pest).Hand.Count);
            Assert.AreEqual(0, s.Player(Side.Pest).Deck.Count);
            Assert.AreEqual(1, s.Player(Side.Pest).Discard.Count);
        }

        [Test]
        public void シロアリ山札が尽きたらシロアリ側のターンの終わりに防除業者側の勝利()
        {
            var s = Game();
            SetDeck(s, Side.Termite, "worker-ie", "worker-ie");
            SetHand(s, Side.Termite, "worker-ie", "worker-ie", "worker-ie");
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(Side.Pest, s.Winner);
            Assert.AreEqual(WinReason.DeckOut, s.WinReason);
        }

        [Test]
        public void 防除山札が尽きても対戦は続く()
        {
            var s = Game(Side.Pest);
            SetDeck(s, Side.Pest);
            s = Engine.Apply(s, GameAction.EndTurn());
            Assert.IsFalse(s.IsOver);
            Assert.AreEqual(Side.Termite, s.Current);
        }
    }

    public class LegalActionTests
    {
        [Test]
        public void 合法手はすべてApplyでき_ランダムに打っても例外なく終わる_カードの総数は変わらない()
        {
            for (uint g = 0; g < 30; g++)
            {
                var s = Engine.NewGame(Db, g);
                var rng = new Rng(g + 1000);
                int n = 0;
                while (!s.IsOver)
                {
                    var acts = Engine.LegalActions(s);
                    Assert.IsNotEmpty(acts);
                    foreach (var a in acts) Assert.IsTrue(Engine.IsLegal(s, a), a.ToString());
                    // ターン終了ばかりにならないよう、カードを使う手を多めに選ぶ
                    var plays = acts.Where(a => a.Type != ActionType.EndTurn).ToList();
                    var pick = plays.Count > 0 && rng.NextDouble() < 0.8 ? plays[rng.Next(plays.Count)] : acts[rng.Next(acts.Count)];
                    s = Engine.Apply(s, pick);
                    Assert.AreEqual(103, TotalCards(s), $"game {g} action {n}");
                    Assert.LessOrEqual(++n, 20000);
                }
            }
        }

        [Test]
        public void ターン終了は常に合法()
        {
            var s = Game();
            Assert.IsTrue(Engine.LegalActions(s).Any(a => a.Type == ActionType.EndTurn && a.MulliganUid < 0));
        }

        [Test]
        public void ログは公開と非公開を分ける()
        {
            var s = Game();
            var h = SetHand(s, Side.Termite, "worker-ie");
            s = Play(s, h[0], Underfloor);
            s = Engine.Apply(s, GameAction.EndTurn());
            var drawn = s.Log.Last(e => e.Type == GameEventType.CardsDrawn);
            Assert.IsTrue(drawn.VisibleTo(Side.Termite));
            Assert.IsFalse(drawn.VisibleTo(Side.Pest));
            Assert.AreEqual("補充:5枚引いた", drawn.TextFor(Side.Pest));
            Assert.IsNotEmpty(drawn.CardIds);
            Assert.IsTrue(s.Log.Any(e => e.Type == GameEventType.TermitePlaced && e.Point == Underfloor && e.VisibleTo(Side.Pest)));
        }
    }
}
