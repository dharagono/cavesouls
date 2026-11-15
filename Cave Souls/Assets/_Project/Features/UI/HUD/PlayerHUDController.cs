using CaveSouls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Souls-like Reactive "Ghost Bar" widget for Health, Focus (FP), and Stamina bars.
    /// Displays a delayed yellow/white sub-bar underneath that smoothly drains after a hit.
    /// Strictly zero GC allocations in Update.
    /// </summary>
    public sealed class ReactiveGhostBarWidget : MonoBehaviour
    {
        [SerializeField] private Image _primaryFillImage;
        [SerializeField] private Image _ghostFillImage;
        [SerializeField] private float _ghostHoldDelay = 0.55f;
        [SerializeField] private float _ghostDrainSpeed = 0.45f;

        private float _targetFill = 1f;
        private float _ghostFill = 1f;
        private float _holdTimer;

        public void SetNormalizedValue(float normalizedValue, bool immediate = false)
        {
            float clamped = Mathf.Clamp01(normalizedValue);

            if (immediate || clamped >= _targetFill)
            {
                _targetFill = clamped;
                if (immediate || clamped > _ghostFill)
                {
                    _ghostFill = clamped;
                    _holdTimer = 0f;
                }
            }
            else
            {
                // Value dropped: hold the ghost bar before draining
                _targetFill = clamped;
                _holdTimer = _ghostHoldDelay;
            }

            ApplyFillImages();
        }

        public void TickBar(float deltaTime)
        {
            if (_ghostFill <= _targetFill) return;

            if (_holdTimer > 0f)
            {
                _holdTimer -= deltaTime;
                return;
            }

            _ghostFill = Mathf.MoveTowards(_ghostFill, _targetFill, _ghostDrainSpeed * deltaTime);
            ApplyFillImages();
        }

        private void ApplyFillImages()
        {
            if (_primaryFillImage != null)
            {
                _primaryFillImage.fillAmount = _targetFill;
            }

            if (_ghostFillImage != null)
            {
                _ghostFillImage.fillAmount = _ghostFill;
            }
        }
    }

    /// <summary>
    /// Main Player HUD Controller listening to OnVitalsChangedEvent via EventBus.
    /// Zero coupling to combat/player internals and zero GC allocations in Update.
    /// </summary>
    public sealed class PlayerHUDController : MonoBehaviour
    {
        [Header("Player Ghost Bars")]
        [SerializeField] private ReactiveGhostBarWidget _healthBar;
        [SerializeField] private ReactiveGhostBarWidget _focusBar;
        [SerializeField] private ReactiveGhostBarWidget _staminaBar;

        private void OnEnable()
        {
            EventBus<OnVitalsChangedEvent>.Subscribe(HandleVitalsChanged);
        }

        private void OnDisable()
        {
            EventBus<OnVitalsChangedEvent>.Unsubscribe(HandleVitalsChanged);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_healthBar != null) _healthBar.TickBar(dt);
            if (_focusBar != null) _focusBar.TickBar(dt);
            if (_staminaBar != null) _staminaBar.TickBar(dt);
        }

        private void HandleVitalsChanged(in OnVitalsChangedEvent evt)
        {
            if (!evt.IsPlayer) return;

            if (_healthBar != null && evt.MaxHealth > 0f)
            {
                _healthBar.SetNormalizedValue(evt.CurrentHealth / evt.MaxHealth);
            }

            if (_focusBar != null && evt.MaxFocus > 0f)
            {
                _focusBar.SetNormalizedValue(evt.CurrentFocus / evt.MaxFocus);
            }

            if (_staminaBar != null && evt.MaxStamina > 0f)
            {
                _staminaBar.SetNormalizedValue(evt.CurrentStamina / evt.MaxStamina);
            }
        }
    }
}
