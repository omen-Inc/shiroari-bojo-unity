// 片方の陣営から見えるものだけを出す読み取り専用のビュー。
// CPU は GameState ではなくこれを受け取るので、相手の手札・山札の中身(と自分の山札の並び)を見られない。

using System.Collections.Generic;
using System.Linq;

namespace ShiroariBojo.Core
{
    public sealed class PlayerView
    {
        private readonly GameState _s;

        public PlayerView(GameState state, Side viewer)
        {
            _s = state;
            Viewer = viewer;
        }

        public Side Viewer { get; }
        public CardDatabase Cards => _s.Cards;
        public GameRules Rules => _s.Rules;
        public int Turn => _s.Turn;
        public Side Current => _s.Current;
        public bool IsMyTurn => _s.Current == Viewer && !_s.IsOver;
        public Phase Phase => _s.Phase;
        public bool TreeFallen => _s.TreeFallen;
        public Side? Winner => _s.Winner;
        public bool IsOver => _s.IsOver;

        // 自分の情報
        public IReadOnlyList<CardInstance> MyHand => _s.Player(Viewer).Hand;
        public int MyDeckCount => _s.Player(Viewer).Deck.Count;
        public IReadOnlyList<CardInstance> MyDiscard => _s.Player(Viewer).Discard;
        public int MyAp => _s.Player(Viewer).Ap;
        public int MyPlayedThisTurn => _s.Player(Viewer).PlayedThisTurn;
        public bool PlacedThisTurn(int point) => _s.Player(Viewer).PlacedThisTurn[point];

        // 相手の情報(公開分だけ)
        public int OpponentHandCount => _s.Player(Viewer.Opponent()).Hand.Count;
        public int OpponentDeckCount => _s.Player(Viewer.Opponent()).Deck.Count;
        public IReadOnlyList<CardInstance> OpponentDiscard => _s.Player(Viewer.Opponent()).Discard;

        public int DeckCount(Side side) => _s.Player(side).Deck.Count;

        // 盤面(公開情報)
        public PointState Point(int i) => _s.PointStates[i];
        public int RequiredAt(int i) => _s.RequiredAt(i);
        public int DestroyedCount => _s.DestroyedCount;
        public int BarrierRemaining(int i) => _s.BarrierRemaining(i);
        public int BaitRemaining(int i) => _s.BaitRemaining(i);
        public bool QueenAvailable(Species sp) => _s.QueenReserve.ContainsKey(sp);

        /// <summary>予知でめくったカード(自分の解決待ちのときだけ。それ以外は空)</summary>
        public IReadOnlyList<CardInstance> ForesightCards =>
            _s.Phase == Phase.ForesightChoice && _s.Current == Viewer ? _s.ForesightCards : (IReadOnlyList<CardInstance>)new CardInstance[0];

        public int HandLimitDiscardCount => _s.Phase == Phase.HandLimitDiscard && _s.Current == Viewer ? _s.HandLimitDiscardCount : 0;

        /// <summary>自分の手番の合法手(手番でなければ空)。合法手は自分が知ってよい情報だけで決まる</summary>
        public List<GameAction> LegalActions() => IsMyTurn ? Engine.LegalActions(_s) : new List<GameAction>();

        public List<PlayOption> HandOptions() => Engine.HandOptions(_s, Viewer);

        public bool CanMulligan => IsMyTurn && Engine.CanMulligan(_s);

        /// <summary>自分に見えるログ</summary>
        public IEnumerable<GameEvent> VisibleLog => _s.Log.Where(e => e.VisibleTo(Viewer));
    }
}
