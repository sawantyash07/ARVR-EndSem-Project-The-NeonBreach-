using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonBreach.RatShooter
{
    /// <summary>
    /// Sound playback for the Rat Shooter scene: a pool of 2D / 3D audio sources for the synthesised effects plus
    /// looping music and countryside ambience. Music and ambience samples are generated on a worker thread so the
    /// scene starts without a hitch. Volumes follow the shared Neon Breach audio settings.
    /// </summary>
    public class RatAudio : MonoBehaviour
    {
        public static RatAudio Instance { get; private set; }

        [Range(0f, 1f)] public float effectsVolume = 0.9f;
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f)] public float ambienceVolume = 0.5f;
        public int poolSize = 16;

        static readonly Dictionary<RatSound, AudioClip> clips = new Dictionary<RatSound, AudioClip>();
        static AudioClip musicClip, ambienceClip;

        AudioSource[] pool;
        AudioSource music, ambience;
        int next;
        Task<float[]> musicTask, ambienceTask;
        float musicDuck = 1f, musicTarget = 1f;

        void Awake()
        {
            Instance = this;
            pool = new AudioSource[poolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject go = new GameObject("sfx" + i);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f;
                s.maxDistance = 60f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }
            music = MakeLoop("music");
            ambience = MakeLoop("ambience");

            if (musicClip == null) musicTask = Task.Run(() => RatSynth.Music());
            if (ambienceClip == null) ambienceTask = Task.Run(() => RatSynth.Ambience());
        }

        AudioSource MakeLoop(string n)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        void Start()
        {
            // warm up the short effects (all together they take a few milliseconds)
            foreach (RatSound s in System.Enum.GetValues(typeof(RatSound))) Get(s);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static AudioClip Get(RatSound s)
        {
            AudioClip c;
            if (clips.TryGetValue(s, out c) && c != null) return c;
            float[] data = RatSynth.Generate(s);
            c = AudioClip.Create("RS_" + s, data.Length, 1, RatSynth.Rate, false);
            c.SetData(data, 0);
            clips[s] = c;
            return c;
        }

        void Update()
        {
            if (musicTask != null && musicTask.IsCompleted)
            {
                if (musicTask.Status == TaskStatus.RanToCompletion) musicClip = MakeClip("RS_Music", musicTask.Result);
                musicTask = null;
            }
            if (ambienceTask != null && ambienceTask.IsCompleted)
            {
                if (ambienceTask.Status == TaskStatus.RanToCompletion) ambienceClip = MakeClip("RS_Ambience", ambienceTask.Result);
                ambienceTask = null;
            }
            if (musicClip != null && music.clip != musicClip) { music.clip = musicClip; music.Play(); }
            if (ambienceClip != null && ambience.clip != ambienceClip) { ambience.clip = ambienceClip; ambience.Play(); }

            musicDuck = Mathf.MoveTowards(musicDuck, musicTarget, Time.unscaledDeltaTime * 1.5f);
            float mv = Mathf.Clamp01(GameSettings.MusicVolume / 0.45f);
            music.volume = Mathf.MoveTowards(music.volume, musicVolume * mv * musicDuck, Time.unscaledDeltaTime * 0.8f);
            ambience.volume = Mathf.MoveTowards(ambience.volume, ambienceVolume, Time.unscaledDeltaTime * 0.5f);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip c = AudioClip.Create(name, data.Length, 1, RatSynth.MusicRate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>0..1: lower the music (pause menu, results jingles).</summary>
        public void DuckMusic(float level) { musicTarget = Mathf.Clamp01(level); }

        public void SetPaused(bool paused)
        {
            for (int i = 0; i < pool.Length; i++) if (pool[i] != null) { if (paused) pool[i].Pause(); else pool[i].UnPause(); }
        }

        public static void Play2D(RatSound s, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            Instance.PlayInternal(s, volume, pitch, Vector3.zero, false);
        }

        public static void Play3D(RatSound s, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (Instance == null) return;
            Instance.PlayInternal(s, volume, pitch, pos, true);
        }

        void PlayInternal(RatSound s, float volume, float pitch, Vector3 pos, bool spatial)
        {
            AudioSource src = pool[next];
            next = (next + 1) % pool.Length;
            src.Stop();
            src.clip = Get(s);
            src.volume = Mathf.Clamp01(volume * effectsVolume);
            src.pitch = pitch;
            src.spatialBlend = spatial ? 0.85f : 0f;
            src.transform.position = pos;
            src.Play();
        }
    }
}
