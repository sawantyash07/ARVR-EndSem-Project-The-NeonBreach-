using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// Farm sound player: pooled 3D / 2D one-shots plus the ambience (wind bed, birds in the trees, the odd crow and a
    /// cow out by the barn). All clips come from FarmAudioSynth.
    /// </summary>
    public class FarmAudio : MonoBehaviour
    {
        public static FarmAudio Instance { get; private set; }

        [Header("Mix")]
        [Range(0f, 1f)] public float effectsVolume = 1f;
        [Range(0f, 1f)] public float ambienceVolume = 0.7f;
        [Range(0f, 1f)] public float uiVolume = 0.8f;
        [Min(4)] public int poolSize = 24;

        [Header("Ambience")]
        public bool playAmbience = true;
        [Tooltip("Seconds between bird calls (random in range).")]
        public Vector2 birdInterval = new Vector2(1.5f, 5f);
        public Vector2 crowInterval = new Vector2(20f, 45f);
        public Vector2 cowInterval = new Vector2(25f, 60f);
        [Tooltip("Where the cow is heard from (pasture behind the barn).")]
        public Vector3 cowPosition = new Vector3(60f, 1f, -90f);
        [Tooltip("Birds sing from these points (tree tops); filled by the scene builder.")]
        public Transform[] birdPerches = new Transform[0];

        AudioSource[] pool;
        int next;
        AudioSource wind;
        float birdAt, crowAt, cowAt;

        void Awake()
        {
            Instance = this;
            pool = new AudioSource[poolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject g = new GameObject("src" + i);
                g.transform.SetParent(transform, false);
                AudioSource s = g.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                pool[i] = s;
            }
            GameObject w = new GameObject("WindBed");
            w.transform.SetParent(transform, false);
            wind = w.AddComponent<AudioSource>();
            wind.playOnAwake = false;
            wind.loop = true;
            wind.spatialBlend = 0f;
            wind.clip = FarmAudioSynth.Get(FarmSound.WindLoop);
            birdAt = Time.time + 1f;
            crowAt = Time.time + Random.Range(crowInterval.x, crowInterval.y);
            cowAt = Time.time + Random.Range(6f, 14f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (playAmbience)
            {
                wind.volume = 0.35f * ambienceVolume;
                wind.Play();
            }
        }

        AudioSource NextSource()
        {
            AudioSource s = pool[next];
            next = (next + 1) % pool.Length;
            s.Stop();
            return s;
        }

        /// <summary>Positional sound. minDistance/maxDistance shape the logarithmic roll-off.</summary>
        public static void Play3D(FarmSound id, Vector3 pos, float volume = 1f, float pitch = 1f, float minDistance = 2f, float maxDistance = 120f)
        {
            FarmAudio a = Instance;
            if (a == null) return;
            AudioSource s = a.NextSource();
            s.transform.position = pos;
            s.spatialBlend = 1f;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.ignoreListenerPause = false;
            s.clip = FarmAudioSynth.Get(id);
            s.volume = volume * a.effectsVolume;
            s.pitch = pitch;
            s.Play();
        }

        public static void Play2D(FarmSound id, float volume = 1f, float pitch = 1f)
        {
            FarmAudio a = Instance;
            if (a == null) return;
            AudioSource s = a.NextSource();
            s.spatialBlend = 0f;
            s.ignoreListenerPause = false;
            s.clip = FarmAudioSynth.Get(id);
            s.volume = volume * a.effectsVolume;
            s.pitch = pitch;
            s.Play();
        }

        /// <summary>Menu sounds keep playing while the game is paused.</summary>
        public static void PlayUI(FarmSound id, float volume = 1f)
        {
            FarmAudio a = Instance;
            if (a == null) return;
            AudioSource s = a.NextSource();
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.clip = FarmAudioSynth.Get(id);
            s.volume = volume * a.uiVolume;
            s.pitch = 1f;
            s.Play();
        }

        void Update()
        {
            if (!playAmbience) return;
            if (wind != null) wind.volume = Mathf.Lerp(wind.volume, 0.35f * ambienceVolume, Time.unscaledDeltaTime);
            Vector3 listener = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float now = Time.time;
            if (now >= birdAt)
            {
                birdAt = now + Random.Range(birdInterval.x, birdInterval.y);
                Vector3 p;
                if (birdPerches != null && birdPerches.Length > 0)
                {
                    Transform perch = birdPerches[Random.Range(0, birdPerches.Length)];
                    p = perch != null ? perch.position : listener + Random.onUnitSphere * 30f;
                }
                else p = listener + new Vector3(Random.Range(-40f, 40f), Random.Range(6f, 14f), Random.Range(-40f, 40f));
                FarmSound s = Random.value < 0.4f ? FarmSound.Bird1 : (Random.value < 0.5f ? FarmSound.Bird2 : FarmSound.Bird3);
                Play3D(s, p, 0.55f * ambienceVolume, Random.Range(0.85f, 1.2f), 6f, 90f);
            }
            if (now >= crowAt)
            {
                crowAt = now + Random.Range(crowInterval.x, crowInterval.y);
                Play3D(FarmSound.Crow, listener + new Vector3(Random.Range(-60f, 60f), 18f, Random.Range(40f, 80f)), 0.6f * ambienceVolume, Random.Range(0.9f, 1.1f), 10f, 160f);
            }
            if (now >= cowAt)
            {
                cowAt = now + Random.Range(cowInterval.x, cowInterval.y);
                Play3D(FarmSound.Cow, cowPosition, 0.7f * ambienceVolume, Random.Range(0.9f, 1.05f), 8f, 160f);
            }
        }
    }
}
