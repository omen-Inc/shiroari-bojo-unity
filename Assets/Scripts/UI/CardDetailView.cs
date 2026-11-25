// 選んだカードの拡大表示(プレハブ CardDetail)。B ポップの枠(design/cards/frames/B_pop.css)を uGUI で組んだもの。
// 文字はすべて CardDef(Assets/Data/cards.json)から出す。効果文は仕様の文言のまま。

using ShiroariBojo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class CardDetailView : MonoBehaviour
    {
        [SerializeField] private CardArtCatalog catalog;

        [Header("種別の色(main)で塗るもの")]
        [SerializeField] private Image[] mainColored = new Image[0];
        [Header("種別の色(dark)で塗るもの")]
        [SerializeField] private Image[] darkColored = new Image[0];
        [Header("種別の色(light)で塗るもの")]
        [SerializeField] private Image[] lightColored = new Image[0];

        [Header("中身")]
        [SerializeField] private Image art;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text effectText;

        [Header("ひとこと(カードの上の札。使えない理由や操作の案内)")]
        [SerializeField] private GameObject hint;
        [SerializeField] private Image hintBack;
        [SerializeField] private TMP_Text hintText;

        /// <summary>表示中のカード ID(隠しているときは null)</summary>
        public string CardId { get; private set; }

        /// <param name="hintLine">カードの上に出すひとこと。空なら出さない</param>
        /// <param name="warning">true なら警告の色(使えない理由)</param>
        public void Show(CardDef d, string hintLine = null, bool warning = false)
        {
            if (d == null) { Hide(); return; }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            bool hasHint = !string.IsNullOrEmpty(hintLine);
            if (hint != null) hint.SetActive(hasHint);
            if (hasHint)
            {
                hintText.text = hintLine;
                hintBack.color = warning ? UiPalette.Danger : UiPalette.Ink;
            }
            if (CardId == d.Id) return;
            CardId = d.Id;
            var theme = UiPalette.CardThemeOf(d);
            foreach (var i in mainColored) if (i != null) i.color = theme.Main;
            foreach (var i in darkColored) if (i != null) i.color = theme.Dark;
            foreach (var i in lightColored) if (i != null) i.color = theme.Light;
            var sprite = catalog != null ? catalog.ArtFor(d.Id) : null;
            art.sprite = sprite;
            art.enabled = sprite != null;
            costText.text = CardLabels.Cost(d);
            costText.color = theme.Dark;
            nameText.text = CardLabels.DisplayName(d);
            typeText.text = CardLabels.TypeLabel(d);
            effectText.text = d.EffectText ?? "";
        }

        public void Hide()
        {
            CardId = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
