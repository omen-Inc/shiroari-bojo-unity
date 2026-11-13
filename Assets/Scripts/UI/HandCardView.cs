// 手札のカード1枚(プレハブ HandCard)。Engine.HandOptions の結果をそのまま表示する。

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
        [SerializeField] private Image frame;
        [SerializeField] private Image face;
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
            face.color = UiPalette.UserColor(d.User);
            frame.color = selected ? UiPalette.Gold : UiPalette.Paper;
            costText.text = d.Cost.HasValue ? d.Cost.Value.ToString() : "-";
            nameText.text = d.Name.Replace("-", "\n");
            kindText.text = KindLabel(d);
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
