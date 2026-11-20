using UnityEngine;
using UnityEngine.UI;

namespace TheLightenRoad.Frontend
{
    /// <summary>Licensed device artwork with separate, device-specific action leader lines.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ControllerSchematic : MaskableGraphic
    {
        public bool SymmetricSticks;
        private Image artwork;
        public Sprite CurrentArtwork => artwork != null ? artwork.sprite : null;

        protected override void OnEnable()
        {
            base.OnEnable();
            SetSymmetric(SymmetricSticks);
        }

        public void SetSymmetric(bool symmetric)
        {
            SymmetricSticks = symmetric;
            if (artwork == null)
            {
                var go = new GameObject("Device artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                artwork = go.GetComponent<Image>();
                artwork.rectTransform.sizeDelta = new Vector2(460f, 460f * 940f / 1452f);
                artwork.preserveAspect = true;
                artwork.raycastTarget = false;
                artwork.color = new Color(.88f, .91f, .96f, .9f);
            }
            artwork.sprite = Resources.Load<Sprite>(symmetric
                ? "UI/ControllerArtwork/PlayStation5"
                : "UI/ControllerArtwork/XboxSeries");
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 artSize = artwork != null && artwork.rectTransform.sizeDelta.x > 10f
                ? artwork.rectTransform.sizeDelta
                : new Vector2(460f, 460f * 940f / 1452f);

            // Native 1452x940 PNG button centers (strictly ordered top-to-bottom per side so leader lines never cross)
            Vector2[] anchors = SymmetricSticks
                ? new[]
                {
                    new Vector2(300f, 445f),  // Left Top: D-Pad
                    new Vector2(518f, 635f),  // Left Bottom: Left Stick
                    new Vector2(1150f, 120f), // Right 0: R2
                    new Vector2(1045f, 315f), // Right 1: Options
                    new Vector2(1152f, 360f), // Right 2: Triangle
                    new Vector2(1152f, 528f)  // Right 3: Cross
                }
                : new[]
                {
                    new Vector2(365f, 420f),  // Left Top: Left Stick
                    new Vector2(548f, 610f),  // Left Bottom: D-Pad
                    new Vector2(1095f, 110f), // Right 0: RT
                    new Vector2(1090f, 318f), // Right 1: Y
                    new Vector2(830f, 412f),  // Right 2: Menu
                    new Vector2(1090f, 495f)  // Right 3: A
                };

            float[] heights = { 48f, -58f, 120f, 48f, -24f, -96f };
            float elbowX = artSize.x * 0.5f + 18f;
            float tipX = artSize.x * 0.5f + 42f;
            var dotColor = new Color(1f, 0.78f, 0.34f, color.a);

            for (int i = 0; i < anchors.Length; i++)
            {
                Vector2 start = new Vector2(
                    (anchors[i].x / 1452f - 0.5f) * artSize.x,
                    (0.5f - anchors[i].y / 940f) * artSize.y);
                float side = i < 2 ? -1f : 1f;
                var elbow = new Vector2(side * elbowX, heights[i]);
                var tip = new Vector2(side * tipX, heights[i]);
                Line(vh, start, elbow, 1.4f, color);
                Line(vh, elbow, tip, 1.4f, color);
                Dot(vh, start, 3.2f, dotColor);
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.001f) return;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, col, Vector2.zero);
            vh.AddVert(a + normal, col, Vector2.zero);
            vh.AddVert(b + normal, col, Vector2.zero);
            vh.AddVert(b - normal, col, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Dot(VertexHelper vh, Vector2 center, float radius, Color col)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center + new Vector2(-radius, -radius), col, Vector2.zero);
            vh.AddVert(center + new Vector2(-radius, radius), col, Vector2.zero);
            vh.AddVert(center + new Vector2(radius, radius), col, Vector2.zero);
            vh.AddVert(center + new Vector2(radius, -radius), col, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
