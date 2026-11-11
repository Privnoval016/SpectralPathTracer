using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Scene view overlay that makes the Main Camera follow the Scene view in Edit Mode, so shots are framed with the
     * Scene view's own navigation.
     * </summary>
     */
    [Overlay(typeof(SceneView), OverlayId, "Follow Scene View", false)]
    public class FollowSceneViewOverlay : Overlay
    {
        #region Private Fields

        private const string OverlayId = "spectral-path-tracer-follow-scene-view";

        #endregion

        public override void OnCreated()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        public override VisualElement CreatePanelContent()
        {
            var toggle = new Toggle("Main Camera follows") { value = FollowSceneViewSettings.instance.Follow };
            toggle.RegisterValueChangedCallback(e =>
            {
                FollowSceneViewSettings.instance.Follow = e.newValue;
                FollowSceneViewSettings.instance.SaveSettings();
            });
            return toggle;
        }

        #region Helpers

        private static void OnSceneGui(SceneView sceneView)
        {
            Camera gameCamera = Camera.main;
            if (!FollowSceneViewSettings.instance.Follow || Application.isPlaying || gameCamera == null)
                return;

            Transform view = sceneView.camera.transform;
            if (gameCamera.transform.position == view.position && gameCamera.transform.rotation == view.rotation && gameCamera.fieldOfView == sceneView.camera.fieldOfView)
                return;

            // only writes when something changed, so a still Scene view doesn't restart accumulation every repaint
            gameCamera.transform.SetPositionAndRotation(view.position, view.rotation);
            gameCamera.fieldOfView = sceneView.camera.fieldOfView;
        }

        #endregion
    }
}