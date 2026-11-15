using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Smart Lock-On System using Physics.OverlapSphereNonAlloc + Camera View Angle (Vector3.Dot)
    /// + Physics.Linecast for wall obstruction checks. Supports Right-Stick target switching.
    /// Zero GC allocations.
    /// </summary>
    public sealed class LockOnSystem : MonoBehaviour
    {
        private const int MaxOverlapColliders = 32;

        [Header("Detection Settings")]
        [SerializeField] private float _lockOnRadius = 20f;
        [SerializeField] private float _maxBreakDistance = 24f;
        [SerializeField] [Range(-1f, 1f)] private float _minCameraViewDot = 0.35f;
        [SerializeField] private LayerMask _targetableLayerMask = ~0;
        [SerializeField] private LayerMask _environmentObstructionMask = 1;
        [SerializeField] private float _switchTargetCooldown = 0.22f;

        private readonly Collider[] _colliderBuffer = new Collider[MaxOverlapColliders];
        private Transform _cameraTransform;
        private ITargetable _currentTarget;
        private float _lastSwitchTime;

        public bool IsLockedOn => _currentTarget != null && _currentTarget.CanBeLockedOn && _currentTarget.LockOnPivot != null;
        public Transform CurrentTargetPivot => IsLockedOn ? _currentTarget.LockOnPivot : null;

        private void Awake()
        {
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        public void SetCameraTransform(Transform camTransform)
        {
            _cameraTransform = camTransform;
        }

        public bool ToggleLockOn()
        {
            if (IsLockedOn)
            {
                ClearLockOn();
                return false;
            }

            return TryAcquireBestTarget();
        }

        public void ClearLockOn()
        {
            if (_currentTarget != null)
            {
                _currentTarget = null;
                EventBus<OnLockOnTargetChangedEvent>.Publish(new OnLockOnTargetChangedEvent
                {
                    NewTarget = null,
                    IsLockedOn = false
                });
            }
        }

        public void ValidateCurrentLockOn()
        {
            if (_currentTarget == null) return;

            if (!_currentTarget.CanBeLockedOn || _currentTarget.LockOnPivot == null)
            {
                if (!TryAcquireBestTarget())
                {
                    ClearLockOn();
                }
                return;
            }

            Vector3 eyePos = _cameraTransform != null ? _cameraTransform.position : transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = _currentTarget.LockOnPivot.position;
            float distSqr = (targetPos - transform.position).sqrMagnitude;

            if (distSqr > _maxBreakDistance * _maxBreakDistance ||
                Physics.Linecast(eyePos, targetPos, _environmentObstructionMask, QueryTriggerInteraction.Ignore))
            {
                ClearLockOn();
            }
        }

        public bool TryAcquireBestTarget()
        {
            Vector3 origin = transform.position;
            Vector3 camPos = _cameraTransform != null ? _cameraTransform.position : origin + Vector3.up * 1.6f;
            Vector3 camFwd = _cameraTransform != null ? _cameraTransform.forward : transform.forward;

            int count = Physics.OverlapSphereNonAlloc(origin, _lockOnRadius, _colliderBuffer, _targetableLayerMask, QueryTriggerInteraction.Collide);

            ITargetable bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _colliderBuffer[i];
                if (col == null || col.transform == transform) continue;

                if (!TryExtractTargetable(col, out ITargetable candidate)) continue;
                if (!candidate.CanBeLockedOn || candidate.LockOnPivot == null) continue;

                Vector3 pivotPos = candidate.LockOnPivot.position;
                Vector3 toTargetFromCam = (pivotPos - camPos).normalized;
                float viewDot = Vector3.Dot(camFwd, toTargetFromCam);

                if (viewDot < _minCameraViewDot) continue;

                // Ensure clear line of sight without walls
                if (Physics.Linecast(camPos, pivotPos, _environmentObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                float dist = Vector3.Distance(origin, pivotPos);
                float normalizedDistScore = 1f - Mathf.Clamp01(dist / _lockOnRadius);
                float score = (viewDot * 2.0f) + normalizedDistScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                SetTarget(bestTarget);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Switches Lock-On target horizontally based on Right Stick input delta.
        /// </summary>
        public void TrySwitchTarget(Vector2 rightStickInput)
        {
            if (!IsLockedOn || Mathf.Abs(rightStickInput.x) < 0.55f) return;
            if (Time.time - _lastSwitchTime < _switchTargetCooldown) return;

            float desiredSign = Mathf.Sign(rightStickInput.x);
            Vector3 origin = transform.position;
            Vector3 camPos = _cameraTransform != null ? _cameraTransform.position : origin + Vector3.up * 1.6f;
            Vector3 camFwd = _cameraTransform != null ? _cameraTransform.forward : transform.forward;
            Vector3 camRight = _cameraTransform != null ? _cameraTransform.right : transform.right;
            Vector3 currentTargetDir = (_currentTarget.LockOnPivot.position - camPos).normalized;

            int count = Physics.OverlapSphereNonAlloc(origin, _lockOnRadius, _colliderBuffer, _targetableLayerMask, QueryTriggerInteraction.Collide);

            ITargetable bestNext = null;
            float bestAngularScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _colliderBuffer[i];
                if (col == null || col.transform == transform) continue;

                if (!TryExtractTargetable(col, out ITargetable candidate)) continue;
                if (ReferenceEquals(candidate, _currentTarget) || !candidate.CanBeLockedOn || candidate.LockOnPivot == null) continue;

                Vector3 pivotPos = candidate.LockOnPivot.position;
                Vector3 candidateDir = (pivotPos - camPos).normalized;

                if (Vector3.Dot(camFwd, candidateDir) < _minCameraViewDot) continue;
                if (Physics.Linecast(camPos, pivotPos, _environmentObstructionMask, QueryTriggerInteraction.Ignore)) continue;

                // Determine whether candidate is to the left or right of current target in camera space
                Vector3 deltaDir = candidateDir - currentTargetDir;
                float horizontalOffset = Vector3.Dot(camRight, deltaDir);

                if (Mathf.Sign(horizontalOffset) != desiredSign || Mathf.Abs(horizontalOffset) < 0.02f) continue;

                float absOffset = Mathf.Abs(horizontalOffset);
                if (absOffset < bestAngularScore)
                {
                    bestAngularScore = absOffset;
                    bestNext = candidate;
                }
            }

            if (bestNext != null)
            {
                _lastSwitchTime = Time.time;
                SetTarget(bestNext);
            }
        }

        private void SetTarget(ITargetable target)
        {
            _currentTarget = target;
            EventBus<OnLockOnTargetChangedEvent>.Publish(new OnLockOnTargetChangedEvent
            {
                NewTarget = _currentTarget.LockOnPivot,
                IsLockedOn = true
            });
        }

        private static bool TryExtractTargetable(Collider col, out ITargetable targetable)
        {
            if (col.TryGetComponent(out targetable)) return true;
            if (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent(out targetable)) return true;
            return false;
        }
    }
}
