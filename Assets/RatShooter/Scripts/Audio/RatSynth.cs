using UnityEngine;

namespace NeonBreach.RatShooter
{
    public enum RatSound
    {
        Shot, Empty, MagOut, MagIn, Bolt, Casing,
        Squeak1, Squeak2, Squeak3, Eek, Nibble, Pop,
        Bonk, Poof, Thud, Ding, Combo,
        Beep, Go, LevelUp, Victory, GameOver,
        Click, Hover, Whoosh, Warning
    }

    /// <summary>
    /// Procedural cartoon sound effects and music (no audio files). Everything is synthesised from sines, noise,
    /// filters and Karplus-Strong plucked strings. Sample generation is plain C# (thread-safe) so the music can be
    /// generated off the main thread; <see cref="RatAudio"/> turns the samples into AudioClips.
    /// </summary>
    public static class RatSynth
    {
        public const int Rate = 44100;
        public const int MusicRate = 22050;
        const float TwoPi = Mathf.PI * 2f;

        static float[] Buf(float seconds, int rate = Rate) { return new float[Mathf.CeilToInt(seconds * rate)]; }

        static float Lp(float cutoff, int rate) { return 1f - Mathf.Exp(-TwoPi * cutoff / rate); }

        static float Note(float semisFromA4) { return 440f * Mathf.Pow(2f, semisFromA4 / 12f); }

        // note names -> semitones from A4
        static float N(string name)
        {
            string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string pitch = name.Substring(0, name.Length - 1);
            int octave = name[name.Length - 1] - '0';
            int idx = System.Array.IndexOf(names, pitch);
            return Note(idx - 9 + (octave - 4) * 12);
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static void FadeEdges(float[] d, int rate, float fadeIn = 0.002f, float fadeOut = 0.01f)
        {
            int fi = Mathf.Min(d.Length, Mathf.CeilToInt(fadeIn * rate));
            int fo = Mathf.Min(d.Length, Mathf.CeilToInt(fadeOut * rate));
            for (int i = 0; i < fi; i++) d[i] *= (float)i / fi;
            for (int i = 0; i < fo; i++) d[d.Length - 1 - i] *= (float)i / fo;
        }

        public static float[] Generate(RatSound s)
        {
            System.Random rng = new System.Random(1000 + (int)s);
            float[] d;
            switch (s)
            {
                case RatSound.Shot: d = Shot(rng); break;
                case RatSound.Empty: d = Clicky(rng, 0.08f, 2200f, 0.6f); break;
                case RatSound.MagOut: d = MagOut(rng); break;
                case RatSound.MagIn: d = MagIn(rng); break;
                case RatSound.Bolt: d = Bolt(rng); break;
                case RatSound.Casing: d = Casing(); break;
                case RatSound.Squeak1: d = Squeak(rng, 3100f, 0.16f, 1, 0.2f); break;
                case RatSound.Squeak2: d = Squeak(rng, 2700f, 0.12f, 2, 0.35f); break;
                case RatSound.Squeak3: d = Squeak(rng, 3500f, 0.2f, 1, -0.15f); break;
                case RatSound.Eek: d = Eek(); break;
                case RatSound.Nibble: d = Nibble(rng); break;
                case RatSound.Pop: d = Pop(); break;
                case RatSound.Bonk: d = Bonk(rng); break;
                case RatSound.Poof: d = Poof(rng); break;
                case RatSound.Thud: d = Thud(rng); break;
                case RatSound.Ding: d = Bell(new[] { N("E6"), N("B6") }, new[] { 0f, 0.07f }, 0.9f); break;
                case RatSound.Combo: d = Bell(new[] { N("C6"), N("E6"), N("G6"), N("C7") }, new[] { 0f, 0.06f, 0.12f, 0.18f }, 0.7f); break;
                case RatSound.Beep: d = Tone(N("A5"), 0.16f, 0.35f); break;
                case RatSound.Go: d = Go(); break;
                case RatSound.LevelUp: d = Jingle(new[] { "C5", "E5", "G5", "C6", "E6" }, 0.11f, 0.7f, rng); break;
                case RatSound.Victory: d = Fanfare(rng); break;
                case RatSound.GameOver: d = Trombone(); break;
                case RatSound.Click: d = UiPop(900f, 520f, 0.05f); break;
                case RatSound.Hover: d = UiPop(1800f, 1500f, 0.025f); break;
                case RatSound.Whoosh: d = Whoosh(rng); break;
                case RatSound.Warning: d = Tone(N("E5"), 0.12f, 0.3f); break;
                default: d = Buf(0.05f); break;
            }
            FadeEdges(d, Rate);
            return d;
        }

        // ------------------------------------------------------------------ weapon

        static float[] Shot(System.Random rng)
        {
            float[] d = Buf(0.75f);
            float lp = 0f, lp2 = 0f, phase = 0f, popPhase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float cutoff = Mathf.Lerp(7000f, 300f, Mathf.Clamp01(t / 0.22f));
                lp += Lp(cutoff, Rate) * (noise - lp);
                lp2 += Lp(500f, Rate) * (noise - lp2);
                float blast = lp * Mathf.Exp(-t * 16f) * 1.1f;
                float tail = lp2 * Mathf.Exp(-t * 4.5f) * 0.55f;
                float f = Mathf.Lerp(150f, 42f, Mathf.Clamp01(t / 0.18f));
                phase += TwoPi * f / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 11f) * 1.2f;
                float pf = Mathf.Lerp(1500f, 260f, Mathf.Clamp01(t / 0.05f));
                popPhase += TwoPi * pf / Rate;
                float pop = Mathf.Sin(popPhase) * Mathf.Exp(-t * 55f) * 0.7f;
                float crack = t < 0.006f ? noise * (1f - t / 0.006f) : 0f;
                float x = blast + tail + thump + pop + crack;
                d[i] = (float)System.Math.Tanh(x * 1.6f);
            }
            Normalize(d, 0.95f);
            return d;
        }

        static float[] Clicky(System.Random rng, float dur, float ring, float amp)
        {
            float[] d = Buf(dur);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                hp = 0.92f * (hp + noise - prev);
                prev = noise;
                d[i] = hp * Mathf.Exp(-t * 160f) * amp + Mathf.Sin(TwoPi * ring * t) * Mathf.Exp(-t * 60f) * amp * 0.5f;
            }
            return d;
        }

        static void AddAt(float[] dst, float[] src, float at, float gain)
        {
            int o = Mathf.RoundToInt(at * Rate);
            for (int i = 0; i < src.Length && o + i < dst.Length; i++) dst[o + i] += src[i] * gain;
        }

        static float[] MagOut(System.Random rng)
        {
            float[] d = Buf(0.3f);
            AddAt(d, Clicky(rng, 0.06f, 2600f, 0.9f), 0f, 1f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                if (t < 0.04f || t > 0.2f) continue;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += Lp(3000f, Rate) * (noise - lp);
                d[i] += lp * Mathf.Sin((t - 0.04f) / 0.16f * Mathf.PI) * 0.35f;
            }
            Normalize(d, 0.7f);
            return d;
        }

        static float[] MagIn(System.Random rng)
        {
            float[] d = Buf(0.25f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] += Mathf.Sin(TwoPi * Mathf.Lerp(220f, 120f, t / 0.06f) * t) * Mathf.Exp(-t * 40f) * 0.9f;
            }
            AddAt(d, Clicky(rng, 0.06f, 3100f, 1f), 0.035f, 0.9f);
            Normalize(d, 0.8f);
            return d;
        }

        static float[] Bolt(System.Random rng)
        {
            float[] d = Buf(0.42f);
            AddAt(d, Clicky(rng, 0.07f, 1900f, 1f), 0f, 1f);
            AddAt(d, Clicky(rng, 0.09f, 2400f, 1f), 0.17f, 1.1f);
            Normalize(d, 0.75f);
            return d;
        }

        static float[] Casing()
        {
            float[] d = Buf(0.5f);
            float[] freqs = { 3150f, 4730f, 6080f, 7900f };
            for (int b = 0; b < 3; b++)
            {
                float at = b == 0 ? 0f : (b == 1 ? 0.14f : 0.24f);
                float g = b == 0 ? 1f : (b == 1 ? 0.5f : 0.25f);
                int o = Mathf.RoundToInt(at * Rate);
                for (int i = 0; o + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float x = 0f;
                    for (int k = 0; k < freqs.Length; k++) x += Mathf.Sin(TwoPi * freqs[k] * t) * Mathf.Exp(-t * (30f + k * 12f)) / (k + 1);
                    d[o + i] += x * g;
                }
            }
            Normalize(d, 0.35f);
            return d;
        }

        // ------------------------------------------------------------------ mice

        static float[] Squeak(System.Random rng, float f0, float dur, int count, float glide)
        {
            float gap = 0.05f;
            float[] d = Buf(count * (dur + gap) + 0.02f);
            for (int c = 0; c < count; c++)
            {
                float start = c * (dur + gap);
                float phase = 0f;
                float vib = 30f + (float)rng.NextDouble() * 20f;
                float fbase = f0 * (1f + c * 0.08f);
                int o = Mathf.RoundToInt(start * Rate);
                int len = Mathf.RoundToInt(dur * Rate);
                for (int i = 0; i < len && o + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float u = t / dur;
                    float f = fbase * (1f + glide * Mathf.Sin(u * Mathf.PI)) + Mathf.Sin(TwoPi * vib * t) * 140f;
                    phase += TwoPi * f / Rate;
                    float env = Mathf.Clamp01(u / 0.08f) * Mathf.Pow(1f - u, 1.4f);
                    d[o + i] += (Mathf.Sin(phase) + 0.28f * Mathf.Sin(phase * 2f) + 0.1f * Mathf.Sin(phase * 3f)) * env;
                }
            }
            Normalize(d, 0.55f);
            return d;
        }

        static float[] Eek()
        {
            float[] d = Buf(0.42f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float u = Mathf.Clamp01(t / 0.3f);
                float f = Mathf.Lerp(2300f, 4800f, Mathf.Sqrt(u)) + Mathf.Sin(TwoPi * 45f * t) * 220f;
                phase += TwoPi * f / Rate;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 6f) * (t < 0.3f ? 1f : Mathf.Clamp01(1f - (t - 0.3f) / 0.1f));
                d[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f)) * env;
            }
            Normalize(d, 0.6f);
            return d;
        }

        static float[] Nibble(System.Random rng)
        {
            float[] d = Buf(0.34f);
            float hp = 0f, prev = 0f;
            for (int b = 0; b < 4; b++)
            {
                int o = Mathf.RoundToInt((b * 0.075f + (float)rng.NextDouble() * 0.02f) * Rate);
                for (int i = 0; i < 1600 && o + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    hp = 0.85f * (hp + noise - prev);
                    prev = noise;
                    d[o + i] += hp * Mathf.Exp(-t * 90f) * (0.6f + (float)rng.NextDouble() * 0.4f);
                }
            }
            Normalize(d, 0.4f);
            return d;
        }

        static float[] Pop()
        {
            float[] d = Buf(0.14f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = Mathf.Lerp(350f, 950f, Mathf.Clamp01(t / 0.07f));
                phase += TwoPi * f / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 28f);
            }
            Normalize(d, 0.5f);
            return d;
        }

        static float[] Bonk(System.Random rng)
        {
            float[] d = Buf(0.55f);
            float p1 = 0f, p2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f1 = Mathf.Lerp(620f, 150f, Mathf.Clamp01(t / 0.11f));
                p1 += TwoPi * f1 / Rate;
                float bonk = Mathf.Sin(p1) * Mathf.Exp(-t * 18f);
                float wobble = Mathf.Sin(TwoPi * 16f * t) * Mathf.Exp(-t * 5f);
                p2 += TwoPi * (300f + wobble * 90f) / Rate;
                float boing = Mathf.Sin(p2) * Mathf.Exp(-t * 6f) * 0.55f * Mathf.Clamp01((t - 0.03f) / 0.02f);
                float click = t < 0.004f ? ((float)rng.NextDouble() * 2f - 1f) * 0.6f : 0f;
                d[i] = bonk + boing + click;
            }
            Normalize(d, 0.8f);
            return d;
        }

        static float[] Poof(System.Random rng)
        {
            float[] d = Buf(0.5f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += Lp(2200f, Rate) * (noise - lp);
                lp2 += Lp(300f, Rate) * (lp - lp2);
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 7f);
                d[i] = (lp - lp2) * env;
            }
            Normalize(d, 0.55f);
            return d;
        }

        static float[] Thud(System.Random rng)
        {
            float[] d = Buf(0.2f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += Lp(400f, Rate) * (noise - lp);
                d[i] = Mathf.Sin(TwoPi * Mathf.Lerp(120f, 60f, t / 0.2f) * t) * Mathf.Exp(-t * 25f) + lp * Mathf.Exp(-t * 30f) * 0.8f;
            }
            Normalize(d, 0.5f);
            return d;
        }

        // ------------------------------------------------------------------ UI / jingles

        static float[] Bell(float[] freqs, float[] starts, float dur)
        {
            float[] d = Buf(starts[starts.Length - 1] + dur);
            for (int n = 0; n < freqs.Length; n++)
            {
                int o = Mathf.RoundToInt(starts[n] * Rate);
                for (int i = 0; o + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float f = freqs[n];
                    float x = Mathf.Sin(TwoPi * f * t) + 0.35f * Mathf.Sin(TwoPi * f * 2.01f * t) * Mathf.Exp(-t * 6f) + 0.12f * Mathf.Sin(TwoPi * f * 3.98f * t) * Mathf.Exp(-t * 10f);
                    d[o + i] += x * Mathf.Exp(-t * 4.2f) * Mathf.Clamp01(t / 0.003f);
                }
            }
            Normalize(d, 0.5f);
            return d;
        }

        static float[] Tone(float f, float dur, float amp)
        {
            float[] d = Buf(dur);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((dur - t) / 0.04f);
                d[i] = (Mathf.Sin(TwoPi * f * t) + 0.25f * Mathf.Sin(TwoPi * f * 3f * t) + 0.12f * Mathf.Sin(TwoPi * f * 2f * t)) * env * amp;
            }
            return d;
        }

        static float[] Go()
        {
            float[] d = Buf(0.6f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float vib = 1f + Mathf.Sin(TwoPi * 6f * t) * 0.01f * Mathf.Clamp01(t / 0.2f);
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((0.6f - t) / 0.2f);
                d[i] = (Mathf.Sin(TwoPi * N("E6") * vib * t) + 0.6f * Mathf.Sin(TwoPi * N("A5") * vib * t) + 0.2f * Mathf.Sin(TwoPi * N("E7") * t)) * env;
            }
            Normalize(d, 0.45f);
            return d;
        }

        /// <summary>Karplus-Strong plucked string mixed into dst.</summary>
        static void Pluck(float[] dst, int rate, float start, float freq, float dur, float amp, float bright, System.Random rng)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(rate / freq));
            float[] ring = new float[n];
            float lpState = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)rng.NextDouble() * 2f - 1f;
                lpState += bright * (x - lpState);
                ring[i] = lpState;
            }
            int o = Mathf.RoundToInt(start * rate);
            int len = Mathf.RoundToInt(dur * rate);
            int idx = 0;
            float decay = 0.996f;
            for (int i = 0; i < len && o + i < dst.Length; i++)
            {
                if (o + i < 0) continue;
                float y = ring[idx];
                int nx = (idx + 1) % n;
                ring[idx] = decay * 0.5f * (ring[idx] + ring[nx]);
                idx = nx;
                float env = Mathf.Clamp01((len - i) / (rate * 0.05f));
                dst[o + i] += y * amp * env;
            }
        }

        static float[] Jingle(string[] notes, float step, float tailDur, System.Random rng)
        {
            float[] d = Buf(notes.Length * step + tailDur);
            for (int i = 0; i < notes.Length; i++)
            {
                float f = N(notes[i]);
                bool last = i == notes.Length - 1;
                Pluck(d, Rate, i * step, f, last ? tailDur + step : step * 2.5f, 0.8f, 0.6f, rng);
                AddSine(d, i * step, f * 2f, last ? tailDur : step * 1.5f, 0.12f);
            }
            Normalize(d, 0.6f);
            return d;
        }

        static void AddSine(float[] d, float start, float f, float dur, float amp)
        {
            int o = Mathf.RoundToInt(start * Rate);
            int len = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < len && o + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[o + i] += Mathf.Sin(TwoPi * f * t) * amp * Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((dur - t) / 0.08f);
            }
        }

        /// <summary>Cartoon brass: filtered sawtooth with vibrato.</summary>
        static void Brass(float[] d, float start, float f, float dur, float amp, float vibrato)
        {
            int o = Mathf.RoundToInt(start * Rate);
            int len = Mathf.RoundToInt(dur * Rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < len && o + i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float vib = 1f + Mathf.Sin(TwoPi * 5.5f * t) * vibrato * Mathf.Clamp01(t / 0.25f);
                phase += f * vib / Rate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float env = Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((dur - t) / 0.06f);
                float cutoff = 500f + 2600f * env;
                lp += Lp(cutoff, Rate) * (saw - lp);
                d[o + i] += lp * env * amp;
            }
        }

        static float[] Fanfare(System.Random rng)
        {
            float[] d = Buf(2.8f);
            string[] mel = { "G4", "C5", "E5", "G5", "E5", "G5", "C6" };
            float[] at = { 0f, 0.16f, 0.32f, 0.48f, 0.8f, 0.96f, 1.12f };
            float[] len = { 0.15f, 0.15f, 0.15f, 0.3f, 0.15f, 0.15f, 1.5f };
            for (int i = 0; i < mel.Length; i++)
            {
                Brass(d, at[i], N(mel[i]), len[i] + 0.05f, 0.5f, i == mel.Length - 1 ? 0.012f : 0.004f);
                Pluck(d, Rate, at[i], N(mel[i]) * 0.5f, len[i] + 0.4f, 0.35f, 0.5f, rng);
            }
            Brass(d, 1.12f, N("E5"), 1.55f, 0.32f, 0.01f);
            Brass(d, 1.12f, N("G4"), 1.55f, 0.28f, 0.01f);
            Normalize(d, 0.7f);
            return d;
        }

        static float[] Trombone()
        {
            float[] d = Buf(2.6f);
            string[] notes = { "D4", "C#4", "C4", "B3" };
            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;
                Brass(d, i * 0.45f, N(notes[i]) * 0.5f, last ? 1.3f : 0.4f, 0.6f, last ? 0.03f : 0.006f);
            }
            Normalize(d, 0.65f);
            return d;
        }

        static float[] UiPop(float f0, float f1, float dur)
        {
            float[] d = Buf(dur);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                phase += TwoPi * Mathf.Lerp(f0, f1, t / dur) / Rate;
                d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * (6f / dur));
            }
            Normalize(d, 0.4f);
            return d;
        }

        static float[] Whoosh(System.Random rng)
        {
            float[] d = Buf(0.35f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float u = t / 0.35f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += Lp(Mathf.Lerp(400f, 2500f, Mathf.Sin(u * Mathf.PI)), Rate) * (noise - lp);
                d[i] = lp * Mathf.Sin(u * Mathf.PI);
            }
            Normalize(d, 0.3f);
            return d;
        }

        // ------------------------------------------------------------------ music + ambience (MusicRate)

        /// <summary>16 s cheerful farm loop: boom-chick banjo, plucked bass, shaker and a whistled tune (G major, 120 bpm).</summary>
        public static float[] Music()
        {
            int rate = MusicRate;
            const float beat = 0.5f;
            const int bars = 8;
            float total = bars * 4 * beat;
            float[] d = new float[Mathf.CeilToInt(total * rate)];
            System.Random rng = new System.Random(77);

            string[][] chords =
            {
                new[] { "G3", "B3", "D4", "G4" }, new[] { "C4", "E4", "G4", "C5" }, new[] { "G3", "B3", "D4", "G4" }, new[] { "D4", "F#4", "A4", "D5" },
                new[] { "G3", "B3", "D4", "G4" }, new[] { "C4", "E4", "G4", "C5" }, new[] { "D4", "F#4", "A4", "D5" }, new[] { "G3", "B3", "D4", "G4" }
            };
            string[] roots = { "G2", "C3", "G2", "D3", "G2", "C3", "D3", "G2" };
            string[] fifths = { "D3", "G2", "D3", "A2", "D3", "G2", "A2", "D3" };

            for (int b = 0; b < bars; b++)
            {
                float bs = b * 4 * beat;
                Pluck(d, rate, bs, N(roots[b]), 1.0f, 0.55f, 0.35f, rng);
                Pluck(d, rate, bs + 2 * beat, N(fifths[b]), 1.0f, 0.5f, 0.35f, rng);
                for (int s = 0; s < 4; s++)
                {
                    bool strong = s == 1 || s == 3;
                    float st = bs + s * beat + (strong ? 0f : beat * 0.5f);
                    if (!strong && s % 2 == 0) st = bs + s * beat + beat * 0.5f;
                    float amp = strong ? 0.3f : 0.16f;
                    for (int n = 0; n < chords[b].Length; n++)
                        Pluck(d, rate, st + n * 0.012f, N(chords[b][n]), 0.45f, amp, 0.75f, rng);
                }
            }

            // whistle melody, eighth-note grid; "-" holds the previous note
            string[] mel =
            {
                "B4","-","D5","-","G5","-","E5","D5",  "E5","-","C5","-","E5","G5","E5","-",
                "D5","-","B4","-","G4","-","A4","B4",  "A4","-","-","F#4","A4","-","D5","-",
                "B4","-","D5","-","G5","-","A5","G5",  "E5","-","G5","-","E5","D5","C5","-",
                "D5","-","A4","-","F#4","-","A4","C5", "B4","-","-","-","G4","-","-","-"
            };
            float eighth = beat * 0.5f;
            for (int i = 0; i < mel.Length; i++)
            {
                if (mel[i] == "-") continue;
                int len = 1;
                while (i + len < mel.Length && mel[i + len] == "-") len++;
                Whistle(d, rate, i * eighth, N(mel[i]), len * eighth * 0.95f, 0.13f, rng);
            }

            // shaker on every eighth, accented off-beats
            float hp = 0f, prev = 0f;
            for (int e = 0; e < bars * 8; e++)
            {
                int o = Mathf.RoundToInt(e * eighth * rate);
                float g = e % 2 == 1 ? 0.07f : 0.04f;
                for (int i = 0; i < rate * 0.06f && o + i < d.Length; i++)
                {
                    float t = (float)i / rate;
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    hp = 0.7f * (hp + noise - prev);
                    prev = noise;
                    d[o + i] += hp * g * Mathf.Exp(-t * 60f) * Mathf.Clamp01(t / 0.004f);
                }
            }
            Normalize(d, 0.8f);
            return d;
        }

        static void Whistle(float[] d, int rate, float start, float f, float dur, float amp, System.Random rng)
        {
            int o = Mathf.RoundToInt(start * rate);
            int len = Mathf.RoundToInt(dur * rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < len && o + i < d.Length; i++)
            {
                float t = (float)i / rate;
                float vib = 1f + Mathf.Sin(TwoPi * 5.2f * t) * 0.008f * Mathf.Clamp01(t / 0.15f);
                float glide = 1f - 0.02f * Mathf.Exp(-t * 40f);
                phase += TwoPi * f * vib * glide / rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp += Lp(f * 1.5f, rate) * (noise - lp);
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((dur - t) / 0.05f);
                d[o + i] += (Mathf.Sin(phase) + lp * 0.08f) * env * amp;
            }
        }

        /// <summary>20 s countryside ambience: soft wind and sparse bird chirps; seamless loop.</summary>
        public static float[] Ambience()
        {
            int rate = MusicRate;
            float total = 20f, xfade = 1f;
            float[] d = new float[Mathf.CeilToInt((total + xfade) * rate)];
            System.Random rng = new System.Random(55);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float gust = 0.5f + 0.5f * Mathf.Sin(t * 0.4f) * Mathf.Sin(t * 0.13f + 1f);
                lp += Lp(250f + gust * 350f, rate) * (noise - lp);
                lp2 += Lp(120f, rate) * (lp - lp2);
                d[i] = (lp - lp2 * 0.5f) * (0.18f + gust * 0.22f);
            }
            float tt = 0.3f;
            while (tt < total)
            {
                int chirps = 1 + rng.Next(4);
                float f0 = 2800f + (float)rng.NextDouble() * 2500f;
                for (int c = 0; c < chirps; c++)
                {
                    float st = tt + c * (0.09f + (float)rng.NextDouble() * 0.05f);
                    float dur = 0.05f + (float)rng.NextDouble() * 0.07f;
                    float up = (float)rng.NextDouble() > 0.5f ? 1f : -1f;
                    int o = Mathf.RoundToInt(st * rate);
                    float phase = 0f;
                    for (int i = 0; i < dur * rate && o + i < d.Length; i++)
                    {
                        float t = (float)i / rate, u = t / dur;
                        float f = f0 * (1f + up * 0.35f * u) + Mathf.Sin(u * 30f) * 200f;
                        phase += TwoPi * f / rate;
                        d[o + i] += Mathf.Sin(phase) * Mathf.Sin(u * Mathf.PI) * 0.09f;
                    }
                }
                tt += 0.6f + (float)rng.NextDouble() * 2.4f;
            }
            // crossfade the extra tail into the head so the loop is seamless
            int L = Mathf.CeilToInt(total * rate), X = Mathf.CeilToInt(xfade * rate);
            float[] o2 = new float[L];
            for (int i = 0; i < L; i++) o2[i] = d[i];
            for (int i = 0; i < X && L + i < d.Length; i++)
            {
                float k = (float)i / X;
                o2[i] = d[i] * k + d[L + i] * (1f - k);
            }
            Normalize(o2, 0.5f);
            return o2;
        }
    }
}
