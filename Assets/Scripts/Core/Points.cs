// 8ポイントと、置けるシロアリの種類の対応表(仕様書「ポイント」)。対応表はクライアント確認前の暫定。

using System.Collections.Generic;

namespace ShiroariBojo.Core
{
    public enum Zone { Wet, Dry, Upper }

    public sealed class PointDef
    {
        public int Index { get; }
        public string Id { get; }
        public string Name { get; }
        public Zone Zone { get; }
        /// <summary>庭の枯れ木(特殊ポイント)</summary>
        public bool IsGardenTree { get; }
        public IReadOnlyList<Species> Allowed { get; }

        internal PointDef(int index, string id, string name, Zone zone, bool tree, params Species[] allowed)
        {
            Index = index; Id = id; Name = name; Zone = zone; IsGardenTree = tree; Allowed = allowed;
        }

        public bool Allows(Species sp)
        {
            foreach (var s in Allowed) if (s == sp) return true;
            return false;
        }

        public string ZoneLabel => IsGardenTree ? "乾燥箇所(特殊)" : Zone == Zone.Wet ? "水回り" : Zone == Zone.Dry ? "乾燥箇所" : "上部";
    }

    public static class Points
    {
        public const int Count = 8;

        /// <summary>添字 0〜7。ゲーム状態の Points[i] と同じ並び</summary>
        public static readonly IReadOnlyList<PointDef> All = new[]
        {
            new PointDef(0, "bath", "浴室・洗面所", Zone.Wet, false, Species.Yamato, Species.Ie),
            new PointDef(1, "kitchen", "キッチン", Zone.Wet, false, Species.Yamato, Species.Ie),
            new PointDef(2, "underfloor", "床下・土台", Zone.Dry, false, Species.Ie),
            new PointDef(3, "entrance", "玄関・框", Zone.Dry, false, Species.Ie),
            new PointDef(4, "garden", "庭の枯れ木", Zone.Dry, true, Species.Ie),
            new PointDef(5, "deck", "ウッドデッキ", Zone.Dry, false, Species.Ie),
            new PointDef(6, "beam", "梁・柱", Zone.Upper, false, Species.Ie, Species.Kanzai),
            new PointDef(7, "attic", "屋根裏・天井", Zone.Upper, false, Species.Ie, Species.Kanzai),
        };

        public const int GardenTreeIndex = 4;

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return i;
            return -1;
        }
    }

    /// <summary>ルールの数値。既定値は仕様書のとおり。プレイテストの調整用に差し替えられる</summary>
    public sealed class GameRules
    {
        public int InitialHand = 5;
        /// <summary>ターンの終わりに、手札がこの枚数になるまで補充する</summary>
        public int RefillTo = 5;
        public int HandLimit = 7;
        public int ActionPoints = 3;
        public int PointsToWin = 5;
        public int InfestationRequired = 3;
        public int GardenTreeRequired = 4;
        /// <summary>庭の枯れ木が倒れた後の、完全侵食に必要な枚数</summary>
        public int AfterTreeFallRequired = 2;
        /// <summary>バリア工法が効くターン数(防除側の自分のターンで数える)</summary>
        public int BarrierTurns = 3;
        /// <summary>ベイト工法が発動するまでの自分のターン数(2 = 置いてから2回目の自分のターン開始時)</summary>
        public int BaitDelay = 2;
        public int ForesightLook = 3;
        public int ReinforceDraw = 2;
        public int QueenSearchWorkers = 2;
        /// <summary>無限ループ防止の安全装置(通算ターン数)。ルール上の上限ではない。超えたら引き分け</summary>
        public int SafetyMaxTurns = 1000;
        /// <summary>ログ(GameEvent)を残すか。シミュレーターでは false にして速くする</summary>
        public bool KeepLog = true;

        public static GameRules Default => new GameRules();

        public GameRules Clone() => (GameRules)MemberwiseClone();
    }
}
