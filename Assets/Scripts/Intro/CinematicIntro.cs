#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
#define NB_NEW_INPUT
#endif

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
#if NB_NEW_INPUT
using UnityEngine.InputSystem;
#endif

namespace NeonBreach
{
    /// <summary>
    /// Cinematic startup sequence: two velvet theatre curtains with neon piping open on a neon stage and reveal
    /// "WELCOME TO NEON BREACH", with music, theatre ambience and curtain / reveal sound effects, then the intro fades
    /// out into the existing main menu.
    ///
    /// The intro is independent of the menu: it draws on its own Screen Space Overlay Canvas (built at runtime, so the
    /// scene only holds this component and its Inspector settings), and while it plays it only disables the IMGUI
    /// <see cref="HUD"/> component (IMGUI is always drawn above uGUI) and mutes the menu music. When it ends it
    /// re-enables the HUD, starts the HUD's own fade-in from the same dark colour and fades the menu music back in.
    /// The menu, game modes and settings are never touched.
    ///
    /// It plays once per application launch, only in the first scene that is loaded: returning to the menu from a game
    /// mode (or reloading this scene from the Rat Shooter / Farm Sniper scenes) goes straight to the menu.
    /// </summary>
    [DefaultExecutionOrder(-50)]   // after GameManager.Awake (-100): the HUD exists and is hidden before it draws once
    [DisallowMultipleComponent]
    [AddComponentMenu("Neon Breach/Cinematic Intro")]
    public class CinematicIntro : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("Play the theatre-curtain intro when the game starts (Play in the Editor or a standalone build).")]
        public bool playIntro = true;
        [Tooltip("Play only at the start of a new application launch. Returning to the main menu from any game mode, " +
                 "or from the Rat Shooter / Farm Sniper scenes, goes straight to the menu.")]
        public bool oncePerLaunch = true;
        [Tooltip("Show the Skip Intro button. Esc, Enter, Space and gamepad Start / A also skip.")]
        public bool allowSkip = true;
        [Tooltip("Seconds before the Skip Intro button fades in.")]
        [Min(0f)] public float skipButtonDelay = 0.6f;
        [Tooltip("Skip the intro while a VR headset is in use: a screen-space intro is not visible inside the headset.")]
        public bool skipInVR = true;

        [Header("Timeline (seconds)")]
        [Tooltip("Fade from black to the closed curtains.")]
        [Min(0f)] public float fadeInDuration = 1.0f;
        [Tooltip("Pause on the closed curtains before they start to open.")]
        [Min(0f)] public float closedHoldDuration = 1.4f;
        [Tooltip("How long the curtains take to open. Lower = faster curtain movement.")]
        [Min(0.3f)] public float curtainOpenDuration = 3.6f;
        [Tooltip("Easing of the curtain movement: time 0..1 -> opened 0..1.")]
        public AnimationCurve curtainOpenCurve = DefaultOpenCurve();
        [Tooltip("Delay after the curtains start opening before the title starts to appear.")]
        [Min(0f)] public float titleRevealDelay = 1.1f;
        [Tooltip("Duration of the title reveal (fade-in, zoom-in and letter spacing).")]
        [Min(0.3f)] public float titleRevealDuration = 2.2f;
        [Tooltip("Easing of the title zoom and letter-spacing animation.")]
        public AnimationCurve titleRevealCurve = DefaultRevealCurve();
        [Tooltip("How long the finished title stays on screen before the intro fades out.")]
        [Min(0f)] public float titleHoldDuration = 2.4f;
        [Tooltip("Fade-out of the whole intro at the end.")]
        [Min(0.1f)] public float outroFadeDuration = 1.2f;
        [Tooltip("Fade-in of the existing main menu after the intro.")]
        [Min(0.05f)] public float menuFadeInDuration = 0.8f;
        [Tooltip("Fade-out used when the intro is skipped.")]
        [Min(0.05f)] public float skipFadeDuration = 0.45f;

        [Header("Curtains")]
        public Color curtainColor = new Color(0.46f, 0.03f, 0.08f, 1f);
        [Tooltip("Neon piping on the leading edge of both curtains.")]
        public Color curtainTrimColor = new Color(1f, 0.22f, 0.64f, 1f);
        [Tooltip("Width of each opened curtain as a fraction of the screen width (the cloth bunches up into it).")]
        [Range(0.05f, 0.3f)] public float curtainStackWidth = 0.13f;
        [Tooltip("Number of vertical folds in each curtain.")]
        [Range(6, 30)] public int curtainFolds = 14;
        [Tooltip("How much the hems lag behind the top and sway after the curtains stop. 0 = rigid.")]
        [Range(0f, 2f)] public float curtainSway = 1f;

        [Header("Title")]
        public string welcomeText = "WELCOME TO";
        public string titleText = "NEON BREACH";
        [Tooltip("Small line under the title. Leave empty to hide it.")]
        public string taglineText = "SHOOTER   ·   ARCHERY   ·   PARACHUTE   ·   RAT SHOOTER";
        [Tooltip("Optional font asset. When empty, the first installed OS font from the list below is used, then Unity's built-in font.")]
        public Font titleFont;
        public string[] osFontNames = { "Bahnschrift", "Century Gothic", "Segoe UI", "Arial" };
        public Color neonCyan = new Color(0.15f, 0.92f, 1f, 1f);
        public Color neonMagenta = new Color(1f, 0.2f, 0.7f, 1f);
        [Range(60, 260)] public int titleSize = 150;
        [Range(16, 90)] public int welcomeSize = 44;
        [Tooltip("Title scale at the start of the reveal: it zooms in to 1.")]
        [Range(0.5f, 1f)] public float titleStartScale = 0.86f;
        [Tooltip("Slow push-in while the finished title is held (1 = none).")]
        [Range(1f, 1.15f)] public float titleHoldZoom = 1.035f;
        [Tooltip("Letter spacing of NEON BREACH at the start and end of the reveal (canvas units, 1080p reference).")]
        public float titleSpacingStart = 42f, titleSpacingEnd = 14f;
        [Tooltip("Letter spacing of WELCOME TO at the start and end of the reveal.")]
        public float welcomeSpacingStart = 64f, welcomeSpacingEnd = 24f;
        [Tooltip("Neon-tube ignition flicker when the title lights up.")]
        public bool neonFlicker = true;
        [Range(0f, 2f)] public float glowIntensity = 1f;

        [Header("Audio")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float ambienceVolume = 0.35f;
        [Range(0f, 1f)] public float curtainSfxVolume = 0.8f;
        [Range(0f, 1f)] public float revealSfxVolume = 0.85f;
        [Tooltip("Scale the intro music with the player's Music volume (Settings > Audio). The game's master volume always applies.")]
        public bool followGameMusicVolume = true;
        [Tooltip("Optional clips that replace the generated sounds. Leave empty to use the procedural ones.")]
        public AudioClip musicClip, ambienceClip, curtainClip, revealClip;
        [Tooltip("With a custom reveal clip: seconds into the clip where its hit lands (synchronised with the title ignition).")]
        [Min(0f)] public float customRevealHitTime = 0f;

        [Header("Look")]
        [Range(0f, 1f)] public float vignetteStrength = 0.85f;
        [Range(0f, 1f)] public float filmGrain = 0.35f;
        [Range(0, 80)] public int dustMotes = 36;

        /// <summary>True while the intro covers the screen (the main menu is hidden until it ends).</summary>
        public static bool IsPlaying { get; private set; }

        static bool firstSceneLoaded, playedThisLaunch;
        static HashSet<string> osFonts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            firstSceneLoaded = false;
            playedThisLaunch = false;
            IsPlaying = false;
        }

        // Called once per launch, after the first scene has loaded (after its Awake calls). Any later load of the game
        // scene is a return to the menu, never a new launch.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MarkFirstSceneLoaded() { firstSceneLoaded = true; }

        /// <summary>The HUD's own fade colour: the intro ends on it and the HUD fades the menu in from it.</summary>
        static readonly Color FadeColor = new Color(0.004f, 0.01f, 0.025f, 1f);
        /// <summary>Share of the reveal after which NEON BREACH ignites (WELCOME TO comes first).</summary>
        const float TitleIgnite = 0.3f;
        const float TitleY = 40f;

        struct Marks { public float curtain, openEnd, reveal, hit, revealEnd, outro, end; }
        struct Mote { public RawImage img; public Vector2 pos, vel; public float tw, ph, baseA; }
        struct Footlight { public RawImage dot, halo; public float on; public Color col; }

        // ---- state
        GameManager gm;
        HUD hud;
        bool hudSuppressed, running, skipping, musicStarted, firedCurtain, firedReveal, hover;
        float t, waitAudio, skipT0, skipAlpha0, pBot, vBot, lastPTop, ripplePhase, grainTimer, cursorIdle, skipHover;
        float fitScale = 1f;
        int warmup;
        Vector2 lastMouse, lastSize;
        Coroutine musicFade;

        // ---- audio
        AudioSource musicSrc, ambSrc, curtainSrc, revealSrc;
        Task<IntroAudio.Bank> audioTask;
        bool audioReady;

        // ---- ui
        readonly List<Object> owned = new List<Object>();
        readonly List<Footlight> footlights = new List<Footlight>();
        RectTransform canvasRect, titleRoot, skipRt, footRoot;
        CanvasGroup content, titleGroup, skipGroup;
        IntroCurtain curtainL, curtainR;
        RawImage valance, beam, floorGrid, hazeM, hazeC, pool, bloomC, bloomM, seamCore, seamWide;
        RawImage divL, divR, underline, underGlow, vignette, grain, skipBg, lipGlow;
        Image skipFrame, keyFrame;
        Text welcome, title, tagline, skipLabel, keyLabel;
        IntroTextFX welcomeFx, titleFx, tagFx;
        Mote[] motes;
        Texture2D glowTex;

        // ============================================================================ lifecycle

        void Awake()
        {
            if (!ShouldPlay()) { enabled = false; return; }
            playedThisLaunch = true;
            Begin();
        }

        void Start()
        {
            // GameManager.Start ran just before this (execution order) and started the menu music: keep it silent
            if (running) Sfx.Instance.SetMusicVolume(0f);
        }

        void Update()
        {
            if (!running) return;
            if (MustAbort()) { Finish(false); return; }
            Sfx.Instance.SetMusicVolume(0f);
            PollAudio();

            Marks m = Timeline();
            // hold on black for the first frames (scene start-up hitches) and, briefly, until the sounds are rendered
            if (warmup > 0 || (!audioReady && waitAudio < 0.6f))
            {
                if (warmup > 0) warmup--;
                else waitAudio += Time.unscaledDeltaTime;
                Animate(m, 0f);
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a long frame never skips part of the animation
            t += dt;
            HandleInput(dt);
            UpdateAudio(m);
            Animate(m, dt);
            if (skipping ? t >= skipT0 + skipFadeDuration : t >= m.end) Finish(true);
        }

        void OnDisable()
        {
            if (running) Finish(false);
            else if (musicFade != null) { musicFade = null; RestoreMenuMusic(); }
        }

        void OnDestroy() { Teardown(); }

        bool ShouldPlay()
        {
            if (!playIntro || Application.isBatchMode) return false;
            if (oncePerLaunch && (playedThisLaunch || firstSceneLoaded)) return false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "-nbSmokeTest" || args[i] == "-nbSkipIntro") return false;   // automated runs, manual opt-out
            if (skipInVR && VRRig.HeadsetPresent) return false;
            return FindAnyObjectByType<GameManager>() != null;   // the intro leads into the main menu of the game scene
        }

        bool MustAbort()
        {
            if (gm != null && gm.State != GameState.Menu) return true;   // a mode was started meanwhile (F5-F7 hot keys)
            if (AutoTest.Active) return true;
            return skipInVR && VRRig.HeadsetPresent;
        }

        void Begin()
        {
            gm = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
            hud = gm != null && gm.Hud != null ? gm.Hud : FindAnyObjectByType<HUD>();
            if (hud != null && hud.enabled) { hud.enabled = false; hudSuppressed = true; }   // IMGUI would draw over the Canvas
            if (musicFade != null) { StopCoroutine(musicFade); musicFade = null; }
            BuildUi();
            BuildAudio();
            t = 0f;
            waitAudio = 0f;
            warmup = 2;
            skipping = musicStarted = firedCurtain = firedReveal = hover = false;
            pBot = vBot = lastPTop = ripplePhase = grainTimer = cursorIdle = skipHover = 0f;
            lastMouse = MousePosition();
            lastSize = Vector2.zero;
            running = true;
            IsPlaying = true;
        }

        /// <summary>End the intro and hand over to the main menu. <paramref name="fadeMenu"/>: normal ending (fade the menu
        /// and its music in); false when aborting (a mode was started, the object was disabled or unloaded).</summary>
        void Finish(bool fadeMenu)
        {
            if (!running) return;
            running = false;
            IsPlaying = false;
            bool inMenu = gm != null && gm.State == GameState.Menu;
            if (hudSuppressed && hud != null)
            {
                hud.enabled = true;
                if (fadeMenu && inMenu) hud.FadeIn(menuFadeInDuration);   // fades in from the colour the intro ended on
            }
            hudSuppressed = false;
            if (inMenu) GameManager.SetCursor(false);
            Teardown();
            if (fadeMenu && isActiveAndEnabled) musicFade = StartCoroutine(FadeInMenuMusic(menuFadeInDuration * 1.6f));
            else RestoreMenuMusic();
        }

        /// <summary>Skip straight to the main menu with a short fade (Skip Intro button, Esc / Enter / Space / pad).</summary>
        public void Skip()
        {
            if (!running || skipping) return;
            skipping = true;
            skipT0 = t;
            skipAlpha0 = content != null ? content.alpha : 1f;
            Sfx.Play2D(SfxId.Click, 0.6f);
        }

        /// <summary>Inspector context menu: play the intro again (Play mode, main menu) to try out settings.</summary>
        [ContextMenu("Replay Intro (Play mode)")]
        public void Replay()
        {
            if (!Application.isPlaying) { Debug.Log("[Neon Breach] Enter Play mode to replay the intro."); return; }
            if (running) Finish(false);
            if (gm == null) gm = FindAnyObjectByType<GameManager>();
            if (gm != null && gm.State != GameState.Menu) { Debug.Log("[Neon Breach] Return to the main menu to replay the intro."); return; }
            enabled = true;
            Begin();
            Sfx.Instance.SetMusicVolume(0f);
        }

        IEnumerator FadeInMenuMusic(float duration)
        {
            float a = 0f;
            while (a < 1f)
            {
                a = Mathf.Min(1f, a + Time.unscaledDeltaTime / Mathf.Max(0.05f, duration));
                Sfx.Instance.SetMusicVolume(GameSettings.MusicVolume * a * a);
                yield return null;
            }
            musicFade = null;
        }

        static void RestoreMenuMusic()
        {
            Sfx s = FindAnyObjectByType<Sfx>();   // never create a new [Sfx] while the scene is being unloaded
            if (s != null) s.SetMusicVolume(GameSettings.MusicVolume);
        }

        void Teardown()
        {
            audioTask = null;   // a render still running finishes in the background and is discarded
            audioReady = false;
            footlights.Clear();
            motes = null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        // ============================================================================ timeline

        Marks Timeline()
        {
            Marks m;
            m.curtain = fadeInDuration + closedHoldDuration;
            m.openEnd = m.curtain + curtainOpenDuration;
            m.reveal = m.curtain + titleRevealDelay;
            m.hit = m.reveal + titleRevealDuration * TitleIgnite;
            m.revealEnd = m.reveal + titleRevealDuration;
            m.outro = Mathf.Max(m.revealEnd, m.openEnd) + titleHoldDuration;
            m.end = m.outro + outroFadeDuration;
            return m;
        }

        // ============================================================================ input

        void HandleInput(float dt)
        {
            Vector2 mp = MousePosition();
            if ((mp - lastMouse).sqrMagnitude > 4f) { cursorIdle = 0f; lastMouse = mp; }
            else cursorIdle += dt;
            bool canSkip = allowSkip && !skipping && t >= skipButtonDelay;
            hover = canSkip && skipGroup.alpha > 0.05f && RectTransformUtility.RectangleContainsScreenPoint(skipRt, mp, null);
            Cursor.visible = cursorIdle < 2.2f || hover;   // hide an idle mouse pointer, like a video player
            if (canSkip && (SkipKeyPressed() || (hover && DesktopInput.MousePressed(0)))) Skip();
        }

        static bool SkipKeyPressed()
        {
            if (DesktopInput.Pressed(KeyCode.Escape) || DesktopInput.Pressed(KeyCode.Return) ||
                DesktopInput.Pressed(KeyCode.KeypadEnter) || DesktopInput.Pressed(KeyCode.Space)) return true;
            if (GamepadInput.AnyStartPressed()) return true;
#if NB_NEW_INPUT
            Gamepad pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
#endif
            return false;
        }

        static Vector2 MousePosition()
        {
#if NB_NEW_INPUT
            Mouse m = Mouse.current;
            return m != null ? m.position.ReadValue() : new Vector2(-9999f, -9999f);
#else
            return Input.mousePosition;
#endif
        }

        // ============================================================================ audio

        void BuildAudio()
        {
            musicSrc = NewSource("Intro Music", false);
            ambSrc = NewSource("Intro Ambience", true);
            curtainSrc = NewSource("Intro Curtain", false);
            revealSrc = NewSource("Intro Reveal", false);

            Marks m = Timeline();
            IntroAudio.Request q = new IntroAudio.Request
            {
                curtainStart = m.curtain,
                openDuration = curtainOpenDuration,
                hit = m.hit,
                revealEnd = m.revealEnd,
                outro = m.outro,
                end = m.end,
                openCurve = SampleCurve(curtainOpenCurve, 256),
                music = musicClip == null,
                ambience = ambienceClip == null,
                curtain = curtainClip == null,
                reveal = revealClip == null
            };
            audioReady = false;
            audioTask = null;
            if (!(q.music || q.ambience || q.curtain || q.reveal)) { audioReady = true; AssignClips(null); return; }
            audioTask = Task.Run(() => IntroAudio.Render(q));   // pure math, off the main thread
        }

        AudioSource NewSource(string n, bool loop)
        {
            GameObject go = new GameObject(n);
            go.transform.SetParent(transform, false);
            owned.Add(go);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 8;
            s.dopplerLevel = 0f;
            s.bypassReverbZones = true;
            s.ignoreListenerPause = true;
            return s;
        }

        void PollAudio()
        {
            if (audioReady || audioTask == null || !audioTask.IsCompleted) return;
            IntroAudio.Bank bank = null;
            if (audioTask.Status == TaskStatus.RanToCompletion) bank = audioTask.Result;
            else Debug.LogWarning("[Neon Breach] Intro audio could not be generated: " +
                                  (audioTask.Exception != null ? audioTask.Exception.GetBaseException().Message : "cancelled"));
            audioTask = null;
            audioReady = true;
            AssignClips(bank);
        }

        void AssignClips(IntroAudio.Bank b)
        {
            musicSrc.clip = musicClip != null ? musicClip : Generated("Intro Music", b != null ? b.music : null);
            ambSrc.clip = ambienceClip != null ? ambienceClip : Generated("Intro Ambience", b != null ? b.ambience : null);
            curtainSrc.clip = curtainClip != null ? curtainClip : Generated("Intro Curtain", b != null ? b.curtain : null);
            revealSrc.clip = revealClip != null ? revealClip : Generated("Intro Reveal", b != null ? b.reveal : null);
        }

        AudioClip Generated(string n, float[] data)
        {
            AudioClip c = IntroAudio.ToClip(n, data);
            if (c != null) owned.Add(c);
            return c;
        }

        void UpdateAudio(Marks m)
        {
            float fin = Smooth01(fadeInDuration > 0.01f ? t / fadeInDuration : 1f);
            float fout = 1f - Smooth01((t - m.outro) / Mathf.Max(0.05f, outroFadeDuration));
            if (skipping) fout = Mathf.Min(fout, 1f - Smooth01((t - skipT0) / Mathf.Max(0.05f, skipFadeDuration)));
            float musicScale = followGameMusicVolume ? Mathf.Clamp01(GameSettings.MusicVolume / 0.45f) : 1f;   // 0.45 = default setting

            if (!musicStarted && audioReady)
            {
                musicStarted = true;
                if (musicSrc.clip != null) PlayAt(musicSrc, t);   // in sync even if the sounds were ready late
                if (ambSrc.clip != null) ambSrc.Play();
            }
            musicSrc.volume = masterVolume * musicVolume * musicScale * fin * fout;
            float hush = Mathf.Lerp(1f, 0.45f, Smooth01((t - m.curtain) / 1.5f));   // the audience quietens as the curtains part
            ambSrc.volume = masterVolume * ambienceVolume * fin * fout * hush;

            if (!firedCurtain && audioReady && t >= m.curtain)
            {
                firedCurtain = true;
                if (!skipping && curtainSrc.clip != null) PlayAt(curtainSrc, t - m.curtain);
            }
            float lead = revealClip != null ? customRevealHitTime : IntroAudio.RevealLead;
            if (!firedReveal && audioReady && t >= m.hit - lead)
            {
                firedReveal = true;
                if (!skipping && revealSrc.clip != null) PlayAt(revealSrc, t - (m.hit - lead));
            }
            curtainSrc.volume = masterVolume * curtainSfxVolume * fout;
            revealSrc.volume = masterVolume * revealSfxVolume * fout;
        }

        static void PlayAt(AudioSource s, float offset)
        {
            if (offset >= s.clip.length - 0.05f) return;
            s.time = Mathf.Max(0f, offset);
            s.Play();
        }

        // ============================================================================ animation

        void Animate(Marks m, float dt)
        {
            Vector2 size = canvasRect.rect.size;
            if ((size - lastSize).sqrMagnitude > 0.5f) Relayout(size);
            float W = size.x, H = size.y;

            // ---- the whole intro fades through one CanvasGroup
            float fin = Smooth01(fadeInDuration > 0.01f ? t / fadeInDuration : 1f);
            float fout = 1f - Smooth01((t - m.outro - outroFadeDuration * 0.25f) / Mathf.Max(0.05f, outroFadeDuration * 0.75f));
            float alpha = Mathf.Min(fin, fout);
            if (skipping) alpha = Mathf.Min(alpha, skipAlpha0 * (1f - Smooth01((t - skipT0) / Mathf.Max(0.05f, skipFadeDuration))));
            content.alpha = alpha;

            // ---- lighting: the curtain spot comes up first, the stage lights come up as the curtains part
            float spotOn = Smooth01((t - 0.1f) / (fadeInDuration + 0.8f));
            float stage = Smooth01((t - m.curtain - 0.2f) / Mathf.Max(0.1f, curtainOpenDuration * 0.8f));
            float footOn = Smooth01(t / Mathf.Max(0.1f, fadeInDuration + closedHoldDuration));

            // ---- curtains: the top follows the curve, the hem follows the top through a spring (lag + sway)
            float open01 = Mathf.Clamp01((t - m.curtain) / curtainOpenDuration);
            float pTop = t < m.curtain ? 0f : EvalCurve(curtainOpenCurve, open01, false);
            if (dt > 0f) StepSpring(pTop, dt);
            float speed = dt > 0f ? Mathf.Abs(pTop - lastPTop) / dt : 0f;
            lastPTop = pTop;
            ripplePhase += dt * (0.25f + speed * 1.2f);
            float ripple = 1.6f + 1.1f * Mathf.Sin(t * 0.9f) + (speed * 28f + Mathf.Abs(vBot) * 10f) * curtainSway;
            ConfigureCurtain(curtainL, pTop, ripple, spotOn, stage, footOn);
            ConfigureCurtain(curtainR, pTop, ripple, spotOn, stage, footOn);

            // light leaking through the seam just before the curtains part
            float leak = Smooth01((t - fadeInDuration * 0.4f) / Mathf.Max(0.1f, closedHoldDuration + fadeInDuration * 0.6f))
                         * (1f - Smooth01((t - m.curtain) / 0.7f)) * (0.85f + 0.15f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 3.1f));
            seamCore.color = new Color(0.8f, 0.96f, 1f, 0.55f * leak);
            seamWide.color = WithA(neonCyan, 0.16f * leak);

            beam.color = new Color(1f, 0.95f, 0.9f, 0.085f * spotOn * (0.92f + 0.08f * Mathf.Sin(t * 1.3f)));
            UpdateDust(W, H, dt, spotOn);

            // ---- stage behind the curtains
            float drift = Mathf.Sin(t * 0.17f);
            hazeM.rectTransform.anchoredPosition = new Vector2(-W * 0.2f + drift * 40f, H * 0.04f + Mathf.Sin(t * 0.11f) * 20f);
            hazeC.rectTransform.anchoredPosition = new Vector2(W * 0.2f - drift * 40f, -H * 0.02f);
            hazeM.color = WithA(neonMagenta, 0.10f + 0.12f * stage);
            hazeC.color = WithA(neonCyan, 0.10f + 0.12f * stage);
            floorGrid.color = new Color(1f, 1f, 1f, 0.3f + 0.6f * stage);
            pool.color = new Color(0.65f, 0.9f, 1f, 0.06f + 0.2f * stage);

            // ---- footlights switch on from the centre outwards
            float footSum = 0f;
            for (int i = 0; i < footlights.Count; i++)
            {
                Footlight f = footlights[i];
                float age = t - f.on;
                float b = Smooth01(age / 0.3f);
                if (age > 0f && age < 0.22f && Mathf.Repeat(age * 23f, 1f) < 0.35f) b *= 0.4f;   // ignition flicker
                b *= 0.7f + 0.3f * stage;
                f.dot.color = WithA(Color.Lerp(f.col, Color.white, 0.5f), b);
                f.halo.color = WithA(f.col, 0.22f * b);
                footSum += b;
            }
            lipGlow.color = WithA(neonCyan, 0.25f * (footlights.Count > 0 ? footSum / footlights.Count : 0f));

            AnimateTitle(m, W);

            // ---- film look
            vignette.color = new Color(0f, 0f, 0f, vignetteStrength);
            grainTimer += dt;
            if (grainTimer >= 1f / 24f)
            {
                grainTimer = 0f;
                grain.uvRect = new Rect(Random.value, Random.value, W / 256f, H / 256f);
            }
            grain.color = new Color(1f, 1f, 1f, filmGrain * 0.09f);

            // ---- skip button
            float sa = allowSkip && !skipping ? Smooth01((t - skipButtonDelay) / 0.4f) : 0f;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, Mathf.Min(sa, alpha), dt * 4f);
            skipHover = Mathf.MoveTowards(skipHover, hover ? 1f : 0f, dt * 8f);
            skipBg.color = Color.Lerp(new Color(0.02f, 0.05f, 0.09f, 0.5f), new Color(0.06f, 0.32f, 0.4f, 0.75f), skipHover);
            skipFrame.color = WithA(neonCyan, Mathf.Lerp(0.45f, 1f, skipHover));
            skipLabel.color = Color.Lerp(new Color(0.8f, 0.92f, 1f, 0.85f), Color.white, skipHover);
            keyFrame.color = WithA(neonCyan, Mathf.Lerp(0.35f, 0.8f, skipHover));
            keyLabel.color = Color.Lerp(new Color(0.7f, 0.85f, 0.95f, 0.75f), Color.white, skipHover);
        }

        void AnimateTitle(Marks m, float W)
        {
            float r01 = Mathf.Clamp01((t - m.reveal) / titleRevealDuration);
            float rc = EvalCurve(titleRevealCurve, r01, true);

            // WELCOME TO: first part of the reveal, letters from the centre outwards while the spacing closes in
            float wy = titleSize * 0.78f;
            welcomeFx.reveal = Mathf.Clamp01(r01 / 0.55f);
            welcomeFx.tracking = Mathf.LerpUnclamped(welcomeSpacingStart, welcomeSpacingEnd, rc);
            welcomeFx.glowAlpha = 0.045f * glowIntensity;
            welcomeFx.Refresh();
            welcome.rectTransform.anchoredPosition = new Vector2(0f, wy);

            float dv = EaseOut(Mathf.Clamp01((r01 - 0.15f) / 0.6f));
            float dLen = 230f * dv, dx = welcomeFx.HalfExtent + 28f;
            divL.rectTransform.anchoredPosition = new Vector2(-dx, wy);
            divR.rectTransform.anchoredPosition = new Vector2(dx, wy);
            divL.rectTransform.sizeDelta = divR.rectTransform.sizeDelta = new Vector2(dLen, 2f);
            divL.color = WithA(neonMagenta, 0.9f * dv);
            divR.color = WithA(neonCyan, 0.9f * dv);

            // NEON BREACH: ignites (neon flicker, synchronised with the reveal sound), magenta layer locks into place
            float lt = t - m.hit;
            float la = Smooth01(lt / (titleRevealDuration * 0.45f));
            float fl = lt < 0f ? 0f : (neonFlicker && lt < IntroAudio.FlickerLength ? IntroAudio.Flicker(lt) : 1f);
            float tr = EvalCurve(titleRevealCurve, Mathf.Clamp01(lt / (titleRevealDuration * (1f - TitleIgnite))), true);
            titleFx.reveal = la;
            titleFx.flicker = fl;
            titleFx.tracking = Mathf.LerpUnclamped(titleSpacingStart, titleSpacingEnd, tr);
            titleFx.glowAlpha = 0.075f * glowIntensity * Smooth01(lt / (titleRevealDuration * 0.6f));
            titleFx.glowRadius = titleSize * 0.085f;
            float lockIn = EaseOut(Mathf.Clamp01(lt / (titleRevealDuration * 0.55f)));
            titleFx.splitOffset = Vector2.LerpUnclamped(new Vector2(28f, -22f), new Vector2(7f, -7f), lockIn) * (titleSize / 140f);
            titleFx.splitAlpha = 0.75f;
            float shineT = (t - (m.revealEnd - 0.2f)) / 1.1f;
            titleFx.shine = shineT > 0f && shineT < 1f ? Mathf.Lerp(-0.35f, 1.35f, shineT) : -10f;
            titleFx.Refresh();

            float ul = EaseOut(Mathf.Clamp01((r01 - 0.55f) / 0.45f));
            float uw = (titleFx.HalfExtent * 2f + 40f) * ul, uy = -titleSize * 0.6f;
            underline.rectTransform.anchoredPosition = underGlow.rectTransform.anchoredPosition = new Vector2(0f, uy);
            underline.rectTransform.sizeDelta = new Vector2(uw, 3f);
            underGlow.rectTransform.sizeDelta = new Vector2(uw * 1.05f, 26f);
            underline.color = new Color(1f, 1f, 1f, ul);
            underGlow.color = WithA(Color.Lerp(neonMagenta, neonCyan, 0.5f), 0.35f * ul * glowIntensity);

            float tg = Mathf.Clamp01((r01 - 0.7f) / 0.3f);
            tagFx.reveal = tg;
            tagFx.tracking = Mathf.Lerp(14f, 6f, EaseOut(tg));
            tagFx.Refresh();
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -titleSize * 0.86f);

            // subtle zoom-in during the reveal, then a slow push-in while it is held
            float scale = Mathf.LerpUnclamped(titleStartScale, 1f, rc);
            if (t > m.revealEnd) scale *= Mathf.Lerp(1f, titleHoldZoom, Mathf.Clamp01((t - m.revealEnd) / Mathf.Max(0.1f, m.end - m.revealEnd)));
            scale *= fitScale;
            titleRoot.localScale = new Vector3(scale, scale, 1f);
            titleGroup.alpha = 1f - Smooth01((t - m.outro) / Mathf.Max(0.05f, outroFadeDuration * 0.7f));   // the title leaves first

            // bloom behind the title (on the stage layer, so it scales with the title by hand)
            float glowA = la * fl * glowIntensity * (0.92f + 0.08f * Mathf.Sin(t * 2.1f)) * titleGroup.alpha;
            float tw = (titleFx.HalfExtent * 2f + 200f) * scale;
            bloomC.rectTransform.anchoredPosition = new Vector2(0f, TitleY);
            bloomC.rectTransform.sizeDelta = new Vector2(tw * 1.25f, titleSize * 2.8f * scale);
            bloomC.color = WithA(neonCyan, 0.2f * glowA);
            bloomM.rectTransform.anchoredPosition = new Vector2(titleSize * 0.12f * scale, TitleY - titleSize * 0.08f * scale);
            bloomM.rectTransform.sizeDelta = new Vector2(tw * 1.05f, titleSize * 2.2f * scale);
            bloomM.color = WithA(neonMagenta, 0.12f * glowA);
        }

        void ConfigureCurtain(IntroCurtain c, float pTop, float ripple, float spotOn, float stage, float footOn)
        {
            c.openTop = pTop;
            c.openBottom = curtainSway > 0.001f ? pBot : pTop;
            c.stack = curtainStackWidth;
            c.ripple = ripple;
            c.phase = ripplePhase + (c.mirror ? 0.37f : 0f);
            c.spot = spotOn * Mathf.Lerp(0.95f, 0.7f, stage);
            c.foot = 0.15f + 0.45f * footOn;
            c.SetVerticesDirty();
        }

        /// <summary>Damped spring that pulls the hem after the top of the curtain: it lags while opening and sways past the stop.</summary>
        void StepSpring(float target, float dt)
        {
            if (curtainSway <= 0.001f) { pBot = target; vBot = 0f; return; }
            float k = 26f / Mathf.Max(0.25f, curtainSway);
            float c = 2f * Mathf.Sqrt(k) * 0.42f;
            const int steps = 3;
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float acc = k * (target - pBot) - c * vBot;
                vBot += acc * h;
                pBot += vBot * h;
            }
            pBot = Mathf.Clamp(pBot, -0.1f, 1.15f);
        }

        void UpdateDust(float W, float H, float dt, float spotOn)
        {
            if (motes == null) return;
            float beamW = W * 0.62f, beamTop = H * 0.5f, beamH = H * 1.02f;
            for (int i = 0; i < motes.Length; i++)
            {
                Mote mo = motes[i];
                mo.pos += mo.vel * dt + new Vector2(Mathf.Sin(t * 0.7f + mo.ph) * 4f * dt, 0f);
                if (mo.pos.y > H * 0.46f) { mo.pos.y = -H * 0.46f; mo.pos.x = Random.Range(-W * 0.28f, W * 0.28f); }
                if (Mathf.Abs(mo.pos.x) > W * 0.4f) mo.pos.x = -Mathf.Sign(mo.pos.x) * W * 0.3f;
                float v = Mathf.Clamp01(1f - (beamTop - mo.pos.y) / beamH);
                float spread = Mathf.Max(1f, IntroArt.BeamSpread(v) * beamW);
                float inBeam = Mathf.Exp(-(mo.pos.x / spread) * (mo.pos.x / spread) * 2.2f);
                float a = spotOn * mo.baseA * inBeam * (0.55f + 0.45f * Mathf.Sin(t * mo.tw + mo.ph));
                mo.img.rectTransform.anchoredPosition = mo.pos;
                mo.img.color = new Color(1f, 0.95f, 0.88f, a);
                motes[i] = mo;
            }
        }

        // ============================================================================ layout (responsive)

        void Relayout(Vector2 size)
        {
            lastSize = size;
            float W = size.x, H = size.y;
            valance.uvRect = new Rect(0f, 0f, Mathf.Max(3, Mathf.RoundToInt(W / 330f)), 1f);   // whole swags at any aspect
            floorGrid.rectTransform.sizeDelta = new Vector2(0f, H * 0.36f);
            beam.rectTransform.sizeDelta = new Vector2(W * 0.62f, H * 1.02f);
            hazeM.rectTransform.sizeDelta = hazeC.rectTransform.sizeDelta = new Vector2(W * 0.75f, H * 0.95f);
            pool.rectTransform.sizeDelta = new Vector2(W * 0.55f, H * 0.22f);
            pool.rectTransform.anchoredPosition = new Vector2(0f, -H * 0.30f);
            seamCore.rectTransform.sizeDelta = new Vector2(30f, H * 0.95f);
            seamWide.rectTransform.sizeDelta = new Vector2(150f, H * 1.05f);
            BuildFootlights(W);

            // keep the whole title inside the gap between the opened curtains on narrow (e.g. 4:3) screens, at its
            // widest moment: the wide letter spacing at the start of the reveal or the end of the slow push-in
            int gaps = Mathf.Max(0, (titleText ?? "").Length - 1);   // letter spacing is applied per character
            float baseW = title.preferredWidth + titleSize * 0.2f;      // + glow and the magenta split layer
            float widest = Mathf.Max((baseW + gaps * titleSpacingEnd) * titleHoldZoom,
                                     (baseW + gaps * titleSpacingStart) * titleStartScale);
            float opening = W * (1f - 2f * curtainStackWidth) * 0.92f;
            fitScale = Mathf.Min(1f, opening / Mathf.Max(1f, widest));
        }

        void BuildFootlights(float W)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(W / 135f), 6, 32);
            if (footlights.Count != count)
            {
                for (int i = 0; i < footlights.Count; i++)
                {
                    if (footlights[i].dot != null) Destroy(footlights[i].dot.gameObject);
                    if (footlights[i].halo != null) Destroy(footlights[i].halo.gameObject);
                }
                footlights.Clear();
                float mid = (count - 1) * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    Footlight f;
                    f.col = i % 2 == 0 ? neonCyan : neonMagenta;
                    f.halo = Raw("Footlight Glow " + i, footRoot, glowTex, Color.clear);
                    f.halo.rectTransform.pivot = new Vector2(0.5f, 0.15f);
                    f.halo.rectTransform.sizeDelta = new Vector2(130f, 170f);
                    f.dot = Raw("Footlight " + i, footRoot, glowTex, Color.clear);
                    f.dot.rectTransform.sizeDelta = new Vector2(26f, 12f);
                    f.on = fadeInDuration * 0.45f + Mathf.Abs(i - mid) / Mathf.Max(1f, mid) * Mathf.Max(0.3f, closedHoldDuration * 0.6f);
                    footlights.Add(f);
                }
            }
            for (int i = 0; i < footlights.Count; i++)
            {
                float x = (i + 0.5f) / footlights.Count * W - W * 0.5f;
                footlights[i].halo.rectTransform.anchoredPosition = new Vector2(x, 22f);
                footlights[i].dot.rectTransform.anchoredPosition = new Vector2(x, 22f);
            }
        }

        // ============================================================================ building the canvas

        void BuildUi()
        {
            GameObject canvasGo = new GameObject("Intro Canvas", typeof(RectTransform));
            canvasGo.layer = 5;   // UI
            canvasGo.transform.SetParent(transform, false);
            owned.Add(canvasGo);
            Canvas cv = canvasGo.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 32000;
            CanvasScaler sc = canvasGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 1f;   // 1080-high layout units, like the IMGUI HUD; the width follows the aspect ratio
            canvasRect = (RectTransform)canvasGo.transform;

            Font font = ResolveFont();
            Texture2D velvet = Own(IntroArt.Velvet(1024, 256, curtainFolds, curtainColor, curtainTrimColor, 11));
            glowTex = Own(IntroArt.Radial(128));

            // solid base in the HUD's fade colour: the intro starts and ends on it
            Stretch(Raw("Base", canvasRect, null, FadeColor).rectTransform);

            RectTransform contentRt = NewRect("Content", canvasRect);
            Stretch(contentRt);
            content = contentRt.gameObject.AddComponent<CanvasGroup>();
            content.alpha = 0f;
            content.interactable = false;
            content.blocksRaycasts = false;

            // ---- stage (revealed behind the curtains)
            Stretch(Raw("Stage Backdrop", contentRt, Own(IntroArt.VerticalGradient(new Color(0.006f, 0.006f, 0.016f, 1f),
                new Color(0.03f, 0.02f, 0.065f, 1f), new Color(0.012f, 0.012f, 0.035f, 1f))), Color.white).rectTransform);
            RectTransform stageRt = NewRect("Stage", contentRt);
            Stretch(stageRt);
            hazeM = Raw("Haze Magenta", stageRt, glowTex, Color.clear);
            hazeC = Raw("Haze Cyan", stageRt, glowTex, Color.clear);
            floorGrid = Raw("Floor Grid", stageRt, Own(IntroArt.Grid(1024, 384, neonCyan, neonMagenta)), Color.white);
            RectTransform fr = floorGrid.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 0f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.anchoredPosition = Vector2.zero;
            pool = Raw("Light Pool", stageRt, glowTex, Color.clear);
            bloomC = Raw("Title Bloom Cyan", stageRt, glowTex, Color.clear);
            bloomM = Raw("Title Bloom Magenta", stageRt, glowTex, Color.clear);

            // ---- title
            titleRoot = NewRect("Title", contentRt);
            titleRoot.anchoredPosition = new Vector2(0f, TitleY);
            titleRoot.sizeDelta = new Vector2(1800f, 700f);
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            titleGroup.interactable = false;
            titleGroup.blocksRaycasts = false;
            Texture2D fade = Own(IntroArt.FadeLine(256));
            divL = Raw("Divider Left", titleRoot, fade, Color.clear);
            divL.uvRect = new Rect(1f, 0f, -1f, 1f);
            divL.rectTransform.pivot = new Vector2(1f, 0.5f);
            divR = Raw("Divider Right", titleRoot, fade, Color.clear);
            divR.rectTransform.pivot = new Vector2(0f, 0.5f);

            welcome = NewText("Welcome", titleRoot, font, welcomeSize, welcomeText, new Color(0.9f, 0.97f, 1f, 0.95f), FontStyle.Normal);
            welcomeFx = welcome.gameObject.AddComponent<IntroTextFX>();
            welcomeFx.reveal = 0f;
            welcomeFx.stagger = 0.6f;
            welcomeFx.rise = 14f;
            welcomeFx.glowColor = neonCyan;
            welcomeFx.glowRadius = 5f;
            welcomeFx.glowRings = 2;
            welcomeFx.glowDirs = 8;

            underGlow = Raw("Underline Glow", titleRoot, glowTex, Color.clear);
            underline = Raw("Underline", titleRoot, Own(IntroArt.Gradient3(neonMagenta, Color.white, neonCyan)), Color.clear);

            title = NewText("Neon Breach", titleRoot, font, titleSize, titleText, Color.Lerp(neonCyan, Color.white, 0.55f), FontStyle.Bold);
            titleFx = title.gameObject.AddComponent<IntroTextFX>();
            titleFx.reveal = 0f;
            titleFx.flicker = 0f;
            titleFx.stagger = 0.3f;
            titleFx.glowColor = neonCyan;
            titleFx.glowRings = 3;
            titleFx.glowDirs = 12;
            titleFx.splitColor = neonMagenta;

            tagline = NewText("Tagline", titleRoot, font, 22, taglineText ?? "", new Color(0.82f, 0.88f, 1f, 0.72f), FontStyle.Normal);
            tagFx = tagline.gameObject.AddComponent<IntroTextFX>();
            tagFx.reveal = 0f;
            tagFx.stagger = 0.7f;
            tagFx.rise = 8f;
            tagline.gameObject.SetActive(!string.IsNullOrEmpty(taglineText));

            // ---- curtains and everything in front of them
            curtainR = NewCurtain("Curtain Right", contentRt, velvet, true);
            curtainL = NewCurtain("Curtain Left", contentRt, velvet, false);
            seamWide = Raw("Seam Light Wide", contentRt, glowTex, Color.clear);
            seamCore = Raw("Seam Light", contentRt, glowTex, Color.clear);

            beam = Raw("Spotlight Beam", contentRt, Own(IntroArt.Beam(128, 256)), Color.clear);
            RectTransform br = beam.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.anchoredPosition = new Vector2(0f, 10f);

            RectTransform dustRt = NewRect("Dust", contentRt);
            int nm = Mathf.Max(0, dustMotes);
            motes = new Mote[nm];
            for (int i = 0; i < nm; i++)
            {
                Mote mo;
                mo.img = Raw("Mote", dustRt, glowTex, Color.clear);
                mo.img.rectTransform.sizeDelta = Vector2.one * Random.Range(5.5f, 15f);
                mo.pos = new Vector2(Random.Range(-520f, 520f), Random.Range(-500f, 500f));
                mo.vel = new Vector2(Random.Range(-7f, 7f), Random.Range(5f, 16f));
                mo.tw = Random.Range(0.6f, 2.2f);
                mo.ph = Random.Range(0f, 6.28f);
                mo.baseA = Random.Range(0.25f, 0.85f);
                motes[i] = mo;
            }

            valance = Raw("Valance", contentRt, Own(IntroArt.Valance(512, 256, curtainColor * 0.92f, neonMagenta, neonCyan)), Color.white);
            RectTransform vr = valance.rectTransform;
            vr.anchorMin = new Vector2(0f, 1f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.pivot = new Vector2(0.5f, 1f);
            vr.anchoredPosition = Vector2.zero;
            vr.sizeDelta = new Vector2(0f, 168f);

            RectTransform lip = NewRect("Stage Lip", contentRt);
            lip.anchorMin = new Vector2(0f, 0f);
            lip.anchorMax = new Vector2(1f, 0f);
            lip.pivot = new Vector2(0.5f, 0f);
            lip.anchoredPosition = Vector2.zero;
            lip.sizeDelta = new Vector2(0f, 34f);
            Stretch(Raw("Lip", lip, Own(IntroArt.VerticalGradient(new Color(0f, 0f, 0f, 1f), new Color(0.03f, 0.02f, 0.05f, 1f),
                new Color(0.07f, 0.04f, 0.1f, 1f))), Color.white).rectTransform);
            lipGlow = Raw("Lip Glow", lip, glowTex, Color.clear);
            RectTransform lg = lipGlow.rectTransform;
            lg.anchorMin = new Vector2(0f, 1f);
            lg.anchorMax = new Vector2(1f, 1f);
            lg.anchoredPosition = Vector2.zero;
            lg.sizeDelta = new Vector2(0f, 46f);
            RawImage lipLine = Raw("Lip Neon", lip, null, WithA(neonCyan, 0.85f));
            RectTransform ll = lipLine.rectTransform;
            ll.anchorMin = new Vector2(0f, 1f);
            ll.anchorMax = new Vector2(1f, 1f);
            ll.pivot = new Vector2(0.5f, 1f);
            ll.anchoredPosition = Vector2.zero;
            ll.sizeDelta = new Vector2(0f, 2f);
            footRoot = NewRect("Footlights", lip);
            footRoot.anchorMin = footRoot.anchorMax = new Vector2(0.5f, 0f);
            footRoot.pivot = new Vector2(0.5f, 0f);
            footRoot.anchoredPosition = Vector2.zero;
            footRoot.sizeDelta = Vector2.zero;

            vignette = Raw("Vignette", contentRt, Own(IntroArt.Vignette(256)), Color.clear);
            Stretch(vignette.rectTransform);
            grain = Raw("Film Grain", contentRt, Own(IntroArt.Grain(256, 5)), Color.clear);
            Stretch(grain.rectTransform);

            BuildSkipButton(font);
        }

        void BuildSkipButton(Font font)
        {
            skipRt = NewRect("Skip Intro", canvasRect);
            skipRt.anchorMin = skipRt.anchorMax = new Vector2(1f, 0f);
            skipRt.pivot = new Vector2(1f, 0f);
            skipRt.anchoredPosition = new Vector2(-44f, 40f);
            skipRt.sizeDelta = new Vector2(236f, 50f);
            skipGroup = skipRt.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = 0f;
            skipGroup.interactable = false;
            skipGroup.blocksRaycasts = false;

            skipBg = Raw("Background", skipRt, null, new Color(0.02f, 0.05f, 0.09f, 0.5f));
            Stretch(skipBg.rectTransform);
            Texture2D frameTex;
            Sprite frame = IntroArt.Frame(out frameTex);
            Own(frameTex);
            Own(frame);
            skipFrame = NewImage("Frame", skipRt, frame);
            Stretch(skipFrame.rectTransform);

            skipLabel = NewText("Label", skipRt, font, 19, "SKIP INTRO", Color.white, FontStyle.Bold);
            Stretch(skipLabel.rectTransform);
            skipLabel.rectTransform.offsetMax = new Vector2(-64f, 0f);
            IntroTextFX lfx = skipLabel.gameObject.AddComponent<IntroTextFX>();
            lfx.tracking = 4f;
            lfx.stagger = 0f;

            RectTransform key = NewRect("Key", skipRt);
            key.anchorMin = key.anchorMax = new Vector2(1f, 0.5f);
            key.pivot = new Vector2(1f, 0.5f);
            key.anchoredPosition = new Vector2(-12f, 0f);
            key.sizeDelta = new Vector2(48f, 26f);
            keyFrame = NewImage("Key Frame", key, frame);
            Stretch(keyFrame.rectTransform);
            keyLabel = NewText("Key Label", key, font, 14, "ESC", Color.white, FontStyle.Bold);
            Stretch(keyLabel.rectTransform);
        }

        IntroCurtain NewCurtain(string n, Transform parent, Texture tex, bool mirror)
        {
            RectTransform rt = NewRect(n, parent);
            Stretch(rt);
            if (rt.GetComponent<CanvasRenderer>() == null) rt.gameObject.AddComponent<CanvasRenderer>();
            IntroCurtain c = rt.gameObject.AddComponent<IntroCurtain>();
            c.cloth = tex;
            c.mirror = mirror;
            c.raycastTarget = false;
            c.footColor = Color.Lerp(neonMagenta, curtainTrimColor, 0.5f) * 0.9f;
            return c;
        }

        Font ResolveFont()
        {
            if (titleFont != null) return titleFont;
            if (osFontNames != null && osFontNames.Length > 0)
            {
                if (osFonts == null)
                {
                    osFonts = new HashSet<string>();
                    string[] names = Font.GetOSInstalledFontNames();
                    if (names != null) for (int i = 0; i < names.Length; i++) osFonts.Add(names[i]);
                }
                // only ask for fonts that are installed, so a missing font never produces a warning
                for (int i = 0; i < osFontNames.Length; i++)
                {
                    string n = osFontNames[i];
                    if (string.IsNullOrEmpty(n) || !osFonts.Contains(n)) continue;
                    Font f = Font.CreateDynamicFontFromOSFont(n, 64);
                    if (f != null) { f.hideFlags = HideFlags.DontSave; return Own(f); }
                }
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ============================================================================ small helpers

        T Own<T>(T o) where T : Object { owned.Add(o); return o; }

        static RectTransform NewRect(string n, Transform parent)
        {
            GameObject go = new GameObject(n, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static RawImage Raw(string n, Transform parent, Texture tex, Color c)
        {
            RawImage img = NewRect(n, parent).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static Image NewImage(string n, Transform parent, Sprite sp)
        {
            Image img = NewRect(n, parent).gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(string n, Transform parent, Font f, int size, string s, Color c, FontStyle style)
        {
            RectTransform rt = NewRect(n, parent);
            rt.sizeDelta = new Vector2(1800f, size * 1.6f);
            Text tx = rt.gameObject.AddComponent<Text>();
            tx.font = f;
            tx.fontSize = size;
            tx.fontStyle = style;
            tx.text = s;
            tx.color = c;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.verticalOverflow = VerticalWrapMode.Overflow;
            tx.supportRichText = false;
            tx.raycastTarget = false;
            return tx;
        }

        static Color WithA(Color c, float a) { c.a = a; return c; }
        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float EaseOut(float x) { x = Mathf.Clamp01(x); float k = 1f - x; return 1f - k * k * k; }

        static float EvalCurve(AnimationCurve c, float x, bool easeOutFallback)
        {
            if (c == null || c.length == 0) return easeOutFallback ? EaseOut(x) : Smooth01(x);
            return c.Evaluate(x);
        }

        static float[] SampleCurve(AnimationCurve c, int n)
        {
            float[] a = new float[n];
            for (int i = 0; i < n; i++) a[i] = EvalCurve(c, i / (float)(n - 1), false);
            return a;
        }

        /// <summary>Heavy cloth: slow start, long glide, soft settle.</summary>
        static AnimationCurve DefaultOpenCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.35f, 0.3f, 1.45f, 1.45f), new Keyframe(1f, 1f, 0f, 0f));
        }

        /// <summary>Ease-out: quick start, gentle landing.</summary>
        static AnimationCurve DefaultRevealCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.4f), new Keyframe(1f, 1f, 0f, 0f));
        }
    }
}
