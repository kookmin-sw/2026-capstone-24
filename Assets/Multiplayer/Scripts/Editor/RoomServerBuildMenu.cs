using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Murang.Multiplayer.Editor
{
    internal static class RoomServerBuildMenu
    {
        private const string ServerScenePath = "Assets/Multiplayer/Scenes/RoomServerBoot.unity";
        private const string ClientScenePath = "Assets/Multiplayer/Scenes/RoomClientSmokeTest.unity";
        private const string WindowsServerOutputPath = "Builds/RoomAutomation/WindowsServer/RoomServer.exe";
        private const string LinuxServerOutputPath = "Builds/RoomAutomation/LinuxServer/RoomServer.x86_64";
        private const string WindowsClientOutputPath = "Builds/RoomAutomation/WindowsClient/RoomClientSmokeTest.exe";

        [MenuItem("Tools/Multiplayer/Build Dedicated Server (Windows)")]
        public static void BuildDedicatedServerWindows()
        {
            BuildDedicatedServer(BuildTarget.StandaloneWindows64, WindowsServerOutputPath);
        }

        [MenuItem("Tools/Multiplayer/Build Dedicated Server (Linux)")]
        public static void BuildDedicatedServerLinux()
        {
            BuildDedicatedServer(BuildTarget.StandaloneLinux64, LinuxServerOutputPath);
        }

        [MenuItem("Tools/Multiplayer/Build Room Test Client (Windows)")]
        public static void BuildRoomTestClientWindows()
        {
            BuildRoomTestClient(BuildTarget.StandaloneWindows64, WindowsClientOutputPath);
        }

        public static void BuildRoomAutomationWindowsArtifacts()
        {
            BuildDedicatedServer(BuildTarget.StandaloneWindows64, WindowsServerOutputPath);
            BuildRoomTestClient(BuildTarget.StandaloneWindows64, WindowsClientOutputPath);
        }

        private static void BuildDedicatedServer(BuildTarget target, string relativeOutputPath)
        {
            string outputPath = ResolveOutputPath(relativeOutputPath);
            EnsureParentDirectoryExists(outputPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ServerScenePath },
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.StrictMode
            };

            string[] backupLoaderGuids = BackupAndClearStandaloneXrLoaders();
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                ThrowIfBuildFailed(report, "dedicated server", outputPath);
            }
            finally
            {
                RestoreStandaloneXrLoaders(backupLoaderGuids);
            }
        }

        private static void BuildRoomTestClient(BuildTarget target, string relativeOutputPath)
        {
            string outputPath = ResolveOutputPath(relativeOutputPath);
            EnsureParentDirectoryExists(outputPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ClientScenePath },
                locationPathName = outputPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.StrictMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            ThrowIfBuildFailed(report, "room test client", outputPath);
        }

        private static string ResolveOutputPath(string relativeOutputPath)
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativeOutputPath));
        }

        private static void EnsureParentDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string[] BackupAndClearStandaloneXrLoaders()
        {
            XRManagerSettings managerSettings = GetStandaloneXrManagerSettings();
            if (managerSettings == null || managerSettings.activeLoaders.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> backupGuids = new List<string>(managerSettings.activeLoaders.Count);
            foreach (XRLoader loader in managerSettings.activeLoaders)
            {
                if (loader == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(loader);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidOperationException("Unable to resolve asset path for a Standalone XR loader before dedicated-server build.");
                }

                string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(assetGuid))
                {
                    throw new InvalidOperationException("Unable to resolve asset GUID for Standalone XR loader at " + assetPath + ".");
                }

                backupGuids.Add(assetGuid);
            }

            if (!managerSettings.TrySetLoaders(new List<XRLoader>()))
            {
                throw new InvalidOperationException("Failed to clear Standalone XR loaders before dedicated-server build.");
            }

            EditorUtility.SetDirty(managerSettings);
            AssetDatabase.SaveAssets();
            return backupGuids.ToArray();
        }

        private static void RestoreStandaloneXrLoaders(string[] backupLoaderGuids)
        {
            if (backupLoaderGuids == null || backupLoaderGuids.Length == 0)
            {
                return;
            }

            XRManagerSettings managerSettings = GetStandaloneXrManagerSettings();
            if (managerSettings == null)
            {
                throw new InvalidOperationException("Standalone XR manager settings were unavailable while restoring XR loaders after dedicated-server build.");
            }

            List<XRLoader> restoredLoaders = new List<XRLoader>(backupLoaderGuids.Length);
            foreach (string loaderGuid in backupLoaderGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(loaderGuid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    throw new InvalidOperationException("Unable to resolve asset path for Standalone XR loader GUID " + loaderGuid + " during restore.");
                }

                XRLoader loader = AssetDatabase.LoadAssetAtPath<XRLoader>(assetPath);
                if (loader == null)
                {
                    throw new InvalidOperationException("Unable to load Standalone XR loader at " + assetPath + " during restore.");
                }

                restoredLoaders.Add(loader);
            }

            if (!managerSettings.TrySetLoaders(restoredLoaders))
            {
                throw new InvalidOperationException("Failed to restore Standalone XR loaders after dedicated-server build.");
            }

            EditorUtility.SetDirty(managerSettings);
            AssetDatabase.SaveAssets();
        }

        private static XRManagerSettings GetStandaloneXrManagerSettings()
        {
            XRGeneralSettings generalSettings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);

            return generalSettings?.Manager;
        }

        private static void ThrowIfBuildFailed(BuildReport report, string artifactName, string outputPath)
        {
            if (report == null)
            {
                throw new InvalidOperationException("Build report was null for " + artifactName + ".");
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to build " + artifactName + " at " + outputPath + ". Result: " + report.summary.result);
            }

            Debug.Log("Built " + artifactName + " at " + outputPath);
        }
    }
}
