using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Handles front Riposte (on StanceBroken / GuardBroken targets) and rear Backstab geometric validation
    /// and execution. Zero GC allocations.
    /// </summary>
    public sealed class ParryAndCriticalSystem : MonoBehaviour
    {
        private const int MaxCriticalCandidates = 8;

        [SerializeField] private float _criticalCheckRadius = 1.85f;
        [SerializeField] private float _riposteFrontDotThreshold = 0.55f;
        [SerializeField] private float _backstabRearDotThreshold = 0.65f;
        [SerializeField] private LayerMask _targetLayerMask = ~0;

        private readonly Collider[] _candidateBuffer = new Collider[MaxCriticalCandidates];

        /// <summary>
        /// Checks if there is a valid Riposte (front of stance-broken enemy) or Backstab (behind enemy) target.
        /// </summary>
        public bool TryFindCriticalTarget(
            Transform attackerTransform,
            int attackerEntityId,
            out IDamageable criticalTarget,
            out bool isRiposte)
        {
            criticalTarget = null;
            isRiposte = false;

            int count = Physics.OverlapSphereNonAlloc(
                attackerTransform.position,
                _criticalCheckRadius,
                _candidateBuffer,
                _targetLayerMask,
                QueryTriggerInteraction.Collide);

            Vector3 attackerPos = attackerTransform.position;
            Vector3 attackerForward = attackerTransform.forward;
            attackerForward.y = 0f;
            attackerForward.Normalize();

            for (int i = 0; i < count; i++)
            {
                Collider col = _candidateBuffer[i];
                if (col == null) continue;

                if (!col.TryGetComponent(out IDamageable candidate))
                {
                    if (col.attachedRigidbody == null || !col.attachedRigidbody.TryGetComponent(out candidate))
                    {
                        continue;
                    }
                }

                if (candidate == null || candidate.EntityId == attackerEntityId || !candidate.IsAlive) continue;

                Transform targetTr = candidate.Transform;
                Vector3 toTarget = targetTr.position - attackerPos;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.001f) continue;
                toTarget.Normalize();

                // Attacker must be facing the target
                if (Vector3.Dot(attackerForward, toTarget) < 0.5f) continue;

                Vector3 targetForward = targetTr.forward;
                targetForward.y = 0f;
                targetForward.Normalize();

                // 1. Check Front Riposte if target is StanceBroken
                if (candidate.IsStanceBroken)
                {
                    // Facing each other: Dot(attackerForward, -targetForward) > threshold
                    float frontAlignment = Vector3.Dot(attackerForward, -targetForward);
                    if (frontAlignment >= _riposteFrontDotThreshold)
                    {
                        criticalTarget = candidate;
                        isRiposte = true;
                        return true;
                    }
                }

                // 2. Check Rear Backstab
                float rearAlignment = Vector3.Dot(attackerForward, targetForward);
                if (rearAlignment >= _backstabRearDotThreshold)
                {
                    criticalTarget = candidate;
                    isRiposte = false;
                    return true;
                }
            }

            return false;
        }
    }
}
