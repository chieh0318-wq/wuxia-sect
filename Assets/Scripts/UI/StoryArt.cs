using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public static class StoryArt
    {
        private static readonly HashSet<string> UniqueStoryPortraits = new()
        {
            "master","friend_m","friend_f","elder_villain","warlord","official","rival_head"
        };
        private static Texture2D ink;

        public static Texture2D Portrait(string id,string gender)
        {
            var requested = string.IsNullOrWhiteSpace(id) ? null : Resources.Load<Texture2D>("Portraits/" + id);
            if (requested != null) return requested;
            if (!UniqueStoryPortraits.Contains(id))
            {
                var fallback = Resources.Load<Texture2D>(gender == "女" ? "Portraits/fallback_f" : "Portraits/fallback_m");
                if (fallback != null) return fallback;
            }
            return Ink();
        }

        public static Texture2D Scene(string id) =>
            (string.IsNullOrWhiteSpace(id) ? null : Resources.Load<Texture2D>("Story/" + id)) ?? Ink();

        private static Texture2D Ink()
        {
            if (ink != null) return ink;
            const int width = 160, height = 200;
            ink = new Texture2D(width,height,TextureFormat.RGB24,false) { name = "Procedural ink placeholder" };
            var pixels = new Color[width*height];
            var paper = new Color(.89f,.80f,.65f);
            var wash = new Color(.26f,.31f,.28f);
            for (int y=0;y<height;y++)
            for (int x=0;x<width;x++)
            {
                float u = x/(float)width, v = y/(float)height;
                float mountains = .19f + Mathf.PerlinNoise(u*3.6f,2.11f)*.49f;
                float silhouette = Mathf.Clamp01((mountains-v)*13);
                float grain = Mathf.PerlinNoise(u*80,v*97)*.12f;
                pixels[y*width+x] = Color.Lerp(paper,wash,silhouette*(.43f+v*.7f)+grain);
            }
            ink.SetPixels(pixels);
            ink.Apply(false,true);
            return ink;
        }
    }
}
