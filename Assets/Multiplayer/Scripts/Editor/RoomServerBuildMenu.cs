using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

            BuildReport report = BuildPipeline.BuildPlayer(options);
            ThrowIfBuildFailed(report, "dedicated server", outputPath);
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
