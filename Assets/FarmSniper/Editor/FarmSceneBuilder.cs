using System.Collections.Generic;
using System.IO;
using NeonBreach.Farm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// Builds the independent "Farm Sniper" level into Assets/Scenes/FarmSniper.unity:
    /// generates the farm (FarmWorldBuilder), bakes the rat NavMesh, saves every generated mesh, texture and material
    /// under Assets/FarmSniper/Generated, applies lighting settings (no baking needed) and adds the scene to Build
    /// Settings next to the existing scenes. Nothing outside Assets/FarmSniper and the new scene file is touched.
    /// Menu: Neon Breach > Farm Sniper.
    /// </summary>
    public static class FarmSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FarmSniper.unity";
        const string GenDir = "Assets/FarmSniper/Generated";

        [MenuItem("Neon Breach/Farm Sniper/Build Farm Scene (rebuild)", false, 100)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
        }

        [MenuItem("Neon Breach/Farm Sniper/Open Farm Scene", false, 101)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { BuildFromMenu(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Neon Breach/Farm Sniper/Play Farm Scene", false, 102)]
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

        /// <summary>Command line: -executeMethod NeonBreach.EditorTools.FarmSceneBuilder.BuildBatch</summary>
        public static void BuildBatch() { Build(false); }

        public static void Build(bool openAfter)
        {
            string previous = SceneManager.GetActiveScene().path;
            try
            {
                EditorUtility.DisplayProgressBar("Farm Sniper", "Preparing...", 0.05f);
                EnsureLayers();
                if (AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.DeleteAsset(GenDir);
                Directory.CreateDirectory(GenDir + "/Textures");
                Directory.CreateDirectory(GenDir + "/Materials");
                Directory.CreateDirectory("Assets/Scenes");
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Farm Sniper", "Generating the farm (terrain, crops, buildings)...", 0.2f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                FarmWorldBuilder.Result res = FarmWorldBuilder.Build(new FarmWorldBuilder.Settings());

                EditorUtility.DisplayProgressBar("Farm Sniper", "Baking the rat NavMesh...", 0.55f);
                BakeNavMesh(res.spawner);

                EditorUtility.DisplayProgressBar("Farm Sniper", "Saving generated textures, materials and meshes...", 0.75f);
                PersistAssets(res);
                ApplyLighting(res);

                EditorUtility.DisplayProgressBar("Farm Sniper", "Saving scene...", 0.92f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddToBuildSettings();
                AssetDatabase.SaveAssets();
                Debug.Log("[Farm Sniper] Scene built: " + ScenePath + " - open it and press Play (or Neon Breach > Farm Sniper > Play Farm Scene).");

                if (!openAfter && !string.IsNullOrEmpty(previous) && previous != ScenePath && File.Exists(previous))
                    EditorSceneManager.OpenScene(previous);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ navmesh

        static void BakeNavMesh(RatSpawner spawner)
        {
            if (spawner == null) return;
            Physics.SyncTransforms();
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(spawner.BuildBounds, spawner.navMeshLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                                                         new List<NavMeshBuildMarkup>(), sources);
            RatSpawner.AddNoWalkZones(sources);
            NavMeshBuildSettings s = spawner.BuildSettings();
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(s, sources, spawner.BuildBounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogWarning("[Farm Sniper] NavMesh bake failed - it will be built at runtime instead."); return; }
            data.name = "FarmRatNavMesh";
            AssetDatabase.CreateAsset(data, GenDir + "/FarmRatNavMesh.asset");
            spawner.navMeshData = data;
            EditorUtility.SetDirty(spawner);
        }

        // ------------------------------------------------------------------ assets

        static void PersistAssets(FarmWorldBuilder.Result res)
        {
            HashSet<Material> mats = new HashSet<Material>();
            HashSet<Mesh> meshes = new HashSet<Mesh>();
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                foreach (Renderer rend in roots[r].GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in rend.sharedMaterials)
                        if (m != null && !EditorUtility.IsPersistent(m)) mats.Add(m);
                // crop / grass chunks are grown by VegetationPlanter on load (HideAndDontSave) - only their materials are saved
                foreach (VegetationPlanter vp in roots[r].GetComponentsInChildren<VegetationPlanter>(true))
                {
                    if (vp.material != null && !EditorUtility.IsPersistent(vp.material)) mats.Add(vp.material);
                    if (vp.dryMaterial != null && !EditorUtility.IsPersistent(vp.dryMaterial)) mats.Add(vp.dryMaterial);
                }
                foreach (MeshFilter mf in roots[r].GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && !EditorUtility.IsPersistent(mf.sharedMesh) && (mf.gameObject.hideFlags & HideFlags.DontSave) == 0) meshes.Add(mf.sharedMesh);
                foreach (MeshCollider mc in roots[r].GetComponentsInChildren<MeshCollider>(true))
                    if (mc.sharedMesh != null && !EditorUtility.IsPersistent(mc.sharedMesh)) meshes.Add(mc.sharedMesh);
            }
            if (res.skybox != null) mats.Add(res.skybox);

            // textures -> PNG assets (compressed on import), then point the materials at the imported copies
            Dictionary<Texture, Texture> saved = new Dictionary<Texture, Texture>();
            HashSet<string> usedNames = new HashSet<string>();
            foreach (Material m in mats)
            {
                foreach (string prop in m.GetTexturePropertyNames())
                {
                    Texture t = m.GetTexture(prop);
                    if (t == null || EditorUtility.IsPersistent(t)) continue;
                    Texture asset;
                    if (!saved.TryGetValue(t, out asset))
                    {
                        asset = SaveTexture(t as Texture2D, usedNames);
                        saved[t] = asset;
                    }
                    if (asset != null) m.SetTexture(prop, asset);
                }
            }

            foreach (Material m in mats)
            {
                string n = Unique(Sanitize(m.name), usedNames);
                AssetDatabase.CreateAsset(m, GenDir + "/Materials/" + n + ".mat");
            }

            string meshPath = GenDir + "/FarmMeshes.asset";
            bool first = true;
            int count = 0;
            foreach (Mesh mesh in meshes)
            {
                if (first) { AssetDatabase.CreateAsset(mesh, meshPath); first = false; }
                else AssetDatabase.AddObjectToAsset(mesh, meshPath);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Farm Sniper] Saved " + saved.Count + " textures, " + mats.Count + " materials, " + count + " meshes to " + GenDir);
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
                imp.mipmapEnabled = true;
                imp.wrapMode = t.wrapMode;
                imp.filterMode = FilterMode.Trilinear;
                imp.anisoLevel = 8;
                imp.maxTextureSize = 2048;
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

        static void ApplyLighting(FarmWorldBuilder.Result res)
        {
            FarmWorldBuilder.ApplyRenderSettings(res.skybox, res.sun);
            // realtime lighting only: nothing to bake, so no lighting-data warnings and instant rebuilds
            LightingSettings ls = new LightingSettings();
            ls.name = "FarmLighting";
            ls.bakedGI = false;
            ls.realtimeGI = false;
            AssetDatabase.CreateAsset(ls, GenDir + "/FarmLighting.lighting");
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
