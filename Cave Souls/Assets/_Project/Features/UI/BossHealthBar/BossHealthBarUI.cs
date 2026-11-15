using CaveSouls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Boss Health Bar UI with Ghost Bar and Phase Indicator.
    /// Listens to OnBossFightStateChangedEvent via EventBus. Zero GC allocations.
    /// </summary>
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _bossNameLabel;
        [SerializeField] private ReactiveGhostBarWidget _bossHealthGhostBar;

        private bool _isVisible;

        private void Awake()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus<OnBossFightStateChangedEvent>.Subscribe(HandleBossFightStateChanged);
        }

        private void OnDisable()
        {
            EventBus<OnBossFightStateChangedEvent>.Unsubscribe(HandleBossFightStateChanged);
        }

        private void Update()
        {
            if (!_isVisible) return;
            if (_bossHealthGhostBar != null)
            {
                _bossHealthGhostBar.TickBar(Time.unscaledDeltaTime);
            }
        }

        private void HandleBossFightStateChanged(in OnBossFightStateChangedEvent evt)
        {
            if (evt.IsActive && evt.MaxHealth > 0f)
            {
                if (!_isVisible)
                {
                    SetVisible(true);
                    if (_bossNameLabel != null)
                    {
                        _bossNameLabel.text = evt.BossDisplayName;
                    }
                    _bossHealthGhostBar?.SetNormalizedValue(evt.CurrentHealth / evt.MaxHealth, immediate: true);
                }
                else
                {
                    _bossHealthGhostBar?.SetNormalizedValue(evt.CurrentHealth / evt.MaxHealth, immediate: false);
                }
            }
            else
            {
                SetVisible(false);
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }
    }
}
