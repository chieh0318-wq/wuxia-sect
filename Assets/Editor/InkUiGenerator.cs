using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wuxia.EditorTools
{
    public static class InkUiGenerator
    {
        private const string Folder = "Assets/Art/UI/Resources/InkUi";

        [MenuItem("Wuxia/Generate Ink UI")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            var patterns = new[]
            {
                ("Paper", "#eadaba"), ("Wood", "#302b24"), ("Selected", "#793528"),
                ("Hover", "#523e2d"), ("Pressed", "#382019"), ("Vermilion", "#9c3e2b"),
                ("Teal", "#182e2f"), ("Seal", "#9c3e2b"), ("Plaque", "#544434"),
                ("Brush", "#9c3e2b"), ("Divider", "#c6a66f")
            };
            foreach (var pattern in patterns)
            {
                ColorUtility.TryParseHtmlString(pattern.Item2, out Color tint);
                Write(pattern.Item1, (x, y) => Sample(pattern.Item1, x, y, tint));
            }
            foreach (string icon in new[] { "Silver", "Food", "Reputation", "Ap", "Disciples", "Month" })
                Write(icon, (x, y) => Icon(icon, x, y));
            AssetDatabase.Refresh();
        }

        private static Color Sample(string style, int x, int y, Color tint)
        {
            int edgeX = Mathf.Min(x, 63 - x), edgeY = Mathf.Min(y, 63 - y);
            int edge = Mathf.Min(edgeX, edgeY);
            float grain = Mathf.PerlinNoise(x * .31f, y * (style == "Paper" ? .67f : .17f));
            if (style == "Brush")
                return Mathf.Abs(y - 32) < 12 + (grain - .5f) * 16 - edgeX / 12 ? tint : Color.clear;
            if (style == "Divider")
                return Mathf.Abs(y - 32) <= 1 || (Mathf.Abs(x - 32) + Mathf.Abs(y - 32) <= 6) ? tint : Color.clear;
            var gold = new Color(.776f, .65f, .435f);
            if (edge == 1 || edge == 5 || (edge == 9 && Mathf.Max(edgeX, edgeY) < 18))
                return style == "Paper" ? Color.Lerp(gold, new Color(.3f, .21f, .12f), .55f) : gold;
            return Color.Lerp(tint * new Color(.91f, .91f, .91f, 1), tint, grain);
        }

        private static Color Icon(string icon, int x, int y)
        {
            int u = x - 32, v = y - 32;
            int radius = u * u + v * v;
            bool ink = icon switch
            {
                "Silver" => Mathf.Abs(u) + Mathf.Abs(v) * 2 < 28 && Mathf.Abs(v) > 4,
                "Food" => Mathf.Abs(u) <= 2 || (Mathf.Abs(u) < 14 && Mathf.Abs(v - Mathf.Abs(u)) % 10 < 3),
                "Reputation" => Mathf.Abs(u) + Mathf.Abs(v) < 22 && Mathf.Abs(u) + Mathf.Abs(v) > 14,
                "Ap" => Mathf.Abs(u) < 19 && Mathf.Abs(v) < 19 && (Mathf.Abs(u) > 13 || Mathf.Abs(v) > 13 || Mathf.Abs(u - v) < 3),
                "Disciples" => (u * u + (v - 10) * (v - 10) < 70) || (v < 0 && u * u + (v + 14) * (v + 14) < 240),
                "Month" => radius < 440 && (u - 9) * (u - 9) + (v - 5) * (v - 5) > 360,
                _ => false
            };
            return ink ? new Color(.88f, .76f, .53f) : Color.clear;
        }

        private static void Write(string name, System.Func<int, int, Color> sample)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                texture.SetPixel(x, y, sample(x, y));
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }

    internal sealed class InkUiImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/UI/", System.StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * 12;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
