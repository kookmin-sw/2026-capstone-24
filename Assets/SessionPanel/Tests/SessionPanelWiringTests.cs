using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SessionPanel
{
    public class SessionPanelWiringTests
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void SessionPanelController_ActiveInstrumentProvider_IsWired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var ctrl = Object.FindFirstObjectByType<SessionPanelController>(FindObjectsInactive.Include);
                Assert.IsNotNull(ctrl, "SessionPanelController not found in SampleScene");

                var so = new SerializedObject(ctrl);
                var prop = so.FindProperty("_activeInstrumentProviderObject");
                Assert.IsNotNull(prop?.objectReferenceValue,
                    "_activeInstrumentProviderObject is not wired — explicit wiring 리팩터 회귀");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SessionPanelController_SongCatalog_IsWired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var ctrl = Object.FindFirstObjectByType<SessionPanelController>(FindObjectsInactive.Include);
                Assert.IsNotNull(ctrl, "SessionPanelController not found in SampleScene");

                var so = new SerializedObject(ctrl);
                var prop = so.FindProperty("_songCatalogObject");
                Assert.IsNotNull(prop?.objectReferenceValue,
                    "_songCatalogObject is not wired — explicit wiring 리팩터 회귀");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SessionPanelController_PanelPrefab_IsWired()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var ctrl = Object.FindFirstObjectByType<SessionPanelController>(FindObjectsInactive.Include);
                Assert.IsNotNull(ctrl, "SessionPanelController not found in SampleScene");

                var so = new SerializedObject(ctrl);
                var prop = so.FindProperty("panelPrefab");
                Assert.IsNotNull(prop?.objectReferenceValue,
                    "panelPrefab is not wired in SampleScene");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
