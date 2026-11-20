using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] mix = new float[2];
        private AudioSource effects;
        private Coroutine fade;
        private int active;

        public static void Initialize()
        {
            if (instance != null) return;
            instance = new GameObject("Wuxia Audio").AddComponent<AudioManager>();
            UiSfx.Handler = Play; // UI kit buttons route click/hover cues through UiSfx
            Music("prologue"); // title and opening story; the courtyard switches to its own cue
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < music.Length; i++)
            {
                music[i] = gameObject.AddComponent<AudioSource>();
                music[i].loop = true;
                music[i].volume = 0;
            }
            effects = gameObject.AddComponent<AudioSource>();
            GameAudioPrefs.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            GameAudioPrefs.Changed -= Apply;
            if (instance == this)
            {
                UiSfx.Handler = null;
                instance = null;
            }
        }

        private void Apply()
        {
            for (int i = 0; i < music.Length; i++)
                music[i].volume = mix[i] * GameAudioPrefs.Master * GameAudioPrefs.Music;
            effects.volume = GameAudioPrefs.Master * GameAudioPrefs.Sfx;
        }

        public static void Music(string cue)
        {
            if (instance == null) return;
            var clip = M2RuntimeAssets.Load()?.music?.FirstOrDefault(item => item != null && item.name == cue);
            if (clip == null || instance.music[instance.active].clip == clip) return;
            instance.Switch(clip);
        }

        public static void Play(string cue)
        {
            if (instance == null) return;
            var clip = M2RuntimeAssets.Load()?.sfx?.FirstOrDefault(item => item != null && item.name == cue);
            if (clip == null) return;
            instance.effects.pitch = UnityEngine.Random.Range(.96f, 1.04f);
            instance.effects.PlayOneShot(clip);
        }

        private void Switch(AudioClip clip)
        {
            if (fade != null) StopCoroutine(fade);
            int outgoing = active;
            active = 1 - active;
            if (music[active].clip != clip || !music[active].isPlaying)
            {
                music[active].clip = clip;
                music[active].Play();
            }
            fade = StartCoroutine(Crossfade(outgoing));
        }

        private IEnumerator Crossfade(int outgoing)
        {
            float from = mix[outgoing], to = mix[active];
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 1.5f)
            {
                mix[outgoing] = Mathf.Lerp(from, 0, t);
                mix[active] = Mathf.Lerp(to, 1, t);
                Apply();
                yield return null;
            }
            mix[outgoing] = 0;
            mix[active] = 1;
            music[outgoing].Stop();
            Apply();
            fade = null;
        }
    }

}
