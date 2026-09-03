#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace RPG.EditorTools
{
    // Bakes soft gradient/glow/speckle textures as real project assets so
    // scene backgrounds can use them instead of flat-colour rectangles — same
    // idea as HomeController's generated hero emblems and UnitVisual's combat
    // VFX glow sprites, just baked once at Editor time (rather than at
    // runtime) since background art needs to survive being saved into the
    // scene file. Re-running a Setup menu command deletes and regenerates
    // these, so they never pile up stale duplicates.
    public static class ProceduralArt
    {
        private const string TextureDir = "Assets/Art/Generated";

        // Smooth top-to-bottom fade — skies, horizons, floor washes.
        public static Sprite VerticalGradient(string name, Color top, Color bottom, int height = 256)
        {
            const int W = 8;
            var tex = new Texture2D(W, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / Mathf.Max(1, height - 1));
                for (int x = 0; x < W; x++) tex.SetPixel(x, y, c);
            }
            return SaveSprite(name, tex);
        }

        // Soft radial falloff from a bright centre to transparent — glows,
        // crystal shards, magic cores. Stretch the display rect (non-square
        // anchors) to turn the circle into an ellipse/band for free.
        public static Sprite RadialGlow(string name, Color center, int size = 128, float power = 2.2f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r);
                    float a = Mathf.Pow(1f - d, power);
                    tex.SetPixel(x, y, new Color(center.r, center.g, center.b, center.a * a));
                }
            return SaveSprite(name, tex);
        }

        // Sparse soft dots on a transparent field — a cheap starfield/ember
        // texture, tiled once across a wide area rather than as many objects.
        public static Sprite Speckle(string name, Color dotColor, int size, int count, float minR, float maxR, int seed)
        {
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            tex.SetPixels(pixels);

            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float cx = (float)rng.NextDouble() * size;
                float cy = (float)rng.NextDouble() * size;
                float r  = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                float a  = 0.35f + (float)rng.NextDouble() * 0.55f;

                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + r));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(cy + r));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Mathf.Max(0.001f, r);
                        if (d > 1f) continue;
                        float falloff = Mathf.Pow(1f - d, 2f) * a;
                        Color existing = tex.GetPixel(x, y);
                        float newA = Mathf.Clamp01(existing.a + falloff);
                        tex.SetPixel(x, y, new Color(dotColor.r, dotColor.g, dotColor.b, newA));
                    }
            }
            return SaveSprite(name, tex);
        }

        // Places a generated sprite stretched across an anchor rect (the
        // Image.color tint composites with the sprite's own gradient/alpha).
        public static Image Place(Transform parent, string name, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite         = sprite;
            img.color          = tint;
            img.raycastTarget  = false;
            return img;
        }

        // Places a generated sprite at a fixed pixel size, pivot-centred —
        // for glows/crystals positioned like the existing MakeBGBeam pattern.
        public static Image PlaceFixed(Transform parent, string name, Sprite sprite,
            Vector2 anchorPos, Vector2 sizeDelta, Color tint, float rotation = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorPos;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            if (rotation != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var img = go.AddComponent<Image>();
            img.sprite        = sprite;
            img.color         = tint;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite SaveSprite(string name, Texture2D tex)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(TextureDir)) AssetDatabase.CreateFolder("Assets/Art", "Generated");

            string path = $"{TextureDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(tex, path);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            AssetDatabase.AddObjectToAsset(sprite, tex);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
