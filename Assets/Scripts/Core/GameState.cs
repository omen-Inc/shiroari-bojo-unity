// ゲームの状態。Engine.Apply は状態を複製してから変更するので、外から見ると「状態 + 行動 → 新しい状態」になる。
// 画面は GameState を読むだけにして、変更は必ず Engine.Apply を通す。

using System.Collections.Generic;

namespace ShiroariBojo.Core
{
    /// <summary>手札・山札・捨て札にあるカード1枚。不変なので複製した状態どうしで共有してよい</summary>
    public sealed class CardInstance
    {
        public int Uid { get; }
        public string DefId { get; }
        public CardInstance(int uid, string defId) { Uid = uid; DefId = defId; }
        public override string ToString() => $"{DefId}#{Uid}";
    }

    /// <summary>ポイントに置かれたシロアリカード</summary>
    public sealed class PlacedCard
    {
        public int Uid { get; internal set; }
        public string DefId { get; internal set; }
        /// <summary>置いた通算ターン</summary>
        public int TurnPlaced { get; internal set; }
        /// <summary>兵アリの薬剤散布の無効化がまだ残っているか(兵アリ1枚につき1回)</summary>
        public bool GuardAvailable { get; internal set; }
        /// <summary>羽アリが女王アリになるシロアリ側の自分のターン番号(0 = なし)</summary>
        public int PromoteAtOwnTurn { get; internal set; }

        public PlacedCard Clone() => (PlacedCard)MemberwiseClone();
    }

    public sealed class PointState
    {
        public int Index { get; internal set; }
        /// <summary>置かれたシロアリカード。先頭が古く、末尾が最後に置かれたカード</summary>
        public IReadOnlyList<PlacedCard> Cards => CardList;
        internal List<PlacedCard> CardList = new List<PlacedCard>();
        /// <summary>完全侵食されて潰れたか。潰れたポイントはすべてのカードの対象外</summary>
        public bool Destroyed { get; internal set; }
        /// <summary>バリア工法が切れる防除側の自分のターン番号(0 = なし)。この番号のターン開始時に切れる</summary>
        public int BarrierUntilPestTurn { get; internal set; }
        /// <summary>ベイト工法が発動する防除側の自分のターン番号(0 = なし)</summary>
        public int BaitAtPestTurn { get; internal set; }
        /// <summary>大量発生が効いているか。シロアリ側の次のターン開始時に切れる</summary>
        public bool Swarm { get; internal set; }

        public bool HasBarrier => BarrierUntilPestTurn > 0;
        public bool HasBait => BaitAtPestTurn > 0;

        public PointDef Def => Points.All[Index];

        public PointState Clone()
        {
            var p = (PointState)MemberwiseClone();
            p.CardList = new List<PlacedCard>(CardList.Count);
            foreach (var c in CardList) p.CardList.Add(c.Clone());
            return p;
        }
    }

    public sealed class PlayerState
    {
        public Side Side { get; internal set; }
        /// <summary>山札。添字 0 が一番上(次に引くカード)</summary>
        public IReadOnlyList<CardInstance> Deck => DeckList;
        internal List<CardInstance> DeckList = new List<CardInstance>();
        public IReadOnlyList<CardInstance> Hand => HandList;
        internal List<CardInstance> HandList = new List<CardInstance>();
        /// <summary>捨て札(公開情報)。山札には戻らない</summary>
        public IReadOnlyList<CardInstance> Discard => DiscardList;
        internal List<CardInstance> DiscardList = new List<CardInstance>();
        /// <summary>行動力の残り</summary>
        public int Ap { get; internal set; }
        /// <summary>自分のターンを何回始めたか(1 から)</summary>
        public int OwnTurns { get; internal set; }
        /// <summary>このターンに使ったカードの枚数(手詰まり防止ルール用)</summary>
        public int PlayedThisTurn { get; internal set; }
        /// <summary>このターンにシロアリカードを置いたポイント(同じポイントへ1ターン1枚)</summary>
        public bool[] PlacedThisTurn { get; internal set; } = new bool[Points.Count];

        public PlayerState Clone()
        {
            var p = (PlayerState)MemberwiseClone();
            p.DeckList = new List<CardInstance>(DeckList);
            p.HandList = new List<CardInstance>(HandList);
            p.DiscardList = new List<CardInstance>(DiscardList);
            p.PlacedThisTurn = (bool[])PlacedThisTurn.Clone();
            return p;
        }
    }

    public enum Phase
    {
        /// <summary>手番の陣営がカードを使うかターンを終える</summary>
        Main,
        /// <summary>予知の解決待ち(ChooseForesight)</summary>
        ForesightChoice,
        /// <summary>手札上限の捨て札選び待ち(DiscardForHandLimit)</summary>
        HandLimitDiscard,
        GameOver,
    }

    public enum WinReason
    {
        None,
        /// <summary>シロアリ側:5ポイントを完全侵食</summary>
        Infestation,
        /// <summary>防除業者側:シロアリ山札がなくなるまで守りきった</summary>
        DeckOut,
        /// <summary>安全装置のターン上限(引き分け)</summary>
        TurnLimit,
    }

    /// <summary>集計用の数(シミュレーターとリザルト画面用)</summary>
    public sealed class GameStats
    {
        /// <summary>[陣営] カードID → 手札に来た回数(初期手札・補充・効果を含む)</summary>
        public Dictionary<string, int>[] Drawn { get; internal set; } = { new Dictionary<string, int>(), new Dictionary<string, int>() };
        /// <summary>[陣営] カードID → 使った回数</summary>
        public Dictionary<string, int>[] Played { get; internal set; } = { new Dictionary<string, int>(), new Dictionary<string, int>() };
        public int TermitesPlaced { get; internal set; }
        public int SprayRemoved { get; internal set; }
        public int SprayBlocked { get; internal set; }
        public int BaitRemoved { get; internal set; }
        public int QueensPromoted { get; internal set; }
        public int[] Mulligans { get; internal set; } = new int[2];
        public int[] HandLimitDiscards { get; internal set; } = new int[2];
        /// <summary>庭の枯れ木が倒れた通算ターン(0 = 倒れていない)</summary>
        public int TreeFellTurn { get; internal set; }

        public GameStats Clone()
        {
            var s = (GameStats)MemberwiseClone();
            s.Drawn = new[] { new Dictionary<string, int>(Drawn[0]), new Dictionary<string, int>(Drawn[1]) };
            s.Played = new[] { new Dictionary<string, int>(Played[0]), new Dictionary<string, int>(Played[1]) };
            s.Mulligans = (int[])Mulligans.Clone();
            s.HandLimitDiscards = (int[])HandLimitDiscards.Clone();
            return s;
        }
    }

    public sealed class GameState
    {
        public GameRules Rules { get; internal set; }
        /// <summary>カード定義(不変。複製した状態どうしで共有する)</summary>
        public CardDatabase Cards { get; internal set; }
        /// <summary>シード付き乱数の内部状態(シャッフル・妨害のランダム)</summary>
        public uint RngState { get; internal set; }
        /// <summary>通算ターン数(片方の手番1回 = 1ターン)。1 から</summary>
        public int Turn { get; internal set; }
        public Side First { get; internal set; }
        public Side Current { get; internal set; }
        public Phase Phase { get; internal set; }
        /// <summary>[0] = シロアリ側、[1] = 防除業者側</summary>
        public PlayerState[] Players { get; internal set; }
        public PointState[] PointStates { get; internal set; }
        public bool TreeFallen { get; internal set; }
        /// <summary>種類ごとの、まだ場に出ていない女王アリ(山札外)</summary>
        public Dictionary<Species, CardInstance> QueenReserve { get; internal set; } = new Dictionary<Species, CardInstance>();
        /// <summary>予知でめくった山札の上のカード(解決待ちのときだけ)</summary>
        public IReadOnlyList<CardInstance> ForesightCards => ForesightList;
        internal List<CardInstance> ForesightList = new List<CardInstance>();
        /// <summary>手札上限で捨てる枚数(HandLimitDiscard のときだけ)</summary>
        public int HandLimitDiscardCount { get; internal set; }
        /// <summary>勝者。引き分け・対戦中は null</summary>
        public Side? Winner { get; internal set; }
        public WinReason WinReason { get; internal set; }
        public int NextUid { get; internal set; }
        /// <summary>これまでの出来事(古い順)。Rules.KeepLog が false なら空</summary>
        public IReadOnlyList<GameEvent> Log => LogList;
        internal List<GameEvent> LogList = new List<GameEvent>();
        public GameStats Stats { get; internal set; } = new GameStats();

        public bool IsOver => Phase == Phase.GameOver;

        public PlayerState Player(Side s) => Players[(int)s];
        public PlayerState CurrentPlayer => Players[(int)Current];
        public PointState Point(int index) => PointStates[index];

        public int DestroyedCount
        {
            get
            {
                int n = 0;
                foreach (var p in PointStates) if (p.Destroyed) n++;
                return n;
            }
        }

        /// <summary>ポイントの完全侵食に必要な枚数</summary>
        public int RequiredAt(int index)
        {
            if (Points.All[index].IsGardenTree && !TreeFallen) return Rules.GardenTreeRequired;
            return TreeFallen ? Rules.AfterTreeFallRequired : Rules.InfestationRequired;
        }

        /// <summary>バリア工法の残りターン(防除側の自分のターンで数える。0 = なし)</summary>
        public int BarrierRemaining(int index)
        {
            var p = PointStates[index];
            return p.HasBarrier ? p.BarrierUntilPestTurn - Player(Side.Pest).OwnTurns : 0;
        }

        /// <summary>ベイト工法が発動するまでの防除側の自分のターン数(0 = なし。1 = 次の防除側ターン開始時)</summary>
        public int BaitRemaining(int index)
        {
            var p = PointStates[index];
            return p.HasBait ? p.BaitAtPestTurn - Player(Side.Pest).OwnTurns : 0;
        }

        public GameState Clone()
        {
            var s = (GameState)MemberwiseClone();
            s.Players = new[] { Players[0].Clone(), Players[1].Clone() };
            s.PointStates = new PointState[PointStates.Length];
            for (int i = 0; i < PointStates.Length; i++) s.PointStates[i] = PointStates[i].Clone();
            s.QueenReserve = new Dictionary<Species, CardInstance>(QueenReserve);
            s.ForesightList = new List<CardInstance>(ForesightList);
            s.LogList = new List<GameEvent>(LogList);
            s.Stats = Stats.Clone();
            return s;
        }
    }
}
