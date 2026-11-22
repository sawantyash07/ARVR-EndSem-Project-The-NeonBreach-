using System.Collections.Generic;
using System.IO;
using NeonBreach.RatShooter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// Builds the independent "Rat Shooter" level into Assets/Scenes/RatShooter.unity: generates the cartoon farm and
    /// gameplay objects (RatWorldBuilder), bakes the mouse NavMesh, saves every generated texture / material / mesh under
    /// Assets/RatShooter/Generated, saves the five mouse prefabs and the rifle prefab under Assets/RatShooter/Prefabs,
    /// applies realtime-only lighting settings and adds the scene to Build Settings after the existing scenes.
    /// Nothing outside Assets/RatShooter and the new scene file is touched. Menu: Neon Breach > Rat Shooter.
    /// </summary>
    public static class RatShooterSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/RatShooter.unity";
        const string GenDir = "Assets/RatShooter/Generated";
        const string PrefabDir = "Assets/RatShooter/Prefabs";

        [MenuItem("Neon Breach/Rat Shooter/Build Rat Shooter Scene (rebuild)", false, 120)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
        }

        [MenuItem("Neon Breach/Rat Shooter/Open Rat Shooter Scene", false, 121)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { BuildFromMenu(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Neon Breach/Rat Shooter/Play Rat Shooter Scene", false, 122)]
        public static void PlayScene()
        {
            if (!File.Exists(ScenePath)) Build(true);
            else if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            EditorApplication.isPlaying = true;
        }

        /// <summary>Command line: -executeMethod NeonBreach.EditorTools.RatShooterSceneBuilder.BuildBatch</summary>
        public static void BuildBatch() { Build(false); }

        public static void Build(bool openAfter)
        {
            string previous = SceneManager.GetActiveScene().path;
            try
            {
                EditorUtility.DisplayProgressBar("Rat Shooter", "Preparing...", 0.05f);
                EnsureLayers();
                if (AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.DeleteAsset(GenDir);
                if (AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.DeleteAsset(PrefabDir);
                Directory.CreateDirectory(GenDir + "/Textures");
                Directory.CreateDirectory(GenDir + "/Materials");
                Directory.CreateDirectory(PrefabDir);
                Directory.CreateDirectory("Assets/Scenes");
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Rat Shooter", "Generating the cartoon farm...", 0.2f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RatGen.Clear();
                RatWorldBuilder.Result res = RatWorldBuilder.Build();

                EditorUtility.DisplayProgressBar("Rat Shooter", "Building the cartoon mice...", 0.4f);
                List<GameObject> protos = new List<GameObject>();
                MouseVariant[] variants = res.spawner.variants;
                for (int i = 0; i < variants.Length; i++)
                {
                    GameObject m = MouseFactory.Build(variants[i]);
                    m.transform.position = new Vector3(i * 2f, -200f, 0f);
                    protos.Add(m);
                }

                EditorUtility.DisplayProgressBar("Rat Shooter", "Baking the mouse NavMesh...", 0.55f);
                BakeNavMesh(res.spawner);

                EditorUtility.DisplayProgressBar("Rat Shooter", "Saving generated textures, materials and meshes...", 0.7f);
                PersistAssets();

                EditorUtility.DisplayProgressBar("Rat Shooter", "Saving prefabs...", 0.85f);
                for (int i = 0; i < protos.Count; i++)
                {
                    protos[i].transform.position = Vector3.zero;
                    string path = PrefabDir + "/CartoonMouse_" + variants[i].id + ".prefab";
                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(protos[i], path);
                    variants[i].prefab = prefab;
                    Object.DestroyImmediate(protos[i]);
                }
                EditorUtility.SetDirty(res.spawner);
                PrefabUtility.SaveAsPrefabAssetAndConnect(res.gun.gameObject, PrefabDir + "/CartoonRifle.prefab", InteractionMode.AutomatedAction);

                ApplyLighting(res);

                EditorUtility.DisplayProgressBar("Rat Shooter", "Saving scene...", 0.95f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings();
                AssetDatabase.SaveAssets();
                Debug.Log("[Rat Shooter] Scene built: " + ScenePath + " - press Play in it, or choose RAT SHOOTER on the Neon Breach main menu.");

                if (!openAfter && !string.IsNullOrEmpty(previous) && previous != ScenePath && File.Exists(previous))
                    EditorSceneManager.OpenScene(previous);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ navmesh

        static void BakeNavMesh(MouseSpawner spawner)
        {
            Physics.SyncTransforms();
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(spawner.BuildBounds, spawner.navMeshLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                                                         new List<NavMeshBuildMarkup>(), sources);
            NavMeshBuildSettings s = spawner.BuildSettings();
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(s, sources, spawner.BuildBounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogWarning("[Rat Shooter] NavMesh bake failed - it will be built at runtime instead."); return; }
            data.name = "RatMouseNavMesh";
            AssetDatabase.CreateAsset(data, GenDir + "/RatMouseNavMesh.asset");
            spawner.navMeshData = data;
            EditorUtility.SetDirty(spawner);
        }

        // ------------------------------------------------------------------ assets

        static void PersistAssets()
        {
            HashSet<string> used = new HashSet<string>();
            Dictionary<Texture, Texture> saved = new Dictionary<Texture, Texture>();
            List<Material> mats = new List<Material>();
            List<Mesh> meshes = new List<Mesh>();
            foreach (Object o in RatGen.Created)
            {
                if (o == null || EditorUtility.IsPersistent(o)) continue;
                if (o is Material) mats.Add((Material)o);
                else if (o is Mesh) meshes.Add((Mesh)o);
                else if (o is Texture2D && !saved.ContainsKey((Texture2D)o)) saved[(Texture2D)o] = SaveTexture((Texture2D)o, used);
            }

            foreach (Material m in mats)
            {
                foreach (string prop in m.GetTexturePropertyNames())
                {
                    Texture t = m.GetTexture(prop);
                    if (t == null || EditorUtility.IsPersistent(t)) continue;
                    Texture asset;
                    if (!saved.TryGetValue(t, out asset)) { asset = SaveTexture(t as Texture2D, used); saved[t] = asset; }
                    if (asset != null) m.SetTexture(prop, asset);
                }
                AssetDatabase.CreateAsset(m, GenDir + "/Materials/" + Unique(Sanitize(m.name), used) + ".mat");
            }

            string meshPath = GenDir + "/RatMeshes.asset";
            bool first = true;
            foreach (Mesh mesh in meshes)
            {
                // big environment meshes are stored compressed (keeps the repository small); small detailed ones stay exact
                if (mesh.vertexCount > 3000) MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);
                if (first) { AssetDatabase.CreateAsset(mesh, meshPath); first = false; }
                else AssetDatabase.AddObjectToAsset(mesh, meshPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Rat Shooter] Saved " + saved.Count + " textures, " + mats.Count + " materials, " + meshes.Count + " meshes to " + GenDir);
        }

        static Texture SaveTexture(Texture2D t, HashSet<string> used)
        {
            if (t == null) return null;
            string n = Unique(Sanitize(t.name), used);
            string path = GenDir + "/Textures/" + n + ".png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.alphaIsTransparency = t.wrapMode == TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.wrapMode = t.wrapMode;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.maxTextureSize = 1024;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) s = "Asset";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static string Unique(string n, HashSet<string> used)
        {
            string u = n;
            int i = 1;
            while (used.Contains(u)) u = n + "_" + (i++);
            used.Add(u);
            return u;
        }

        // ------------------------------------------------------------------ lighting + settings

        static void ApplyLighting(RatWorldBuilder.Result res)
        {
            RatEnvironment env = Object.FindAnyObjectByType<RatEnvironment>();
            if (env != null) env.Apply();
            // realtime lighting only: nothing to bake, no lighting-data warnings, instant rebuilds
            LightingSettings ls = new LightingSettings();
            ls.name = "RatLighting";
            ls.bakedGI = false;
            ls.realtimeGI = false;
            AssetDatabase.CreateAsset(ls, GenDir + "/RatLighting.lighting");
            Lightmapping.lightingSettings = ls;
        }

        static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool has = false;
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == ScenePath) { scenes[i] = new EditorBuildSettingsScene(ScenePath, true); has = true; }
            if (!has) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Same layer names as the Neon Breach builder (Player 8, Enemy 9, Environment 10, ...); only fills them in if missing.</summary>
        static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            SerializedObject tm = new SerializedObject(assets[0]);
            SerializedProperty layers = tm.FindProperty("layers");
            bool changed = false;
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(Layers.Player + i);
                if (string.IsNullOrEmpty(sp.stringValue)) { sp.stringValue = Layers.Names[i]; changed = true; }
            }
            if (changed) tm.ApplyModifiedProperties();
        }
    }
}
