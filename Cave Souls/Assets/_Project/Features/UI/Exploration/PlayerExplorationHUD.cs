using CaveSouls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Lightweight, zero-GC HUD for cavern exploration in "El Lecho de Lodo Primordial".
    /// Displays localized interaction prompts, controls tutorial banner, and lore inspection dialogues.
    /// Fully event-driven via EventBus.
    /// </summary>
    public sealed class PlayerExplorationHUD : MonoBehaviour
    {
        [Header("Controls Tutorial Banner")]
        [SerializeField] private CanvasGroup _controlsBannerGroup;
        [SerializeField] private Text _controlsBannerText;
        [SerializeField] private float _controlsBannerDuration = 9.0f;

        [Header("Interaction Prompt")]
        [SerializeField] private CanvasGroup _promptGroup;
        [SerializeField] private Text _promptText;

        [Header("Lore Inspection Banner")]
        [SerializeField] private CanvasGroup _loreBannerGroup;
        [SerializeField] private Text _loreTitleText;
        [SerializeField] private Text _loreDescriptionText;
        [SerializeField] private float _loreDisplayDuration = 6.5f;

        private float _controlsTimer;
        private float _loreTimer;

        private void OnEnable()
        {
            EventBus<OnInteractionPromptChangedEvent>.Subscribe(HandleInteractionPromptChanged);
            EventBus<OnLoreInspectedEvent>.Subscribe(HandleLoreInspected);
        }

        private void OnDisable()
        {
            EventBus<OnInteractionPromptChangedEvent>.Unsubscribe(HandleInteractionPromptChanged);
            EventBus<OnLoreInspectedEvent>.Unsubscribe(HandleLoreInspected);
        }

        private void Start()
        {
            RefreshLocalizedControlsText();
            if (_promptGroup != null) _promptGroup.alpha = 0f;
            if (_loreBannerGroup != null) _loreBannerGroup.alpha = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_controlsBannerGroup != null && _controlsBannerGroup.alpha > 0f)
            {
                _controlsTimer += dt;
                if (_controlsTimer >= _controlsBannerDuration)
                {
                    _controlsBannerGroup.alpha = Mathf.MoveTowards(_controlsBannerGroup.alpha, 0f, dt * 1.5f);
                }
            }

            if (_loreBannerGroup != null && _loreBannerGroup.alpha > 0f)
            {
                _loreTimer += dt;
                if (_loreTimer >= _loreDisplayDuration)
                {
                    _loreBannerGroup.alpha = Mathf.MoveTowards(_loreBannerGroup.alpha, 0f, dt * 1.2f);
                }
            }
        }

        private void HandleInteractionPromptChanged(in OnInteractionPromptChangedEvent evt)
        {
            if (_promptGroup != null)
            {
                _promptGroup.alpha = evt.IsVisible ? 1f : 0f;
            }
            if (_promptText != null && evt.IsVisible && !string.IsNullOrEmpty(evt.PromptText))
            {
                _promptText.text = evt.PromptText;
            }
        }

        private void HandleLoreInspected(in OnLoreInspectedEvent evt)
        {
            if (_loreBannerGroup == null) return;

            if (_loreTitleText != null) _loreTitleText.text = evt.Title;
            if (_loreDescriptionText != null) _loreDescriptionText.text = evt.Description;

            _loreTimer = 0f;
            _loreBannerGroup.alpha = 1f;
        }

        public void RefreshLocalizedControlsText()
        {
            if (_controlsBannerText == null) return;

            bool isEs = GameLocalizationService.CurrentLanguage == GameLanguage.Spanish;
            _controlsBannerText.text = isEs
                ? "[ W A S D ]  M O V E R S E     |     [ S H I F T ]  C O R R E R     |     [ R A T Ó N ]  C Á M A R A"
                : "[ W A S D ]  M O V E     |     [ S H I F T ]  S P R I N T     |     [ M O U S E ]  C A M E R A";
        }
    }
}
