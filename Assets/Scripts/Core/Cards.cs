// カード定義と、cards.json(Notion のカード一覧の写し)の読み込み。
// Core はファイルを読まない。呼び出し側が JSON の文字列を渡す(Unity では TextAsset.text)。

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShiroariBojo.Core
{
    /// <summary>陣営。Pest = 防除業者側</summary>
    public enum Side { Termite = 0, Pest = 1 }

    /// <summary>カードを使える陣営</summary>
    public enum CardUser { Termite, Pest, Neutral, Nobody }

    /// <summary>区分(カード一覧の「区分」)</summary>
    public enum CardKind { Termite, Event, Neutral, Control, Extermination, Unknown }

    public enum Species { None, Yamato, Ie, Kanzai }

    public enum Caste { None, Worker, Soldier, Winged, Queen }

    /// <summary>効果の種類。カードID から決まる</summary>
    public enum CardEffect
    {
        /// <summary>シロアリカード(職アリ・兵アリ・羽アリ)を置く</summary>
        PlaceTermite,
        /// <summary>女王アリ。手札からは使えない</summary>
        Queen,
        Spray,
        Barrier,
        Bait,
        Swarm,
        ImportFurniture,
        Reinforce,
        Scout,
        Disrupt,
        Foresight,
        /// <summary>対応表にないカード(Notion で新しく追加されたもの)。使うと捨て札になるだけ</summary>
        Unknown,
    }

    public static class SideExt
    {
        public static Side Opponent(this Side s) => s == Side.Termite ? Side.Pest : Side.Termite;
        public static string Label(this Side s) => s == Side.Termite ? "シロアリ側" : "防除業者側";
    }

    public sealed class CardDef
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        /// <summary>区分の元の文字列(例:「イベント(シロアリ側)」)</summary>
        public string Category { get; internal set; }
        public CardKind Kind { get; internal set; }
        public CardUser User { get; internal set; }
        public CardEffect Effect { get; internal set; }
        public string Status { get; internal set; }
        /// <summary>コスト。女王アリなど手札から使わないカードは null</summary>
        public int? Cost { get; internal set; }
        public int CountTermiteDeck { get; internal set; }
        public int CountPestDeck { get; internal set; }
        public int CountOutside { get; internal set; }
        public string EffectText { get; internal set; }
        public string Memo { get; internal set; }
        public string Basis { get; internal set; }
        public Species Species { get; internal set; }
        public Caste Caste { get; internal set; }

        /// <summary>対象のポイントを選ぶカードか</summary>
        public bool NeedsTarget =>
            Effect == CardEffect.PlaceTermite || Effect == CardEffect.Spray || Effect == CardEffect.Barrier ||
            Effect == CardEffect.Bait || Effect == CardEffect.Swarm;

        public bool IsTermiteCard => Kind == CardKind.Termite;

        public int CountInDeck(Side side) => side == Side.Termite ? CountTermiteDeck : CountPestDeck;

        public override string ToString() => $"{Name}({Id})";
    }

    /// <summary>カード定義の一覧。ステータス「没」は読み込まない</summary>
    public sealed class CardDatabase
    {
        private readonly Dictionary<string, CardDef> _byId = new Dictionary<string, CardDef>();
        private readonly List<CardDef> _all = new List<CardDef>();

        public IReadOnlyList<CardDef> All => _all;
        /// <summary>読み込み時の注意(対応表にないカードなど)</summary>
        public IReadOnlyList<string> Warnings => _warnings;
        private readonly List<string> _warnings = new List<string>();

        public CardDef Get(string id)
        {
            if (_byId.TryGetValue(id, out var d)) return d;
            throw new KeyNotFoundException($"不明なカードID: {id}");
        }

        public bool TryGet(string id, out CardDef def) => _byId.TryGetValue(id, out def);

        /// <summary>種類の女王アリ(山札外のカード)</summary>
        public CardDef QueenOf(Species sp)
        {
            foreach (var d in _all) if (d.Caste == Caste.Queen && d.Species == sp) return d;
            return null;
        }

        /// <summary>陣営の山札の枚数(定義上)</summary>
        public int DeckSize(Side side)
        {
            int n = 0;
            foreach (var d in _all) n += d.CountInDeck(side);
            return n;
        }

        /// <summary>cards.json の文字列から作る</summary>
        public static CardDatabase FromJson(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null || !root.TryGetValue("cards", out var cardsObj) || !(cardsObj is List<object> cards))
                throw new FormatException("cards.json に cards 配列がありません");
            var db = new CardDatabase();
            foreach (var o in cards)
            {
                if (!(o is Dictionary<string, object> rec)) continue;
                string status = Str(rec, "status");
                if (status == "没") continue;
                var def = new CardDef
                {
                    Id = Str(rec, "id"),
                    Name = Str(rec, "name"),
                    Category = Str(rec, "category"),
                    Status = status,
                    Cost = NullableInt(rec, "cost"),
                    EffectText = Str(rec, "effect"),
                    Memo = Str(rec, "memo"),
                    Basis = Str(rec, "basis"),
                };
                if (string.IsNullOrEmpty(def.Id)) throw new FormatException($"id のないカード: {def.Name}");
                if (rec.TryGetValue("counts", out var co) && co is Dictionary<string, object> counts)
                {
                    def.CountTermiteDeck = Int(counts, "termite");
                    def.CountPestDeck = Int(counts, "pest");
                    def.CountOutside = Int(counts, "outside");
                }
                def.Kind = KindOf(def.Category);
                Identify(def);
                def.User = UserOf(def);
                if (def.Effect == CardEffect.Unknown)
                    db._warnings.Add($"効果が未実装のカード: {def.Name}({def.Id})。使うと捨て札になるだけ");
                if (db._byId.ContainsKey(def.Id)) throw new FormatException($"カードID が重複しています: {def.Id}");
                db._byId[def.Id] = def;
                db._all.Add(def);
            }
            return db;
        }

        public static CardKind KindOf(string category)
        {
            if (category == "シロアリ") return CardKind.Termite;
            if (category.StartsWith("イベント", StringComparison.Ordinal)) return CardKind.Event;
            if (category == "中立") return CardKind.Neutral;
            if (category == "防除") return CardKind.Control;
            if (category == "駆除") return CardKind.Extermination;
            return CardKind.Unknown;
        }

        private static CardUser UserOf(CardDef d)
        {
            if (d.Caste == Caste.Queen) return CardUser.Nobody;
            switch (d.Kind)
            {
                case CardKind.Termite: return CardUser.Termite;
                case CardKind.Event: return d.Category.Contains("防除") ? CardUser.Pest : CardUser.Termite;
                case CardKind.Control:
                case CardKind.Extermination: return CardUser.Pest;
                case CardKind.Neutral: return CardUser.Neutral;
            }
            if (d.CountTermiteDeck > 0 && d.CountPestDeck > 0) return CardUser.Neutral;
            return d.CountPestDeck > 0 ? CardUser.Pest : CardUser.Termite;
        }

        private static void Identify(CardDef d)
        {
            // シロアリカードは「カースト-種類」の ID(例:worker-yamato)
            int dash = d.Id.IndexOf('-');
            if (d.Kind == CardKind.Termite && dash > 0)
            {
                string caste = d.Id.Substring(0, dash), sp = d.Id.Substring(dash + 1);
                d.Caste = caste == "worker" ? Caste.Worker : caste == "soldier" ? Caste.Soldier
                    : caste == "winged" ? Caste.Winged : caste == "queen" ? Caste.Queen : Caste.None;
                d.Species = sp == "yamato" ? Species.Yamato : sp == "ie" ? Species.Ie : sp == "kanzai" ? Species.Kanzai : Species.None;
                if (d.Caste != Caste.None && d.Species != Species.None)
                {
                    d.Effect = d.Caste == Caste.Queen ? CardEffect.Queen : CardEffect.PlaceTermite;
                    return;
                }
            }
            switch (d.Id)
            {
                case "spray": d.Effect = CardEffect.Spray; break;
                case "barrier": d.Effect = CardEffect.Barrier; break;
                case "bait": d.Effect = CardEffect.Bait; break;
                case "swarm": d.Effect = CardEffect.Swarm; break;
                case "import-furniture": d.Effect = CardEffect.ImportFurniture; break;
                case "reinforce": d.Effect = CardEffect.Reinforce; break;
                case "scout": d.Effect = CardEffect.Scout; break;
                case "disrupt": d.Effect = CardEffect.Disrupt; break;
                case "foresight": d.Effect = CardEffect.Foresight; break;
                default: d.Effect = CardEffect.Unknown; break;
            }
        }

        private static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";

        private static int Int(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is double x ? (int)x : 0;

        private static int? NullableInt(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is double x ? (int?)(int)x : null;
    }
}
