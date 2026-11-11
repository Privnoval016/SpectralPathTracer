using UnityEngine;
using UnityEngine.InputSystem;

namespace SpectralPathTracer
{
    /**
     * <summary>
     * Flies the camera in Play Mode.
     * </summary>
     */
    [DisallowMultipleComponent]
    public class FlyCamera : MonoBehaviour
    {
        #region Private Fields

        private const string MapName = "Fly";
        private const float MaxPitch = 89f; // at 90 the view would flip, since forward lines up with world up
        private const float SpeedStep = 1.1f;

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _upDown;
        private InputAction _look;
        private InputAction _lookStick;
        private InputAction _lookEnable;
        private InputAction _boost;
        private InputAction _speedScroll;

        private float _yaw;
        private float _pitch;
        private float _speed;

        #endregion

        #region Inspector Fields

        [field: Tooltip("The fly controls.")]
        [field: SerializeField] public InputActionAsset Actions { get; private set; }

        [field: Tooltip("Starting speed in meters per second.")]
        [field: Min(0.001f)]
        [field: SerializeField] public float StartSpeed { get; private set; } = 0.5f;

        [field: Tooltip("Speed multiplier while Boost is held.")]
        [field: Min(1f)]
        [field: SerializeField] public float BoostMultiplier { get; private set; } = 4f;

        [field: Tooltip("Degrees turned per pixel of mouse or trackpad movement.")]
        [field: SerializeField] public float LookSensitivity { get; private set; } = 0.15f;

        [field: Tooltip("Degrees per second at full gamepad stick.")]
        [field: SerializeField] public float StickLookSpeed { get; private set; } = 120f;

        #endregion

        private void OnEnable()
        {
            _map = Actions.FindActionMap(MapName, throwIfNotFound: true);
            _move = _map.FindAction("Move", throwIfNotFound: true);
            _upDown = _map.FindAction("UpDown", throwIfNotFound: true);
            _look = _map.FindAction("Look", throwIfNotFound: true);
            _lookStick = _map.FindAction("LookStick", throwIfNotFound: true);
            _lookEnable = _map.FindAction("LookEnable", throwIfNotFound: true);
            _boost = _map.FindAction("Boost", throwIfNotFound: true);
            _speedScroll = _map.FindAction("Speed", throwIfNotFound: true);
            _map.Enable();

            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x; // eulerAngles reports -10 degrees as 350
            _speed = StartSpeed;
        }

        private void OnDisable()
        {
            _map?.Disable();
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            UpdateLook();
            UpdateSpeed();
            UpdateMove();
        }

        #region Helpers

        private void UpdateLook()
        {
            bool looking = _lookEnable.IsPressed();
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;

            Vector2 turn = _lookStick.ReadValue<Vector2>() * (StickLookSpeed * Time.deltaTime); // a stick is a rate
            if (looking)
                turn += _look.ReadValue<Vector2>() * LookSensitivity; // a mouse delta is already this frame's distance

            _yaw += turn.x;
            _pitch = Mathf.Clamp(_pitch - turn.y, -MaxPitch, MaxPitch);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateSpeed()
        {
            float scroll = _speedScroll.ReadValue<float>();
            if (scroll != 0f)
                _speed *= scroll > 0f ? SpeedStep : 1f / SpeedStep; // by sign since trackpads report larger values than wheels
        }

        private void UpdateMove()
        {
            Vector2 move = _move.ReadValue<Vector2>();
            Vector3 direction = transform.right * move.x + transform.forward * move.y + Vector3.up * _upDown.ReadValue<float>();
            float speed = _speed * (_boost.IsPressed() ? BoostMultiplier : 1f);
            transform.position += direction * (speed * Time.deltaTime);
        }

        #endregion
    }
}