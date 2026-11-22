using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Wuxia
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const float FadeSeconds = 2.5f;
        private static AudioManager instance;
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] mix = new float[2];
        private AudioSource effects, voice;
        private Coroutine fade;
        private int active;
        private string cue;
        private float duck = 1, startedAt;
        private AudioClip[] library;
        private Dictionary<string, VoiceEntry> voiceMap;

        /// <summary>Tracks that suit each scene cue; the playlist rotates through them instead of looping one cue.</summary>
        private static readonly Dictionary<string, string[]> Playlists = new()
        {
            ["prologue"] = new[] { "prologue", "intel_scheme" },
            ["courtyard"] = new[] { "courtyard", "courtyard_autumn", "courtyard_night" },
            ["map"] = new[] { "map", "map_campaign", "intel_scheme" }
        };

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
                music[i].loop = false;
                music[i].volume = 0;
            }
            effects = gameObject.AddComponent<AudioSource>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.loop = false;
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
                music[i].volume = mix[i] * duck * GameAudioPrefs.Master * GameAudioPrefs.Music;
            effects.volume = GameAudioPrefs.Master * GameAudioPrefs.Sfx;
            voice.volume = GameAudioPrefs.Master * GameAudioPrefs.Sfx;
        }

        private void Update()
        {
            // Duck the score a little under spoken lines.
            float target = voice.isPlaying ? .55f : 1;
            if (!Mathf.Approximately(duck, target))
            {
                duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * 1.5f);
                Apply();
            }
            // Auto-advance: when the current track is about to end, crossfade into another fitting track.
            var current = music[active];
            if (fade != null || current.clip == null || string.IsNullOrEmpty(cue)) return;
            float length = current.clip.length;
            bool ending = current.isPlaying ? current.time >= length - FadeSeconds
                : Time.unscaledTime - startedAt >= Mathf.Max(FadeSeconds, length - FadeSeconds);
            if (ending)
                Switch(NextTrack(current.clip));
        }

        private AudioClip[] Library
        {
            get
            {
                if (library != null) return library;
                // Resources/Audio/Music wins over the serialized M2RuntimeAssets list, so freshly copied WAVs are
                // picked up without re-serializing the asset.
                var builtIn = M2RuntimeAssets.Load()?.music ?? Array.Empty<AudioClip>();
                var extra = Resources.LoadAll<AudioClip>("Audio/Music") ?? Array.Empty<AudioClip>();
                library = extra.Concat(builtIn).Where(clip => clip != null)
                    .GroupBy(clip => clip.name).Select(group => group.First()).ToArray();
                return library;
            }
        }

        private AudioClip[] Tracks(string name)
        {
            var names = Playlists.TryGetValue(name, out var list) ? list : new[] { name };
            var tracks = names.Select(id => Resources.Load<AudioClip>("Audio/Music/" + id) ??
                Library.FirstOrDefault(clip => clip.name == id)).Where(clip => clip != null).Distinct().ToList();
            foreach (var clip in Library.Where(clip => clip.name.StartsWith(name, StringComparison.Ordinal)))
                if (!tracks.Contains(clip)) tracks.Add(clip);
            return tracks.ToArray();
        }

        private AudioClip NextTrack(AudioClip current)
        {
            var tracks = Tracks(cue);
            if (tracks.Length == 0) return current;
            if (tracks.Length == 1) return tracks[0];
            int index = Array.IndexOf(tracks, current);
            // Step forward through the playlist with a little shuffle so the order is not monotonous.
            int step = UnityEngine.Random.value < .7f ? 1 : 2;
            return tracks[((index < 0 ? -1 : index) + step + tracks.Length) % tracks.Length];
        }

        public static void Music(string cue)
        {
            if (instance == null) return;
            var tracks = instance.Tracks(cue);
            if (tracks.Length == 0) return;
            var playing = instance.music[instance.active];
            instance.cue = cue;
            // Already playing something from this cue's playlist: let it run, the playlist continues from there.
            if (playing.clip != null && playing.isPlaying && tracks.Contains(playing.clip)) return;
            instance.Switch(tracks[0]);
        }

        internal static string PlaylistSummary => instance == null ? "" :
            string.Join(" | ", Playlists.Keys.Select(key => key + ":" + string.Join(",", instance.Tracks(key).Select(clip => clip.name))));

        internal static string CurrentTrack => instance == null ? null : instance.music[instance.active].clip?.name;

        public static void Play(string cue)
        {
            if (instance == null) return;
            var clip = M2RuntimeAssets.Load()?.sfx?.FirstOrDefault(item => item != null && item.name == cue);
            if (clip == null) return;
            instance.effects.pitch = UnityEngine.Random.Range(.96f, 1.04f);
            instance.effects.PlayOneShot(clip);
        }

        /// <summary>Plays an AI voice-over clip from Resources/Audio/Voice, replacing any line still speaking.</summary>
        public static void Voice(string clipId)
        {
            if (instance == null || string.IsNullOrEmpty(clipId)) return;
            var clip = Resources.Load<AudioClip>("Audio/Voice/" + clipId);
            instance.voice.Stop();
            if (clip == null) return;
            instance.voice.clip = clip;
            instance.voice.volume = GameAudioPrefs.Master * GameAudioPrefs.Sfx;
            instance.voice.Play();
        }

        public static void StopVoice()
        {
            if (instance != null && instance.voice != null) instance.voice.Stop();
        }

        internal static bool VoicePlaying => instance != null && instance.voice.isPlaying;

        /// <summary>Looks up the voice clip for a raw story-beat text (12-char md5 key in voice_map.json) and plays it.</summary>
        public static void VoiceForText(string rawText, bool femaleHero = false)
        {
            if (instance == null) return;
            string clip = ClipFor(rawText, femaleHero);
            if (clip == null) StopVoice();
            else Voice(clip);
        }

        /// <summary>Clip for a raw beat text; a female protagonist gets the entry's <c>clip_f</c> read when one exists.</summary>
        internal static string ClipFor(string rawText, bool femaleHero = false)
        {
            if (instance == null || string.IsNullOrEmpty(rawText)) return null;
            instance.voiceMap ??= LoadVoiceMap();
            if (!instance.voiceMap.TryGetValue(VoiceKey(rawText), out var entry)) return null;
            return femaleHero && !string.IsNullOrEmpty(entry.clip_f) ? entry.clip_f : entry.clip;
        }

        internal static string VoiceKey(string rawText)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(rawText ?? ""));
            var builder = new StringBuilder(12);
            for (int i = 0; i < 6; i++) builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        [Serializable] private sealed class VoiceEntry { public string hash, clip, clip_f; }
        [Serializable] private sealed class VoiceManifest { public VoiceEntry[] entries; }

        private static Dictionary<string, VoiceEntry> LoadVoiceMap()
        {
            var map = new Dictionary<string, VoiceEntry>();
            string path = Path.Combine(Application.streamingAssetsPath, "Data", "voice_map.json");
            try
            {
                if (!File.Exists(path)) return map;
                var manifest = JsonUtility.FromJson<VoiceManifest>(File.ReadAllText(path));
                foreach (var entry in manifest?.entries ?? Array.Empty<VoiceEntry>())
                    if (!string.IsNullOrEmpty(entry?.hash) && !string.IsNullOrEmpty(entry.clip))
                        map[entry.hash] = entry;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Audio] voice_map: {error.Message}");
            }
            return map;
        }

        private void Switch(AudioClip clip)
        {
            if (clip == null) return;
            if (fade != null) StopCoroutine(fade);
            int outgoing = active;
            active = 1 - active;
            music[active].clip = clip;
            music[active].time = 0;
            music[active].Play();
            startedAt = Time.unscaledTime;
            fade = StartCoroutine(Crossfade(outgoing));
        }

        private IEnumerator Crossfade(int outgoing)
        {
            float from = mix[outgoing], to = mix[active];
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / FadeSeconds)
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
