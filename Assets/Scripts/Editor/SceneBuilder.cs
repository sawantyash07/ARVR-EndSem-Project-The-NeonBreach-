using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// Bakes the whole game into a real Unity scene: physics layers, material assets, the neon-grid texture,
    /// arena, lights, player rig and managers. Runs automatically the first time the project is opened
    /// and can be re-run from the "Neon Breach" menu at any time.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/NeonBreach.unity";
        const string MatDir = "Assets/Resources/Materials";
        const string TexDir = "Assets/Resources/Textures";

        [MenuItem("Neon Breach/Build Game Scene (rebuild)")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
        }

        /// <summary>Command line: -executeMethod NeonBreach.EditorTools.SceneBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build(false);
            Debug.Log("[Neon Breach] Batch scene build finished.");
        }

        /// <summary>Command line: rebuild the scene and build a Windows player into Builds/SmokeTest for automated testing.</summary>
        public static void BuildSmokePlayer()
        {
            Build(false);
            UnityEditor.Build.Reporting.BuildReport r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/SmokeTest/NeonBreach.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log("[Neon Breach] Smoke player build: " + r.summary.result + " errors=" + r.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Neon Breach/Play From Start")]
        public static void PlayFromStart()
        {
            if (!File.Exists(ScenePath)) Build(true);
            else if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            EditorApplication.isPlaying = true;
        }

        public static void Build(bool openAfter)
        {
            EditorUtility.DisplayProgressBar("Neon Breach", "Preparing project...", 0.1f);
            try
            {
                EnsureLayers();
                Directory.CreateDirectory(MatDir);
                Directory.CreateDirectory(TexDir);
                Directory.CreateDirectory("Assets/Scenes");
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("Neon Breach", "Generating textures and materials...", 0.3f);
                Texture2D grid = CreateGridTexture();
                CreateMaterials(grid);
                Mats.ClearCache();

                EditorUtility.DisplayProgressBar("Neon Breach", "Building arena, player and managers...", 0.6f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                WorldBuilder.BuildAll();
                EnsureModeManagers();
                EditorSceneManager.SaveScene(scene, ScenePath);

                // Neon Breach stays scene 0; other levels already in Build Settings (e.g. Farm Sniper) are kept
                System.Collections.Generic.List<EditorBuildSettingsScene> scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                    if (s.path != ScenePath && File.Exists(s.path)) scenes.Add(s);
                EditorBuildSettings.scenes = scenes.ToArray();
                PlayerSettings.productName = "Neon Breach";
                PlayerSettings.companyName = "COEP Tech";

                AssetDatabase.SaveAssets();
                Debug.Log("[Neon Breach] Scene built: " + ScenePath + "  -  press Play!");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// The game-mode managers (ModeManager + WindSystem + ArcheryMode + ParachuteMode) live on the GameManager object.
        /// WorldBuilder already adds them; this makes sure a rebuilt scene always has them. The archery range and the
        /// parachute island are generated at runtime when their mode starts, so they are not baked into the scene.
        /// </summary>
        static void EnsureModeManagers()
        {
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null) { Debug.LogError("[Neon Breach] No GameManager in the built scene."); return; }
            GameObject go = gm.gameObject;
            if (go.GetComponent<WindSystem>() == null) go.AddComponent<WindSystem>();
            if (go.GetComponent<ArcheryMode>() == null) go.AddComponent<ArcheryMode>();
            if (go.GetComponent<ParachuteMode>() == null) go.AddComponent<ParachuteMode>();
            if (go.GetComponent<ModeManager>() == null) go.AddComponent<ModeManager>();
        }

        static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[Neon Breach] TagManager not found - layer names not set (the game still works).");
                return;
            }
            SerializedObject tm = new SerializedObject(assets[0]);
            SerializedProperty layers = tm.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(Layers.Player + i);
                if (sp.stringValue != Layers.Names[i]) sp.stringValue = Layers.Names[i];
            }
            tm.ApplyModifiedProperties();
        }

        static Texture2D CreateGridTexture()
        {
            string path = TexDir + "/NeonGrid.png";
            Texture2D src = ProcTex.Grid(256);
            File.WriteAllBytes(path, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void CreateMaterials(Texture2D grid)
        {
            for (int i = 0; i < Mats.Defs.Count; i++)
            {
                Mats.Def d = Mats.Defs[i];
                string path = MatDir + "/" + d.name + ".mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
                Material m = Mats.Build(d);
                if (d.name == "Floor" && grid != null) Mats.ApplyGrid(m, grid);
                AssetDatabase.CreateAsset(m, path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
