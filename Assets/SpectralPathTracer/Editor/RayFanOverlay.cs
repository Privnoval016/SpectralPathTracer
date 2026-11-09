using System;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Scene view overlay that draws a grid of the path tracer's camera rays from the main camera.
     * </summary>
     */
    [Overlay(typeof(SceneView), OverlayId, "Ray Fan", false)]
    public class RayFanOverlay : Overlay
    {
        #region Private Fields

        private const string OverlayId = "spectral-path-tracer-ray-fan";
        private const int MaxRaysPerAxis = 64;
        private const float MaxRayLength = 50f;
        private const float MaxThickness = 8f;

        #endregion

        /**
         * <summary>
         * Starts drawing into the Scene view this overlay belongs to.
         * </summary>
         */
        public override void OnCreated()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        /**
         * <summary>
         * Stops drawing when the overlay is removed.
         * </summary>
         */
        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        public override VisualElement CreatePanelContent()
        {
            RayFanSettings settings = RayFanSettings.instance;
            var root = new VisualElement { style = { minWidth = 260 } };

            var settingsField = new ObjectField("Settings")
            {
                objectType = typeof(PathTracerSettings),
                allowSceneObjects = false,
                value = settings.PathTracerSettings
            };
            settingsField.RegisterValueChangedCallback(e => Apply(() => settings.PathTracerSettings = e.newValue as PathTracerSettings));
            root.Add(settingsField);

            root.Add(IntSlider("Columns", settings.Columns, 1, MaxRaysPerAxis, v => settings.Columns = v));
            root.Add(IntSlider("Rows", settings.Rows, 1, MaxRaysPerAxis, v => settings.Rows = v));
            root.Add(FloatSlider("Ray Length", settings.RayLength, 0.1f, MaxRayLength, v => settings.RayLength = v));
            root.Add(FloatSlider("Thickness", settings.Thickness, 0f, MaxThickness, v => settings.Thickness = v));

            var colorField = new ColorField("Ray Color") { value = settings.RayColor };
            colorField.RegisterValueChangedCallback(e => Apply(() => settings.RayColor = e.newValue));
            SetVisible(colorField, !settings.ColorByDirection);

            // the fixed color only matters when rays aren't colored by direction
            root.Add(Toggle("Color By Direction", settings.ColorByDirection, v =>
            {
                settings.ColorByDirection = v;
                SetVisible(colorField, !v);
            }));
            root.Add(colorField);

            root.Add(Toggle("Show Image Plane", settings.ShowImagePlane, v => settings.ShowImagePlane = v));
            root.Add(Toggle("Show Frustum Edges", settings.ShowFrustumEdges, v => settings.ShowFrustumEdges = v));
            return root;
        }

        #region Event Handlers

        private void OnSceneGui(SceneView sceneView)
        {
            // every Scene view gets its own overlay instance, so only draw into ours, and only when repainting
            if (!displayed || sceneView != containerWindow || Event.current.type != EventType.Repaint)
                return;

            RayFanSettings settings = RayFanSettings.instance;
            Camera camera = Camera.main;
            if (camera == null || settings.PathTracerSettings == null)
                return;

            PinholeCamera pinhole = PinholeCamera.From(camera, settings.PathTracerSettings);
            DrawRayGrid(pinhole, settings);

            if (settings.ShowFrustumEdges)
                DrawFrustumEdges(pinhole, settings);

            if (settings.ShowImagePlane)
                DrawImagePlane(pinhole);
        }

        #endregion

        #region Helpers

        private static void DrawRayGrid(PinholeCamera pinhole, RayFanSettings settings)
        {
            for (int y = 0; y < settings.Rows; y++)
            {
                for (int x = 0; x < settings.Columns; x++)
                {
                    // same pixel-center mapping as the kernel, so Columns x Rows = output size gives the kernel's exact rays
                    Vector2 ndc = new Vector2(
                        (x + 0.5f) / settings.Columns * 2f - 1f,
                        (y + 0.5f) / settings.Rows * 2f - 1f);
                    DrawRay(pinhole, ndc, settings);
                }
            }
        }

        private static void DrawFrustumEdges(PinholeCamera pinhole, RayFanSettings settings)
        {
            DrawRay(pinhole, new Vector2(-1f, -1f), settings);
            DrawRay(pinhole, new Vector2(1f, -1f), settings);
            DrawRay(pinhole, new Vector2(1f, 1f), settings);
            DrawRay(pinhole, new Vector2(-1f, 1f), settings);
        }

        private static void DrawRay(PinholeCamera pinhole, Vector2 ndc, RayFanSettings settings)
        {
            Vector3 direction = pinhole.RayDirection(ndc);
            Handles.color = settings.ColorByDirection ? DirectionColor(direction) : settings.RayColor;
            Handles.DrawLine(pinhole.Position, pinhole.Position + direction * settings.RayLength, settings.Thickness);
        }

        private static void DrawImagePlane(PinholeCamera pinhole)
        {
            float halfHeight = pinhole.TanHalfFov;
            float halfWidth = pinhole.AspectRatio * halfHeight;
            Vector3[] corners =
            {
                pinhole.CameraToWorld.MultiplyPoint3x4(new Vector3(-halfWidth, -halfHeight, 1f)),
                pinhole.CameraToWorld.MultiplyPoint3x4(new Vector3(halfWidth, -halfHeight, 1f)),
                pinhole.CameraToWorld.MultiplyPoint3x4(new Vector3(halfWidth, halfHeight, 1f)),
                pinhole.CameraToWorld.MultiplyPoint3x4(new Vector3(-halfWidth, halfHeight, 1f)),
                pinhole.CameraToWorld.MultiplyPoint3x4(new Vector3(-halfWidth, -halfHeight, 1f))
            };
            Handles.color = Color.white;
            Handles.DrawPolyLine(corners);
        }

        /**
         * <summary>
         * The kernel's direction-to-color mapping, gamma encoded so the Scene view lines match the colors
         * the Game view displays.
         * </summary>
         */
        private static Color DirectionColor(Vector3 direction)
        {
            Vector3 c = direction * 0.5f + Vector3.one * 0.5f;
            return new Color(c.x, c.y, c.z).gamma;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void Apply(Action change)
        {
            change();
            RayFanSettings.instance.Persist();
            SceneView.RepaintAll();
        }

        private static SliderInt IntSlider(string label, int value, int min, int max, Action<int> set)
        {
            var slider = new SliderInt(label, min, max) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(e => Apply(() => set(e.newValue)));
            return slider;
        }

        private static Slider FloatSlider(string label, float value, float min, float max, Action<float> set)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(e => Apply(() => set(e.newValue)));
            return slider;
        }

        private static Toggle Toggle(string label, bool value, Action<bool> set)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(e => Apply(() => set(e.newValue)));
            return toggle;
        }

        #endregion
    }
}
