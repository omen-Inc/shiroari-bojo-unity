// カードの表示用の文字(カード名・種別ラベル・コスト)。文字はすべて cards.json(CardDef)から作る。

using ShiroariBojo.Core;

namespace ShiroariBojo.UI
{
    public static class CardLabels
    {
        /// <summary>
        /// 表示するカード名。シロアリカードの d.Name は「職アリ-ヤマト」の形なので、「-」より前だけにする
        /// (種類は種別ラベルで示す)。それ以外のカードは d.Name のまま
        /// </summary>
        public static string DisplayName(CardDef d)
        {
            if (d.Kind == CardKind.Termite && d.Species != Species.None)
            {
                int dash = d.Name.IndexOf('-');
                if (dash > 0) return d.Name.Substring(0, dash);
            }
            return d.Name;
        }

        /// <summary>種別ラベル。シロアリは「ヤマトシロアリ」などの種の名前、それ以外は区分(防除・駆除・イベント・中立)</summary>
        public static string TypeLabel(CardDef d)
        {
            if (d.Kind == CardKind.Termite && d.Species != Species.None)
                return UiPalette.SpeciesName(d.Species) + "シロアリ";
            return HandCardView.KindLabel(d);
        }

        /// <summary>コストの表示。女王アリのようにコストのないカードは「−」</summary>
        public static string Cost(CardDef d) => d.Cost.HasValue ? d.Cost.Value.ToString() : "−";
    }
}
