// ルールエンジン本体。状態 + 行動 → 新しい状態(引数の状態は変更しない)。
// ルールは仕様書(Notion。docs/spec/spec.md)に従い、細部は sb-proto(index.html)の実装を引き継ぐ。

using System;
using System.Collections.Generic;
using System.Linq;

namespace ShiroariBojo.Core
{
    /// <summary>手札のカード1枚が今使えるか(画面のカードの明暗・対象ポイントの光らせ方に使う)</summary>
    public sealed class PlayOption
    {
        public int Uid { get; internal set; }
        public CardDef Def { get; internal set; }
        public int Cost { get; internal set; }
        public bool Usable { get; internal set; }
        public bool NeedsTarget { get; internal set; }
        /// <summary>使える対象のポイント(添字)。NeedsTarget のときだけ</summary>
        public IReadOnlyList<int> Targets { get; internal set; } = new int[0];
        /// <summary>使えない理由(使えるときは null)</summary>
        public string Reason { get; internal set; }
    }

    public static class Engine
    {
        // ───────────── 公開 API ─────────────

        /// <summary>新しい対戦。first を省略するとコイントス(シード付き乱数)で先攻を決める</summary>
        public static GameState NewGame(CardDatabase cards, uint seed, Side? first = null, GameRules rules = null)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var s = new GameState
            {
                Rules = rules != null ? rules.Clone() : GameRules.Default,
                Cards = cards,
                Players = new[] { new PlayerState { Side = Side.Termite }, new PlayerState { Side = Side.Pest } },
                PointStates = new PointState[Points.Count],
                Phase = Phase.Main,
            };
            for (int i = 0; i < Points.Count; i++) s.PointStates[i] = new PointState { Index = i };

            int uid = 1;
            foreach (Side side in new[] { Side.Termite, Side.Pest })
                foreach (var def in cards.All)
                    for (int k = 0; k < def.CountInDeck(side); k++)
                        s.Player(side).DeckList.Add(new CardInstance(uid++, def.Id));
            foreach (var def in cards.All)
                if (def.Caste == Caste.Queen && def.CountOutside > 0 && !s.QueenReserve.ContainsKey(def.Species))
                    s.QueenReserve[def.Species] = new CardInstance(uid++, def.Id);
            s.NextUid = uid;

            var r = new Runner(s, seed);
            r.Rng.Shuffle(s.Player(Side.Termite).DeckList);
            r.Rng.Shuffle(s.Player(Side.Pest).DeckList);
            var f = first ?? (r.Rng.NextDouble() < 0.5 ? Side.Termite : Side.Pest);
            s.First = f;
            s.Current = f;
            r.Draw(Side.Termite, s.Rules.InitialHand, null);
            r.Draw(Side.Pest, s.Rules.InitialHand, null);
            r.Log(GameEventType.GameStarted, f, first.HasValue ? $"{f.Label()}が先攻" : $"コイントスの結果、{f.Label()}が先攻");
            r.StartTurn(f);
            r.Commit();
            return s;
        }

        /// <summary>状態 + 行動 → 新しい状態。不正な行動は InvalidOperationException</summary>
        public static GameState Apply(GameState state, GameAction action)
        {
            if (!IsLegal(state, action, out var reason))
                throw new InvalidOperationException($"不正な行動 {action}: {reason}");
            var s = state.Clone();
            var r = new Runner(s, s.RngState);
            switch (action.Type)
            {
                case ActionType.PlayCard: r.Play(action.Uid, action.Point); break;
                case ActionType.EndTurn: r.EndTurn(action.MulliganUid); break;
                case ActionType.ChooseForesight: r.ResolveForesight(action.Uid, action.Uids); break;
                case ActionType.DiscardForHandLimit: r.HandLimitDiscard(action.Uids); break;
            }
            r.Commit();
            return s;
        }

        public static bool IsLegal(GameState s, GameAction a) => IsLegal(s, a, out _);

        public static bool IsLegal(GameState s, GameAction a, out string reason)
        {
            reason = null;
            if (a == null) { reason = "行動がありません"; return false; }
            if (s.IsOver) { reason = "対戦は終わっています"; return false; }
            var p = s.CurrentPlayer;
            switch (a.Type)
            {
                case ActionType.PlayCard:
                {
                    if (s.Phase != Phase.Main) { reason = "選択の途中です"; return false; }
                    var card = p.HandList.FirstOrDefault(c => c.Uid == a.Uid);
                    if (card == null) { reason = "手札にないカードです"; return false; }
                    var opt = OptionFor(s, s.Current, card);
                    if (!opt.Usable) { reason = opt.Reason; return false; }
                    if (opt.NeedsTarget && !opt.Targets.Contains(a.Point)) { reason = "そのポイントには使えません"; return false; }
                    if (!opt.NeedsTarget && a.Point != -1) { reason = "対象を選ばないカードです"; return false; }
                    return true;
                }
                case ActionType.EndTurn:
                    if (s.Phase != Phase.Main) { reason = "選択の途中です"; return false; }
                    if (a.MulliganUid >= 0 && !CanMulliganWith(s, a.MulliganUid)) { reason = "手詰まり防止は使えません"; return false; }
                    return true;
                case ActionType.ChooseForesight:
                {
                    if (s.Phase != Phase.ForesightChoice) { reason = "予知の選択中ではありません"; return false; }
                    var ids = s.ForesightCards.Select(c => c.Uid).ToList();
                    if (!ids.Contains(a.Uid)) { reason = "めくったカードではありません"; return false; }
                    var rest = ids.Where(u => u != a.Uid).OrderBy(u => u).ToList();
                    if (!a.Uids.OrderBy(u => u).SequenceEqual(rest)) { reason = "残りのカードを全部、山札の下に置く順で指定してください"; return false; }
                    return true;
                }
                case ActionType.DiscardForHandLimit:
                {
                    if (s.Phase != Phase.HandLimitDiscard) { reason = "手札上限の捨て札選び中ではありません"; return false; }
                    if (a.Uids.Count != s.HandLimitDiscardCount) { reason = $"{s.HandLimitDiscardCount}枚選んでください"; return false; }
                    if (a.Uids.Distinct().Count() != a.Uids.Count) { reason = "同じカードを2回選んでいます"; return false; }
                    if (!a.Uids.All(u => p.HandList.Any(c => c.Uid == u))) { reason = "手札にないカードです"; return false; }
                    return true;
                }
            }
            reason = "不明な行動です";
            return false;
        }

        /// <summary>今の手番の合法手をすべて返す。Main ではターン終了(EndTurn)が必ず含まれる</summary>
        public static List<GameAction> LegalActions(GameState s)
        {
            var list = new List<GameAction>();
            if (s.IsOver) return list;
            var p = s.CurrentPlayer;
            switch (s.Phase)
            {
                case Phase.Main:
                    foreach (var o in HandOptions(s, s.Current))
                    {
                        if (!o.Usable) continue;
                        if (o.NeedsTarget) foreach (var t in o.Targets) list.Add(GameAction.Play(o.Uid, t));
                        else list.Add(GameAction.Play(o.Uid));
                    }
                    list.Add(GameAction.EndTurn());
                    if (CanMulligan(s)) foreach (var c in p.HandList) list.Add(GameAction.EndTurn(c.Uid));
                    break;
                case Phase.ForesightChoice:
                    foreach (var take in s.ForesightCards)
                    {
                        var rest = s.ForesightCards.Where(c => c != take).Select(c => c.Uid).ToList();
                        foreach (var perm in Permutations(rest)) list.Add(GameAction.ChooseForesight(take.Uid, perm));
                    }
                    break;
                case Phase.HandLimitDiscard:
                    foreach (var combo in Combinations(p.HandList.Select(c => c.Uid).ToList(), s.HandLimitDiscardCount))
                        list.Add(GameAction.DiscardForHandLimit(combo));
                    break;
            }
            return list;
        }

        /// <summary>手札の各カードが使えるか。side の手番でなければすべて使えない(理由つき)</summary>
        public static List<PlayOption> HandOptions(GameState s, Side side) =>
            s.Player(side).HandList.Select(c => OptionFor(s, side, c)).ToList();

        /// <summary>手詰まり防止(1枚も使わなかったターンは、補充の前に手札を1枚捨てて1枚引ける)が使えるか</summary>
        public static bool CanMulligan(GameState s)
        {
            var p = s.CurrentPlayer;
            return !s.IsOver && s.Phase == Phase.Main && p.PlayedThisTurn == 0 && p.HandList.Count > 0 && p.DeckList.Count > 0;
        }

        /// <summary>そのカードを side が置ける/使えるポイント(コストや手番は見ない)</summary>
        public static List<int> TargetsFor(GameState s, Side side, CardDef def)
        {
            var list = new List<int>();
            if (!def.NeedsTarget) return list;
            for (int i = 0; i < Points.Count; i++)
                if (CanTarget(s, side, def, i)) list.Add(i);
            return list;
        }

        public static bool CanTarget(GameState s, Side side, CardDef def, int i)
        {
            var p = s.PointStates[i];
            if (p.Destroyed) return false; // 完全侵食されたポイントは、すべてのカードの対象外
            switch (def.Effect)
            {
                case CardEffect.PlaceTermite:
                    return side == Side.Termite && Points.All[i].Allows(def.Species) &&
                           !s.Player(side).PlacedThisTurn[i] && !p.HasBarrier;
                case CardEffect.Swarm: return !p.Swarm;
                case CardEffect.Spray: return !p.Swarm && p.CardList.Count > 0;
                case CardEffect.Bait: return !p.Swarm && !p.HasBait;
                case CardEffect.Barrier: return !p.Swarm && !p.HasBarrier;
            }
            return false;
        }

        /// <summary>手札から使うときのコスト(使えないカードは -1)</summary>
        public static int CostOf(CardDef def) => def.Cost ?? -1;

        // ───────────── 内部 ─────────────

        private static bool CanMulliganWith(GameState s, int uid) => CanMulligan(s) && s.CurrentPlayer.HandList.Any(c => c.Uid == uid);

        private static PlayOption OptionFor(GameState s, Side side, CardInstance card)
        {
            var def = s.Cards.Get(card.DefId);
            var o = new PlayOption { Uid = card.Uid, Def = def, Cost = def.Cost ?? 0, NeedsTarget = def.NeedsTarget };
            var me = s.Player(side);
            var opp = s.Player(side.Opponent());
            string reason = null;
            if (s.IsOver) reason = "対戦は終わっています";
            else if (s.Current != side) reason = "相手のターンです";
            else if (s.Phase != Phase.Main) reason = "選択の途中です";
            else if (def.User == CardUser.Nobody || def.Cost == null) reason = "手札から使えないカードです";
            else if (def.User == CardUser.Termite && side != Side.Termite || def.User == CardUser.Pest && side != Side.Pest) reason = "相手の陣営のカードです";
            else if (def.Cost.Value > me.Ap) reason = "行動力が足りません";
            else if ((def.Effect == CardEffect.Scout || def.Effect == CardEffect.Disrupt) && opp.HandList.Count == 0) reason = "相手の手札がありません";
            else if ((def.Effect == CardEffect.Foresight || def.Effect == CardEffect.Reinforce || def.Effect == CardEffect.ImportFurniture) && me.DeckList.Count == 0) reason = "山札がありません";
            if (reason == null && def.NeedsTarget)
            {
                o.Targets = TargetsFor(s, side, def);
                if (o.Targets.Count == 0) reason = "使える場所がありません";
            }
            o.Reason = reason;
            o.Usable = reason == null;
            return o;
        }

        private static IEnumerable<List<int>> Permutations(List<int> items)
        {
            if (items.Count <= 1) { yield return new List<int>(items); yield break; }
            for (int i = 0; i < items.Count; i++)
            {
                var rest = items.Where((_, k) => k != i).ToList();
                foreach (var tail in Permutations(rest))
                {
                    tail.Insert(0, items[i]);
                    yield return tail;
                }
            }
        }

        private static IEnumerable<List<int>> Combinations(List<int> items, int k)
        {
            if (k == 0) { yield return new List<int>(); yield break; }
            for (int i = 0; i <= items.Count - k; i++)
                foreach (var tail in Combinations(items.Skip(i + 1).ToList(), k - 1))
                {
                    tail.Insert(0, items[i]);
                    yield return tail;
                }
        }

        /// <summary>複製した状態を変更する作業用。Apply の中だけで使う</summary>
        private sealed class Runner
        {
            private readonly GameState _s;
            public readonly Rng Rng;

            public Runner(GameState s, uint rngState) { _s = s; Rng = new Rng(rngState); }

            public void Commit() => _s.RngState = Rng.State;

            private GameRules R => _s.Rules;
            private CardDef Def(string id) => _s.Cards.Get(id);
            private static string PName(int i) => Points.All[i].Name;

            public void Log(GameEventType type, Side side, string text, string cardId = null, int point = -1, int count = 0,
                IReadOnlyList<string> cardIds = null, EventVisibility vis = EventVisibility.Public, string textForOpponent = null)
            {
                if (!R.KeepLog) return;
                _s.LogList.Add(new GameEvent
                {
                    Type = type, Turn = _s.Turn, Side = side, Text = text, CardId = cardId, Point = point, Count = count,
                    CardIds = cardIds ?? new string[0], Visibility = vis,
                    TextForOpponent = vis == EventVisibility.Public ? text : textForOpponent,
                });
            }

            private static EventVisibility OnlyFor(Side side) => side == Side.Termite ? EventVisibility.TermiteOnly : EventVisibility.PestOnly;

            private void Count(Dictionary<string, int> d, string id) => d[id] = (d.TryGetValue(id, out var n) ? n : 0) + 1;

            /// <summary>山札の上から n 枚引く。山札が尽きても捨て札は混ぜ直さない。reason が null ならログを出さない</summary>
            public List<CardInstance> Draw(Side side, int n, string reason)
            {
                var p = _s.Player(side);
                var got = new List<CardInstance>();
                for (int i = 0; i < n && p.DeckList.Count > 0; i++)
                {
                    var c = p.DeckList[0];
                    p.DeckList.RemoveAt(0);
                    p.HandList.Add(c);
                    got.Add(c);
                    Count(_s.Stats.Drawn[(int)side], c.DefId);
                }
                if (reason != null)
                    Log(GameEventType.CardsDrawn, side, $"{reason}:{got.Count}枚引いた({string.Join("・", got.Select(c => Def(c.DefId).Name))})",
                        count: got.Count, cardIds: got.Select(c => c.DefId).ToList(), vis: OnlyFor(side),
                        textForOpponent: $"{reason}:{got.Count}枚引いた");
                return got;
            }

            /// <summary>山札の上から順に探して、条件に合うカードを n 枚まで手札に加える(なければ空振り)。山札は混ぜない</summary>
            private List<CardInstance> SearchDeck(Side side, Func<CardDef, bool> pred, int n)
            {
                var p = _s.Player(side);
                var got = new List<CardInstance>();
                for (int i = 0; i < p.DeckList.Count && got.Count < n;)
                {
                    if (pred(Def(p.DeckList[i].DefId)))
                    {
                        var c = p.DeckList[i];
                        p.DeckList.RemoveAt(i);
                        p.HandList.Add(c);
                        got.Add(c);
                        Count(_s.Stats.Drawn[(int)side], c.DefId);
                    }
                    else i++;
                }
                return got;
            }

            private void ToDiscard(Side owner, int uid, string defId) => _s.Player(owner).DiscardList.Add(new CardInstance(uid, defId));

            public void StartTurn(Side side)
            {
                _s.Turn++;
                _s.Current = side;
                _s.Phase = Phase.Main;
                var p = _s.Player(side);
                p.OwnTurns++;
                p.Ap = R.ActionPoints; // 余った行動力は持ち越さない
                p.PlayedThisTurn = 0;
                p.PlacedThisTurn = new bool[Points.Count];
                if (_s.Turn > R.SafetyMaxTurns)
                {
                    Finish(null, WinReason.TurnLimit);
                    return;
                }
                Log(GameEventType.TurnStarted, side,
                    $"― {side.Label()}のターン(シロアリ山札{_s.Player(Side.Termite).DeckList.Count}枚・防除山札{_s.Player(Side.Pest).DeckList.Count}枚)",
                    count: p.OwnTurns);

                if (side == Side.Pest)
                {
                    foreach (var pt in _s.PointStates)
                    {
                        // バリア工法:置いてから3ターン(防除側の自分のターンで数える)で切れる
                        if (pt.HasBarrier && pt.BarrierUntilPestTurn <= p.OwnTurns)
                        {
                            pt.BarrierUntilPestTurn = 0;
                            Log(GameEventType.BarrierExpired, side, $"{PName(pt.Index)}のバリア工法が切れた", point: pt.Index);
                        }
                        if (pt.Destroyed) continue;
                        // ベイト工法:置いてから2回目の自分のターン開始時に、ヤマト・イエをすべて除去(兵アリでも防げない)
                        if (pt.HasBait && pt.BaitAtPestTurn <= p.OwnTurns)
                        {
                            pt.BaitAtPestTurn = 0;
                            var kept = new List<PlacedCard>();
                            var removed = new List<string>();
                            foreach (var c in pt.CardList)
                            {
                                var sp = Def(c.DefId).Species;
                                if (sp == Species.Yamato || sp == Species.Ie)
                                {
                                    ToDiscard(Side.Termite, c.Uid, c.DefId);
                                    removed.Add(c.DefId);
                                }
                                else kept.Add(c);
                            }
                            pt.CardList = kept;
                            _s.Stats.BaitRemoved += removed.Count;
                            Log(GameEventType.BaitTriggered, side, $"ベイト工法が発動。{PName(pt.Index)}のシロアリを{removed.Count}枚駆除",
                                point: pt.Index, count: removed.Count, cardIds: removed);
                        }
                    }
                }
                else
                {
                    foreach (var pt in _s.PointStates)
                    {
                        // 大量発生:シロアリ側の次のターン開始時に切れる(「次の自分のターンまで」)
                        if (pt.Swarm)
                        {
                            pt.Swarm = false;
                            Log(GameEventType.SwarmExpired, side, $"{PName(pt.Index)}の大量発生が収まった", point: pt.Index);
                        }
                        if (pt.Destroyed) continue;
                        for (int k = 0; k < pt.CardList.Count; k++)
                        {
                            var c = pt.CardList[k];
                            if (c.PromoteAtOwnTurn <= 0 || c.PromoteAtOwnTurn > p.OwnTurns) continue;
                            c.PromoteAtOwnTurn = 0;
                            var winged = Def(c.DefId);
                            if (!_s.QueenReserve.TryGetValue(winged.Species, out var queen))
                            {
                                Log(GameEventType.QueenUnavailable, side,
                                    $"{PName(pt.Index)}の{winged.Name}は女王アリになれなかった(同じ種類の女王アリが場に出ている)", winged.Id, pt.Index);
                                continue;
                            }
                            // 羽アリは捨て札へ。同じ場所に女王アリが入る(1枚分として数える)
                            _s.QueenReserve.Remove(winged.Species);
                            ToDiscard(Side.Termite, c.Uid, c.DefId);
                            c.Uid = queen.Uid;
                            c.DefId = queen.DefId;
                            c.GuardAvailable = false;
                            _s.Stats.QueensPromoted++;
                            var qdef = Def(queen.DefId);
                            Log(GameEventType.QueenPromoted, side, $"{PName(pt.Index)}の{winged.Name}が{qdef.Name}になった", qdef.Id, pt.Index);
                            var got = SearchDeck(Side.Termite, d => d.Caste == Caste.Worker && d.Species == winged.Species, R.QueenSearchWorkers);
                            Log(GameEventType.DeckSearched, side, $"{qdef.Name}の効果で、山札から職アリを{got.Count}枚手札に加えた",
                                qdef.Id, pt.Index, got.Count, got.Select(x => x.DefId).ToList());
                        }
                    }
                }
            }

            public void Play(int uid, int point)
            {
                var side = _s.Current;
                var p = _s.Player(side);
                int idx = p.HandList.FindIndex(c => c.Uid == uid);
                var card = p.HandList[idx];
                var def = Def(card.DefId);
                p.HandList.RemoveAt(idx);
                p.Ap -= def.Cost ?? 0;
                p.PlayedThisTurn++;
                Count(_s.Stats.Played[(int)side], def.Id);
                Log(GameEventType.CardPlayed, side, point >= 0 ? $"{def.Name}を使った → {PName(point)}" : $"{def.Name}を使った", def.Id, point);

                if (def.Effect == CardEffect.PlaceTermite)
                {
                    PlaceTermite(card, def, point);
                    return;
                }
                var opp = _s.Player(side.Opponent());
                switch (def.Effect)
                {
                    case CardEffect.Spray: Spray(side, point); break;
                    case CardEffect.Barrier:
                        _s.PointStates[point].BarrierUntilPestTurn = _s.Player(Side.Pest).OwnTurns + R.BarrierTurns;
                        Log(GameEventType.BarrierPlaced, side, $"バリア工法を{PName(point)}に施工({R.BarrierTurns}ターン)", def.Id, point, R.BarrierTurns);
                        break;
                    case CardEffect.Bait:
                        _s.PointStates[point].BaitAtPestTurn = _s.Player(Side.Pest).OwnTurns + R.BaitDelay;
                        Log(GameEventType.BaitPlaced, side, $"ベイト工法を{PName(point)}に設置({R.BaitDelay}回後の自分のターン開始時に発動)", def.Id, point, R.BaitDelay);
                        break;
                    case CardEffect.Swarm:
                        _s.PointStates[point].Swarm = true;
                        Log(GameEventType.SwarmPlaced, side, $"大量発生。{PName(point)}に次のシロアリ側のターンまで防除・駆除カードを置けない", def.Id, point);
                        break;
                    case CardEffect.ImportFurniture:
                    {
                        var got = SearchDeck(side, d => d.Kind == CardKind.Termite && d.Species == Species.Kanzai, 1);
                        Log(GameEventType.DeckSearched, side,
                            got.Count > 0 ? $"輸入家具購入。山札から{Def(got[0].DefId).Name}を手札に加えた" : "輸入家具購入。山札にカンザイシロアリがなかった(空振り)",
                            def.Id, count: got.Count, cardIds: got.Select(x => x.DefId).ToList(), vis: OnlyFor(side),
                            textForOpponent: $"輸入家具購入。カンザイシロアリを{got.Count}枚手札に加えた");
                        break;
                    }
                    case CardEffect.Reinforce: Draw(side, R.ReinforceDraw, "増援"); break;
                    case CardEffect.Scout:
                    {
                        var ids = opp.HandList.Select(c => c.DefId).ToList();
                        Log(GameEventType.Scouted, side, $"偵察で相手の手札を見た({string.Join("・", ids.Select(x => Def(x).Name))})",
                            def.Id, count: ids.Count, cardIds: ids, vis: OnlyFor(side), textForOpponent: "偵察で手札を見られた");
                        break;
                    }
                    case CardEffect.Disrupt:
                    {
                        int k = Rng.Next(opp.HandList.Count);
                        var c = opp.HandList[k];
                        opp.HandList.RemoveAt(k);
                        opp.DiscardList.Add(c);
                        Log(GameEventType.Disrupted, side, $"妨害で相手の「{Def(c.DefId).Name}」を捨てさせた", c.DefId);
                        break;
                    }
                    case CardEffect.Foresight:
                    {
                        int n = Math.Min(R.ForesightLook, p.DeckList.Count);
                        _s.ForesightList = p.DeckList.Take(n).ToList();
                        p.DeckList.RemoveRange(0, n);
                        _s.Phase = Phase.ForesightChoice;
                        Log(GameEventType.ForesightRevealed, side, $"予知で山札の上{n}枚を見た({string.Join("・", _s.ForesightCards.Select(c => Def(c.DefId).Name))})",
                            def.Id, count: n, cardIds: _s.ForesightCards.Select(c => c.DefId).ToList(), vis: OnlyFor(side),
                            textForOpponent: $"予知で山札の上{n}枚を見た");
                        break;
                    }
                    default:
                        Log(GameEventType.CardPlayed, side, $"{def.Name}の効果は未実装", def.Id);
                        break;
                }
                p.DiscardList.Add(card);
            }

            private void PlaceTermite(CardInstance card, CardDef def, int i)
            {
                var side = _s.Current;
                var pt = _s.PointStates[i];
                pt.CardList.Add(new PlacedCard
                {
                    Uid = card.Uid, DefId = card.DefId, TurnPlaced = _s.Turn,
                    GuardAvailable = def.Caste == Caste.Soldier,
                    PromoteAtOwnTurn = def.Caste == Caste.Winged ? _s.Player(side).OwnTurns + 1 : 0,
                });
                _s.Player(side).PlacedThisTurn[i] = true;
                _s.Stats.TermitesPlaced++;
                Log(GameEventType.TermitePlaced, side, $"{def.Name}を{PName(i)}に置いた({pt.CardList.Count}/{_s.RequiredAt(i)})", def.Id, i, pt.CardList.Count);
                CheckCompletions();
            }

            private void Spray(Side side, int i)
            {
                var pt = _s.PointStates[i];
                // 兵アリ(無効化が残っているもの)がいれば、最後に置かれた1枚の無効化を使って除去しない
                for (int k = pt.CardList.Count - 1; k >= 0; k--)
                {
                    if (!pt.CardList[k].GuardAvailable) continue;
                    pt.CardList[k].GuardAvailable = false;
                    _s.Stats.SprayBlocked++;
                    Log(GameEventType.SprayBlocked, side, $"{PName(i)}に薬剤散布。{Def(pt.CardList[k].DefId).Name}が耐えた", pt.CardList[k].DefId, i);
                    return;
                }
                // 仮:最後に置かれたカードを除去する
                var c = pt.CardList[pt.CardList.Count - 1];
                pt.CardList.RemoveAt(pt.CardList.Count - 1);
                ToDiscard(Side.Termite, c.Uid, c.DefId);
                _s.Stats.SprayRemoved++;
                Log(GameEventType.SprayRemoved, side, $"{PName(i)}に薬剤散布。「{Def(c.DefId).Name}」を駆除", c.DefId, i);
            }

            /// <summary>完全侵食の判定。庭の枯れ木が倒れると必要枚数が減るので、変化がなくなるまで繰り返す</summary>
            private void CheckCompletions()
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var pt in _s.PointStates)
                    {
                        if (pt.Destroyed || pt.CardList.Count == 0 || pt.CardList.Count < _s.RequiredAt(pt.Index)) continue;
                        pt.Destroyed = true;
                        pt.BarrierUntilPestTurn = 0;
                        pt.BaitAtPestTurn = 0;
                        pt.Swarm = false;
                        changed = true;
                        Log(GameEventType.PointDestroyed, Side.Termite, $"{PName(pt.Index)}が完全侵食された({_s.DestroyedCount}/{R.PointsToWin})",
                            point: pt.Index, count: _s.DestroyedCount);
                        if (Points.All[pt.Index].IsGardenTree && !_s.TreeFallen)
                        {
                            _s.TreeFallen = true;
                            _s.Stats.TreeFellTurn = _s.Turn;
                            Log(GameEventType.GardenTreeFell, Side.Termite,
                                $"庭の枯れ木が倒れた。完全侵食に必要な枚数が{R.AfterTreeFallRequired}枚になる", point: pt.Index, count: R.AfterTreeFallRequired);
                        }
                    }
                }
                if (_s.DestroyedCount >= R.PointsToWin) Finish(Side.Termite, WinReason.Infestation);
            }

            public void ResolveForesight(int takeUid, IReadOnlyList<int> bottomOrder)
            {
                var side = _s.Current;
                var p = _s.Player(side);
                var take = _s.ForesightCards.First(c => c.Uid == takeUid);
                p.HandList.Add(take);
                Count(_s.Stats.Drawn[(int)side], take.DefId);
                foreach (var u in bottomOrder) p.DeckList.Add(_s.ForesightCards.First(c => c.Uid == u)); // 末尾 = 山札の一番下
                _s.ForesightList = new List<CardInstance>();
                _s.Phase = Phase.Main;
                Log(GameEventType.ForesightResolved, side, $"予知で「{Def(take.DefId).Name}」を手札に加え、{bottomOrder.Count}枚を山札の下へ置いた",
                    take.DefId, count: bottomOrder.Count, vis: OnlyFor(side),
                    textForOpponent: $"予知で1枚を手札に加え、{bottomOrder.Count}枚を山札の下へ置いた");
            }

            public void EndTurn(int mulliganUid)
            {
                var side = _s.Current;
                var p = _s.Player(side);
                if (mulliganUid >= 0)
                {
                    int idx = p.HandList.FindIndex(c => c.Uid == mulliganUid);
                    var c = p.HandList[idx];
                    p.HandList.RemoveAt(idx);
                    p.DiscardList.Add(c);
                    _s.Stats.Mulligans[(int)side]++;
                    Log(GameEventType.Mulligan, side, $"手詰まり防止:「{Def(c.DefId).Name}」を捨てた", c.DefId);
                    Draw(side, 1, "手詰まり防止");
                }
                // 補充:手札が5枚になるまで引く。5枚を超えていれば引かない
                int need = R.RefillTo - p.HandList.Count;
                if (need > 0) Draw(side, need, "補充");
                if (p.HandList.Count > R.HandLimit)
                {
                    _s.Phase = Phase.HandLimitDiscard;
                    _s.HandLimitDiscardCount = p.HandList.Count - R.HandLimit;
                    return;
                }
                FinishTurn();
            }

            public void HandLimitDiscard(IReadOnlyList<int> uids)
            {
                var side = _s.Current;
                var p = _s.Player(side);
                var names = new List<string>();
                foreach (var u in uids)
                {
                    int idx = p.HandList.FindIndex(c => c.Uid == u);
                    var c = p.HandList[idx];
                    p.HandList.RemoveAt(idx);
                    p.DiscardList.Add(c);
                    names.Add(c.DefId);
                }
                _s.Stats.HandLimitDiscards[(int)side] += uids.Count;
                _s.HandLimitDiscardCount = 0;
                Log(GameEventType.HandLimitDiscarded, side, $"手札上限で{uids.Count}枚捨てた({string.Join("・", names.Select(x => Def(x).Name))})",
                    count: uids.Count, cardIds: names);
                FinishTurn();
            }

            private void FinishTurn()
            {
                var side = _s.Current;
                _s.Phase = Phase.Main;
                Log(GameEventType.TurnEnded, side, $"{side.Label()}のターン終了");
                // 防除業者側の勝利:シロアリ山札がなくなるまで守りきる(シロアリ側のターン終わりの補充の後に判定する)
                if (side == Side.Termite && _s.Player(Side.Termite).DeckList.Count == 0)
                {
                    Finish(Side.Pest, WinReason.DeckOut);
                    return;
                }
                StartTurn(side.Opponent());
            }

            private void Finish(Side? winner, WinReason reason)
            {
                _s.Winner = winner;
                _s.WinReason = reason;
                _s.Phase = Phase.GameOver;
                string text = reason == WinReason.Infestation ? $"シロアリ側の勝利({R.PointsToWin}ポイントを完全侵食)"
                    : reason == WinReason.DeckOut ? "防除業者側の勝利(シロアリの山札が尽きるまで守りきった)"
                    : "ターン数の上限に達したため引き分け";
                Log(GameEventType.GameOver, winner ?? _s.Current, text);
            }
        }
    }
}
