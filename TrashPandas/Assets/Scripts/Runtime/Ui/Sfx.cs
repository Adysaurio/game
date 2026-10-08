using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Runtime.Ui
{
    public enum Sound { Jump, Land, Grab, Throw, Deliver, Hit, Caught, Rescue, Siren, Spotted }

    /// <summary>
    /// Placeholder sound effects synthesized in code (no audio files yet): cartoon boings, pops, bonks and a
    /// cash register. Every action gets a sound — half of "juice" is audio.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<Sound, AudioClip> s_clips = new Dictionary<Sound, AudioClip>();
        public static float Volume = 0.6f;

        public static void Play(Sound sound, Vector3 at, float volume = 1f)
        {
            if (Application.isBatchMode) return;
            var clip = Clip(sound);
            if (!clip) return;
            var go = new GameObject($"Sfx_{sound}");
            go.transform.position = at;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 0.75f;
            src.minDistance = 3f;
            src.maxDistance = 30f;
            src.volume = Volume * volume;
            src.pitch = Random.Range(0.94f, 1.06f); // never the exact same twice
            src.Play();
            Object.Destroy(go, clip.length / src.pitch + 0.1f);
        }

        /// <summary>Non-positional (UI-ish) sounds: the siren, getting caught, a rescue.</summary>
        public static void Play2D(Sound sound, float volume = 1f)
        {
            var cam = Camera.main;
            Play(sound, cam ? cam.transform.position : Vector3.zero, volume);
        }

        static AudioClip Clip(Sound s)
        {
            if (s_clips.TryGetValue(s, out var c)) return c;
            c = s switch
            {
                Sound.Jump => Sweep(0.16f, 300f, 720f, 0.5f, square: false),
                Sound.Land => Thud(0.12f, 110f),
                Sound.Grab => Sweep(0.07f, 600f, 1100f, 0.45f, square: true),
                Sound.Throw => Noise(0.18f, 0.35f, sweepDown: true),
                Sound.Deliver => Notes(new[] { 1318f, 1975f }, 0.09f, 0.5f),
                Sound.Hit => Bonk(),
                Sound.Caught => Notes(new[] { 392f, 370f, 349f, 330f }, 0.22f, 0.45f, slideLast: true),
                Sound.Rescue => Notes(new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.45f),
                Sound.Siren => Siren(1.6f),
                Sound.Spotted => Sweep(0.12f, 900f, 1400f, 0.4f, square: true),
                _ => null,
            };
            s_clips[s] = c;
            return c;
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float len) => Mathf.Clamp01(t / 0.008f) * Mathf.Clamp01((len - t) / (len * 0.6f));

        static AudioClip Sweep(float len, float f0, float f1, float amp, bool square)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, f = Mathf.Lerp(f0, f1, t / len);
                ph += 2 * Mathf.PI * f / Rate;
                float v = (float)System.Math.Sin(ph);
                if (square) v = Mathf.Sign(v) * 0.6f;
                d[i] = v * amp * Env(t, len);
            }
            return Make("sweep", d);
        }

        static AudioClip Thud(float len, float f)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            var rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = 1f - t / len;
                d[i] = ((float)System.Math.Sin(2 * Mathf.PI * f * t * (1f - 0.4f * t / len)) * 0.8f + ((float)rng.NextDouble() - 0.5f) * 0.3f * k) * k * k * 0.7f;
            }
            return Make("thud", d);
        }

        static AudioClip Bonk()
        {
            float len = 0.25f;
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = Mathf.Exp(-t * 14f);
                d[i] = ((float)System.Math.Sin(2 * Mathf.PI * 520f * t) + 0.5f * (float)System.Math.Sin(2 * Mathf.PI * 1310f * t)) * k * 0.45f;
            }
            return Make("bonk", d);
        }

        static AudioClip Noise(float len, float amp, bool sweepDown)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            var rng = new System.Random(3);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, a = sweepDown ? 0.6f - 0.5f * t / len : 0.3f;
                last = Mathf.Lerp(last, (float)rng.NextDouble() * 2f - 1f, a);
                d[i] = last * amp * Env(t, len);
            }
            return Make("noise", d);
        }

        static AudioClip Notes(float[] freqs, float each, float amp, bool slideLast = false)
        {
            int per = (int)(each * Rate), n = per * freqs.Length + (slideLast ? per * 2 : 0);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                int note = Mathf.Min(i / per, freqs.Length - 1);
                float f = freqs[note];
                float local = (i - note * per) / (float)Rate, len = (note == freqs.Length - 1 && slideLast) ? each * 3f : each;
                if (note == freqs.Length - 1 && slideLast) f *= 1f - 0.12f * local / len + 0.02f * Mathf.Sin(local * 40f);
                ph += 2 * Mathf.PI * f / Rate;
                float v = (float)System.Math.Sin(ph) * 0.7f + (float)System.Math.Sin(ph * 2) * 0.2f;
                d[i] = v * amp * Env(local, len);
            }
            return Make("notes", d);
        }

        static AudioClip Siren(float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, f = 700f + 350f * Mathf.Sin(t * Mathf.PI * 2f * 1.8f);
                ph += 2 * Mathf.PI * f / Rate;
                d[i] = Mathf.Sign((float)System.Math.Sin(ph)) * 0.18f * Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((len - t) / 0.3f);
            }
            return Make("siren", d);
        }
    }
}
