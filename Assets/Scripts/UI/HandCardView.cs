// 手札のカード1枚(プレハブ HandCard)。Engine.HandOptions の結果をそのまま表示する。
// 枠は種別の色、中央にイラスト(CardArtCatalog)を出す。

using System;
using ShiroariBojo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class HandCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CardArtCatalog catalog;
        [Tooltip("外枠。種別の色(main)で塗る")]
        [SerializeField] private Image frame;
        [Tooltip("イラストの下地。種別の色(light)で塗る。絵がないカードはこの色だけになる")]
        [SerializeField] private Image faceBack;
        [Tooltip("イラスト")]
        [SerializeField] private Image face;
        [Tooltip("種別ラベルの帯。種別の色(main)で塗る")]
        [SerializeField] private Image kindBadge;
        [Tooltip("名前の帯。種別の色(dark)で塗る")]
        [SerializeField] private Image nameBand;
        [Tooltip("コストの丸の外周。種別の色(dark)で塗る")]
        [SerializeField] private Image costRing;
        [Tooltip("選択中の印(金色の縁)")]
        [SerializeField] private GameObject selectedMark;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private TMP_Text reasonText;
        [SerializeField] private GameObject dim;

        public int Uid { get; private set; }

        private Action<int> _onClick;

        private void Awake()
        {
            button.onClick.AddListener(() => _onClick?.Invoke(Uid));
        }

        /// <param name="showReason">自分の手番のときだけ使えない理由を出す</param>
        public void Bind(PlayOption o, bool selected, bool interactable, bool showReason, Action<int> onClick)
        {
            Uid = o.Uid;
            _onClick = onClick;
            var d = o.Def;
            var theme = UiPalette.CardThemeOf(d);
            frame.color = theme.Main;
            faceBack.color = theme.Light;
            kindBadge.color = theme.Main;
            nameBand.color = theme.Dark;
            costRing.color = theme.Dark;
            costText.color = theme.Dark;
            var art = catalog != null ? catalog.ArtFor(d.Id) : null;
            face.sprite = art;
            face.enabled = art != null;
            selectedMark.SetActive(selected);
            costText.text = CardLabels.Cost(d);
            nameText.text = CardLabels.DisplayName(d);
            kindText.text = CardLabels.TypeLabel(d);
            bool dimmed = showReason && !o.Usable;
            reasonText.gameObject.SetActive(dimmed && !string.IsNullOrEmpty(o.Reason));
            reasonText.text = o.Reason ?? "";
            dim.SetActive(dimmed);
            button.interactable = interactable;
        }

        public static string KindLabel(CardDef d)
        {
            switch (d.Kind)
            {
                case CardKind.Termite: return "シロアリ";
                case CardKind.Event: return "イベント";
                case CardKind.Neutral: return "中立";
                case CardKind.Control: return "防除";
                case CardKind.Extermination: return "駆除";
                default: return d.Category;
            }
        }
    }
}
