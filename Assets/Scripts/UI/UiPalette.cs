// 画面の色(sb-proto の配色を引き継ぐ)。シロアリ側 = 赤、防除業者側 = 緑青、中立 = 灰。

using ShiroariBojo.Core;
using UnityEngine;

namespace ShiroariBojo.UI
{
    public static class UiPalette
    {
        public static readonly Color Background = Hex(0x2f2a24);
        public static readonly Color Mat = Hex(0xf3ead8);
        public static readonly Color Paper = Hex(0xfffdf8);
        public static readonly Color Ink = Hex(0x2b231c);
        public static readonly Color Muted = Hex(0x6f6254);
        public static readonly Color Line = Hex(0xddd1bd);

        public static readonly Color Termite = Hex(0xb8402f);
        public static readonly Color TermiteSoft = Hex(0xf3d9d2);
        public static readonly Color Pest = Hex(0x2b7d72);
        public static readonly Color PestSoft = Hex(0xd6ece8);
        public static readonly Color Neutral = Hex(0x6b7480);
        public static readonly Color NeutralSoft = Hex(0xe3e6ea);

        public static readonly Color Barrier = Hex(0x2b7d72);
        public static readonly Color Bait = Hex(0x2f6fa8);
        public static readonly Color Swarm = Hex(0xd0782c);
        public static readonly Color Danger = Hex(0xbf3a2a);
        public static readonly Color DangerSoft = Hex(0xfbe3dd);
        public static readonly Color Gold = Hex(0xe0a82e);
        /// <summary>白地の上で読める金色(文字用)</summary>
        public static readonly Color GoldText = Hex(0xa8740f);

        public static readonly Color Wet = Hex(0x4f8fb5);
        public static readonly Color Dry = Hex(0xa9855a);
        public static readonly Color Upper = Hex(0x6c7a88);
        public static readonly Color Tree = Hex(0xc9962c);
        public static readonly Color TreeSoft = Hex(0xfff3d6);

        public static readonly Color Yamato = Hex(0xc27a2c);
        public static readonly Color Ie = Hex(0xb8402f);
        public static readonly Color Kanzai = Hex(0x6d52a6);

        public static Color SideColor(Side s) => s == Side.Termite ? Termite : Pest;
        public static Color SideSoft(Side s) => s == Side.Termite ? TermiteSoft : PestSoft;
        /// <summary>暗い背景(ログ)の上で読める陣営色</summary>
        public static Color SideOnDark(Side s) => s == Side.Termite ? Hex(0xf29a85) : Hex(0x74cdbf);

        public static Color UserColor(CardUser u) =>
            u == CardUser.Termite ? Termite : u == CardUser.Pest ? Pest : Neutral;

        public static Color ZoneColor(PointDef p) =>
            p.IsGardenTree ? Tree : p.Zone == Zone.Wet ? Wet : p.Zone == Zone.Upper ? Upper : Dry;

        public static Color SpeciesColor(Species sp) =>
            sp == Species.Yamato ? Yamato : sp == Species.Ie ? Ie : sp == Species.Kanzai ? Kanzai : Neutral;

        public static string SpeciesName(Species sp) =>
            sp == Species.Yamato ? "ヤマト" : sp == Species.Ie ? "イエ" : sp == Species.Kanzai ? "カンザイ" : "";

        public static string CasteShort(Caste c) =>
            c == Caste.Worker ? "職" : c == Caste.Soldier ? "兵" : c == Caste.Winged ? "羽" : c == Caste.Queen ? "女王" : "?";

        public static string Tag(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
    }
}
