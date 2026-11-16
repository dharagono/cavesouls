using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Dark fantasy third-person orbital camera with collision avoidance for cavern navigation.
    /// Clamps pitch, smooths rotations, and prevents camera clipping through cave walls and pillars.
    /// Strictly zero GC allocations during Update and LateUpdate.
    /// </summary>
    [ExecuteAlways]
    public sealed class SoulsOrbitCamera : MonoBehaviour
    {
        [Header("Target & Offsets")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private float _defaultDistance = 3.6f;
        [SerializeField] private float _minDistance = 1.0f;
        [SerializeField] private float _maxDistance = 6.0f;

        [Header("Sensitivity & Smoothing")]
        [SerializeField] private float _mouseSensitivityX = 0.15f;
        [SerializeField] private float _mouseSensitivityY = 0.12f;
        [SerializeField] private float _minPitch = -15f;
        [SerializeField] private float _maxPitch = 48f;
        [SerializeField] private float _rotationDamping = 12f;
        [SerializeField] private float _distanceDamping = 14f;

        [Header("Collision Avoidance")]
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [SerializeField] private float _cameraCollisionRadius = 0.25f;

        private float _currentYaw;
        private float _currentPitch = 6f;
        private float _targetYaw;
        private float _targetPitch = 6f;
        private float _currentDistance;
        private float _desiredDistance;
        private int _warmupFrames = 5;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        public float CurrentYaw => _currentYaw;

        private void Start()
        {
            _currentDistance = _defaultDistance;
            _desiredDistance = _defaultDistance;

            if (_target != null)
            {
                _targetYaw = _target.eulerAngles.y;
                _currentYaw = _targetYaw;
            }

            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_warmupFrames > 0)
            {
                _warmupFrames--;
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            Vector2 lookInput = Vector2.zero;

            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                delta.x = Mathf.Clamp(delta.x, -30f, 30f);
                delta.y = Mathf.Clamp(delta.y, -30f, 30f);
                lookInput = delta;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                lookInput += stick * 120f * Time.deltaTime;
            }

            _targetYaw += lookInput.x * _mouseSensitivityX;
            _targetPitch -= lookInput.y * _mouseSensitivityY;
            _targetPitch = Mathf.Clamp(_targetPitch, _minPitch, _maxPitch);

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _desiredDistance = Mathf.Clamp(_desiredDistance - Mathf.Sign(scroll) * 0.5f, _minDistance, _maxDistance);
                }
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float dt = Time.deltaTime;

            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, dt * _rotationDamping);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, dt * _rotationDamping);

            Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 focusPoint = _target.position + _targetOffset;
            Vector3 direction = rotation * -Vector3.forward;

            float targetDist = _desiredDistance;
            int hitCount = Physics.SphereCastNonAlloc(
                focusPoint,
                _cameraCollisionRadius,
                direction,
                _hitBuffer,
                _desiredDistance,
                _collisionLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                float closestHit = _desiredDistance;
                for (int i = 0; i < hitCount; i++)
                {
                    if (_hitBuffer[i].transform.IsChildOf(_target)) continue;
                    if (_hitBuffer[i].distance < closestHit && _hitBuffer[i].distance > 0.1f)
                    {
                        closestHit = _hitBuffer[i].distance;
                    }
                }
                targetDist = Mathf.Max(_minDistance, closestHit - 0.1f);
            }

            _currentDistance = Mathf.Lerp(_currentDistance, targetDist, dt * _distanceDamping);

            transform.position = focusPoint + direction * _currentDistance;
            transform.rotation = rotation;
        }
    }
}
