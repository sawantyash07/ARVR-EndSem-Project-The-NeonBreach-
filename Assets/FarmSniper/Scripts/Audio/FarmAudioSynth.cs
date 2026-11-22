using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.Farm
{
    public enum FarmSound
    {
        RifleShot, BoltOpen, BoltClose, MagOut, MagIn, DryFire, ScopeZoom, HitConfirm, KillConfirm,
        RatSqueak, RatScurry, StepGrass, StepDirt, StepWood, Jump, Land,
        ImpactDirt, ImpactWood, ImpactMetal, ImpactWater, ImpactHay,
        Bird1, Bird2, Bird3, Crow, Cow, WindLoop, UiClick, UiSelect, Victory, Fail, Tick, AmmoPickup, Breath
    }

    /// <summary>
    /// Generates every farm sound mathematically (same approach as the project's AudioSynth, separate ids so the
    /// shooter's sound table is untouched).
    /// </summary>
    public static class FarmAudioSynth
    {
        const int SR = 44100;
        static readonly Dictionary<FarmSound, AudioClip> cache = new Dictionary<FarmSound, AudioClip>();
        static System.Random rng = new System.Random(4242);
        static float lp1, lp2;

        static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
        static float Low(float k) { lp1 += (Noise() - lp1) * k; return lp1 * 3f; }
        static float Low2(float k) { lp2 += (Low(k) / 3f - lp2) * k; return lp2 * 5f; }
        static float Sine(float t, float f) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        static float Env(float t, float k) { return t < 0f ? 0f : Mathf.Exp(-t * k); }
        static float Atk(float t, float a) { return Mathf.Clamp01(t / a); }
        static float Sweep(float t, float f0, float k) { return Mathf.Sin(2f * Mathf.PI * f0 * (1f - Mathf.Exp(-k * t)) / k); }

        static AudioClip Make(string name, float dur, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(dur * SR);
            float[] data = new float[n];
            int fade = Mathf.Min(n / 2, (int)(0.01f * SR));
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
                if (i > n - fade) s *= (n - i) / (float)fade;
                data[i] = s;
            }
            AudioClip c = AudioClip.Create("farm_" + name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip MakeLoop(string name, float dur, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(dur * SR);
            int xf = (int)(0.5f * SR);
            float[] raw = new float[n + xf];
            for (int i = 0; i < raw.Length; i++) raw[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = raw[i];
                if (i < xf) { float k = i / (float)xf; s = raw[i] * k + raw[n + i] * (1f - k); }
                data[i] = s;
            }
            AudioClip c = AudioClip.Create("farm_" + name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        public static AudioClip Get(FarmSound id)
        {
            AudioClip c;
            if (cache.TryGetValue(id, out c) && c != null) return c;
            lp1 = lp2 = 0f;
            c = Create(id);
            cache[id] = c;
            return c;
        }

        /// <summary>Shot body: supersonic crack + muzzle boom.</summary>
        static float ShotCore(float t)
        {
            if (t < 0f) return 0f;
            float crack = Noise() * Env(t, 160f) * 1.0f;
            float boom = (Low(0.09f) * 0.8f + Sweep(t, 110f, 7f) * 0.9f) * Env(t, 7f) * Atk(t, 0.002f);
            float body = Low2(0.2f) * Env(t, 18f) * 0.6f;
            return crack + boom + body;
        }

        static float Bird(float t, float baseF, float rate, int notes, float noteLen)
        {
            int k = (int)(t / noteLen);
            if (k >= notes) return 0f;
            float lt = t - k * noteLen;
            float f = baseF * (1f + 0.25f * Mathf.Sin(lt * rate) + 0.12f * k % 3);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(lt / (noteLen * 0.8f)));
            return Mathf.Sin(2f * Mathf.PI * f * lt + 3f * Mathf.Sin(2f * Mathf.PI * 40f * lt)) * env * 0.35f;
        }

        static AudioClip Create(FarmSound id)
        {
            switch (id)
            {
                case FarmSound.RifleShot:
                    return Make("rifleShot", 2.6f, t =>
                    {
                        float s = ShotCore(t);
                        // valley echoes from the hills: delayed, darker, quieter copies
                        float e = 0f;
                        if (t > 0.38f) e += Low(0.05f) * Env(t - 0.38f, 5f) * 0.35f;
                        if (t > 0.85f) e += Low(0.04f) * Env(t - 0.85f, 4f) * 0.22f;
                        if (t > 1.45f) e += Low(0.03f) * Env(t - 1.45f, 3.5f) * 0.12f;
                        float tail = Low(0.06f) * Env(t, 2.2f) * 0.18f;
                        return (s + e + tail) * 0.85f;
                    });
                case FarmSound.BoltOpen:
                    return Make("boltOpen", 0.32f, t =>
                    {
                        float a = (Noise() * Env(t, 120f) + Sine(t, 2300f) * Env(t, 60f) * 0.6f) * 0.5f;
                        float t2 = t - 0.14f;
                        if (t2 > 0f) a += (Noise() * Env(t2, 90f) * 0.6f + Sine(t2, 1500f) * Env(t2, 50f) * 0.5f) * 0.5f;
                        return a;
                    });
                case FarmSound.BoltClose:
                    return Make("boltClose", 0.3f, t =>
                    {
                        float a = (Noise() * Env(t, 100f) * 0.6f + Sine(t, 1700f) * Env(t, 45f) * 0.5f) * 0.55f;
                        float t2 = t - 0.12f;
                        if (t2 > 0f) a += (Noise() * Env(t2, 140f) * 0.7f + Sine(t2, 2900f) * Env(t2, 70f) * 0.4f) * 0.6f;
                        return a;
                    });
                case FarmSound.MagOut:
                    return Make("magOut", 0.25f, t => (Noise() * Env(t, 70f) * 0.5f + Sine(t, 900f) * Env(t, 40f) * 0.4f + Sine(t, 180f) * Env(t, 30f) * 0.3f) * 0.6f);
                case FarmSound.MagIn:
                    return Make("magIn", 0.3f, t =>
                    {
                        float a = Noise() * Env(t, 60f) * 0.4f + Sine(t, 600f) * Env(t, 35f) * 0.3f;
                        float t2 = t - 0.08f;
                        if (t2 > 0f) a += Noise() * Env(t2, 150f) * 0.7f + Sine(t2, 2100f) * Env(t2, 60f) * 0.4f;
                        return a * 0.6f;
                    });
                case FarmSound.DryFire:
                    return Make("dry", 0.1f, t => (Noise() * Env(t, 160f) * 0.5f + Sine(t, 3100f) * Env(t, 110f) * 0.4f) * 0.6f);
                case FarmSound.ScopeZoom:
                    return Make("zoom", 0.16f, t =>
                    {
                        float a = 0f;
                        for (int k = 0; k < 3; k++) { float tk = t - k * 0.045f; if (tk > 0f) a += Sine(tk, 4200f) * Env(tk, 220f) * 0.35f + Noise() * Env(tk, 300f) * 0.2f; }
                        return a;
                    });
                case FarmSound.HitConfirm:
                    return Make("hit", 0.18f, t => Low(0.3f) * Env(t, 45f) * 0.45f + Sine(t, 1850f) * Env(t, 40f) * 0.18f);
                case FarmSound.KillConfirm:
                    return Make("kill", 0.5f, t =>
                    {
                        float a = Sine(t, 1046.5f) * Env(t, 9f) * 0.22f;
                        if (t > 0.08f) a += Sine(t, 1568f) * Env(t - 0.08f, 8f) * 0.2f;
                        return a;
                    });
                case FarmSound.RatSqueak:
                    return Make("squeak", 0.32f, t =>
                    {
                        float f = 3400f + 900f * Mathf.Sin(t * 60f) + 1400f * Mathf.Sin(t * 9f);
                        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
                        return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.3f;
                    });
                case FarmSound.RatScurry:
                    return Make("scurry", 0.5f, t => Low(0.5f) * (0.5f + 0.5f * Mathf.Abs(Sine(t, 18f))) * Mathf.Sin(Mathf.PI * t / 0.5f) * 0.25f);
                case FarmSound.StepGrass:
                    return Make("stepGrass", 0.16f, t => (Low(0.45f) * 0.4f + Noise() * 0.15f) * Env(t, 30f) * Atk(t, 0.01f));
                case FarmSound.StepDirt:
                    return Make("stepDirt", 0.14f, t => (Low(0.3f) * 0.5f + Sine(t, 85f) * 0.3f) * Env(t, 40f));
                case FarmSound.StepWood:
                    return Make("stepWood", 0.18f, t => (Low(0.25f) * 0.3f + Sine(t, 190f) * 0.45f + Sine(t, 420f) * 0.15f) * Env(t, 32f));
                case FarmSound.Jump:
                    return Make("jump", 0.15f, t => Low(0.3f) * Env(t, 25f) * 0.35f);
                case FarmSound.Land:
                    return Make("land", 0.22f, t => (Low(0.25f) * 0.4f + Sine(t, 70f) * 0.6f) * Env(t, 25f));
                case FarmSound.ImpactDirt:
                    return Make("impDirt", 0.35f, t => (Low(0.2f) * 0.6f + Noise() * Env(t, 60f) * 0.3f) * Env(t, 14f));
                case FarmSound.ImpactWood:
                    return Make("impWood", 0.3f, t => (Sine(t, 380f) * 0.5f + Sine(t, 820f) * 0.2f + Noise() * Env(t, 80f) * 0.5f) * Env(t, 22f));
                case FarmSound.ImpactMetal:
                    return Make("impMetal", 0.8f, t => (Sine(t, 2650f) * 0.35f + Sine(t, 3920f) * 0.2f + Sine(t, 1210f) * 0.2f) * Env(t, 6f) + Noise() * Env(t, 90f) * 0.4f);
                case FarmSound.ImpactWater:
                    return Make("impWater", 0.6f, t => Low(0.35f) * Env(t, 7f) * 0.5f + Sweep(t, 900f, -3f) * Env(t, 18f) * 0.15f);
                case FarmSound.ImpactHay:
                    return Make("impHay", 0.3f, t => Low(0.5f) * Env(t, 16f) * 0.45f);
                case FarmSound.Bird1:
                    return Make("bird1", 0.9f, t => Bird(t, 3600f, 70f, 5, 0.16f));
                case FarmSound.Bird2:
                    return Make("bird2", 1.3f, t =>
                    {
                        float f = 2600f + 1800f * Mathf.Abs(Mathf.Sin(t * 14f));
                        float env = Mathf.Clamp01(Mathf.Sin(t * 30f)) * Env(t, 1.6f);
                        return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.3f;
                    });
                case FarmSound.Bird3:
                    return Make("bird3", 0.7f, t =>
                    {
                        float a = 0f;
                        float t2 = t - 0.3f;
                        a += Sweep(t, 4800f, 4f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.22f)) * 0.3f;
                        if (t2 > 0f) a += Sweep(t2, 4200f, -2f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t2 / 0.3f)) * 0.3f;
                        return a;
                    });
                case FarmSound.Crow:
                    return Make("crow", 1.2f, t =>
                    {
                        float a = 0f;
                        for (int k = 0; k < 2; k++)
                        {
                            float tk = t - k * 0.5f;
                            if (tk < 0f || tk > 0.38f) continue;
                            float saw = 2f * ((640f * tk + 30f * Mathf.Sin(tk * 40f)) % 1f) - 1f;
                            a += (saw * 0.35f + Noise() * 0.25f) * Mathf.Sin(Mathf.PI * tk / 0.38f);
                        }
                        return a * 0.5f;
                    });
                case FarmSound.Cow:
                    return Make("cow", 1.9f, t =>
                    {
                        float f = 120f + 40f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.7f));
                        float saw = 2f * ((f * t) % 1f) - 1f;
                        float formant = Mathf.Sin(2f * Mathf.PI * (380f + 260f * Mathf.Clamp01(t / 0.8f)) * t);
                        float env = Atk(t, 0.15f) * Env(Mathf.Max(0f, t - 1.2f), 4f);
                        return (saw * 0.5f + saw * formant * 0.35f) * env * 0.35f;
                    });
                case FarmSound.WindLoop:
                    return MakeLoop("wind", 9f, t =>
                    {
                        float gust = 0.55f + 0.3f * Mathf.Sin(t * 0.7f) + 0.15f * Mathf.Sin(t * 1.9f + 1f);
                        return Low2(0.05f) * gust * 0.5f + Low(0.25f) * 0.03f * gust;
                    });
                case FarmSound.UiClick:
                    return Make("uiClick", 0.06f, t => Sine(t, 1800f) * Env(t, 80f) * 0.35f);
                case FarmSound.UiSelect:
                    return Make("uiSelect", 0.18f, t => (Sine(t, 880f) * 0.5f + Sine(t, 1320f) * 0.3f) * Env(t, 22f) * 0.45f);
                case FarmSound.Victory:
                    return Make("victory", 2.2f, t =>
                    {
                        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                        float a = 0f;
                        for (int k = 0; k < notes.Length; k++)
                        {
                            float tk = t - k * 0.16f;
                            if (tk <= 0f) continue;
                            a += (Sine(tk, notes[k]) + Sine(tk, notes[k] * 2f) * 0.3f + Sine(tk, notes[k] * 3f) * 0.1f) * Env(tk, k == 3 ? 1.6f : 4f) * Atk(tk, 0.01f);
                        }
                        return a * 0.22f;
                    });
                case FarmSound.Fail:
                    return Make("fail", 1.8f, t =>
                    {
                        float[] notes = { 392f, 349.23f, 311.13f, 261.63f };
                        float a = 0f;
                        for (int k = 0; k < notes.Length; k++)
                        {
                            float tk = t - k * 0.28f;
                            if (tk <= 0f) continue;
                            a += (Sine(tk, notes[k]) + Sine(tk, notes[k] * 0.5f) * 0.4f) * Env(tk, k == 3 ? 1.5f : 4f) * Atk(tk, 0.02f);
                        }
                        return a * 0.22f;
                    });
                case FarmSound.Tick:
                    return Make("tick", 0.08f, t => Sine(t, 1400f) * Env(t, 60f) * 0.35f);
                case FarmSound.AmmoPickup:
                    return Make("ammo", 0.5f, t =>
                    {
                        float a = 0f;
                        for (int k = 0; k < 4; k++) { float tk = t - k * 0.09f; if (tk > 0f) a += Sine(tk, 2400f + k * 300f) * Env(tk, 45f) * 0.25f + Noise() * Env(tk, 120f) * 0.2f; }
                        return a;
                    });
                case FarmSound.Breath:
                    return Make("breath", 1.0f, t => Low2(0.12f) * Mathf.Sin(Mathf.PI * t) * 0.25f);
            }
            return Make("silence", 0.05f, t => 0f);
        }
    }
}
