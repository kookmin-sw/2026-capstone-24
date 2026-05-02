using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Murang.Multiplayer.Editor
{
    internal static class RoomEditModeTestMenu
    {
        private const string RoomTestAssemblyName = "Murang.Multiplayer.Room.Tests";
        private static bool s_IsRunInProgress;

        [MenuItem("Tools/Multiplayer/Open Test Runner")]
        private static void OpenTestRunner()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
        }

        [MenuItem("Tools/Multiplayer/Run Room EditMode Tests")]
        private static void RunRoomEditModeTests()
        {
            if (s_IsRunInProgress)
            {
                Debug.LogWarning("A Unity test run is already in progress.");
                return;
            }

            s_IsRunInProgress = true;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = ScriptableObject.CreateInstance<RoomEditModeTestCallbacks>();
            callbacks.Initialize(api);
            api.RegisterCallbacks(callbacks);

            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { RoomTestAssemblyName }
            };

            Debug.Log("Starting room EditMode tests...");
            api.Execute(new ExecutionSettings
            {
                filters = new[] { filter },
                runSynchronously = true
            });
        }

        private sealed class RoomEditModeTestCallbacks : ScriptableObject, ICallbacks
        {
            private TestRunnerApi _api;

            public void Initialize(TestRunnerApi api)
            {
                _api = api;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"Room EditMode tests queued: {testsToRun?.Name}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                int total = CountLeafTests(result);
                int failed = CountLeafTestsByStatus(result, TestStatus.Failed);
                int inconclusive = CountLeafTestsByStatus(result, TestStatus.Inconclusive);
                int skipped = CountLeafTestsByStatus(result, TestStatus.Skipped);
                int passed = total - failed - inconclusive - skipped;

                if (failed == 0 && inconclusive == 0)
                {
                    Debug.Log($"Room EditMode tests finished. total={total}, passed={passed}, skipped={skipped}");
                }
                else
                {
                    Debug.LogError($"Room EditMode tests finished with failures. total={total}, passed={passed}, failed={failed}, inconclusive={inconclusive}, skipped={skipped}");
                }

                Cleanup();
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private void Cleanup()
            {
                s_IsRunInProgress = false;

                if (_api != null)
                {
                    _api.UnregisterCallbacks(this);
                }

                DestroyImmediate(this);
            }

            private static int CountLeafTests(ITestResultAdaptor result)
            {
                if (result == null)
                {
                    return 0;
                }

                if (!result.HasChildren)
                {
                    return result.Test.IsSuite ? 0 : 1;
                }

                return result.Children.Sum(CountLeafTests);
            }

            private static int CountLeafTestsByStatus(ITestResultAdaptor result, TestStatus status)
            {
                if (result == null)
                {
                    return 0;
                }

                if (!result.HasChildren)
                {
                    return !result.Test.IsSuite && result.TestStatus == status ? 1 : 0;
                }

                return result.Children.Sum(child => CountLeafTestsByStatus(child, status));
            }
        }
    }
}
