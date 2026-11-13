// 盤面の1ポイント(プレハブ PointTile)。状態から毎回描き直す。

using System;
using System.Text;
using ShiroariBojo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiroariBojo.UI
{
    public sealed class PointView : MonoBehaviour
    {
        [SerializeField] private int pointIndex;
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image body;
        [SerializeField] private Image header;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text metaText;
        [SerializeField] private TMP_Text cardsText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject barrierBadge;
        [SerializeField] private TMP_Text barrierText;
        [SerializeField] private GameObject baitBadge;
        [SerializeField] private TMP_Text baitText;
        [SerializeField] private GameObject swarmBadge;
        [SerializeField] private GameObject destroyedStamp;
        [SerializeField] private TMP_Text destroyedText;
        [SerializeField] private GameObject targetMarker;

        public int PointIndex => pointIndex;

        private Action<int> _onClick;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(pointIndex));
        }

        public void Bind(GameState s, bool isTarget, Action<int> onClick)
        {
            _onClick = onClick;
            var def = Points.All[pointIndex];
            var pt = s.Point(pointIndex);
            var zone = UiPalette.ZoneColor(def);

            nameText.text = def.Name;
            header.color = zone;
            frame.color = isTarget ? UiPalette.Gold : pt.Destroyed ? UiPalette.Danger : zone;
            body.color = pt.Destroyed ? UiPalette.DangerSoft : def.IsGardenTree ? UiPalette.TreeSoft : UiPalette.Paper;

            var meta = new StringBuilder();
            for (int k = 0; k < def.Allowed.Count; k++)
            {
                if (k > 0) meta.Append(' ');
                var sp = def.Allowed[k];
                meta.Append($"<color={UiPalette.Tag(UiPalette.SpeciesColor(sp))}><b>{UiPalette.SpeciesName(sp)}</b></color>");
            }
            meta.Append($"\n<size=80%><color={UiPalette.Tag(UiPalette.Muted)}>{ZoneLabel(s, def)}</color></size>");
            metaText.text = meta.ToString();

            var cards = new StringBuilder();
            foreach (var c in pt.Cards)
            {
                if (!s.Cards.TryGet(c.DefId, out var cd)) continue;
                if (cards.Length > 0) cards.Append(' ');
                cards.Append($"<color={UiPalette.Tag(UiPalette.SpeciesColor(cd.Species))}><b>{UiPalette.CasteShort(cd.Caste)}</b></color>");
                if (c.GuardAvailable) cards.Append("<size=70%>盾</size>");
                if (c.PromoteAtOwnTurn > 0) cards.Append("<size=70%>↑</size>");
            }
            cardsText.text = cards.ToString();

            int req = s.RequiredAt(pointIndex);
            int have = pt.Cards.Count;
            var prog = new StringBuilder();
            int cells = Math.Max(req, have);
            for (int k = 0; k < cells; k++)
                prog.Append(k < have ? $"<color={UiPalette.Tag(UiPalette.Danger)}>■</color>" : $"<color={UiPalette.Tag(UiPalette.Line)}>■</color>");
            prog.Append($" {have}/{req}");
            progressText.text = prog.ToString();

            int barrier = s.BarrierRemaining(pointIndex);
            barrierBadge.SetActive(barrier > 0);
            if (barrier > 0) barrierText.text = $"バリア 残{barrier}";
            int bait = s.BaitRemaining(pointIndex);
            baitBadge.SetActive(bait > 0);
            if (bait > 0) baitText.text = bait <= 1 ? "ベイト 次で発動" : $"ベイト あと{bait}";
            swarmBadge.SetActive(pt.Swarm);

            destroyedStamp.SetActive(pt.Destroyed);
            if (pt.Destroyed) destroyedText.text = def.IsGardenTree ? "倒れた" : "完全侵食";

            targetMarker.SetActive(isTarget);
            if (button != null) button.interactable = isTarget;
        }

        private static string ZoneLabel(GameState s, PointDef def)
        {
            if (!def.IsGardenTree) return def.ZoneLabel;
            return s.TreeFallen ? "特殊:倒れた" : $"特殊:{s.Rules.GardenTreeRequired}枚で倒れる";
        }
    }
}
