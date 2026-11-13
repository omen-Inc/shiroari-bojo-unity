// 選択パネルの1行(プレハブ ChoiceRow)。押せるラベルと、並べ替え用の「上へ」ボタン。

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class ChoiceRowView : MonoBehaviour
    {
        [SerializeField] private Button mainButton;
        [SerializeField] private Image mainImage;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button upButton;

        private Action _onClick;
        private Action _onUp;

        private void Awake()
        {
            mainButton.onClick.AddListener(() => _onClick?.Invoke());
            upButton.onClick.AddListener(() => _onUp?.Invoke());
        }

        public void Bind(ChoiceRow row)
        {
            label.text = row.Label;
            _onClick = row.OnClick;
            _onUp = row.OnUp;
            mainButton.interactable = row.OnClick != null;
            mainImage.color = row.Selected ? UiPalette.DangerSoft : UiPalette.Paper;
            upButton.gameObject.SetActive(row.ShowUp);
            upButton.interactable = row.OnUp != null;
        }
    }

    public sealed class ChoiceRow
    {
        public string Label;
        public bool Selected;
        /// <summary>null なら押せない(表示だけ)</summary>
        public Action OnClick;
        public bool ShowUp;
        /// <summary>null なら「上へ」を押せない</summary>
        public Action OnUp;
    }
}
