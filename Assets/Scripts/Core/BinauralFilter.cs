using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Lightweight HRTF-style binaural spatializer, written from scratch so it works without any vendor plugin.
    /// For each sound it models the three main cues the brain uses to localise sound:
    ///   ITD  - interaural time difference: the far ear hears the sound up to ~0.66 ms later (Woodworth model),
    ///   ILD  - interaural level difference: the far ear is quieter,
    ///   head shadow + pinna cue - the far ear and sounds from behind lose high frequencies (one-pole low-pass).
    /// Distance attenuation is applied here too (the AudioSource itself is 2D so Unity's panner does not interfere).
    /// If the project has a real spatializer plugin selected (Project Settings > Audio), that is used instead.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BinauralFilter : MonoBehaviour
    {
        public float minDistance = 4f;
        public float maxDistance = 70f;

        const float HeadRadius = 0.0875f;   // metres
        const float SpeedOfSound = 343f;
        const int BufLen = 256;

        // written on the main thread, read on the audio thread
        volatile float gainL = 1f, gainR = 1f, delayL, delayR, lpL = 1f, lpR = 1f;
        volatile bool active;

        readonly float[] ring = new float[BufLen];
        int write;
        float zL, zR;
        int sampleRate = 48000;

        static bool? pluginChecked;

        /// <summary>True when the project has a spatializer plugin (e.g. Meta XR Audio / Steam Audio) selected.</summary>
        public static bool HasPlugin
        {
            get
            {
                if (!pluginChecked.HasValue)
                    pluginChecked = !string.IsNullOrEmpty(AudioSettings.GetSpatializerPluginName());
                return pluginChecked.Value;
            }
        }

        /// <summary>Configure an AudioSource for 3D playback with the current settings.</summary>
        public static void Configure(AudioSource s, BinauralFilter f)
        {
            if (GameSettings.Hrtf && HasPlugin)
            {
                s.spatialize = true;
                s.spatialBlend = 1f;
                if (f != null) f.active = false;
            }
            else if (GameSettings.Hrtf && f != null)
            {
                s.spatialize = false;
                s.spatialBlend = 0f;     // our filter does the panning + attenuation
                f.active = true;
                f.UpdateParams();
            }
            else
            {
                s.spatialize = false;
                s.spatialBlend = 1f;     // Unity's standard stereo panner
                if (f != null) f.active = false;
            }
        }

        public void SetActive(bool on) { active = on; }

        void Awake() { sampleRate = AudioSettings.outputSampleRate; }

        void LateUpdate() { if (active) UpdateParams(); }

        static Transform Listener()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Player != null && gm.Player.cam != null) return gm.Player.cam.transform;
            Camera c = Camera.main;
            return c != null ? c.transform : null;
        }

        public void UpdateParams()
        {
            Transform lis = Listener();
            if (lis == null) { gainL = gainR = 1f; delayL = delayR = 0f; lpL = lpR = 1f; return; }

            Vector3 local = lis.InverseTransformPoint(transform.position);
            float dist = local.magnitude;
            Vector3 dir = dist > 0.001f ? local / dist : Vector3.forward;

            // distance attenuation (linear, same curve as the stock sources)
            float att = 1f - Mathf.Clamp01((dist - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));
            att *= att;

            // azimuth: lateral = sin(azimuth); +1 = fully to the right
            float lateral = Mathf.Clamp(dir.x, -1f, 1f);
            float az = Mathf.Asin(Mathf.Abs(lateral));
            float itd = HeadRadius / SpeedOfSound * (az + Mathf.Sin(az));       // Woodworth, seconds
            float itdSamples = itd * sampleRate;

            float far = Mathf.Abs(lateral);
            float near = 1f + 0.15f * far;
            float farGain = 1f - 0.45f * far;

            // head shadow on the far ear (20 kHz -> ~1.8 kHz), pinna cue for sources behind the head (-> ~5 kHz)
            float behind = Mathf.Clamp01(-dir.z);
            float cutFar = Mathf.Lerp(18000f, 1800f, far);
            float cutNear = Mathf.Lerp(18000f, 5000f, behind);
            cutFar = Mathf.Min(cutFar, cutNear);
            float aFar = 1f - Mathf.Exp(-2f * Mathf.PI * cutFar / sampleRate);
            float aNear = 1f - Mathf.Exp(-2f * Mathf.PI * cutNear / sampleRate);

            if (lateral >= 0f)
            {
                gainR = near * att; gainL = farGain * att;
                delayR = 0f; delayL = itdSamples;
                lpR = aNear; lpL = aFar;
            }
            else
            {
                gainL = near * att; gainR = farGain * att;
                delayL = 0f; delayR = itdSamples;
                lpL = aNear; lpR = aFar;
            }
        }

        float ReadDelayed(float delay)
        {
            float pos = write - 1 - delay;
            while (pos < 0f) pos += BufLen;
            int i0 = (int)pos;
            float frac = pos - i0;
            int i1 = (i0 + 1) % BufLen;
            return ring[i0 % BufLen] * (1f - frac) + ring[i1] * frac;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!active || channels < 2) return;
            float gl = gainL, gr = gainR, dl = delayL, dr = delayR, al = lpL, ar = lpR;
            for (int i = 0; i < data.Length; i += channels)
            {
                float mono = (data[i] + data[i + 1]) * 0.5f;
                ring[write] = mono;
                write = (write + 1) % BufLen;

                float l = ReadDelayed(dl);
                float r = ReadDelayed(dr);
                zL += (l - zL) * al;
                zR += (r - zR) * ar;
                data[i] = zL * gl;
                data[i + 1] = zR * gr;
                for (int c = 2; c < channels; c++) data[i + c] = 0f;
            }
        }
    }
}
