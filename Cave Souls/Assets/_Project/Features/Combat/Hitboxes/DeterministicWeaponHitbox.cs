using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Frame-rate independent, deterministic weapon hitbox using interpolated NonAlloc BoxCasts/CapsuleCasts
    /// between Socket_Trail_Base and Socket_Trail_Tip from the previous frame to the current frame.
    /// Strictly zero GC allocations in Update/FixedUpdate.
    /// </summary>
    public sealed class DeterministicWeaponHitbox : MonoBehaviour
    {
        private const int MaxHitsPerSubStep = 16;
        private const int MaxUniqueTargetsPerSwing = 32;

        [Header("Weapon Blade Sockets")]
        [SerializeField] private Transform _socketTrailBase;
        [SerializeField] private Transform _socketTrailTip;
        [SerializeField] private float _bladeRadius = 0.12f;
        [SerializeField] private int _interpolationSubSteps = 4;
        [SerializeField] private LayerMask _hitLayerMask = ~0;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxHitsPerSubStep];
        private readonly int[] _alreadyHitEntityIds = new int[MaxUniqueTargetsPerSwing];
        private int _alreadyHitCount;

        private Vector3 _prevBasePos;
        private Vector3 _prevTipPos;
        private bool _isHitboxOpen;
        private int _ownerEntityId;
        private DamagePayload _activePayload;

        public bool IsHitboxOpen => _isHitboxOpen;
        public Transform SocketTrailBase => _socketTrailBase;
        public Transform SocketTrailTip => _socketTrailTip;

        public void Initialize(int ownerEntityId)
        {
            _ownerEntityId = ownerEntityId;
        }

        public void OpenHitbox(in DamagePayload payload)
        {
            _activePayload = payload;
            _alreadyHitCount = 0;

            if (_socketTrailBase != null && _socketTrailTip != null)
            {
                _prevBasePos = _socketTrailBase.position;
                _prevTipPos = _socketTrailTip.position;
            }
            else
            {
                _prevBasePos = transform.position;
                _prevTipPos = transform.position + transform.forward;
            }

            _isHitboxOpen = true;
        }

        public void CloseHitbox()
        {
            _isHitboxOpen = false;
            _alreadyHitCount = 0;
        }

        public void SampleHitbox()
        {
            if (!_isHitboxOpen) return;

            Vector3 currBasePos = _socketTrailBase != null ? _socketTrailBase.position : transform.position;
            Vector3 currTipPos = _socketTrailTip != null ? _socketTrailTip.position : transform.position + transform.forward;

            int steps = Mathf.Max(1, _interpolationSubSteps);
            Vector3 subStepStartBase = _prevBasePos;
            Vector3 subStepStartTip = _prevTipPos;

            for (int step = 1; step <= steps; step++)
            {
                float t = (float)step / steps;
                Vector3 subStepEndBase = Vector3.Lerp(_prevBasePos, currBasePos, t);
                Vector3 subStepEndTip = Vector3.Lerp(_prevTipPos, currTipPos, t);

                PerformSubStepSweep(subStepStartBase, subStepStartTip, subStepEndBase, subStepEndTip);

                subStepStartBase = subStepEndBase;
                subStepStartTip = subStepEndTip;

                // Stop immediately if attacker was parried mid-swing
                if (!_isHitboxOpen) break;
            }

            _prevBasePos = currBasePos;
            _prevTipPos = currTipPos;
        }

        private void PerformSubStepSweep(Vector3 startBase, Vector3 startTip, Vector3 endBase, Vector3 endTip)
        {
            Vector3 startMid = (startBase + startTip) * 0.5f;
            Vector3 endMid = (endBase + endTip) * 0.5f;
            Vector3 sweepVector = endMid - startMid;
            float sweepDistance = sweepVector.magnitude;

            Vector3 sweepDir = sweepDistance > 0.0001f
                ? sweepVector / sweepDistance
                : (_socketTrailTip != null ? _socketTrailTip.forward : transform.forward);

            int hitCount = Physics.CapsuleCastNonAlloc(
                startBase,
                startTip,
                _bladeRadius,
                sweepDir,
                _hitBuffer,
                Mathf.Max(0.01f, sweepDistance),
                _hitLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitBuffer[i].collider;
                if (col == null) continue;

                // TryGetComponent on collider or attachedRigidbody without GC allocation
                IDamageable damageable = null;
                if (!col.TryGetComponent(out damageable))
                {
                    Rigidbody rb = col.attachedRigidbody;
                    if (rb != null)
                    {
                        rb.TryGetComponent(out damageable);
                    }
                }

                if (damageable != null)
                {
                    if (damageable.EntityId == _ownerEntityId || !damageable.IsAlive) continue;
                    if (HasAlreadyHitEntity(damageable.EntityId)) continue;

                    RegisterHitEntity(damageable.EntityId);

                    Vector3 hitPoint = _hitBuffer[i].point != Vector3.zero
                        ? _hitBuffer[i].point
                        : col.ClosestPoint(endMid);
                    Vector3 hitNormal = _hitBuffer[i].normal != Vector3.zero
                        ? _hitBuffer[i].normal
                        : -sweepDir;

                    // Check if target is in active Parry window and attack is parryable
                    if (damageable.IsInParryWindow && _activePayload.CanBeParried)
                    {
                        damageable.NotifyParriedBy(damageable.Transform.gameObject);
                        if (_activePayload.Attacker != null &&
                            _activePayload.Attacker.TryGetComponent(out IDamageable attackerDamageable))
                        {
                            attackerDamageable.NotifyParriedBy(damageable.Transform.gameObject);
                        }

                        EventBus<OnHitImpactEvent>.Publish(new OnHitImpactEvent
                        {
                            HitPoint = hitPoint,
                            HitNormal = hitNormal,
                            WeaponMaterial = _activePayload.WeaponMaterial,
                            SurfaceMaterial = SurfaceMaterialType.Metal,
                            ImpactForce = 2.0f,
                            WasBlockedOrParried = true,
                            IsHeavyHit = true
                        });

                        CloseHitbox();
                        return;
                    }

                    DamagePayload hitPayload = _activePayload;
                    hitPayload.HitPoint = hitPoint;
                    hitPayload.HitNormal = hitNormal;
                    hitPayload.HitDirection = sweepDir;

                    DamageResult result = damageable.ReceiveDamage(in hitPayload);

                    if (!result.WasDodgedByIFrames)
                    {
                        EventBus<OnHitImpactEvent>.Publish(new OnHitImpactEvent
                        {
                            HitPoint = hitPoint,
                            HitNormal = hitNormal,
                            WeaponMaterial = hitPayload.WeaponMaterial,
                            SurfaceMaterial = result.ImpactedMaterial,
                            ImpactForce = hitPayload.IsHeavyAttack ? 1.5f : 1.0f,
                            WasBlockedOrParried = result.WasBlocked || result.WasParried,
                            IsHeavyHit = hitPayload.IsHeavyAttack || result.TriggeredStanceBreak
                        });
                    }
                }
                else
                {
                    // Environment wall/floor spark check (only once per swing)
                    int colliderHash = col.GetHashCode();
                    if (!col.isTrigger && !HasAlreadyHitEntity(colliderHash))
                    {
                        RegisterHitEntity(colliderHash);
                        EventBus<OnHitImpactEvent>.Publish(new OnHitImpactEvent
                        {
                            HitPoint = _hitBuffer[i].point,
                            HitNormal = _hitBuffer[i].normal,
                            WeaponMaterial = _activePayload.WeaponMaterial,
                            SurfaceMaterial = SurfaceMaterialType.Stone,
                            ImpactForce = 0.6f,
                            WasBlockedOrParried = true,
                            IsHeavyHit = false
                        });
                    }
                }
            }
        }

        private bool HasAlreadyHitEntity(int entityId)
        {
            for (int i = 0; i < _alreadyHitCount; i++)
            {
                if (_alreadyHitEntityIds[i] == entityId) return true;
            }
            return false;
        }

        private void RegisterHitEntity(int entityId)
        {
            if (_alreadyHitCount < MaxUniqueTargetsPerSwing)
            {
                _alreadyHitEntityIds[_alreadyHitCount++] = entityId;
            }
        }
    }
}
