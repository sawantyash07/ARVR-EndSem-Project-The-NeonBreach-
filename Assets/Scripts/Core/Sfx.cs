using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Pooled audio playback for the procedural sound effects plus the looping music.
    /// 3D sounds go through the binaural (HRTF-style) filter when "Spatial audio: HRTF" is enabled.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        static Sfx inst;
        AudioSource[] pool;
        BinauralFilter[] filters;
        AudioSource musicSource;
        int next;

        public static Sfx Instance
        {
            get
            {
                if (inst == null)
                {
                    GameObject go = new GameObject("[Sfx]");
                    inst = go.AddComponent<Sfx>();
                    inst.Init();
                }
                return inst;
            }
        }

        void Awake()
        {
            if (inst == null) { inst = this; Init(); }
        }

        void Init()
        {
            if (pool != null) return;
            pool = new AudioSource[32];
            filters = new BinauralFilter[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject child = new GameObject("src" + i);
                child.transform.SetParent(transform, false);
                AudioSource s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 70f;
                pool[i] = s;
                filters[i] = child.AddComponent<BinauralFilter>();
            }
            GameObject m = new GameObject("music");
            m.transform.SetParent(transform, false);
            musicSource = m.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0.5f;
        }

        public void StartMusic()
        {
            if (musicSource.isPlaying) return;
            musicSource.clip = AudioSynth.Music();
            musicSource.Play();
        }

        public void SetMusicVolume(float v) { musicSource.volume = v; }

        /// <summary>Music pitch drops while slow motion is active.</summary>
        void Update()
        {
            if (musicSource == null) return;
            float want = PowerUps.SlowMo && GameManager.Instance != null && GameManager.Instance.Playing ? 0.82f : 1f;
            musicSource.pitch = Mathf.MoveTowards(musicSource.pitch, want, Time.unscaledDeltaTime * 0.8f);
        }

        int NextIndex()
        {
            int i = next;
            next = (next + 1) % pool.Length;
            return i;
        }

        public static void Play3D(SfxId id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            Sfx s = Instance;
            int i = s.NextIndex();
            AudioSource src = s.pool[i];
            src.Stop();
            src.transform.position = pos;
            BinauralFilter.Configure(src, s.filters[i]);
            src.clip = AudioSynth.Get(id);
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        public static void Play2D(SfxId id, float volume = 1f, float pitch = 1f)
        {
            Sfx s = Instance;
            int i = s.NextIndex();
            AudioSource src = s.pool[i];
            src.Stop();
            s.filters[i].SetActive(false);
            src.spatialize = false;
            src.spatialBlend = 0f;
            src.clip = AudioSynth.Get(id);
            src.volume = volume;
            src.pitch = pitch;
            src.Play();
        }

        /// <summary>Attach a looping positional sound to a moving object (enemy hum, engine...).</summary>
        public static AudioSource AttachLoop(GameObject go, SfxId id, float volume, float maxDistance = 30f)
        {
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1.5f;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0f;
            BinauralFilter f = go.AddComponent<BinauralFilter>();
            f.minDistance = 1.5f;
            f.maxDistance = maxDistance;
            BinauralFilter.Configure(src, f);
            src.clip = AudioSynth.Get(id);
            src.volume = volume;
            src.time = Random.Range(0f, src.clip.length * 0.9f);
            src.Play();
            return src;
        }

        /// <summary>Re-apply the spatial audio mode to every source (after the HRTF option changes).</summary>
        public static void RefreshSpatial()
        {
            BinauralFilter[] all = FindObjectsByType<BinauralFilter>();
            for (int i = 0; i < all.Length; i++)
            {
                AudioSource src = all[i].GetComponent<AudioSource>();
                if (src != null && src.loop) BinauralFilter.Configure(src, all[i]);   // pooled one-shots reconfigure on play
            }
        }
    }
}
