using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.VFXAndFeedback
{
    /// <summary>
    /// HitStop Controller (Time Dilation / Freeze of 0.04s to 0.08s when connecting hits).
    /// Driven by unscaledDeltaTime in Update to avoid Coroutine allocations.
    /// </summary>
    public sealed class HitStopController : MonoBehaviour
    {
        [SerializeField] private float _lightHitStopDuration = 0.04f;
        [SerializeField] private float _heavyHitStopDuration = 0.08f;
        [SerializeField] private float _hitStopTimeScale = 0.05f;

        private float _remainingHitStopTimer;
        private bool _isHitStopped;

        private void OnEnable()
        {
            EventBus<OnHitImpactEvent>.Subscribe(HandleHitImpact);
            EventBus<OnPoiseBrokenEvent>.Subscribe(HandlePoiseBroken);
        }

        private void OnDisable()
        {
            EventBus<OnHitImpactEvent>.Unsubscribe(HandleHitImpact);
            EventBus<OnPoiseBrokenEvent>.Unsubscribe(HandlePoiseBroken);
            RestoreTimeScaleImmediate();
        }

        private void Update()
        {
            if (!_isHitStopped) return;

            _remainingHitStopTimer -= Time.unscaledDeltaTime;
            if (_remainingHitStopTimer <= 0f)
            {
                RestoreTimeScaleImmediate();
            }
        }

        public void TriggerHitStop(float durationSeconds)
        {
            _remainingHitStopTimer = Mathf.Max(_remainingHitStopTimer, durationSeconds);
            if (!_isHitStopped)
            {
                _isHitStopped = true;
                Time.timeScale = _hitStopTimeScale;
            }
        }

        private void RestoreTimeScaleImmediate()
        {
            if (_isHitStopped)
            {
                _isHitStopped = false;
                Time.timeScale = 1.0f;
            }
        }

        private void HandleHitImpact(in OnHitImpactEvent evt)
        {
            float duration = evt.IsHeavyHit ? _heavyHitStopDuration : _lightHitStopDuration;
            TriggerHitStop(duration);
        }

        private void HandlePoiseBroken(in OnPoiseBrokenEvent evt)
        {
            TriggerHitStop(_heavyHitStopDuration);
        }
    }
}
