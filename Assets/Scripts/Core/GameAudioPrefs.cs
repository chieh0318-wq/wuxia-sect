using System;
using UnityEngine;

namespace Wuxia
{
    public static class GameAudioPrefs
    {
        public static event Action Changed;
        public static float Master { get => Muted ? 0 : MasterVolume; set => Set("master", value); }
        public static float MasterVolume => Get("master");
        public static float Music { get => Get("music"); set => Set("music", value); }
        public static float Sfx { get => Get("sfx"); set => Set("sfx", value); }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("wuxia.audio.mute") != 0;
            set
            {
                PlayerPrefs.SetInt("wuxia.audio.mute", value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        private static float Get(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat("wuxia.audio." + key, 1));

        private static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat("wuxia.audio." + key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static class UiSfx
    {
        public static Action<string> Handler;
        public static void Play(string cue) => Handler?.Invoke(cue);
    }
}
