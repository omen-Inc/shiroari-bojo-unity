// 選択用のシート(予知・手札上限・手詰まり防止で共用)。行は ChoiceRow プレハブを複製して並べる。

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class ChoicePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private ChoiceRowView rowPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelLabel;

        private readonly List<ChoiceRowView> _rows = new List<ChoiceRowView>();
        private Action _onConfirm;
        private Action _onCancel;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            confirmButton.onClick.AddListener(() => _onConfirm?.Invoke());
            cancelButton.onClick.AddListener(() => _onCancel?.Invoke());
        }

        /// <param name="onConfirm">null なら確定ボタンを出さない</param>
        /// <param name="onCancel">null なら取り消しボタンを出さない</param>
        public void Show(string title, string desc, IList<ChoiceRow> rows,
            string confirm, bool confirmEnabled, Action onConfirm, string cancel = null, Action onCancel = null)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            descText.text = desc;
            while (_rows.Count < rows.Count) _rows.Add(Instantiate(rowPrefab, rowContainer));
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i < rows.Count;
                _rows[i].gameObject.SetActive(on);
                if (on) _rows[i].Bind(rows[i]);
            }
            _onConfirm = onConfirm;
            confirmButton.gameObject.SetActive(onConfirm != null);
            confirmButton.interactable = confirmEnabled;
            confirmLabel.text = confirm ?? "";
            _onCancel = onCancel;
            cancelButton.gameObject.SetActive(onCancel != null);
            cancelLabel.text = cancel ?? "";
            // ボタンが1つもないときは、ボタンの段ごと隠す(空白を残さない)
            confirmButton.transform.parent.gameObject.SetActive(onConfirm != null || onCancel != null);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
