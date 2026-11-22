using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace NeonBreach
{
    /// <summary>
    /// Runtime optimisation helpers plus the live performance counters shown by the F4 overlay.
    ///
    /// All scenery is generated in code at runtime, so the editor's "Static" flag and build-time static batching
    /// never see it. <see cref="BatchStatic"/> does the same job at runtime: it merges every non-moving mesh under a
    /// root into a few big meshes per material, cutting draw calls (the arena walls, the archery range, the
    /// parachute island's 230 trees). Objects that move (targets, flags) are passed in <c>skip</c>. Off by default: see <see cref="BatchingEnabled"/>.
    /// </summary>
    public static class Perf
    {
        /// <summary>Total number of meshes merged by <see cref="BatchStatic"/> this session (shown in the overlay).</summary>
        public static int MeshesBatched { get; private set; }

        /// <summary>
        /// Off by default because we measured it (F4 overlay, same camera pose, Unity 6.5 Built-in forward path):
        /// runtime static batching RAISED draw calls - archery range 691 -> 812, arena 321 -> 453 - since batched meshes
        /// lose GPU instancing and every object lit by several per-pixel point lights is still drawn once per light.
        /// GPU instancing plus no shadows from neon strips gave the better result. Set true before a world is built to repeat the test.
        /// </summary>
        public static bool BatchingEnabled = false;

        /// <summary>Neon (emissive) parts cast no shadows - see <see cref="Util.Part"/>. A switch for A/B measurements.</summary>
        public static bool NeonCastNoShadows = true;

        /// <summary>Static-batch every MeshRenderer under <paramref name="root"/> except those under a skipped transform.</summary>
        public static int BatchStatic(GameObject root, params Transform[] skip)
        {
            if (root == null || !BatchingEnabled) return 0;
            List<GameObject> list = new List<GameObject>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter f = filters[i];
                if (f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                Renderer r = f.GetComponent<MeshRenderer>();
                if (r == null || r.isPartOfStaticBatch) continue;
                if (Skipped(f.transform, skip)) continue;
                list.Add(f.gameObject);
            }
            if (list.Count < 2) return 0;
            StaticBatchingUtility.Combine(list.ToArray(), root);
            MeshesBatched += list.Count;
            return list.Count;
        }

        /// <summary>
        /// Turn off shadow casting on every neon part under a root. <see cref="Util.Part"/> already does this for new
        /// parts; this also covers an arena that was saved into the scene before the rule existed.
        /// </summary>
        public static int NeonNoShadows(GameObject root)
        {
            if (root == null || !NeonCastNoShadows) return 0;
            int n = 0;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material m = rs[i].sharedMaterial;
                if (m == null || !m.name.StartsWith("Neon") || rs[i].shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                rs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                n++;
            }
            return n;
        }

        static bool Skipped(Transform t, Transform[] skip)
        {
            if (skip == null) return false;
            for (int i = 0; i < skip.Length; i++)
                if (skip[i] != null && t.IsChildOf(skip[i])) return true;
            return false;
        }

        // ------------------------------------------------------------------------------ live counters (F4 overlay)

        // Unity 6 splits the old "Draw Calls Count" / "Batches Count" into per-path counters; we sum them.
        static readonly string[] DrawNames = { "Standard Draw Calls Count", "Standard Instanced Draw Calls Count", "Standard Indirect Draw Calls Count", "Draw Calls Count" };
        static ProfilerRecorder[] draws = new ProfilerRecorder[0];
        static ProfilerRecorder setPass, tris, verts, staticBatches, staticDraws, instBatches, instDraws, shadowCasters, mainThread;
        static bool recording;
        static float frameAvg, frameMax, frameMaxWindow, windowTimer;
        static int sceneObjects, sceneRenderers, sceneLights;

        static ProfilerRecorder Rec(string name) { return ProfilerRecorder.StartNew(ProfilerCategory.Render, name); }

        public static void StartRecording()
        {
            if (recording) return;
            draws = new ProfilerRecorder[DrawNames.Length];
            for (int i = 0; i < DrawNames.Length; i++) draws[i] = Rec(DrawNames[i]);
            setPass = Rec("SetPass Calls Count");
            tris = Rec("Triangles Count");
            verts = Rec("Vertices Count");
            staticBatches = Rec("Static Batches Count");
            staticDraws = Rec("Static Batched Draw Calls Count");
            instBatches = Rec("Instanced Batches Count");
            instDraws = Rec("Instanced Batched Draw Calls Count");
            shadowCasters = Rec("Shadow Casters Count");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            recording = true;
            windowTimer = 1f;
        }

        public static void StopRecording()
        {
            if (!recording) return;
            for (int i = 0; i < draws.Length; i++) draws[i].Dispose();
            setPass.Dispose(); tris.Dispose(); verts.Dispose(); staticBatches.Dispose(); staticDraws.Dispose();
            instBatches.Dispose(); instDraws.Dispose(); shadowCasters.Dispose(); mainThread.Dispose();
            recording = false;
        }

        /// <summary>Draw calls in the last frame (all paths), or -1 if the counters are unavailable (release player).</summary>
        public static long DrawCalls
        {
            get
            {
                long sum = 0; bool any = false;
                for (int i = 0; i < draws.Length; i++)
                    if (draws[i].Valid) { sum += draws[i].LastValue; any = true; }
                return any && sum > 0 ? sum : -1;
            }
        }

        public static long SetPassCalls { get { return setPass.Valid ? setPass.LastValue : -1; } }

        /// <summary>Called once per frame by the HUD while the overlay is visible.</summary>
        public static void Tick(float unscaledDt)
        {
            float ms = unscaledDt * 1000f;
            frameAvg = frameAvg <= 0f ? ms : Mathf.Lerp(frameAvg, ms, 0.05f);
            frameMaxWindow = Mathf.Max(frameMaxWindow, ms);
            windowTimer += unscaledDt;
            if (windowTimer >= 1f)
            {
                windowTimer = 0f;
                frameMax = frameMaxWindow;
                frameMaxWindow = 0f;
                // object counts: cheap enough once a second
                sceneObjects = Object.FindObjectsByType<Transform>().Length;
                Renderer[] rs = Object.FindObjectsByType<Renderer>();
                int vis = 0;
                for (int i = 0; i < rs.Length; i++) if (rs[i].isVisible) vis++;
                sceneRenderers = vis;
                sceneLights = Object.FindObjectsByType<Light>().Length;
            }
        }

        static string Count(ProfilerRecorder r)
        {
            return Count(r.Valid ? r.LastValue : -1);
        }

        static string Count(long v)
        {
            if (v < 0) return "n/a";
            if (v >= 1000000) return (v / 1000000f).ToString("0.00") + " M";
            if (v >= 10000) return (v / 1000f).ToString("0.0") + " k";
            return v.ToString();
        }

        static double AverageMs(ProfilerRecorder r)
        {
            int n = r.Count;
            if (n == 0) return 0;
            double sum = 0;
            for (int i = 0; i < n; i++) sum += r.GetSample(i).Value;
            return sum / n * 1e-6;   // nanoseconds -> ms
        }

        /// <summary>Lines for the overlay (label, value).</summary>
        public static void Lines(List<KeyValuePair<string, string>> outLines)
        {
            outLines.Clear();
            float fps = frameAvg > 0f ? 1000f / frameAvg : 0f;
            outLines.Add(new KeyValuePair<string, string>("FPS", fps.ToString("0")));
            outLines.Add(new KeyValuePair<string, string>("Frame time", frameAvg.ToString("0.0") + " ms (worst " + frameMax.ToString("0.0") + ")"));
            double cpu = recording ? AverageMs(mainThread) : 0;
            outLines.Add(new KeyValuePair<string, string>("CPU main thread", cpu > 0 ? cpu.ToString("0.0") + " ms" : "n/a"));
            outLines.Add(new KeyValuePair<string, string>("Draw calls", Count(DrawCalls)));
            outLines.Add(new KeyValuePair<string, string>("SetPass calls", Count(setPass)));
            outLines.Add(new KeyValuePair<string, string>("Static batches (draws)", Count(staticBatches) + " (" + Count(staticDraws) + ")"));
            outLines.Add(new KeyValuePair<string, string>("Instanced batches (draws)", Count(instBatches) + " (" + Count(instDraws) + ")"));
            outLines.Add(new KeyValuePair<string, string>("Shadow casters", Count(shadowCasters)));
            outLines.Add(new KeyValuePair<string, string>("Triangles / vertices", Count(tris) + " / " + Count(verts)));
            outLines.Add(new KeyValuePair<string, string>("Visible renderers", sceneRenderers.ToString()));
            outLines.Add(new KeyValuePair<string, string>("Scene objects / lights", sceneObjects + " / " + sceneLights));
            outLines.Add(new KeyValuePair<string, string>("Runtime static batching", BatchingEnabled ? "on (" + MeshesBatched + " meshes)" : "off"));
            long mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            long total = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            outLines.Add(new KeyValuePair<string, string>("Memory (managed / total)", (mono >> 20) + " / " + (total >> 20) + " MB"));
            outLines.Add(new KeyValuePair<string, string>("Quality level", QualitySettings.names.Length > 0 ? QualitySettings.names[QualitySettings.GetQualityLevel()] : "-"));
        }
    }
}
