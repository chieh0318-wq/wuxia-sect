using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wuxia
{
    // MERGE: replace with GameAudioPrefs
    public static class AudioVolumes
    {
        public static event Action Changed;
        public static float Master { get => Read("Master", .8f); set => Write("Master", value); }
        public static float Music { get => Read("Music", .55f); set => Write("Music", value); }
        public static float Sfx { get => Read("Sfx", .75f); set => Write("Sfx", value); }
        private static float Read(string key, float fallback) => PlayerPrefs.GetFloat("Audio." + key, fallback);
        private static void Write(string key, float value)
        {
            PlayerPrefs.SetFloat("Audio." + key, Mathf.Clamp01(value));
            Changed?.Invoke();
        }
    }

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
            Music("courtyard");
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
            AudioVolumes.Changed += Apply;
            Apply();
        }

        private void OnDestroy() => AudioVolumes.Changed -= Apply;

        private void Apply()
        {
            for (int i = 0; i < music.Length; i++)
                music[i].volume = mix[i] * AudioVolumes.Master * AudioVolumes.Music;
            effects.volume = AudioVolumes.Master * AudioVolumes.Sfx;
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

    internal sealed class M2ButtonAudio : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => AudioManager.Play("hover");
    }
}
