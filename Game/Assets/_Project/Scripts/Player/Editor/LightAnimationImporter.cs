using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TheLightenRoad.Player.Editor
{
    /// <summary>Rebuild the delivered LibreSprite exports without changing their original bytes.</summary>
    public static class LightAnimationImporter
    {
        public const string ArtRoot = "Assets/_Project/Art/Characters/LightAnimations";
        public const string CatalogPath = "Assets/_Project/Resources/Player/LightAnimations.asset";
        private const string PrefabPath = "Assets/_Project/Prefabs/PlayerPrototype.prefab";
        private const float Ppu = 36;
        private sealed class Frame { public Rect Rect; public float Duration; }

        [MenuItem("The Lighten Road/Player/Rebuild Maksim animations")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before rebuilding the sprite assets.");
            Directory.CreateDirectory(ArtRoot + "/Clips");
            AssetDatabase.Refresh();
            var items = new List<PlayerSpriteSequence>();
            foreach (LightAnimation kind in Enum.GetValues(typeof(LightAnimation))) items.Add(Import(kind));
            var catalog = AssetDatabase.LoadAssetAtPath<PlayerAnimationCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<PlayerAnimationCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Sequences = items.ToArray(); EditorUtility.SetDirty(catalog);
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var animator = prefab.GetComponent<PlayerSpriteAnimator>();
                if (animator == null) animator = prefab.AddComponent<PlayerSpriteAnimator>();
                if (prefab.GetComponent<PlayerLedgeClimb>() == null) prefab.AddComponent<PlayerLedgeClimb>();
                var renderer = prefab.GetComponentInChildren<SpriteRenderer>();
                var serialized = new SerializedObject(animator);
                serialized.FindProperty("catalog").objectReferenceValue = catalog;
                serialized.FindProperty("target").objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.sprite = catalog.Find(LightAnimation.Idle).Frames[0];
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("Imported Maksim's 8 animation sheets and updated PlayerPrototype. Source PNG/JSON bytes are unchanged.");
        }
        private static PlayerSpriteSequence Import(LightAnimation kind)
        {
            string name = kind.ToString(), path = ArtRoot + "/" + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Missing delivered sheet", path);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                var frames = ReadFrames(kind, source.width, source.height);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = Ppu;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = false;
                importer.maxTextureSize = 1024;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
                var rects = new List<SpriteRect>(); var names = new List<SpriteNameFileIdPair>();
                var offsets = new List<Vector2>();
                var reaches = new List<float>();
                var grips = new List<Vector2>();
                var soles = new List<Vector2>();
                var plants = new List<Vector2>();
                Vector2 lastLight = new Vector2(13.5f / Ppu, 16.1f / Ppu);
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i]; string frameName = name + "_" + i.ToString("D3");
                    var id = previous.TryGetValue(frameName, out var existing) ? existing : new GUID(Hash128.Compute(path + "/" + frameName).ToString());
                    // Normal frames share a 35px registration point. Climb has a 10px taller
                    // canvas and a low-foot guide at 79px; use a 30px vertical pivot for that sheet.
                    var pivot = new Vector2(.5f, (kind == LightAnimation.Climb ? 30f : 35f) / f.Rect.height);
                    rects.Add(new SpriteRect { name = frameName, spriteID = id, alignment = SpriteAlignment.Custom, pivot = pivot,
                        rect = new Rect(f.Rect.x, source.height - f.Rect.y - f.Rect.height, f.Rect.width, f.Rect.height) });
                    names.Add(new SpriteNameFileIdPair(frameName, id));
                    // Track the artist's small gold flame pixels; hold the previous position while
                    // the flame is occluded. This is a visual anchor only, never a fuel/light-mode change.
                    Vector2 sum = Vector2.zero; int count = 0;
                    float reachX = -1;
                    float reachY = 0; int reachCount = 0;
                    int soleY = -1; float soleX = 0; int soleCount = 0;
                    int plantX = -1, plantY = -1;
                    int shirtBottom = -1; float shirtX = 0; int shirtCount = 0;
                    for (int y = 0; y < (int)f.Rect.height; y++) for (int x = 0; x < (int)f.Rect.width; x++)
                    {
                        Color32 c = source.GetPixel((int)f.Rect.x + x, source.height - (int)f.Rect.y - y - 1);
                        if (c.a > 100)
                        {
                            if (y < f.Rect.height * .65f && c.r > 100 && c.g > 100 && c.b > 100)
                            {
                                if (x + .5f > reachX) { reachX = x + .5f; reachY = 0; reachCount = 0; }
                                if (Mathf.Approximately(x + .5f, reachX)) { reachY += y + .5f; reachCount++; }
                            }
                            if (y > soleY) { soleY = y; soleX = 0; soleCount = 0; }
                            if (y == soleY) { soleX += x + .5f; soleCount++; }
                            if (y >= f.Rect.height * .55f && (x > plantX || x == plantX && y > plantY))
                            { plantX = x; plantY = y; }
                            // Cloth at the waist is a better light origin while the lantern is stowed.
                            if (c.r >= c.g && c.g > c.b + 10 && c.r > 100 && c.b < 200 && x < f.Rect.width * .5f)
                            {
                                if (y > shirtBottom) { shirtBottom = y; shirtX = 0; shirtCount = 0; }
                                if (y == shirtBottom) { shirtX += x + .5f; shirtCount++; }
                            }
                        }
                        if (c.a > 100 && c.r > 200 && c.g > 140 && c.b < 150 && c.r - c.b > 100) { sum += new Vector2(x + .5f, f.Rect.height - y - .5f); count++; }
                    }
                    if (count > 0) lastLight = (sum / count - Vector2.Scale(pivot, f.Rect.size)) / Ppu;
                    else if (kind == LightAnimation.Climb && shirtCount > 0)
                        lastLight = (new Vector2(shirtX / shirtCount, f.Rect.height - shirtBottom - 2.5f) - Vector2.Scale(pivot, f.Rect.size)) / Ppu;
                    offsets.Add(lastLight);
                    reaches.Add(reachX >= 0 ? (reachX - pivot.x * f.Rect.width) / Ppu : 0);
                    if (reachCount == 0 || soleCount == 0 || plantX < 0) throw new InvalidDataException("Missing visible pose registration in " + frameName);
                    grips.Add((new Vector2(reachX, f.Rect.height - reachY / reachCount) - Vector2.Scale(pivot, f.Rect.size)) / Ppu);
                    soles.Add((new Vector2(soleX / soleCount, f.Rect.height - soleY - .5f) - Vector2.Scale(pivot, f.Rect.size)) / Ppu);
                    plants.Add((new Vector2(plantX + .5f, f.Rect.height - plantY - .5f) - Vector2.Scale(pivot, f.Rect.size)) / Ppu);
                }
                provider.SetSpriteRects(rects.ToArray());
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);
                provider.Apply(); importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                if (sprites.Length != frames.Count) throw new InvalidDataException($"{name}: expected {frames.Count} sprites, got {sprites.Length}.");
                var clipPath = ArtRoot + "/Clips/Light_" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
                clip.name = "Light_" + name; clip.frameRate = 10;
                var keys = new ObjectReferenceKeyframe[frames.Count]; float time = 0;
                bool loop = kind == LightAnimation.Idle || kind == LightAnimation.Walk;
                for (int i = 0; i < frames.Count; i++) { keys[i] = new ObjectReferenceKeyframe { time = Mathf.Round(time * 1000) / 1000, value = sprites[i] }; time += frames[i].Duration; }
                // Unity adds one sample interval after the last sprite key; an extra endpoint
                // would insert a duplicate 100ms frame into every preview clip.
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Placeholder Visual", typeof(SpriteRenderer), "m_Sprite"), keys);
                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
                return new PlayerSpriteSequence { Kind = kind, Frames = sprites, Durations = frames.Select(f => f.Duration).ToArray(), LanternOffsets = offsets.ToArray(), ReachOffsetsX = kind == LightAnimation.Climb ? reaches.ToArray() : Array.Empty<float>(), GripOffsets = kind == LightAnimation.Climb ? grips.ToArray() : Array.Empty<Vector2>(), SoleOffsets = soles.ToArray(), PlantOffsets = kind == LightAnimation.Climb ? plants.ToArray() : Array.Empty<Vector2>(), PreviewClip = clip, Loop = loop };
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        private static List<Frame> ReadFrames(LightAnimation kind, int width, int height)
        {
            string path = ArtRoot + "/" + kind + ".json";
            var result = new List<Frame>();
            if (kind == LightAnimation.Idle && !File.Exists(path))
            {
                if (width != 700 || height != 70) throw new InvalidDataException("Idle fallback expects the delivered 700 x 70 sheet.");
                for (int i = 0; i < 10; i++) result.Add(new Frame { Rect = new Rect(i * 70, 0, 70, 70), Duration = .1f });
                return result;
            }
            var json = JObject.Parse(File.ReadAllText(path));
            if ((int)json["meta"]["size"]["w"] != width || (int)json["meta"]["size"]["h"] != height) throw new InvalidDataException("Sheet dimensions do not match " + path);
            var values = json["frames"] is JObject map ? map.Properties().Select(p => p.Value) : json["frames"].Children();
            foreach (var value in values)
            {
                var f = value["frame"]; var rect = new Rect((int)f["x"], (int)f["y"], (int)f["w"], (int)f["h"]);
                float duration = (float)value["duration"] / 1000;
                if ((bool)value["rotated"] || (bool)value["trimmed"] || rect.width <= 0 || rect.height <= 0 || rect.x < 0 || rect.y < 0 || rect.xMax > width || rect.yMax > height || duration <= 0)
                    throw new InvalidDataException("Unsupported or invalid frame in " + path);
                result.Add(new Frame { Rect = rect, Duration = duration });
            }
            if (result.Count == 0) throw new InvalidDataException("Empty animation in " + path);
            return result;
        }
    }
}
