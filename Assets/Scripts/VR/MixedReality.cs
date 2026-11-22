using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace NeonBreach
{
    /// <summary>
    /// Mixed-reality passthrough mode (Meta Quest via OpenXR + AR Foundation + Unity's Meta OpenXR package).
    ///  - The camera clears to transparent black and an ARCameraManager turns on the headset's passthrough feed,
    ///    so the player sees their real room.
    ///  - The neon arena is hidden; the real floor and the guardian/boundary polygon (XRInputSubsystem) become
    ///    invisible colliders, and a NavMesh is built for the room.
    ///  - Enemies spawn along the room's boundary ("breaching through your walls"), fewer at a time, and drones fly lower.
    ///  - Artificial locomotion and turning are disabled: you move by physically walking, so the virtual and real
    ///    rooms always stay aligned.
    /// Without a passthrough-capable headset the background is simply black.
    /// </summary>
    public static class MixedReality
    {
        public static bool Active { get; private set; }
        public static Transform[] SpawnPoints { get; private set; }
        public static Vector2 RoomSize { get; private set; }
        public static bool UsedRealBoundary { get; private set; }

        static GameObject room;
        static GameObject session;
        static ARCameraManager camManager;
        static readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        static readonly List<Collider> hiddenColliders = new List<Collider>();
        static readonly List<Light> dimmedLights = new List<Light>();
        static bool savedFog;
        static CameraClearFlags savedClear;
        static Color savedBg;

        public static void Enable(ArenaInfo arena)
        {
            if (Active) return;
            Active = true;
            GameManager gm = GameManager.Instance;
            Camera cam = gm != null && gm.Player != null ? gm.Player.cam : Camera.main;

            // hide the virtual arena (keep its lights so enemies are still lit)
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
            dimmedLights.Clear();
            if (arena != null)
            {
                Renderer[] rs = arena.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < rs.Length; i++) if (rs[i].enabled) { rs[i].enabled = false; hiddenRenderers.Add(rs[i]); }
                Collider[] cs = arena.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cs.Length; i++) if (cs[i].enabled) { cs[i].enabled = false; hiddenColliders.Add(cs[i]); }
                Light[] ls = arena.GetComponentsInChildren<Light>();
                for (int i = 0; i < ls.Length; i++) if (ls[i].type == LightType.Directional) { ls[i].shadows = LightShadows.None; dimmedLights.Add(ls[i]); }
            }
            savedFog = RenderSettings.fog;
            RenderSettings.fog = false;

            BuildRoom();

            // passthrough
            if (cam != null)
            {
                savedClear = cam.clearFlags;
                savedBg = cam.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camManager = cam.GetComponent<ARCameraManager>();
                if (camManager == null) camManager = cam.gameObject.AddComponent<ARCameraManager>();
                camManager.enabled = true;
            }
            if (session == null)
            {
                session = new GameObject("AR Session");
                session.AddComponent<ARSession>();
            }
            session.SetActive(true);
        }

        public static void Disable()
        {
            if (!Active) return;
            Active = false;
            for (int i = 0; i < hiddenRenderers.Count; i++) if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = true;
            for (int i = 0; i < hiddenColliders.Count; i++) if (hiddenColliders[i] != null) hiddenColliders[i].enabled = true;
            for (int i = 0; i < dimmedLights.Count; i++) if (dimmedLights[i] != null) dimmedLights[i].shadows = LightShadows.Soft;
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
            dimmedLights.Clear();
            RenderSettings.fog = savedFog;

            GameManager gm = GameManager.Instance;
            Camera cam = gm != null && gm.Player != null ? gm.Player.cam : Camera.main;
            if (cam != null)
            {
                cam.clearFlags = savedClear;
                cam.backgroundColor = savedBg.a > 0f ? savedBg : new Color(0.02f, 0.03f, 0.07f, 1f);
            }
            if (camManager != null) camManager.enabled = false;
            if (session != null) session.SetActive(false);
            if (room != null) { room.SetActive(false); Object.Destroy(room); room = null; }
            SpawnPoints = null;
        }

        /// <summary>Read the guardian / play-area polygon in tracking space (falls back to a 3.5 m square).</summary>
        static List<Vector3> RoomPolygon()
        {
            List<Vector3> pts = new List<Vector3>();
            List<XRInputSubsystem> subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            for (int i = 0; i < subs.Count; i++)
            {
                if (subs[i] == null || !subs[i].running) continue;
                pts.Clear();
                if (subs[i].TryGetBoundaryPoints(pts) && pts.Count >= 3) { UsedRealBoundary = true; return pts; }
            }
            UsedRealBoundary = false;
            pts.Clear();
            float h = 1.75f;
            pts.Add(new Vector3(-h, 0f, -h)); pts.Add(new Vector3(h, 0f, -h));
            pts.Add(new Vector3(h, 0f, h)); pts.Add(new Vector3(-h, 0f, h));
            return pts;
        }

        static void BuildRoom()
        {
            room = new GameObject("MR Room");
            List<Vector3> poly = RoomPolygon();
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < poly.Count; i++) centroid += poly[i];
            centroid /= poly.Count;
            centroid.y = 0f;

            // invisible real floor
            GameObject floor = new GameObject("RealFloor");
            floor.transform.SetParent(room.transform, false);
            floor.layer = Layers.Environment;
            BoxCollider fc = floor.AddComponent<BoxCollider>();
            fc.center = new Vector3(centroid.x, -0.5f, centroid.z);
            fc.size = new Vector3(40f, 1f, 40f);

            // invisible walls along the boundary
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 a = poly[i];
                Vector3 b = poly[(i + 1) % poly.Count];
                a.y = 0f; b.y = 0f;
                min = Vector2.Min(min, new Vector2(a.x, a.z));
                max = Vector2.Max(max, new Vector2(a.x, a.z));
                Vector3 mid = (a + b) * 0.5f;
                Vector3 edge = b - a;
                float len = edge.magnitude;
                if (len < 0.05f) continue;
                Vector3 outward = mid - centroid;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
                GameObject w = new GameObject("RealWall" + i);
                w.transform.SetParent(room.transform, false);
                w.layer = Layers.Environment;
                w.transform.position = mid + outward * 0.15f + Vector3.up * 1.5f;
                w.transform.rotation = Quaternion.LookRotation(edge.normalized, Vector3.up);
                BoxCollider bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.3f, 3f, len + 0.3f);
            }
            RoomSize = max - min;

            // spawn points spread along the boundary, pulled 0.5 m into the room
            List<Transform> spawns = new List<Transform>();
            float perimeter = 0f;
            for (int i = 0; i < poly.Count; i++) perimeter += Vector3.Distance(poly[i], poly[(i + 1) % poly.Count]);
            int count = 12;
            for (int k = 0; k < count; k++)
            {
                float d = perimeter * k / count;
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector3 a = poly[i], b = poly[(i + 1) % poly.Count];
                    float len = Vector3.Distance(a, b);
                    if (d > len) { d -= len; continue; }
                    Vector3 p = Vector3.Lerp(a, b, len > 0.001f ? d / len : 0f);
                    p.y = 0f;
                    Vector3 inward = centroid - p;
                    inward.y = 0f;
                    if (inward.sqrMagnitude > 0.0001f) p += inward.normalized * Mathf.Min(0.5f, inward.magnitude * 0.4f);
                    GameObject s = new GameObject("RoomSpawn" + k);
                    s.transform.SetParent(room.transform, false);
                    s.transform.position = p + Vector3.up * 0.05f;
                    spawns.Add(s.transform);
                    break;
                }
            }
            SpawnPoints = spawns.ToArray();
        }
    }
}
