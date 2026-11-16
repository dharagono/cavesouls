using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Audio
{
    /// <summary>
    /// Surface-aware Footstep System.
    /// Casts a short Physics.RaycastNonAlloc downward from the foot transform on each OnFootstep animation event,
    /// detects the SurfaceMaterialIdentifier or PhysicsMaterial, and publishes OnFootstepEvent
    /// with the equipped armor weight ratio. Zero GC allocations.
    /// </summary>
    public sealed class FootstepSystem : MonoBehaviour
    {
        [SerializeField] private Transform _leftFootTransform;
        [SerializeField] private Transform _rightFootTransform;
        [SerializeField] private float _raycastDistance = 0.6f;
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        private readonly RaycastHit[] _footHitBuffer = new RaycastHit[2];
        private float _currentArmorWeightRatio;
        private bool _isSprinting;

        public void SetFootstepContext(float armorWeightRatio, bool isSprinting)
        {
            _currentArmorWeightRatio = armorWeightRatio;
            _isSprinting = isSprinting;
        }

        /// <summary>
        /// Called by Unity Animation Event `OnFootstep(int footIndex)` (0 = Left, 1 = Right).
        /// </summary>
        public void OnFootstep(int footIndex)
        {
            Transform footTr = (footIndex == 0 ? _leftFootTransform : _rightFootTransform);
            if (footTr == null) footTr = transform;

            Vector3 rayOrigin = footTr.position + Vector3.up * 0.2f;
            SurfaceMaterialType detectedSurface = SurfaceMaterialType.Stone;

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                _footHitBuffer,
                _raycastDistance,
                _groundLayerMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0 && _footHitBuffer[0].collider != null)
            {
                Collider hitCol = _footHitBuffer[0].collider;
                if (hitCol.TryGetComponent(out SurfaceMaterialIdentifier surfaceId))
                {
                    detectedSurface = surfaceId.SurfaceType;
                }
            }

            EventBus<OnFootstepEvent>.Publish(new OnFootstepEvent
            {
                FootPosition = footTr.position,
                Surface = detectedSurface,
                ArmorWeightRatio = _currentArmorWeightRatio,
                IsSprinting = _isSprinting
            });
        }
    }
}
