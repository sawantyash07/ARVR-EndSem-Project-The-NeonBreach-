using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Procedural sound for the cinematic intro (no audio files, like the rest of the project): a slow cinematic music
    /// bed that follows the intro timeline, theatre ambience (room tone + audience murmur), the curtain sweep and the
    /// title-reveal hit with a neon-tube ignition buzz. Everything is rendered to interleaved stereo float arrays on a
    /// worker thread (pure math, no Unity objects) and turned into AudioClips on the main thread with <see cref="ToClip"/>.
    /// </summary>
    public static class IntroAudio
    {
        public const int SR = 44100;
        /// <summary>Length of the riser in the reveal clip: the hit lands exactly this many seconds after the clip starts.</summary>
        public const float RevealLead = 1.6f;

        const float TwoPi = 6.2831853f;
        const double TwoPiD = 6.283185307179586;

        /// <summary>Timeline marks (seconds from the start of the intro) the generated sounds are composed against.</summary>
        public sealed class Request
        {
            public float curtainStart, openDuration, hit, revealEnd, outro, end;
            /// <summary>The curtain AnimationCurve sampled on the main thread (AnimationCurve is not used off the main thread).</summary>
            public float[] openCurve;
            public bool music = true, ambience = true, curtain = true, reveal = true;
            public int seed = 7;
        }

        public sealed class Bank { public float[] music, ambience, curtain, reveal; }

        // ------------------------------------------------------------------ neon ignition pattern
        // Shared by the title flicker (CinematicIntro) and the buzz in the reveal sound, so picture and sound match.
        static readonly float[] FlickT = { 0f, 0.05f, 0.09f, 0.17f, 0.22f, 0.34f, 0.40f };
        static readonly float[] FlickL = { 0.85f, 0.1f, 1f, 0.25f, 0.9f, 0.5f, 1f };
        public const float FlickerLength = 0.40f;

        /// <summary>Brightness (0..1) of the neon title <paramref name="tau"/> seconds after it ignites.</summary>
        public static float Flicker(float tau)
        {
            if (tau < 0f) return 0f;
            for (int i = FlickT.Length - 1; i >= 0; i--) if (tau >= FlickT[i]) return FlickL[i];
            return 0f;
        }

        public static Bank Render(Request q)
        {
            Bank b = new Bank();
            if (q.music) b.music = Music(q, q.seed);
            if (q.ambience) b.ambience = Ambience(q.seed + 1);
            if (q.curtain) b.curtain = Curtain(q, q.seed + 2);
            if (q.reveal) b.reveal = Reveal(q.seed + 3);
            return b;
        }

        /// <summary>Main thread only: wrap rendered stereo samples in an AudioClip.</summary>
        public static AudioClip ToClip(string name, float[] interleaved)
        {
            if (interleaved == null || interleaved.Length < 4) return null;
            AudioClip c = AudioClip.Create(name, interleaved.Length / 2, 2, SR, false);
            c.SetData(interleaved, 0);
            return c;
        }

        // ================================================================== DSP building blocks

        sealed class Rng
        {
            readonly System.Random r;
            public Rng(int seed) { r = new System.Random(seed); }
            public float N() { return (float)(r.NextDouble() * 2.0 - 1.0); }
            public float U() { return (float)r.NextDouble(); }
            public float Range(float a, float b) { return a + (b - a) * (float)r.NextDouble(); }
        }

        /// <summary>Chamberlin state-variable filter (cutoff can change every sample, used for sweeps).</summary>
        struct Svf
        {
            float low, band;

            void Step(float x, float fc, float damp)
            {
                float f = TwoPi * Mathf.Min(fc, 7000f) / SR;
                low += f * band;
                float high = x - low - damp * band;
                band += f * high;
            }

            public float Low(float x, float fc, float damp) { Step(x, fc, damp); return low; }
            public float Band(float x, float fc, float damp) { Step(x, fc, damp); return band; }
        }

        struct OnePole
        {
            float z;
            public float Lp(float x, float a) { z += a * (x - z); return z; }
        }

        static float Coef(float fc) { return 1f - Mathf.Exp(-TwoPi * fc / SR); }

        /// <summary>Band-limited (PolyBLEP) sawtooth, phase in 0..1.</summary>
        static float Saw(ref double ph, double inc)
        {
            ph += inc;
            if (ph >= 1.0) ph -= 1.0;
            float t = (float)ph, dt = (float)inc;
            float y = 2f * t - 1f;
            if (t < dt) { float x = t / dt; y -= x + x - x * x - 1f; }
            else if (t > 1f - dt) { float x = (t - 1f) / dt; y -= x * x + x + x + 1f; }
            return y;
        }

        /// <summary>Small Schroeder / Freeverb-style reverb: parallel damped combs into series all-passes.</summary>
        sealed class Reverb
        {
            static readonly int[] CombLen = { 1116, 1188, 1277, 1356, 1422, 1491 };
            static readonly int[] ApLen = { 556, 441, 341 };
            readonly float[][] comb, ap;
            readonly int[] ci, ai;
            readonly float[] store;
            readonly float feedback, damp;

            public Reverb(int spread, float room, float damping)
            {
                feedback = room;
                damp = damping;
                comb = new float[CombLen.Length][];
                ci = new int[CombLen.Length];
                store = new float[CombLen.Length];
                for (int i = 0; i < CombLen.Length; i++) comb[i] = new float[CombLen[i] + spread];
                ap = new float[ApLen.Length][];
                ai = new int[ApLen.Length];
                for (int i = 0; i < ApLen.Length; i++) ap[i] = new float[ApLen[i] + spread];
            }

            public float Process(float x)
            {
                float o = 0f;
                for (int c = 0; c < comb.Length; c++)
                {
                    float[] buf = comb[c];
                    int i = ci[c];
                    float y = buf[i];
                    store[c] = y * (1f - damp) + store[c] * damp;
                    buf[i] = x + store[c] * feedback;
                    ci[c] = i + 1 >= buf.Length ? 0 : i + 1;
                    o += y;
                }
                for (int a = 0; a < ap.Length; a++)
                {
                    float[] buf = ap[a];
                    int i = ai[a];
                    float bo = buf[i];
                    float y = bo - o;
                    buf[i] = o + bo * 0.5f;
                    ai[a] = i + 1 >= buf.Length ? 0 : i + 1;
                    o = y;
                }
                return o;
            }
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) { float a = d[i] < 0f ? -d[i] : d[i]; if (a > m) m = a; }
            if (m < 1e-6f) return;
            float g = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }

        static void FadeEdges(float[] d, float inSec, float outSec)
        {
            int frames = d.Length / 2;
            int fi = Mathf.Min(frames, Mathf.CeilToInt(inSec * SR)), fo = Mathf.Min(frames, Mathf.CeilToInt(outSec * SR));
            for (int i = 0; i < fi; i++) { float g = i / (float)fi; d[2 * i] *= g; d[2 * i + 1] *= g; }
            for (int i = 0; i < fo; i++)
            {
                int k = frames - 1 - i;
                float g = i / (float)fo;
                d[2 * k] *= g; d[2 * k + 1] *= g;
            }
        }

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // ================================================================== music

        sealed class Chord
        {
            public readonly float start, end, attack, release, level;
            public readonly float[] f;
            public readonly double[] ph;
            public double subPh;

            public Chord(Rng rng, float start, float end, float attack, float release, float level, params float[] freqs)
            {
                this.start = start; this.end = end; this.attack = attack; this.release = release; this.level = level;
                f = freqs;
                ph = new double[freqs.Length * 3];
                for (int i = 0; i < ph.Length; i++) ph[i] = rng.U();
            }

            public float Env(float t)
            {
                if (t < start) return 0f;
                float a = attack > 0f ? Smooth01((t - start) / attack) : 1f;
                float r = t > end ? 1f - Smooth01((t - end) / release) : 1f;
                return a * r * level;
            }
        }

        struct Note { public float t, f, amp, pan; }

        static float MusicCutoff(float t, Request q)
        {
            if (t < q.curtainStart) return Mathf.Lerp(260f, 650f, t / Mathf.Max(0.1f, q.curtainStart));
            if (t < q.hit) return Mathf.Lerp(650f, 1500f, (t - q.curtainStart) / Mathf.Max(0.1f, q.hit - q.curtainStart));
            float fc = 1900f + 2600f * Mathf.Exp(-(t - q.hit) * 1.4f);
            if (t > q.outro) fc = Mathf.Lerp(fc, 700f, Mathf.Clamp01((t - q.outro) / Mathf.Max(0.2f, q.end - q.outro)));
            return fc;
        }

        /// <summary>
        /// D minor, i - VI - III - VII (Dm, Bb, F, C): a dark drone on the closed curtains, a swell while they open, a bright
        /// F major chord exactly on the title ignition with arpeggiated neon "bell" plucks, and a suspended C to fade out on.
        /// </summary>
        static float[] Music(Request q, int seed)
        {
            float len = q.end + 2.5f;
            int n = Mathf.CeilToInt(len * SR);
            float[] o = new float[n * 2];
            Rng rng = new Rng(seed);
            float hit = q.hit;
            float c3 = hit + Mathf.Max(1.8f, (q.outro - hit) * 0.55f);

            Chord[] chords =
            {
                new Chord(rng, 0f, q.curtainStart + 0.4f, 2.4f, 1.8f, 0.8f, 73.42f, 110.00f, 146.83f, 174.61f, 220.00f),             // Dm
                new Chord(rng, q.curtainStart - 0.4f, hit + 0.1f, 1.8f, 0.9f, 0.95f, 58.27f, 87.31f, 116.54f, 146.83f, 174.61f),   // Bb
                new Chord(rng, hit - 0.01f, c3 + 0.3f, 0.03f, 1.6f, 1.2f, 87.31f, 130.81f, 174.61f, 220.00f, 261.63f),             // F
                new Chord(rng, c3 - 0.3f, len, 1.4f, 1.8f, 1.0f, 65.41f, 98.00f, 130.81f, 164.81f, 196.00f),                       // C
            };

            // neon bell arpeggio after the hit
            float[] arpF = { 698.46f, 880.00f, 1046.50f, 880.00f, 698.46f, 523.25f, 698.46f, 1046.50f };
            float[] arpC = { 523.25f, 659.26f, 783.99f, 1046.50f, 783.99f, 659.26f };
            List<Note> bells = new List<Note>();
            int step = 0;
            for (float bt = hit + 0.12f; bt < q.outro + 0.4f; bt += 0.30f, step++)
            {
                float[] arr = bt >= c3 ? arpC : arpF;
                bells.Add(new Note { t = bt, f = arr[step % arr.Length], amp = step == 0 ? 1.3f : 0.85f, pan = step % 2 == 0 ? -0.45f : 0.45f });
            }

            // low tension pulses while the curtains open
            List<Note> pulses = new List<Note>();
            for (float pt = q.curtainStart + 0.15f; pt < hit - 0.25f; pt += 0.62f)
                pulses.Add(new Note { t = pt, amp = Mathf.Lerp(0.25f, 0.85f, (pt - q.curtainStart) / Mathf.Max(0.1f, hit - q.curtainStart)) });

            Svf fl = default(Svf), fr = default(Svf);
            Reverb rl = new Reverb(0, 0.86f, 0.35f), rr = new Reverb(23, 0.86f, 0.35f);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float l = 0f, r = 0f, sub = 0f;
                for (int c = 0; c < chords.Length; c++)
                {
                    Chord ch = chords[c];
                    float e = ch.Env(t);
                    if (e <= 0.0001f) continue;
                    float cl = 0f, cr = 0f;
                    for (int k = 0; k < ch.f.Length; k++)
                    {
                        double inc = ch.f[k] / (double)SR;
                        float a = Saw(ref ch.ph[k * 3], inc * 0.9965);
                        float b = Saw(ref ch.ph[k * 3 + 1], inc);
                        float d = Saw(ref ch.ph[k * 3 + 2], inc * 1.0035);
                        cl += a + 0.55f * b;
                        cr += d + 0.55f * b;
                    }
                    l += cl * e;
                    r += cr * e;
                    ch.subPh += ch.f[0] / (double)SR;
                    if (ch.subPh >= 1.0) ch.subPh -= 1.0;
                    sub += (float)System.Math.Sin(ch.subPh * TwoPiD) * e;
                }
                float fc = MusicCutoff(t, q);
                l = fl.Low(l * 0.07f, fc, 0.9f);
                r = fr.Low(r * 0.07f, fc, 0.9f);

                float bl = 0f, br = 0f;
                for (int b = 0; b < bells.Count; b++)
                {
                    float tau = t - bells[b].t;
                    if (tau < 0f) break;
                    if (tau > 2.2f) continue;
                    float env = (1f - Mathf.Exp(-tau * 300f)) * Mathf.Exp(-tau * 3.2f) * bells[b].amp;
                    double ph = TwoPiD * bells[b].f * tau;
                    float s = (float)(System.Math.Sin(ph) + 0.3 * System.Math.Sin(ph * 2.0) + 0.1 * System.Math.Sin(ph * 3.97)) * env;
                    bl += s * (1f - bells[b].pan) * 0.5f;
                    br += s * (1f + bells[b].pan) * 0.5f;
                }

                float pu = 0f;
                for (int p = 0; p < pulses.Count; p++)
                {
                    float tau = t - pulses[p].t;
                    if (tau < 0f) break;
                    if (tau > 0.8f) continue;
                    float ph = TwoPi * (52f * tau + 38f * (1f - Mathf.Exp(-16f * tau)) / 16f);
                    pu += Mathf.Sin(ph) * Mathf.Exp(-tau * 6.5f) * pulses[p].amp;
                }

                float dryL = l + sub * 0.22f + pu * 0.45f + bl * 0.3f;
                float dryR = r + sub * 0.22f + pu * 0.45f + br * 0.3f;
                float wl = rl.Process((l + bl * 0.6f) * 0.03f);
                float wr = rr.Process((r + br * 0.6f) * 0.03f);
                o[2 * i] = dryL + wl * 0.9f;
                o[2 * i + 1] = dryR + wr * 0.9f;
            }
            FadeEdges(o, 0.8f, 2.0f);
            Normalize(o, 0.82f);
            return o;
        }

        // ================================================================== ambience

        /// <summary>One murmuring audience member: band-passed noise shaped into syllables, words and pauses.</summary>
        sealed class Voice
        {
            public float fc, gl, gr;
            float amp, fcMul = 1f, sylLen = 0.1f, remain;
            bool inSyl;
            int sylLeft;
            Svf f;

            public Voice(Rng r)
            {
                fc = r.Range(280f, 950f);
                float pan = r.Range(-0.85f, 0.85f);
                float lv = r.Range(0.55f, 1f);
                gl = Mathf.Sqrt(0.5f * (1f - pan)) * lv;
                gr = Mathf.Sqrt(0.5f * (1f + pan)) * lv;
                remain = r.Range(0f, 1.2f);
                sylLeft = 2 + (int)(r.U() * 6f);
            }

            public float Next(Rng r)
            {
                remain -= 1f / SR;
                if (remain <= 0f)
                {
                    if (inSyl)
                    {
                        inSyl = false;
                        if (--sylLeft <= 0) { sylLeft = 3 + (int)(r.U() * 7f); remain = r.Range(0.25f, 1.3f); }
                        else remain = r.Range(0.025f, 0.11f);
                    }
                    else
                    {
                        inSyl = true;
                        sylLen = r.Range(0.08f, 0.24f);
                        remain = sylLen;
                        fcMul = r.Range(0.78f, 1.3f);
                        amp = r.Range(0.45f, 1f);
                    }
                }
                float bp = f.Band(r.N(), fc * fcMul, 0.35f);
                if (!inSyl) return 0f;
                float ph = 1f - remain / sylLen;
                return bp * 4f * ph * (1f - ph) * amp;
            }
        }

        /// <summary>Seamless 9 s loop: theatre room tone, a faint mains hum and a distant audience murmur.</summary>
        static float[] Ambience(int seed)
        {
            const float len = 9f;
            int n = (int)(len * SR), xf = (int)(0.6f * SR), m = n + xf;
            float[] raw = new float[m * 2];
            Rng rng = new Rng(seed);
            Voice[] voices = new Voice[12];
            for (int v = 0; v < voices.Length; v++) voices[v] = new Voice(rng);
            OnePole lpL = default(OnePole), lpR = default(OnePole);
            float a350 = Coef(350f), bl = 0f, br = 0f;
            Reverb revL = new Reverb(0, 0.88f, 0.45f), revR = new Reverb(23, 0.88f, 0.45f);

            for (int i = 0; i < m; i++)
            {
                float t = i / (float)SR;
                bl = bl * 0.995f + rng.N() * 0.05f;
                br = br * 0.995f + rng.N() * 0.05f;
                float roomL = lpL.Lp(bl, a350), roomR = lpR.Lp(br, a350);
                float hum = 0.012f * Mathf.Sin(TwoPi * 50f * t) + 0.006f * Mathf.Sin(TwoPi * 100f * t);
                float ml = 0f, mr = 0f;
                for (int v = 0; v < voices.Length; v++)
                {
                    float s = voices[v].Next(rng);
                    ml += s * voices[v].gl;
                    mr += s * voices[v].gr;
                }
                float wl = revL.Process(ml * 0.02f), wr = revR.Process(mr * 0.02f);
                raw[2 * i] = roomL * 0.9f + hum + ml * 0.25f + wl * 1.2f;
                raw[2 * i + 1] = roomR * 0.9f + hum + mr * 0.25f + wr * 1.2f;
            }

            // cross-fade the extra tail into the head so the loop has no seam
            float[] data = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                float k = i < xf ? i / (float)xf : 1f;
                for (int ch = 0; ch < 2; ch++)
                {
                    float s = raw[2 * i + ch];
                    if (i < xf) s = s * k + raw[2 * (n + i) + ch] * (1f - k);
                    data[2 * i + ch] = s;
                }
            }
            Normalize(data, 0.6f);
            return data;
        }

        // ================================================================== curtain

        static float Thump(float t, float t0, float f, float decay)
        {
            float tau = t - t0;
            if (tau < 0f || tau > 1.2f) return 0f;
            return Mathf.Sin(TwoPi * f * tau * (1f + 0.4f * Mathf.Exp(-tau * 20f))) * Mathf.Exp(-tau * decay);
        }

        /// <summary>
        /// Two heavy curtains travelling apart: fabric swoosh whose brightness follows the curtain speed, body whoosh,
        /// track rumble and carrier ticks. Each curtain pans from the centre to its own side as it opens.
        /// </summary>
        static float[] Curtain(Request q, int seed)
        {
            float dur = Mathf.Max(0.3f, q.openDuration);
            float len = dur + 1.6f;
            int n = Mathf.CeilToInt(len * SR);
            float[] o = new float[n * 2];
            float[] p = q.openCurve;
            if (p == null || p.Length < 4)
            {
                p = new float[64];
                for (int i = 0; i < p.Length; i++) p[i] = Smooth01(i / 63f);
            }
            int M = p.Length;
            float[] vel = new float[M];
            float vmax = 1e-5f;
            for (int i = 0; i < M; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(M - 1, i + 1);
                vel[i] = Mathf.Abs(p[b] - p[a]) * (M - 1) / Mathf.Max(1, b - a);
                vmax = Mathf.Max(vmax, vel[i]);
            }
            for (int i = 0; i < M; i++) vel[i] /= vmax;

            Rng rng = new Rng(seed);
            Svf[] fab = new Svf[2], rum = new Svf[2], tickF = new Svf[2];
            OnePole[] body = new OnePole[2], flut = new OnePole[2];
            float[] tickNext = { 0.1f, 0.13f }, tickEnv = new float[2];
            float c12 = Coef(12f), c220 = Coef(220f);
            Reverb rl = new Reverb(0, 0.7f, 0.5f), rr = new Reverb(23, 0.7f, 0.5f);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float tau = t / dur, prog, v;
                if (tau <= 1f)
                {
                    float x = tau * (M - 1);
                    int k = Mathf.Min((int)x, M - 1), k1 = Mathf.Min(k + 1, M - 1);
                    float fr = x - k;
                    prog = Mathf.Lerp(p[k], p[k1], fr);
                    v = Mathf.Lerp(vel[k], vel[k1], fr);
                }
                else { prog = p[M - 1]; v = 0f; }
                float ve = v;
                if (t > dur) ve = Mathf.Max(ve, 0.22f * Mathf.Exp(-(t - dur) * 3.5f));   // cloth settling after the stop

                float L = 0f, R = 0f;
                for (int s = 0; s < 2; s++)
                {
                    float fl = flut[s].Lp(rng.N(), c12) * 6f;
                    float fabric = fab[s].Band(rng.N(), 450f + 2300f * ve, 0.9f) * Mathf.Pow(ve, 0.9f) * (0.72f + 0.28f * fl);
                    float bod = body[s].Lp(rng.N(), c220) * 3.2f * ve;
                    float rumble = rum[s].Band(rng.N(), 95f, 0.5f) * ve * 0.8f;
                    tickNext[s] -= 1f / SR;
                    if (tickNext[s] <= 0f && v > 0.05f)
                    {
                        tickEnv[s] = 0.35f * v + 0.05f;
                        tickNext[s] = 1f / (5f + 24f * v) * rng.Range(0.7f, 1.3f);
                    }
                    tickEnv[s] *= 0.9965f;
                    float tick = tickF[s].Band(rng.N(), 1700f, 0.3f) * tickEnv[s];
                    float sig = fabric * 0.9f + bod * 0.6f + rumble + tick * 0.5f;
                    float pan = (s == 0 ? -1f : 1f) * (0.08f + Mathf.Clamp01(prog) * 0.8f);
                    L += sig * Mathf.Sqrt(0.5f * (1f - pan));
                    R += sig * Mathf.Sqrt(0.5f * (1f + pan));
                }
                float th = Thump(t, 0.02f, 68f, 9f) * 0.55f + Thump(t, dur * 0.97f, 55f, 7f) * 0.15f;
                L += th; R += th;
                float wl = rl.Process(L * 0.03f), wr = rr.Process(R * 0.03f);
                o[2 * i] = L + wl * 0.6f;
                o[2 * i + 1] = R + wr * 0.6f;
            }
            FadeEdges(o, 0.005f, 0.4f);
            Normalize(o, 0.8f);
            return o;
        }

        // ================================================================== title reveal

        static readonly float[] ShingRatio = { 1f, 2.76f, 5.40f, 8.93f, 13.3f };
        static readonly float[] ShingAmp = { 0.5f, 0.3f, 0.18f, 0.1f, 0.06f };
        static readonly float[] ShingDecay = { 2.2f, 2.8f, 3.6f, 4.5f, 6f };

        /// <summary>
        /// A <see cref="RevealLead"/> second riser (noise swell + rising tone), then the hit: sub boom, transient, a bright
        /// metallic "shing", a high shimmer and the neon-tube buzz gated by the same flicker pattern as the title.
        /// </summary>
        static float[] Reveal(int seed)
        {
            float lead = RevealLead, len = lead + 5f;
            int n = Mathf.CeilToInt(len * SR);
            float[] o = new float[n * 2];
            Rng rng = new Rng(seed);
            OnePole hpL = default(OnePole), hpR = default(OnePole), bodyLp = default(OnePole), buzzLp = default(OnePole);
            Svf cymL = default(Svf), cymR = default(Svf);
            float c1800 = Coef(1800f), c320 = Coef(320f), c2500 = Coef(2500f);
            Reverb rl = new Reverb(0, 0.9f, 0.3f), rr = new Reverb(23, 0.9f, 0.3f);
            double sweepPh = 0, boomPh = 0, buzzPh = 0;
            float gate = 0f, lastLevel = 0f, crack = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float l = 0f, r = 0f;
                if (t < lead - 0.012f)
                {
                    float x = t / lead;
                    float amp = x * x * x * 0.9f + x * 0.05f;
                    float nl = rng.N(), nr = rng.N();
                    float hl = nl - hpL.Lp(nl, c1800), hr = nr - hpR.Lp(nr, c1800);
                    float cy = x * x * x * x * 0.35f;
                    float f = 160f * Mathf.Pow(1100f / 160f, x);
                    sweepPh += f / (double)SR;
                    float sw = (float)System.Math.Sin(sweepPh * TwoPiD) * amp * 0.25f;
                    l += hl * amp * 0.45f + cymL.Band(nl, 5200f + 1200f * x, 0.6f) * cy + sw;
                    r += hr * amp * 0.45f + cymR.Band(nr, 5200f + 1200f * x, 0.6f) * cy + sw;
                }

                float tau = t - lead;
                if (tau >= 0f)
                {
                    float fb = 38f + 90f * Mathf.Exp(-tau * 9f);
                    boomPh += fb / (double)SR;
                    float boom = (float)System.Math.Sin(boomPh * TwoPiD) * Mathf.Exp(-tau * 2.3f) * 0.95f;
                    float click = rng.N() * Mathf.Exp(-tau * 350f) * 0.7f;
                    float bodyN = bodyLp.Lp(rng.N(), c320) * 3f * Mathf.Exp(-tau * 5.5f) * 0.6f;

                    float shL = 0f, shR = 0f;
                    float att = 1f - Mathf.Exp(-tau * 800f);
                    for (int p = 0; p < ShingRatio.Length; p++)
                    {
                        float env = Mathf.Exp(-tau * ShingDecay[p]) * att * ShingAmp[p];
                        double fq = 880.0 * ShingRatio[p] * TwoPiD * tau;
                        shL += (float)System.Math.Sin(fq * 0.997) * env;
                        shR += (float)System.Math.Sin(fq * 1.003) * env;
                    }

                    float shimmer = 0f;
                    if (tau < 4.5f)
                        shimmer = (float)(System.Math.Sin(TwoPiD * 1046.5 * tau) + System.Math.Sin(TwoPiD * 1396.9 * tau) + System.Math.Sin(TwoPiD * 1760.0 * tau))
                                  * 0.03f * Smooth01(tau / 0.4f) * Mathf.Exp(-tau * 0.9f) * (0.8f + 0.2f * Mathf.Sin(TwoPi * 6f * tau));

                    // neon tube: 120 Hz buzz following the flicker, crackle on every on/off step, then a fading hum
                    float level = Flicker(tau);
                    if (Mathf.Abs(level - lastLevel) > 0.2f) crack = 1f;
                    lastLevel = level;
                    crack *= 0.9975f;
                    gate += (level - gate) * 0.02f;
                    buzzPh += 120.0 / SR;
                    float bz = buzzLp.Lp((float)System.Math.Tanh(3.0 * System.Math.Sin(buzzPh * TwoPiD)), c2500);
                    float buzzAmp = tau < FlickerLength ? 0.16f * gate : 0.07f * Mathf.Exp(-(tau - FlickerLength) * 1.1f);
                    float crackle = rng.N() * crack * 0.45f;

                    float common = boom + click + bodyN + shimmer + bz * buzzAmp + crackle;
                    l += common + shL * 0.25f;
                    r += common + shR * 0.25f;
                }
                float wl = rl.Process(l * 0.03f), wr = rr.Process(r * 0.03f);
                o[2 * i] = l + wl * 0.8f;
                o[2 * i + 1] = r + wr * 0.8f;
            }
            FadeEdges(o, 0.01f, 1.2f);
            Normalize(o, 0.9f);
            return o;
        }
    }
}
