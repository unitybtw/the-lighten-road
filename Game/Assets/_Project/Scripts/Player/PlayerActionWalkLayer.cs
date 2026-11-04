using System.Collections.Generic;
using UnityEngine;

namespace TheLightenRoad.Player
{
    /// <summary>Pixel-preserving upper action / lower gait composition. Never changes artist textures.</summary>
    internal sealed class PlayerActionWalkLayer
    {
        // Includes one overlapping trouser row, below the dangling hand in the delivered Flare.
        private const int SplitRow = 42;
        private readonly SpriteRenderer body, legs;
        private readonly Dictionary<Sprite, Sprite> upperCache = new Dictionary<Sprite, Sprite>();
        private readonly Dictionary<Sprite, Sprite> lowerCache = new Dictionary<Sprite, Sprite>();
        public SpriteRenderer Legs => legs;

        public PlayerActionWalkLayer(SpriteRenderer bodyRenderer)
        {
            body = bodyRenderer;
            var node = new GameObject("Action walking legs", typeof(SpriteRenderer));
            node.transform.SetParent(body.transform, false);
            node.layer = body.gameObject.layer;
            legs = node.GetComponent<SpriteRenderer>();
            Clear();
        }
        public void Show(Sprite upper, Sprite lower, float waistOffset)
        {
            legs.gameObject.SetActive(true);
            body.sprite = Crop(upper, true);
            legs.sprite = Crop(lower, false);
            float height = lower.rect.height - SplitRow;
            legs.transform.localPosition = new Vector3(waistOffset * (body.flipX ? -1 : 1),
                (height - lower.pivot.y) / lower.pixelsPerUnit, 0);
            legs.flipX = body.flipX; legs.flipY = body.flipY;
            legs.color = body.color; legs.sharedMaterial = body.sharedMaterial;
            legs.sortingLayerID = body.sortingLayerID;
            legs.sortingOrder = body.sortingOrder; // Props must not render between the two halves of the actor.
            legs.maskInteraction = body.maskInteraction;
            legs.spriteSortPoint = body.spriteSortPoint;
        }
        private Sprite Crop(Sprite source, bool upper)
        {
            var cache = upper ? upperCache : lowerCache;
            if (cache.TryGetValue(source, out var result)) return result;
            Rect rect = source.rect;
            Vector2 pivot;
            if (upper)
            {
                float height = SplitRow + 1;
                float bottom = source.rect.height - height;
                rect.y += bottom; rect.height = height;
                pivot = new Vector2(source.pivot.x / rect.width, (source.pivot.y - bottom) / height);
            }
            else
            {
                rect.height = source.rect.height - SplitRow;
                pivot = new Vector2(source.pivot.x / rect.width, 1);
            }
            result = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = source.name + (upper ? " upper action" : " walking legs");
            result.hideFlags = HideFlags.HideAndDontSave;
            cache.Add(source, result); return result;
        }
        public void Clear() { if (legs != null) legs.gameObject.SetActive(false); }
        public void Dispose()
        {
            Clear();
            if (legs != null) Release(legs.gameObject);
            foreach (var sprite in upperCache.Values) Release(sprite);
            foreach (var sprite in lowerCache.Values) Release(sprite);
            upperCache.Clear(); lowerCache.Clear();
        }
        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
        }
    }
}
