using UnityEngine;

namespace TheLightenRoad.Effects
{
    public static class ParticleTextures
    {
        private static Texture2D softCircle;
        private static Texture2D spark;
        private static Texture2D dustPuff;

        public static Texture2D SoftCircle
        {
            get
            {
                if (softCircle == null)
                    softCircle = GenerateSoftCircle(32);
                return softCircle;
            }
        }

        public static Texture2D Spark
        {
            get
            {
                if (spark == null)
                    spark = GenerateSpark(32);
                return spark;
            }
        }

        public static Texture2D DustPuff
        {
            get
            {
                if (dustPuff == null)
                    dustPuff = GenerateDustPuff(32);
                return dustPuff;
            }
        }

        private static Texture2D GenerateSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    byte alpha = (byte)(dist < 1f ? Mathf.RoundToInt(255f * (1f - dist) * (1f - dist)) : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateSpark(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center) / center;
                    float dy = Mathf.Abs(y - center) / center;
                    float dist = dx + dy;
                    byte alpha = (byte)(dist < 1f ? Mathf.RoundToInt(255f * Mathf.Pow(1f - dist, 1.8f)) : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateDustPuff(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    byte alpha = (byte)(dist < 1f ? Mathf.RoundToInt(255f * Mathf.Pow(1f - dist, 1.3f)) : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Material CreateMaterial(bool isLit, Texture2D texture)
        {
            string shaderName = isLit
                ? "Universal Render Pipeline/2D/Sprite-Lit-Default"
                : "Universal Render Pipeline/2D/Sprite-Unlit-Default";

            var shader = Shader.Find(shaderName);
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (texture != null)
                mat.mainTexture = texture;
            return mat;
        }
    }
}
