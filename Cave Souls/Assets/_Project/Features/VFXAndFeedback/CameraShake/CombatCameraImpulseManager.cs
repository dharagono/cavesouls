using CaveSouls.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace CaveSouls.Features.VFXAndFeedback
{
    /// <summary>
    /// Triggers Cinemachine Impulse camera shakes scaled by hit force and stance breaks.
    /// Zero GC allocations.
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public sealed class CombatCameraImpulseManager : MonoBehaviour
    {
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [SerializeField] private float _baseShakeMultiplier = 0.45f;
        [SerializeField] private float _poiseBreakShakeIntensity = 1.25f;

        private void Awake()
        {
            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }
        }

        private void OnEnable()
        {
            EventBus<OnHitImpactEvent>.Subscribe(HandleHitImpact);
            EventBus<OnPoiseBrokenEvent>.Subscribe(HandlePoiseBroken);
        }

        private void OnDisable()
        {
            EventBus<OnHitImpactEvent>.Unsubscribe(HandleHitImpact);
            EventBus<OnPoiseBrokenEvent>.Unsubscribe(HandlePoiseBroken);
        }

        private void HandleHitImpact(in OnHitImpactEvent evt)
        {
            if (_impulseSource == null) return;
            float force = evt.ImpactForce * _baseShakeMultiplier;
            _impulseSource.GenerateImpulseWithForce(force);
        }

        private void HandlePoiseBroken(in OnPoiseBrokenEvent evt)
        {
            if (_impulseSource == null) return;
            _impulseSource.GenerateImpulseWithForce(_poiseBreakShakeIntensity);
        }
    }
}
