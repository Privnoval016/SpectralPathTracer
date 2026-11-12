using UnityEngine;
using UnityEngine.InputSystem;

namespace SpectralPathTracer
{
    // drags the split screen divider with the mouse in Play Mode, through PathTracerOverrides
    [DisallowMultipleComponent]
    public class SplitDivider : MonoBehaviour
    {
        #region Private Fields

        private bool _dragging;

        #endregion

        #region Inspector Fields

        [field: Tooltip("The settings whose output size the image is letterboxed by.")]
        [field: SerializeField] public PathTracerSettings Settings { get; private set; }

        [field: Tooltip("Starts a drag when pressed on the divider, and ends it when released.")]
        [field: SerializeField] public InputAction Drag { get; private set; } = new InputAction("Drag", InputActionType.Button, "<Mouse>/leftButton");

        [field: Tooltip("Where the pointer is, in screen pixels.")]
        [field: SerializeField] public InputAction Point { get; private set; } = new InputAction("Point", InputActionType.Value, "<Pointer>/position");

        [field: Tooltip("How close to the divider, in screen pixels, a drag can start.")]
        [field: Min(1f)]
        [field: SerializeField] public float GrabDistance { get; private set; } = 12f;

        #endregion

        private void OnEnable()
        {
            Drag.Enable();
            Point.Enable();
        }

        private void OnDisable()
        {
            Drag.Disable();
            Point.Disable();

            // the settings value takes over again, so a Play Mode drag never changes the asset
            PathTracerOverrides.SplitPosition = null;
            _dragging = false;
        }

        private void Update()
        {
            Rect image = PresentStage.FitInside(Settings.OutputResolution, new Vector2Int(Screen.width, Screen.height));
            Vector2 pointer = Point.ReadValue<Vector2>();
            float position = PathTracerOverrides.SplitPosition ?? Settings.SplitPosition;

            // a drag only starts on the line, so clicks anywhere else stay free
            if (Drag.WasPressedThisFrame())
                _dragging = Mathf.Abs(pointer.x - (image.x + position * image.width)) <= GrabDistance;

            if (Drag.WasReleasedThisFrame())
                _dragging = false;


            // where the cursor is in the image, not the window, so the line stays under it
            if (_dragging)
                PathTracerOverrides.SplitPosition = Mathf.Clamp01((pointer.x - image.x) / image.width);
        }
    }
}
