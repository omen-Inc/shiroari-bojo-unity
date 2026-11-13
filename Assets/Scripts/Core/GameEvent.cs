// ゲーム中の出来事。Engine.Apply のたびに GameState.Log の末尾へ追加される。
// 画面は「前の状態の Log.Count 以降」を読めば、その行動で起きたことを順に演出できる。

using System.Collections.Generic;

namespace ShiroariBojo.Core
{
    public enum GameEventType
    {
        GameStarted,
        TurnStarted,
        /// <summary>カードを使った(すべてのカードで最初に出る。続けて効果のイベントが出る)</summary>
        CardPlayed,
        TermitePlaced,
        PointDestroyed,
        GardenTreeFell,
        SprayBlocked,
        SprayRemoved,
        BarrierPlaced,
        BarrierExpired,
        BaitPlaced,
        BaitTriggered,
        SwarmPlaced,
        SwarmExpired,
        QueenPromoted,
        /// <summary>羽アリが女王アリになれなかった(その種類の女王アリがすでに場に出ている)</summary>
        QueenUnavailable,
        /// <summary>山札から特定のカードを手札に加えた(輸入家具購入・女王アリ)。Count = 加えた枚数(0 = 空振り)</summary>
        DeckSearched,
        /// <summary>山札から引いた(補充・増援・手詰まり防止)。CardIds は引いた本人にだけ見せる</summary>
        CardsDrawn,
        Scouted,
        Disrupted,
        ForesightRevealed,
        ForesightResolved,
        Mulligan,
        HandLimitDiscarded,
        TurnEnded,
        GameOver,
    }

    /// <summary>誰に見せてよい出来事か</summary>
    public enum EventVisibility { Public, TermiteOnly, PestOnly }

    public sealed class GameEvent
    {
        public GameEventType Type { get; internal set; }
        public int Turn { get; internal set; }
        /// <summary>その出来事を起こした陣営(ターン開始時の処理は手番の陣営)</summary>
        public Side Side { get; internal set; }
        /// <summary>関係するカードID(なければ null)</summary>
        public string CardId { get; internal set; }
        /// <summary>関係するポイントの添字(なければ -1)</summary>
        public int Point { get; internal set; } = -1;
        /// <summary>枚数など(意味は種類ごと)</summary>
        public int Count { get; internal set; }
        /// <summary>関係する複数のカードID(偵察で見た手札・引いたカードなど)</summary>
        public IReadOnlyList<string> CardIds { get; internal set; } = new string[0];
        public EventVisibility Visibility { get; internal set; } = EventVisibility.Public;
        /// <summary>ログ表示用の日本語。非公開の情報を含む場合は TextForOpponent を相手に見せる</summary>
        public string Text { get; internal set; }
        /// <summary>相手に見せるときの文(null なら相手には見せない)</summary>
        public string TextForOpponent { get; internal set; }

        public bool VisibleTo(Side viewer) =>
            Visibility == EventVisibility.Public ||
            (Visibility == EventVisibility.TermiteOnly && viewer == Side.Termite) ||
            (Visibility == EventVisibility.PestOnly && viewer == Side.Pest);

        /// <summary>viewer に見せる文。見せないなら null</summary>
        public string TextFor(Side viewer) => VisibleTo(viewer) ? Text : TextForOpponent;

        public override string ToString() => $"[{Turn}:{Side.Label()}] {Text}";
    }
}
