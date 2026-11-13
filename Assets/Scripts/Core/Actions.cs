// プレイヤー(人・CPU)の行動。Engine.Apply(state, action) に渡す。

using System.Collections.Generic;
using System.Linq;

namespace ShiroariBojo.Core
{
    public enum ActionType
    {
        /// <summary>手札のカードを使う(Phase.Main)</summary>
        PlayCard,
        /// <summary>ターンを終える(Phase.Main)。MulliganUid を指定すると手詰まり防止を使う</summary>
        EndTurn,
        /// <summary>予知の解決(Phase.ForesightChoice)</summary>
        ChooseForesight,
        /// <summary>手札上限の捨て札(Phase.HandLimitDiscard)</summary>
        DiscardForHandLimit,
    }

    public sealed class GameAction
    {
        public ActionType Type { get; private set; }
        /// <summary>PlayCard:使うカード。ChooseForesight:手札に加えるカード</summary>
        public int Uid { get; private set; } = -1;
        /// <summary>PlayCard:対象のポイント(添字 0〜7。対象のないカードは -1)</summary>
        public int Point { get; private set; } = -1;
        /// <summary>EndTurn:手詰まり防止で捨てるカード(-1 = 使わない)</summary>
        public int MulliganUid { get; private set; } = -1;
        /// <summary>ChooseForesight:山札の下に置く順(先頭ほど上、末尾が山札の一番下)。DiscardForHandLimit:捨てるカード</summary>
        public IReadOnlyList<int> Uids { get; private set; } = new int[0];

        private GameAction() { }

        public static GameAction Play(int uid, int point = -1) => new GameAction { Type = ActionType.PlayCard, Uid = uid, Point = point };
        public static GameAction EndTurn(int mulliganUid = -1) => new GameAction { Type = ActionType.EndTurn, MulliganUid = mulliganUid };
        public static GameAction ChooseForesight(int takeUid, IEnumerable<int> bottomOrder) =>
            new GameAction { Type = ActionType.ChooseForesight, Uid = takeUid, Uids = bottomOrder.ToArray() };
        public static GameAction DiscardForHandLimit(IEnumerable<int> uids) =>
            new GameAction { Type = ActionType.DiscardForHandLimit, Uids = uids.ToArray() };

        public bool SameAs(GameAction o) =>
            o != null && Type == o.Type && Uid == o.Uid && Point == o.Point && MulliganUid == o.MulliganUid && Uids.SequenceEqual(o.Uids);

        public override string ToString()
        {
            switch (Type)
            {
                case ActionType.PlayCard: return Point >= 0 ? $"Play({Uid}→{Points.All[Point].Id})" : $"Play({Uid})";
                case ActionType.EndTurn: return MulliganUid >= 0 ? $"EndTurn(mulligan {MulliganUid})" : "EndTurn";
                case ActionType.ChooseForesight: return $"Foresight(take {Uid}, bottom [{string.Join(",", Uids)}])";
                default: return $"Discard([{string.Join(",", Uids)}])";
            }
        }
    }
}
