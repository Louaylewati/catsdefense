using System.Collections.Generic;
using UnityEngine;

namespace CatDefense
{
    /// <summary>
    /// The kit has no audio, so v1 synthesises simple placeholder SFX and two music loops at startup.
    /// Swap in real clips later by assigning AudioClips with the same names.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioSource sfx, music;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        static string currentMusic;

        public static void Init(GameObject host)
        {
            if (sfx != null) return;
            sfx = host.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            music = host.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0.35f;
            music.playOnAwake = false;

            clips["shot0"] = Make(0.09f, t => Square(t * Lerp(900, 300, t / 0.09f)) * Env(t, 0.09f) * 0.35f + Noise() * Env(t, 0.03f) * 0.2f);
            clips["shot1"] = Make(0.16f, t => Saw(t * Lerp(500, 120, t / 0.16f)) * Env(t, 0.16f) * 0.4f + Noise() * Env(t, 0.06f) * 0.35f);
            clips["shot2"] = Make(0.2f, t => Sine(t * Lerp(220, 60, t / 0.2f)) * Env(t, 0.2f) * 0.6f + Noise() * Env(t, 0.08f) * 0.3f);
            clips["hit"] = Make(0.06f, t => Noise() * Env(t, 0.06f) * 0.3f + Square(t * 180) * Env(t, 0.04f) * 0.15f);
            clips["death"] = Make(0.35f, t => Saw(t * Lerp(260, 70, t / 0.35f)) * Env(t, 0.35f) * 0.35f);
            clips["boom"] = Make(0.6f, t => Noise() * Env(t, 0.6f) * 0.5f * (1 - t / 0.6f) + Sine(t * Lerp(90, 30, t / 0.6f)) * Env(t, 0.5f) * 0.5f);
            clips["barricade"] = Make(0.12f, t => Sine(t * 110) * Env(t, 0.12f) * 0.5f + Noise() * Env(t, 0.05f) * 0.3f);
            clips["roar"] = Make(1.1f, t => (Saw(t * (70 + 15 * Mathf.Sin(t * 30))) * 0.5f + Noise() * 0.3f) * Env(t, 1.1f) * Mathf.Min(1, t * 6) * 0.6f);
            clips["coin"] = Make(0.12f, t => Square(t * (t < 0.05f ? 1320 : 1760)) * Env(t, 0.12f) * 0.18f);
            clips["click"] = Make(0.05f, t => Sine(t * 900) * Env(t, 0.05f) * 0.4f);
            clips["wave"] = Notes(new[] { 523f, 659f, 784f }, 0.11f, 0.3f);
            clips["win"] = Notes(new[] { 523f, 659f, 784f, 1047f, 784f, 1047f }, 0.13f, 0.3f);
            clips["lose"] = Notes(new[] { 392f, 330f, 262f, 196f }, 0.22f, 0.3f);
            clips["special"] = Make(0.3f, t => Square(t * Lerp(400, 1600, t / 0.3f)) * Env(t, 0.3f) * 0.2f);
            clips["jump"] = Make(0.15f, t => Sine(t * Lerp(300, 700, t / 0.15f)) * Env(t, 0.15f) * 0.3f);
            clips["reload"] = Make(0.25f, t => (Square(t * (t < 0.1f ? 260 : 520)) * 0.15f + Noise() * 0.1f) * Env(Mathf.Repeat(t, 0.12f), 0.12f));
            clips["hurt"] = Make(0.2f, t => Square(t * Lerp(520, 180, t / 0.2f)) * Env(t, 0.2f) * 0.25f + Noise() * Env(t, 0.05f) * 0.2f);
            clips["free"] = Notes(new[] { 784f, 988f, 1175f, 1568f }, 0.09f, 0.3f);
            clips["warn"] = Make(0.5f, t => Square(t * (Mathf.Repeat(t, 0.25f) < 0.12f ? 440 : 0)) * 0.2f);

            clips["music_menu"] = Song(96, new[] { 0, 4, 7, 12, 7, 4, 0, 4, 5, 9, 12, 9, 7, 11, 14, 11 }, 262f, false);
            clips["music_battle"] = Song(132, new[] { 0, 0, 7, 0, 3, 0, 10, 7, 0, 0, 7, 0, 5, 3, 2, -2 }, 110f, true);
        }

        public static void Play(string name, float volume = 1f, float minGap = 0.03f)
        {
            if (sfx == null || !Save.Sound || !clips.TryGetValue(name, out var c)) return;
            // stop rapid-fire sounds from stacking into noise
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(name, out var last) && now - last < minGap) return;
            lastPlayed[name] = now;
            sfx.PlayOneShot(c, volume);
        }

        public static void Music(string name)
        {
            currentMusic = name;
            if (music == null) return;
            if (!Save.Music) { music.Stop(); return; }
            if (music.clip == clips[name] && music.isPlaying) return;
            music.clip = clips[name];
            music.Play();
        }

        public static void RefreshMusic()
        {
            if (currentMusic != null) Music(currentMusic);
        }

        public static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Save.Vibration) Handheld.Vibrate();
#endif
        }

        // ---- tiny synth ----
        delegate float Gen(float t);
        static readonly System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Sine(float p) => Mathf.Sin(p * 2 * Mathf.PI);
        static float Square(float p) => Mathf.Repeat(p, 1) < 0.5f ? 1 : -1;
        static float Saw(float p) => Mathf.Repeat(p, 1) * 2 - 1;
        static float Tri(float p) => Mathf.Abs(Mathf.Repeat(p, 1) * 4 - 2) - 1;
        static float Lerp(float a, float b, float t) => a + (b - a) * Mathf.Clamp01(t);
        static float Env(float t, float len) => Mathf.Clamp01(1 - t / len) * Mathf.Clamp01(t * 400);

        static AudioClip Make(float len, Gen g)
        {
            int n = (int)(len * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(g(i / (float)Rate), -1, 1);
            var c = AudioClip.Create("sfx", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Notes(float[] freqs, float step, float vol)
        {
            int per = (int)(step * Rate), n = per * freqs.Length + Rate / 5;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
                for (int i = 0; i < per + Rate / 5 && k * per + i < n; i++)
                {
                    float t = i / (float)Rate;
                    data[k * per + i] += Square(t * freqs[k]) * vol * Mathf.Clamp01(1 - t / (step * 1.6f)) * 0.6f;
                }
            var c = AudioClip.Create("notes", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Song(float bpm, int[] steps, float root, bool drums)
        {
            float beat = 60f / bpm / 2f; // eighth notes
            int per = (int)(beat * Rate);
            int bars = 2;
            int n = per * steps.Length * bars;
            var data = new float[n];
            for (int b = 0; b < bars; b++)
                for (int k = 0; k < steps.Length; k++)
                {
                    float f = root * Mathf.Pow(2, steps[k] / 12f) * (b == 1 && !drums ? 1.5f : 1f);
                    int off = (b * steps.Length + k) * per;
                    for (int i = 0; i < per; i++)
                    {
                        float t = i / (float)Rate;
                        float env = Mathf.Clamp01(1 - t / (beat * 0.95f)) * Mathf.Clamp01(t * 300);
                        float v = (drums ? Square(t * f) * 0.12f + Tri(t * f * 2) * 0.1f : Tri(t * f) * 0.22f + Sine(t * f * 2) * 0.06f) * env;
                        if (drums)
                        {
                            if (k % 4 == 0) v += Sine(t * Lerp(120, 40, t / 0.12f)) * Mathf.Clamp01(1 - t / 0.15f) * 0.5f;
                            if (k % 4 == 2) v += Noise() * Mathf.Clamp01(1 - t / 0.08f) * 0.18f;
                            v += Noise() * Mathf.Clamp01(1 - t / 0.02f) * 0.05f;
                        }
                        data[off + i] += v;
                    }
                }
            var c = AudioClip.Create("music", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
