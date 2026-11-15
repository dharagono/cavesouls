using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.AI
{
    /// <summary>
    /// Decoupled Visual (Vision Cone + Line of Sight Linecast) and Auditory (Acoustic EventBus) AI Sensor.
    /// Strictly uses Physics.OverlapSphereNonAlloc with pre-allocated buffers. Zero GC allocations.
    /// </summary>
    public sealed class AISensor : MonoBehaviour
    {
        private const int MaxDetectedColliders = 16;

        [Header("Visual Detection")]
        [SerializeField] private Transform _eyeOrigin;
        [SerializeField] private float _visionRadius = 16f;
        [SerializeField] [Range(10f, 180f)] private float _visionAngleDegrees = 110f;
        [SerializeField] private LayerMask _targetLayerMask = ~0;
        [SerializeField] private LayerMask _obstructionLayerMask = 1;
        [SerializeField] private float _scanInterval = 0.15f;

        [Header("Auditory Detection")]
        [SerializeField] private float _hearingSensitivityMultiplier = 1.0f;
        [SerializeField] private float _alertMemoryDuration = 10f;

        private readonly Collider[] _overlapBuffer = new Collider[MaxDetectedColliders];
        private Transform _detectedTarget;
        private Vector3 _lastKnownTargetPosition;
        private float _scanTimer;
        private float _alertTimer;
        private float _cosHalfVisionAngle;
        private bool _hasLineOfSight;

        public Transform DetectedTarget => _detectedTarget;
        public Vector3 LastKnownTargetPosition => _lastKnownTargetPosition;
        public bool HasTarget => _detectedTarget != null || _alertTimer > 0f;
        public bool HasLineOfSight => _hasLineOfSight;

        private void Awake()
        {
            if (_eyeOrigin == null) _eyeOrigin = transform;
            _cosHalfVisionAngle = Mathf.Cos(_visionAngleDegrees * 0.5f * Mathf.Deg2Rad);
        }

        private void OnEnable()
        {
            EventBus<OnSoundEmittedEvent>.Subscribe(HandleSoundEmitted);
        }

        private void OnDisable()
        {
            EventBus<OnSoundEmittedEvent>.Unsubscribe(HandleSoundEmitted);
        }

        public void TickSensor(float deltaTime)
        {
            if (_alertTimer > 0f)
            {
                _alertTimer -= deltaTime;
                if (_alertTimer <= 0f && !_hasLineOfSight)
                {
                    _detectedTarget = null;
                }
            }

            _scanTimer += deltaTime;
            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0f;
                PerformVisualScan();
            }
        }

        public void ClearDetection()
        {
            _detectedTarget = null;
            _hasLineOfSight = false;
            _alertTimer = 0f;
        }

        public void ForceAlertToTarget(Transform target)
        {
            if (target == null) return;
            _detectedTarget = target;
            _lastKnownTargetPosition = target.position;
            _alertTimer = _alertMemoryDuration;
        }

        private void PerformVisualScan()
        {
            _hasLineOfSight = false;
            Vector3 eyePos = _eyeOrigin.position;
            Vector3 eyeFwd = _eyeOrigin.forward;

            int count = Physics.OverlapSphereNonAlloc(eyePos, _visionRadius, _overlapBuffer, _targetLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.transform == transform) continue;

                if (!col.TryGetComponent(out IDamageable damageable) || !damageable.IsAlive) continue;

                Vector3 targetCenter = col.bounds.center;
                Vector3 dirToTarget = (targetCenter - eyePos).normalized;

                float dot = Vector3.Dot(eyeFwd, dirToTarget);
                if (dot < _cosHalfVisionAngle) continue;

                if (!Physics.Linecast(eyePos, targetCenter, _obstructionLayerMask, QueryTriggerInteraction.Ignore))
                {
                    _detectedTarget = damageable.Transform;
                    _lastKnownTargetPosition = _detectedTarget.position;
                    _alertTimer = _alertMemoryDuration;
                    _hasLineOfSight = true;
                    return;
                }
            }
        }

        private void HandleSoundEmitted(in OnSoundEmittedEvent evt)
        {
            if (evt.Source == null || evt.Source == gameObject) return;

            float effectiveRadius = evt.NoiseRadius * _hearingSensitivityMultiplier;
            float distSqr = (evt.Position - transform.position).sqrMagnitude;

            if (distSqr <= effectiveRadius * effectiveRadius)
            {
                _lastKnownTargetPosition = evt.Position;
                _detectedTarget = evt.Source.transform;
                _alertTimer = _alertMemoryDuration;
            }
        }
    }
}
