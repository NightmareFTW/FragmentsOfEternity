using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public enum Sfx { Attack, Hit, Crit, Heal, Block, Click, Victory, Defeat }

    // Self-creating audio singleton. All sounds are synthesised procedurally at
    // runtime (no asset files), matching the project's code-generated style.
    public class AudioManager : MonoBehaviour
    {
        private const int Rate = 44100;

        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AudioSource _sfx;
        private AudioSource _music;
        private Dictionary<Sfx, AudioClip> _clips;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            _clips = new Dictionary<Sfx, AudioClip>
            {
                { Sfx.Attack,  Sweep("attack", 520f, 170f, 0.16f, 0.30f, noise: 0.30f) },
                { Sfx.Hit,     Thud("hit", 0.16f, 0.50f) },
                { Sfx.Crit,    Thud("crit", 0.22f, 0.62f, pitch: 1.5f) },
                { Sfx.Heal,    Notes("heal",  new[] { 660f, 880f }, 0.32f, 0.26f) },
                { Sfx.Block,   Ting("block", 1150f, 0.18f, 0.30f) },
                { Sfx.Click,   Blip("click", 900f, 0.05f, 0.22f) },
                { Sfx.Victory, Notes("victory", new[] { 523f, 659f, 784f, 1047f }, 0.55f, 0.32f) },
                { Sfx.Defeat,  Notes("defeat",  new[] { 440f, 349f, 262f }, 0.5f, 0.30f) },
            };

            StartMusic();
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (_clips != null && _clips.TryGetValue(sfx, out var clip) && clip != null)
                _sfx.PlayOneShot(clip, volume);
        }

        // ── Ambient music ───────────────────────────────────────────────────

        private void StartMusic()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop        = true;
            _music.volume      = 0.06f;
            _music.playOnAwake = false;
            _music.clip        = Pad("music", new[] { 130.81f, 196f, 261.63f }, 8f);
            _music.Play();
        }

        // ── Synthesis ───────────────────────────────────────────────────────

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Sine sweep f0→f1 with optional noise and a decaying envelope (whoosh).
        private static AudioClip Sweep(string name, float f0, float f1, float dur, float vol, float noise = 0f)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                float env = Mathf.Pow(1f - t, 1.5f);
                float s = Mathf.Sin(phase) * (1f - noise) + (Random.value * 2f - 1f) * noise;
                data[i] = s * env * vol;
            }
            return Clip(name, data);
        }

        // Low sine + noise with a fast exponential decay (impact).
        private static AudioClip Thud(string name, float dur, float vol, float pitch = 1f)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Exp(-9f * t);
                phase += 2f * Mathf.PI * 120f * pitch / Rate;
                float s = Mathf.Sin(phase) * 0.6f + (Random.value * 2f - 1f) * 0.4f;
                data[i] = s * env * vol;
            }
            return Clip(name, data);
        }

        // A short sequence of bell-enveloped notes (heal / victory / defeat).
        private static AudioClip Notes(string name, float[] notes, float dur, float vol)
        {
            int per = Mathf.CeilToInt(Rate * dur / notes.Length);
            var data = new float[per * notes.Length];
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < per; i++)
                {
                    float t   = (float)i / per;
                    float env = Mathf.Sin(Mathf.PI * t);   // fades in and out → click-free
                    data[k * per + i] = Mathf.Sin(2f * Mathf.PI * notes[k] * i / Rate) * env * vol;
                }
            return Clip(name, data);
        }

        // High twin sine with quick metallic decay (shield block).
        private static AudioClip Ting(string name, float f, float dur, float vol)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / n;
                float env = Mathf.Exp(-12f * t);
                float s   = Mathf.Sin(2f * Mathf.PI * f * i / Rate)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * i / Rate);
                data[i] = s * env * vol * 0.6f;
            }
            return Clip(name, data);
        }

        // Very short blip (UI click).
        private static AudioClip Blip(string name, float f, float dur, float vol)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * i / Rate) * Mathf.Exp(-30f * t) * vol;
            }
            return Clip(name, data);
        }

        // Soft looping chord pad; amplitude LFO is ~0 at the seam so it loops
        // without a click.
        private static AudioClip Pad(string name, float[] chord, float dur)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / n;
                float lfo = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t);
                float s   = 0f;
                foreach (var f in chord) s += Mathf.Sin(2f * Mathf.PI * f * i / Rate);
                data[i] = (s / chord.Length) * lfo * 0.5f;
            }
            return Clip(name, data);
        }
    }
}
