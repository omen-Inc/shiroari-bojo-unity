// 子の UI を Screen.safeArea の内側に収める(ノッチ・ホームバー対策)。
// この RectTransform のアンカーを安全領域に合わせるだけで、レイアウトは子に任せる。

using UnityEngine;

namespace ShiroariBojo.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply(true);
        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            var area = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && area == _applied && screen == _screen) return;
            _applied = area;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;
            var rt = (RectTransform)transform;
            var min = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            var max = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            if (rt.anchorMin == min && rt.anchorMax == max) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
