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
            // Native PNG coordinates; convert against the artwork's displayed size.
            Vector2[] anchors = SymmetricSticks
                ? new[] { new Vector2(310,447), new Vector2(500,645), new Vector2(1050,307),
                    new Vector2(1155,357), new Vector2(1055,444), new Vector2(1155,535) }
                : new[] { new Vector2(550,620), new Vector2(375,420), new Vector2(830,414),
                    new Vector2(1090,320), new Vector2(995,405), new Vector2(1090,490) };
            float[] heights = {65, -15, 100, 48, -8, -65};
            for (int i = 0; i < anchors.Length; i++)
            {
                Vector2 start = new Vector2((anchors[i].x / 1452f - .5f) * 460f,
                    (.5f - anchors[i].y / 940f) * (460f * 940f / 1452f));
                float side = i < 2 ? -1f : 1f;
                var elbow = new Vector2(side * 260f, heights[i]);
                Line(vh, start, elbow, 1.2f);
                Line(vh, elbow, new Vector2(side * 300f, heights[i]), 1.2f);
            }
        }

        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 scale = new Vector2(rectTransform.rect.width / 800f, rectTransform.rect.height / 360f);
            a = Vector2.Scale(a, scale);
            b = Vector2.Scale(b, scale);
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a-normal, color, Vector2.zero);
            vh.AddVert(a+normal, color, Vector2.zero);
            vh.AddVert(b+normal, color, Vector2.zero);
            vh.AddVert(b-normal, color, Vector2.zero);
            vh.AddTriangle(start, start+1, start+2);
            vh.AddTriangle(start, start+2, start+3);
        }
    }
}
