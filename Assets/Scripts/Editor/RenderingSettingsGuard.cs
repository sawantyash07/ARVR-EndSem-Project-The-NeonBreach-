using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace NeonBreach.EditorTools
{
    /// <summary>
    /// Keeps the project on GPU instancing instead of the deprecated Dynamic Batching:
    /// - Dynamic Batching is switched off (Project Settings > Player > Other Settings) for the active build target when
    ///   the Editor loads and whenever the build target changes, so Unity's "Dynamic Batching is deprecated" warning
    ///   does not come back (Standalone, Android, iOS and WebGL are also stored off in ProjectSettings.asset).
    ///   Static batching is left exactly as it is.
    /// - GPU instancing is enabled on every Standard material in Assets/Resources/Materials.
    /// All generated runtime Standard materials already enable instancing (Mats.Build).
    /// By hand: Neon Breach > Rendering > Apply GPU Instancing + No Dynamic Batching.
    ///
    /// Unity 6.5 marks the public Get/SetDynamicBatchingForPlatform as obsolete (calling them would itself add
    /// console warnings), so the batching flags are read and written through PlayerSettings' internal
    /// Get/SetBatchingForPlatform. If a future Unity removes them (i.e. removes Dynamic Batching), this does nothing.
    /// </summary>
    [InitializeOnLoad]
    public class RenderingSettingsGuard : IActiveBuildTargetChanged
    {
        const string MaterialFolder = "Assets/Resources/Materials";
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        static RenderingSettingsGuard()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        public int callbackOrder { get { return 0; } }

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget) { Apply(false); }

        [MenuItem("Neon Breach/Rendering/Apply GPU Instancing + No Dynamic Batching")]
        static void ApplyFromMenu() { Apply(true); }

        static void Apply(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int changed = 0;

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (DisableDynamicBatching(target)) changed++;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (m == null || m.enableInstancing || m.shader == null || m.shader.name != "Standard") continue;
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                changed++;
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            if (verbose || changed > 0)
                Debug.Log("[Neon Breach] Rendering: Dynamic Batching off for " + target + ", GPU instancing on for project materials (" + changed + " change" + (changed == 1 ? "" : "s") + ").");
        }

        /// <summary>Turns Dynamic Batching off for one build target, keeping its static batching flag. True if it changed.</summary>
        static bool DisableDynamicBatching(BuildTarget target)
        {
            MethodInfo get = typeof(PlayerSettings).GetMethod("GetBatchingForPlatform", Flags, null,
                                                              new[] { typeof(BuildTarget), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, null);
            MethodInfo set = typeof(PlayerSettings).GetMethod("SetBatchingForPlatform", Flags, null,
                                                              new[] { typeof(BuildTarget), typeof(int), typeof(int) }, null);
            if (get == null || set == null) return false;
            object[] args = { target, 0, 0 };
            get.Invoke(null, args);
            int staticBatching = (int)args[1], dynamicBatching = (int)args[2];
            if (dynamicBatching == 0) return false;
            set.Invoke(null, new object[] { target, staticBatching, 0 });
            return true;
        }
    }
}
