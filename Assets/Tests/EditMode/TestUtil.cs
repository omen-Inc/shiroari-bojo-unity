using System.IO;
using System.Linq;
using NUnit.Framework;
using ShiroariBojo.Core;
using UnityEngine;

namespace ShiroariBojo.Tests
{
    /// <summary>テスト用の状態づくり。Core の internal を使う(InternalsVisibleTo)</summary>
    internal static class T
    {
        private static CardDatabase _db;

        public static CardDatabase Db => _db ??= CardDatabase.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "cards.json")));

        // ポイントの添字
        public const int Bath = 0, Kitchen = 1, Underfloor = 2, Entrance = 3, Garden = 4, Deck = 5, Beam = 6, Attic = 7;

        /// <summary>指定の陣営が手番の、ターン開始直後の状態</summary>
        public static GameState Game(Side first = Side.Termite, uint seed = 1) => Engine.NewGame(Db, seed, first);

        public static CardInstance New(GameState s, string id) => new CardInstance(s.NextUid++, id);

        public static CardInstance[] SetHand(GameState s, Side side, params string[] ids)
        {
            var p = s.Player(side);
            p.HandList.Clear();
            var cards = ids.Select(id => New(s, id)).ToArray();
            p.HandList.AddRange(cards);
            return cards;
        }

        /// <summary>山札を差し替える(先頭が一番上)</summary>
        public static void SetDeck(GameState s, Side side, params string[] ids)
        {
            var p = s.Player(side);
            p.DeckList.Clear();
            p.DeckList.AddRange(ids.Select(id => New(s, id)));
        }

        /// <summary>山札を n 枚の職アリ-イエ(シロアリ)/増援(防除)にする</summary>
        public static void FillDeck(GameState s, Side side, int n)
        {
            SetDeck(s, side, Enumerable.Repeat(side == Side.Termite ? "worker-ie" : "reinforce", n).ToArray());
        }

        public static void Place(GameState s, int point, params string[] ids)
        {
            foreach (var id in ids)
            {
                var def = Db.Get(id);
                s.PointStates[point].CardList.Add(new PlacedCard
                {
                    Uid = s.NextUid++, DefId = id, TurnPlaced = s.Turn,
                    GuardAvailable = def.Caste == Caste.Soldier,
                    PromoteAtOwnTurn = def.Caste == Caste.Winged ? s.Player(Side.Termite).OwnTurns + 1 : 0,
                });
            }
        }

        public static GameState Play(GameState s, CardInstance c, int point = -1) => Engine.Apply(s, GameAction.Play(c.Uid, point));

        /// <summary>今の手番をそのまま終える(手札上限になったら先頭から捨てる)</summary>
        public static GameState End(GameState s)
        {
            s = Engine.Apply(s, GameAction.EndTurn());
            if (s.Phase == Phase.HandLimitDiscard)
                s = Engine.Apply(s, GameAction.DiscardForHandLimit(s.CurrentPlayer.Hand.Take(s.HandLimitDiscardCount).Select(c => c.Uid)));
            return s;
        }

        public static string[] PointIds(GameState s, int point) => s.Point(point).Cards.Select(c => c.DefId).ToArray();

        public static int TotalCards(GameState s)
        {
            int n = s.QueenReserve.Count + s.ForesightCards.Count;
            foreach (var p in s.Players) n += p.Deck.Count + p.Hand.Count + p.Discard.Count;
            foreach (var pt in s.PointStates) n += pt.Cards.Count;
            return n;
        }

        public static void AssertIllegal(GameState s, GameAction a) => Assert.IsFalse(Engine.IsLegal(s, a), $"{a} は不正のはず");
    }
}
