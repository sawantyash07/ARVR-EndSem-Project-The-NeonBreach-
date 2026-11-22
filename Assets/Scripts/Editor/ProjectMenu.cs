using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace NeonBreach.EditorTools
{
    /// <summary>Extra menu items under "Neon Breach": VR / OpenXR setup, standalone build, utilities.</summary>
    public static class ProjectMenu
    {
        const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";

        [MenuItem("Neon Breach/VR/1 - Open XR Plug-in Management")]
        static void OpenXrSettings()
        {
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
            Debug.Log("[Neon Breach] Tick 'OpenXR' on the PC tab (and the Android tab for Quest), then run 'Neon Breach > VR > 2 - Configure OpenXR features'.");
        }

        /// <summary>
        /// One-click OpenXR setup: assigns the OpenXR loader (PC + Android) and enables controller profiles,
        /// hand tracking (XR Hands) and, for Quest, the Meta session + camera (passthrough) features.
        /// </summary>
        [MenuItem("Neon Breach/VR/2 - Configure OpenXR features (controllers, hands, Quest passthrough)")]
        static void ConfigureOpenXr()
        {
            int changed = 0;
            BuildTargetGroup[] groups = { BuildTargetGroup.Standalone, BuildTargetGroup.Android };
            foreach (BuildTargetGroup g in groups)
            {
                XRGeneralSettings general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(g);
                if (general == null || general.AssignedSettings == null)
                {
                    Debug.LogWarning("[Neon Breach] No XR settings for " + g + " yet. Open 'Neon Breach > VR > 1 - Open XR Plug-in Management' once, then run this again.");
                    continue;
                }
                if (XRPackageMetadataStore.AssignLoader(general.AssignedSettings, OpenXRLoader, g)) changed++;

                OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
                if (settings == null) continue;
                foreach (OpenXRFeature f in settings.GetFeatures())
                {
                    if (f == null) continue;
                    string n = f.GetType().Name;
                    bool want =
                        n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" ||
                        n == "MetaQuestTouchProControllerProfile" || n == "ValveIndexControllerProfile" ||
                        n == "HTCViveControllerProfile" || n == "HandTracking" || n == "MetaHandTrackingAim" ||
                        (g == BuildTargetGroup.Android && (n == "MetaQuestFeature" || n == "ARSessionFeature" || n == "ARCameraFeature"));
                    if (want && !f.enabled)
                    {
                        f.enabled = true;
                        EditorUtility.SetDirty(f);
                        changed++;
                    }
                }
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Neon Breach] OpenXR configured (" + changed + " changes). Check 'Project Validation' in XR Plug-in Management for any remaining fixes.");
        }

        [MenuItem("Neon Breach/Build Standalone Player")]
        static void BuildPlayer()
        {
            if (!File.Exists(SceneBuilder.ScenePath)) SceneBuilder.Build(false);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string ext = target == BuildTarget.StandaloneWindows64 ? ".exe" : (target == BuildTarget.StandaloneOSX ? ".app" : (target == BuildTarget.Android ? ".apk" : ""));
            string path = "Builds/NeonBreach/NeonBreach" + ext;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            BuildPlayerOptions opts = new BuildPlayerOptions();
            opts.scenes = new[] { SceneBuilder.ScenePath };
            opts.locationPathName = path;
            opts.target = target;
            opts.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Neon Breach] Build OK: " + path);
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError("[Neon Breach] Build failed: " + report.summary.result);
            }
        }

        [MenuItem("Neon Breach/Reset High Score + Stats")]
        static void ResetHighScore()
        {
            PlayerPrefs.DeleteKey("nb_high");
            PlayerPrefs.DeleteKey("nb_board_v2");
            PlayerPrefs.DeleteKey("nb_lifetime_v1");
            for (int i = 0; i < 2; i++) PlayerPrefs.DeleteKey("nb_deaths_" + i);
            PlayerPrefs.DeleteKey("nb_deaths_100");
            PlayerPrefs.Save();
            Debug.Log("[Neon Breach] High score, leaderboard, lifetime stats and heatmaps reset.");
        }

        [MenuItem("Neon Breach/Replay Tutorial On Next Start")]
        static void ResetTutorial()
        {
            PlayerPrefs.SetInt("nb_tut_done", 0);
            PlayerPrefs.Save();
            Debug.Log("[Neon Breach] The tutorial will run the next time you press START.");
        }
    }
}
