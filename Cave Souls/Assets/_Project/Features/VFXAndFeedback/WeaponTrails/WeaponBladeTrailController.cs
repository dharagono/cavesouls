using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;

namespace CaveSouls.Features.VFXAndFeedback
{
    /// <summary>
    /// Synchronizes a TrailRenderer with DeterministicWeaponHitbox active windows
    /// and applies elemental shader colors using MaterialPropertyBlock and pre-hashed ShaderIDs.
    /// Zero GC allocations.
    /// </summary>
    public sealed class WeaponBladeTrailController : MonoBehaviour
    {
        [SerializeField] private DeterministicWeaponHitbox _weaponHitbox;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private Color _trailColor = new Color(0.9f, 0.92f, 1.0f, 0.8f);

        private MaterialPropertyBlock _propertyBlock;
        private bool _wasEmitting;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            if (_trailRenderer != null)
            {
                _trailRenderer.emitting = false;
                _trailRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ShaderIDs.BaseColor, _trailColor);
                _trailRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void LateUpdate()
        {
            if (_weaponHitbox == null || _trailRenderer == null) return;

            bool shouldEmit = _weaponHitbox.IsHitboxOpen;
            if (shouldEmit != _wasEmitting)
            {
                _wasEmitting = shouldEmit;
                _trailRenderer.emitting = shouldEmit;
            }
        }

        public void SetElementalTrailColor(Color baseColor, Color emissionColor)
        {
            if (_trailRenderer == null) return;
            _trailRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ShaderIDs.BaseColor, baseColor);
            _propertyBlock.SetColor(ShaderIDs.EmissionColor, emissionColor);
            _trailRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
