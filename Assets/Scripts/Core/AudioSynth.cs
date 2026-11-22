using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    public enum SfxId
    {
        Pistol, Rifle, Shotgun, Reload, ReloadEnd, Dry, Hit, Headshot, EnemyDie, Explosion, Pickup, Hurt,
        Jump, Land, Step, WaveStart, Dash, Melee, Throw, Switch, EnemyShot, Click, Slam, Beep,
        DroneHum, TankHum, ServoLoop, SniperCharge, SniperShot, ShieldBlock, PowerUp, Buy, Deny,
        // mini-game modes (appended so existing ids keep their values)
        BowDraw, BowRelease, ArrowThud, TargetHit, Bullseye, RingPass, Coin, ChuteOpen, WindLoop, Splash, Bonk, PlaneLoop,
        // skydive (appended)
        CanopyFlutter, AltimeterBeep, Ripcord, CanopyRustle
    }

    /// <summary>
    /// Generates every sound effect and the music loop mathematically at runtime (no audio files needed).
    /// </summary>
    public static class AudioSynth
    {
        const int SR = 44100;
        static readonly Dictionary<SfxId, AudioClip> cache = new Dictionary<SfxId, AudioClip>();
        static System.Random rng = new System.Random(1337);
        static float lp;
        static AudioClip music;

        static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
        static float Low(float k) { lp += (Noise() - lp) * k; return lp * 3.2f; }
        static float Sine(float t, float f) { return Mathf.Sin(2f * Mathf.PI * f * t); }
        static float Env(float t, float k) { return Mathf.Exp(-t * k); }

        // Exponential frequency sweep: f(t) = f0 * exp(-k t). Negative k sweeps upwards.
        static float Sweep(float t, float f0, float k)
        {
            return Mathf.Sin(2f * Mathf.PI * f0 * (1f - Mathf.Exp(-k * t)) / k);
        }

        static AudioClip Make(string name, float dur, System.Func<float, float> f, bool loop = false)
        {
            int n = Mathf.CeilToInt(dur * SR);
            float[] data = new float[n];
            int fade = Mathf.Min(n / 2, (int)(0.004f * SR));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float s = Mathf.Clamp(f(t), -1f, 1f);
                if (!loop && i > n - fade) s *= (n - i) / (float)fade;
                data[i] = s;
            }
            AudioClip c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>Seamless loop for noisy sounds: renders a little extra and cross-fades the tail into the head.</summary>
        static AudioClip MakeLoop(string name, float dur, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(dur * SR);
            int xf = (int)(0.25f * SR);
            float[] raw = new float[n + xf];
            for (int i = 0; i < raw.Length; i++) raw[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = raw[i];
                if (i < xf)
                {
                    float k = i / (float)xf;
                    s = raw[i] * k + raw[n + i] * (1f - k);
                }
                data[i] = s;
            }
            AudioClip c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        public static AudioClip Get(SfxId id)
        {
            AudioClip c;
            if (cache.TryGetValue(id, out c) && c != null) return c;
            c = Create(id);
            cache[id] = c;
            return c;
        }

        static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.Pistol:
                    return Make("pistol", 0.28f, t => (Noise() * 0.6f * Env(t, 35f) + Sweep(t, 320f, 25f) * 0.8f * Env(t, 16f)) * 0.9f);
                case SfxId.Rifle:
                    return Make("rifle", 0.2f, t => (Noise() * 0.6f * Env(t, 45f) + Sweep(t, 210f, 30f) * 0.9f * Env(t, 22f)) * 0.9f);
                case SfxId.Shotgun:
                    return Make("shotgun", 0.6f, t => Noise() * 0.8f * Env(t, 9f) + Sweep(t, 120f, 10f) * Env(t, 7f));
                case SfxId.Reload:
                    return Make("reload", 0.35f, t =>
                    {
                        float a = Noise() * Env(t, 80f) * 0.5f + Sine(t, 900f) * Env(t, 60f) * 0.3f;
                        float t2 = t - 0.2f;
                        if (t2 > 0f) a += Noise() * Env(t2, 90f) * 0.5f + Sine(t2, 500f) * Env(t2, 50f) * 0.4f;
                        return a;
                    });
                case SfxId.ReloadEnd:
                    return Make("reloadEnd", 0.2f, t => Noise() * Env(t, 60f) * 0.5f + Sine(t, 420f) * Env(t, 45f) * 0.5f);
                case SfxId.Dry:
                    return Make("dry", 0.08f, t => Noise() * Env(t, 90f) * 0.4f + Sine(t, 1200f) * Env(t, 90f) * 0.3f);
                case SfxId.Hit:
                    return Make("hit", 0.07f, t => Sine(t, 1800f) * Env(t, 70f) * 0.5f);
                case SfxId.Headshot:
                    return Make("headshot", 0.14f, t => Sine(t, 2600f) * Env(t, 35f) * 0.5f + Sine(t, 1300f) * Env(t, 35f) * 0.3f);
                case SfxId.EnemyDie:
                    return Make("enemyDie", 0.5f, t => Noise() * Env(t, 10f) * 0.5f + Sweep(t, 420f, 5f) * Env(t, 6f) * 0.6f);
                case SfxId.Explosion:
                    return Make("explosion", 1.3f, t => Low(0.08f) * Env(t, 3f) * 0.95f + Sweep(t, 75f, 2f) * Env(t, 3f));
                case SfxId.Pickup:
                    return Make("pickup", 0.3f, t =>
                    {
                        float f = t < 0.1f ? 660f : (t < 0.2f ? 880f : 1320f);
                        return Sine(t, f) * Env(t % 0.1f, 15f) * 0.5f;
                    });
                case SfxId.Hurt:
                    return Make("hurt", 0.3f, t => Sweep(t, 160f, 8f) * Env(t, 9f) * 0.8f + Noise() * Env(t, 25f) * 0.3f);
                case SfxId.Jump:
                    return Make("jump", 0.15f, t => Low(0.2f) * Env(t, 25f) * 0.5f + Sweep(t, 180f, -4f) * Env(t, 20f) * 0.3f);
                case SfxId.Land:
                    return Make("land", 0.18f, t => Noise() * Env(t, 40f) * 0.35f + Sine(t, 70f) * Env(t, 30f) * 0.7f);
                case SfxId.Step:
                    return Make("step", 0.1f, t => Low(0.25f) * Env(t, 50f) * 0.6f + Sine(t, 90f) * Env(t, 40f) * 0.4f);
                case SfxId.WaveStart:
                    return Make("wave", 1.1f, t =>
                    {
                        float env = Mathf.Min(t / 0.05f, 1f) * Env(t, 2.5f);
                        return (Sine(t, 220f) + Sine(t, 330f) * 0.7f + Sine(t, 440f) * 0.5f) * env * 0.35f;
                    });
                case SfxId.Dash:
                    return Make("dash", 0.3f, t => Low(0.12f) * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.7f);
                case SfxId.Melee:
                    return Make("melee", 0.25f, t => Low(0.15f) * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.6f + Sine(t, 120f) * Env(t, 20f) * 0.5f);
                case SfxId.Throw:
                    return Make("throw", 0.2f, t => Low(0.18f) * Mathf.Sin(Mathf.PI * t / 0.2f) * 0.5f);
                case SfxId.Switch:
                    return Make("switch", 0.15f, t => Noise() * Env(t, 70f) * 0.4f + Sine(t, 700f) * Env(t, 60f) * 0.3f);
                case SfxId.EnemyShot:
                    return Make("enemyShot", 0.3f, t => Sweep(t, 1400f, 9f) * Env(t, 12f) * 0.5f + Sine(t, 300f) * Env(t, 25f) * 0.3f);
                case SfxId.Click:
                    return Make("click", 0.05f, t => Sine(t, 1500f) * Env(t, 90f) * 0.4f);
                case SfxId.Slam:
                    return Make("slam", 0.9f, t => Sweep(t, 55f, 3f) * Env(t, 4f) + Low(0.1f) * Env(t, 5f) * 0.6f);
                // ---- seamless loops (frequencies chosen so a whole number of cycles fits in the clip)
                case SfxId.DroneHum:
                    return Make("droneHum", 1f, t =>
                    {
                        float saw = 2f * ((110f * t) % 1f) - 1f;
                        float lfo = 0.75f + 0.25f * Sine(t, 6f);
                        return (saw * 0.25f + Sine(t, 220f) * 0.25f + Low(0.3f) * 0.08f) * lfo;
                    }, true);
                case SfxId.TankHum:
                    return Make("tankHum", 1f, t =>
                    {
                        float sq = ((45f * t) % 1f) < 0.5f ? 1f : -1f;
                        return sq * 0.18f + Sine(t, 90f) * 0.3f * (0.7f + 0.3f * Sine(t, 3f)) + Low(0.05f) * 0.15f;
                    }, true);
                case SfxId.ServoLoop:
                    return Make("servo", 1f, t => (Sine(t, 160f) * 0.15f + Low(0.4f) * 0.06f) * (0.5f + 0.5f * Mathf.Abs(Sine(t, 2f))), true);
                case SfxId.SniperCharge:
                    return Make("sniperCharge", 1.2f, t => Sweep(t, 300f, -1.4f) * Mathf.Min(t / 0.1f, 1f) * 0.35f);
                case SfxId.SniperShot:
                    return Make("sniperShot", 0.6f, t => Noise() * Env(t, 14f) * 0.7f + Sweep(t, 2400f, 14f) * Env(t, 8f) * 0.6f);
                case SfxId.ShieldBlock:
                    return Make("shieldBlock", 0.18f, t => Sine(t, 620f) * Env(t, 25f) * 0.5f + Sine(t, 1240f) * Env(t, 40f) * 0.3f);
                case SfxId.PowerUp:
                    return Make("powerUp", 0.7f, t => Sweep(t, 300f, -3f) * Env(t, 3f) * 0.45f + Sine(t, 900f) * Env(t, 6f) * 0.2f);
                case SfxId.Buy:
                    return Make("buy", 0.25f, t => Sine(t, t < 0.08f ? 990f : 1480f) * Env(t % 0.08f, 20f) * 0.45f);
                case SfxId.Deny:
                    return Make("deny", 0.25f, t => (((140f * t) % 1f) < 0.5f ? 1f : -1f) * Env(t, 8f) * 0.25f);
                // ---- archery
                case SfxId.BowDraw:
                    return Make("bowDraw", 0.9f, t => (Low(0.06f) * 0.25f + Sweep(t, 70f, -0.9f) * 0.08f) * Mathf.Clamp01(t / 0.6f) * (0.7f + 0.3f * Sine(t, 23f)));
                case SfxId.BowRelease:
                    return Make("bowRelease", 0.5f, t =>
                    {
                        float pitch = 1f + Env(t, 30f) * 0.6f;
                        float s = Sine(t, 98f * pitch) * 0.6f + Sine(t, 196f * pitch) * 0.3f + Sine(t, 294f) * 0.12f;
                        return s * Env(t, 9f) * 0.7f + Low(0.3f) * Env(t, 45f) * 0.5f;
                    });
                case SfxId.ArrowThud:
                    return Make("arrowThud", 0.28f, t => Low(0.25f) * Env(t, 32f) * 0.7f + Sine(t, 150f) * Env(t, 26f) * 0.6f + Sine(t, 420f) * Env(t, 60f) * 0.2f);
                case SfxId.TargetHit:
                    return Make("targetHit", 0.4f, t => (Sine(t, 1320f) * 0.5f + Sine(t, 1980f) * 0.3f) * Env(t, 10f) * 0.45f);
                case SfxId.Bullseye:
                    return Make("bullseye", 1.0f, t =>
                    {
                        float a = Sine(t, 880f) * Env(t, 5f);
                        if (t > 0.1f) a += Sine(t, 1320f) * Env(t - 0.1f, 5f);
                        if (t > 0.2f) a += Sine(t, 1760f) * Env(t - 0.2f, 4f);
                        return a * 0.3f;
                    });
                // ---- parachute
                case SfxId.RingPass:
                    return Make("ringPass", 0.55f, t => Sweep(t, 520f, -2.2f) * Env(t, 6f) * 0.35f + Sine(t, 1560f) * Env(t, 9f) * 0.2f);
                case SfxId.Coin:
                    return Make("coin", 0.28f, t => Sine(t, t < 0.07f ? 1568f : 2093f) * Env(t < 0.07f ? t : t - 0.07f, 14f) * 0.4f);
                case SfxId.ChuteOpen:
                    return Make("chuteOpen", 0.9f, t => Low(0.25f) * Mathf.Clamp01(t / 0.08f) * Env(t, 4.5f) * (0.6f + 0.4f * Sine(t, 17f)) * 0.9f);
                case SfxId.WindLoop:
                    return MakeLoop("windLoop", 2f, t => Low(0.035f) * (0.75f + 0.25f * Mathf.PerlinNoise(t * 1.7f, 0.5f)) * 0.8f);
                case SfxId.Splash:
                    return Make("splash", 1.1f, t => (Noise() * 0.45f + Low(0.15f) * 0.5f) * Mathf.Clamp01(t / 0.02f) * Env(t, 4f));
                case SfxId.Bonk:
                    return Make("bonk", 0.32f, t => Sweep(t, 320f, 12f) * Env(t, 13f) * 0.7f + Noise() * Env(t, 50f) * 0.3f);
                case SfxId.PlaneLoop:
                    return Make("planeLoop", 1f, t =>
                    {
                        float saw = 2f * ((60f * t) % 1f) - 1f;
                        return saw * 0.2f + Sine(t, 120f) * 0.15f * (0.8f + 0.2f * Sine(t, 4f)) + Low(0.08f) * 0.12f;
                    }, true);
                // ---- skydive
                case SfxId.CanopyFlutter:
                    // fabric flapping in the airflow: band-limited noise, amplitude-modulated at a wobbling rate
                    return MakeLoop("canopyFlutter", 2f, t =>
                    {
                        float rate = 7f + 2.5f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                        float flap = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * rate * t * 0.5f));
                        return Low(0.12f) * flap * 0.55f;
                    });
                case SfxId.AltimeterBeep:
                    // audible altimeter: a short, piercing two-tone chirp
                    return Make("altimeterBeep", 0.14f, t => (Sine(t, 2350f) * 0.7f + Sine(t, 3520f) * 0.3f) * Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((0.14f - t) / 0.02f) * 0.35f);
                case SfxId.Ripcord:
                    // the handle clicks out, then the pilot chute whooshes away
                    return Make("ripcord", 0.45f, t => Noise() * Env(t, 70f) * 0.5f + Sine(t, 1200f) * Env(t, 90f) * 0.25f
                                                     + Low(0.3f) * Mathf.Clamp01((t - 0.05f) / 0.05f) * Env(Mathf.Max(0f, t - 0.05f), 7f) * 0.35f);
                case SfxId.CanopyRustle:
                    // the canopy collapsing onto the ground after landing
                    return Make("canopyRustle", 1.3f, t => Low(0.22f) * Mathf.Clamp01(t / 0.05f) * Env(t, 2.4f) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 11f * t)) * 0.6f);
                default:
                    return Make("beep", 0.2f, t => Sine(t, 880f) * (t < 0.1f ? 1f : 0f) * 0.4f);
            }
        }

        /// <summary>A short dark-synthwave loop: kick, hats, saw bass and a square arp (8 seconds, seamless).</summary>
        public static AudioClip Music()
        {
            if (music != null) return music;
            const float step = 0.25f;
            const int steps = 32;
            int[] bass = { 0, 0, 12, 0, 3, 3, 15, 3, -2, -2, 10, -2, -4, -4, 8, -4 };
            int[] arp = { 0, 7, 12, 7, 3, 10, 15, 10 };
            float dur = step * steps;
            music = Make("music", dur, t =>
            {
                int s = Mathf.Min(steps - 1, (int)(t / step));
                float local = t - s * step;
                float gate = Mathf.Min(local / 0.004f, 1f) * Mathf.Clamp01((step - local) / 0.012f);

                float f = 55f * Mathf.Pow(2f, bass[s % 16] / 12f);
                float ph = (f * local) % 1f;
                float saw = 2f * ph - 1f;
                float b = saw * Env(local, 5f) * gate * 0.28f;

                float kick = 0f;
                if (s % 4 == 0) kick = Sweep(local, 130f, 22f) * Env(local, 11f) * 0.55f;

                float hat = 0f;
                if (s % 2 == 1) hat = Noise() * Env(local, 45f) * 0.07f;

                float fa = 220f * Mathf.Pow(2f, (bass[s % 16] + arp[s % 8]) / 12f);
                float sq = ((fa * local) % 1f) < 0.5f ? 1f : -1f;
                float a = sq * Env(local, 9f) * gate * 0.05f;

                return (b + kick + hat + a) * 0.8f;
            });
            return music;
        }
    }
}
