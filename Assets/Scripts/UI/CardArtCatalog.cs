// カードの絵柄(イラスト)の対応表。カード ID → Sprite。
// Core(ShiroariBojo.Core)は UnityEngine を参照しないので、絵柄の対応は画面側のこのアセットに持たせる。
// アセット:Assets/Art/Cards/CardArtCatalog.asset(エディタで割り当てる)。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiroariBojo.UI
{
    [CreateAssetMenu(fileName = "CardArtCatalog", menuName = "ShiroariBojo/カード絵柄カタログ")]
    public sealed class CardArtCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Assets/Data/cards.json のカード ID(例:worker-yamato)")]
            public string cardId;
            public Sprite art;
        }

        [Tooltip("共通の裏面")]
        [SerializeField] private Sprite back;
        [SerializeField] private Entry[] entries = new Entry[0];

        private Dictionary<string, Sprite> _map;

        public Sprite Back => back;
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>カード ID の絵柄。無ければ null(種別の色だけで表示する)</summary>
        public Sprite ArtFor(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>();
                foreach (var e in entries)
                    if (!string.IsNullOrEmpty(e.cardId) && e.art != null) _map[e.cardId] = e.art;
            }
            return _map.TryGetValue(cardId, out var s) ? s : null;
        }

        private void OnValidate() => _map = null;
        private void OnEnable() => _map = null;
    }
}
