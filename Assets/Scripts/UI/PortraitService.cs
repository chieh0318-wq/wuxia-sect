using System;
using UnityEngine;

namespace Wuxia
{
    public static class PortraitService
    {
        private static Texture2D silhouette;
        private static readonly System.Collections.Generic.HashSet<string> missingStoryPortraits = new();

        internal static string StoryId(string id, string friendGender)
        {
            if (id == "friend" || id?.StartsWith("friend__", StringComparison.Ordinal) == true)
                return (friendGender == "男" ? "friend_m" : "friend_f") + id.Substring("friend".Length);
            return id;
        }

        internal static string IdOrDefault(string portrait, string fallback) =>
            string.IsNullOrWhiteSpace(portrait) ? fallback : portrait;

        internal static Texture2D FindPortrait(string id) =>
            string.IsNullOrWhiteSpace(id) ? null : FindTexture("portraits", id);

        public static Texture2D Resolve(string id, string gender = null, bool required = false)
        {
            id = StoryId(id, gender);
            var portrait = FindPortrait(id);
            int expression = id?.IndexOf("__", StringComparison.Ordinal) ?? -1;
            if (portrait == null && expression > 0)
                portrait = FindPortrait(id.Substring(0, expression));
            if (portrait != null)
                return portrait;
            if (required)
            {
                if (missingStoryPortraits.Add(id))
                    Debug.LogWarning($"Missing story portrait: {id}");
            }
            return FindTexture("portraits", gender == "女" ? "fallback_f" : "fallback_m")
                ?? (silhouette ??= CreateSilhouette());
        }

        internal static string ResponseArt(string route, string response, string fallback)
        {
            string id = $"v1124_{route}_{response}";
            return Story(id) != null ? id : fallback;
        }

        public static Texture2D Story(string id) => string.IsNullOrWhiteSpace(id) ? null : FindTexture("story", id);

        private static Texture2D FindTexture(string folder, string id)
        {
            string path = folder + "/" + id.Trim().ToLowerInvariant();
            var asset = Resources.Load(path);
            if (asset is Texture2D texture)
                return texture;
            if (asset is Sprite sprite)
                return sprite.texture;
            return Resources.Load<Texture2D>(path);
        }

        private static Texture2D CreateSilhouette()
        {
            const int width = 96, height = 120;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var paper = new Color32(226, 204, 164, 255);
            var ink = new Color32(55, 49, 42, 255);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (x + .5f) / width - .5f;
                float v = (y + .5f) / height;
                bool head = Ellipse(u, v - .63f, .17f, .19f);
                bool knot = Ellipse(u - .02f, v - .82f, .07f, .07f);
                bool shoulders = Ellipse(u, v - .12f, .39f, .29f);
                bool neck = Mathf.Abs(u) < .09f && v > .34f && v < .58f;
                pixels[y * width + x] = head || knot || shoulders || neck ? ink : paper;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool Ellipse(float x, float y, float width, float height) =>
            x * x / (width * width) + y * y / (height * height) <= 1;
    }
}
