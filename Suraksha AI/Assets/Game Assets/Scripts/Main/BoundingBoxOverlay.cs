using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoundingBoxOverlay : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private RectTransform _rootTransform;
    [SerializeField] private TMP_FontAsset _fontAsset;

    [Header("Properties")]
    [SerializeField] private float lineThickness = 3f;
    [SerializeField] private float labelFontSize = 18f;

    private class BoxVisual
    {
        public RectTransform[] edges;
        public RectTransform labelRect;
        public TextMeshProUGUI label;
    }

    private readonly Dictionary<string, BoxVisual> boxes = new();
    private bool _loggedFirstDraw;

    public void UpdateBoxes(List<ThreatBox> targets)
    {
        if (_rootTransform == null)
        {
            Debug.LogError("[Overlay] Root Transform not assigned.");
            return;
        }

        Debug.Log($"[Overlay] UpdateBoxes called, targets = {(targets == null ? -1 : targets.Count)}");

        var seen = new HashSet<string>();
        float w = _rootTransform.rect.width;
        float h = _rootTransform.rect.height;

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t == null || t.bbox == null) continue;

                string id = string.IsNullOrWhiteSpace(t.id) ? "unknown" : t.id;
                seen.Add(id);

                if (!boxes.TryGetValue(id, out var v))
                {
                    v = CreateBox(id);
                    boxes[id] = v;
                }

                var b = t.bbox;
                float x = b.x_min * w, y = b.y_min * h;
                float bw = b.width * w, bh = b.height * h;

                if (!_loggedFirstDraw)
                {
                    Debug.Log($"[Overlay] First box {id}: root={w}x{h} x={x} y={y} bw={bw} bh={bh}");
                    _loggedFirstDraw = true;
                }

                SetEdge(v.edges[0], x, -y, bw, lineThickness);                         // top
                SetEdge(v.edges[1], x, -(y + bh - lineThickness), bw, lineThickness);  // bottom
                SetEdge(v.edges[2], x, -y, lineThickness, bh);                         // left
                SetEdge(v.edges[3], x + bw - lineThickness, -y, lineThickness, bh);    // right

                v.labelRect.anchoredPosition = new Vector2(x, -y + 24f);
                v.labelRect.sizeDelta = new Vector2(Mathf.Max(bw, 260f), 24f);
                v.label.text = $"{t.level} | {t.threat_score:F2} | {t.distance_m:F1}m";

                Color c = t.level == "CRITICAL" ? Color.red
                        : t.level == "MODERATE" ? new Color(1f, 0.6f, 0f)
                        : Color.green;
                foreach (var e in v.edges) e.GetComponent<Image>().color = c;
            }
        }

        // remove boxes for ids not in this frame
        List<string> stale = null;
        foreach (var kv in boxes)
            if (!seen.Contains(kv.Key))
                (stale ??= new List<string>()).Add(kv.Key);

        if (stale != null)
            foreach (var id in stale)
            {
                DestroyBox(boxes[id]);
                boxes.Remove(id);
            }
    }

    private BoxVisual CreateBox(string id)
    {
        var v = new BoxVisual { edges = new RectTransform[4] };

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"Det_{id}_Edge_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_rootTransform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            v.edges[i] = rt;
        }

        var lgo = new GameObject($"Det_{id}_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lgo.transform.SetParent(_rootTransform, false);
        v.labelRect = lgo.GetComponent<RectTransform>();
        v.labelRect.anchorMin = v.labelRect.anchorMax = new Vector2(0, 1);
        v.labelRect.pivot = new Vector2(0, 1);
        v.label = lgo.GetComponent<TextMeshProUGUI>();
        if (_fontAsset != null) v.label.font = _fontAsset;
        v.label.fontSize = labelFontSize;
        v.label.color = Color.white;
        v.label.alignment = TextAlignmentOptions.Left;
        v.label.enableWordWrapping = false;
        v.label.raycastTarget = false;
        return v;
    }

    private void SetEdge(RectTransform e, float x, float y, float w, float h)
    {
        e.anchoredPosition = new Vector2(x, y);
        e.sizeDelta = new Vector2(w, h);
    }

    private void DestroyBox(BoxVisual v)
    {
        foreach (var e in v.edges) if (e != null) Destroy(e.gameObject);
        if (v.labelRect != null) Destroy(v.labelRect.gameObject);
    }

    public void ClearBoxes()
    {
        foreach (var kv in boxes) DestroyBox(kv.Value);
        boxes.Clear();
    }

    [ContextMenu("Test Box")]
    private void TestBox()
    {
        UpdateBoxes(new List<ThreatBox> {
            new ThreatBox {
                id = "test", level = "CRITICAL", threat_score = 0.87f, distance_m = 2f,
                bbox = new BoundaryBox { x_min = 0.3f, y_min = 0.2f, width = 0.3f, height = 0.5f }
            }
        });
    }
}