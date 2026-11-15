using CaveSouls.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CaveSouls.Features.VFXAndFeedback
{
    /// <summary>
    /// Pooled Particle + URP Decal Projector instance for Blood Splatters or Impact Sparks.
    /// Automatically returns to its ComponentPool after lifetime expires without Coroutines.
    /// </summary>
    public sealed class PooledVfxAndDecalInstance : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particleSystem;
        [SerializeField] private DecalProjector _decalProjector;
        [SerializeField] private float _lifetime = 4.0f;

        private ComponentPool<PooledVfxAndDecalInstance> _ownerPool;
        private float _timer;
        private float _initialDecalOpacity = 1f;

        private void Awake()
        {
            if (_decalProjector != null)
            {
                _initialDecalOpacity = _decalProjector.fadeFactor;
            }
        }

        public void Play(ComponentPool<PooledVfxAndDecalInstance> ownerPool, Vector3 decalPosition, Quaternion decalRotation, bool enableDecal)
        {
            _ownerPool = ownerPool;
            _timer = _lifetime;

            if (_particleSystem != null)
            {
                _particleSystem.Clear(true);
                _particleSystem.Play(true);
            }

            if (_decalProjector != null)
            {
                _decalProjector.enabled = enableDecal;
                if (enableDecal)
                {
                    _decalProjector.fadeFactor = _initialDecalOpacity;
                    _decalProjector.transform.SetPositionAndRotation(decalPosition, decalRotation);
                }
            }
        }

        private void Update()
        {
            _timer -= Time.deltaTime;

            if (_decalProjector != null && _decalProjector.enabled && _timer < 1.0f)
            {
                _decalProjector.fadeFactor = Mathf.Clamp01(_timer) * _initialDecalOpacity;
            }

            if (_timer <= 0f)
            {
                _ownerPool?.Release(this);
            }
        }
    }

    /// <summary>
    /// Object-Pooled Manager for Impact Sparks (on hitting walls/shields/armor) and Blood Particles
    /// with URP DecalProjector surface projection. Uses Physics.RaycastNonAlloc. Zero GC allocations.
    /// </summary>
    public sealed class CombatImpactVfxPoolManager : MonoBehaviour
    {
        [Header("Pooled Prefabs")]
        [SerializeField] private PooledVfxAndDecalInstance _sparkVfxPrefab;
        [SerializeField] private PooledVfxAndDecalInstance _bloodVfxAndDecalPrefab;

        [Header("Decal Projection Raycast Settings")]
        [SerializeField] private LayerMask _environmentProjectionMask = 1;
        [SerializeField] private float _decalProjectionDistance = 3.0f;

        private readonly RaycastHit[] _decalRayHits = new RaycastHit[2];
        private ComponentPool<PooledVfxAndDecalInstance> _sparkPool;
        private ComponentPool<PooledVfxAndDecalInstance> _bloodPool;

        private void Awake()
        {
            if (_sparkVfxPrefab != null)
            {
                _sparkPool = new ComponentPool<PooledVfxAndDecalInstance>(_sparkVfxPrefab, transform, 16, 64, prewarm: true);
            }

            if (_bloodVfxAndDecalPrefab != null)
            {
                _bloodPool = new ComponentPool<PooledVfxAndDecalInstance>(_bloodVfxAndDecalPrefab, transform, 16, 64, prewarm: true);
            }
        }

        private void OnEnable()
        {
            EventBus<OnHitImpactEvent>.Subscribe(HandleHitImpact);
        }

        private void OnDisable()
        {
            EventBus<OnHitImpactEvent>.Unsubscribe(HandleHitImpact);
        }

        private void HandleHitImpact(in OnHitImpactEvent evt)
        {
            Quaternion hitRot = evt.HitNormal != Vector3.zero
                ? Quaternion.LookRotation(evt.HitNormal)
                : Quaternion.identity;

            bool isFleshImpact = evt.SurfaceMaterial == SurfaceMaterialType.Flesh && !evt.WasBlockedOrParried;

            if (isFleshImpact)
            {
                if (_bloodPool == null) return;

                PooledVfxAndDecalInstance bloodInstance = _bloodPool.Get(evt.HitPoint, hitRot);

                // Project Blood Decal onto floor or nearby wall using RaycastNonAlloc
                Vector3 rayDir = ( Vector3.down * 0.7f - evt.HitNormal * 0.3f).normalized;
                int hitCount = Physics.RaycastNonAlloc(
                    evt.HitPoint,
                    rayDir,
                    _decalRayHits,
                    _decalProjectionDistance,
                    _environmentProjectionMask,
                    QueryTriggerInteraction.Ignore);

                if (hitCount > 0 && _decalRayHits[0].collider != null)
                {
                    Vector3 decalPos = _decalRayHits[0].point + (_decalRayHits[0].normal * 0.02f);
                    Quaternion decalRot = Quaternion.LookRotation(-_decalRayHits[0].normal);
                    bloodInstance.Play(_bloodPool, decalPos, decalRot, enableDecal: true);
                }
                else
                {
                    bloodInstance.Play(_bloodPool, evt.HitPoint, hitRot, enableDecal: false);
                }
            }
            else
            {
                if (_sparkPool == null) return;
                PooledVfxAndDecalInstance sparkInstance = _sparkPool.Get(evt.HitPoint, hitRot);
                sparkInstance.Play(_sparkPool, evt.HitPoint, hitRot, enableDecal: false);
            }
        }
    }
}
