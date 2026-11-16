using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Interactive lore monument or environmental item in the cavern.
    /// Implements IInteractable and publishes OnLoreInspectedEvent when inspected.
    /// </summary>
    public sealed class LoreInteractable : MonoBehaviour, IInteractable
    {
        [Header("Lore Content (Spanish)")]
        [SerializeField] private string _titleEs = "Restos de Ropa Superficial";
        [TextArea(2, 5)]
        [SerializeField] private string _descriptionEs = "Tiras de tela tejida y desteñida... un tenue recuerdo de cuando nuestros ancestros aún caminaban bajo la luz del cielo.";

        [Header("Lore Content (English)")]
        [SerializeField] private string _titleEn = "Remains of Surface Garments";
        [TextArea(2, 5)]
        [SerializeField] private string _descriptionEn = "Faded strips of woven cloth... a fragile relic of when our ancestors still walked beneath the light of the sky.";

        [Header("Audio")]
        [SerializeField] private AudioClip _interactChimeClip;
        [SerializeField] private AudioSource _audioSource;

        [Header("Visual Glow")]
        [SerializeField] private Light _crystalGlowLight;

        private bool _hasBeenInspected;

        public string InteractionPrompt => GameLocalizationService.CurrentLanguage == GameLanguage.Spanish
            ? "[E]  E X A M I N A R   R E S T O S"
            : "[E]  E X A M I N E   R E M A I N S";

        public bool CanInteract(GameObject interactor) => true;

        public string GetLocalizedTitle()
        {
            return GameLocalizationService.CurrentLanguage == GameLanguage.Spanish ? _titleEs : _titleEn;
        }

        public string GetLocalizedDescription()
        {
            return GameLocalizationService.CurrentLanguage == GameLanguage.Spanish ? _descriptionEs : _descriptionEn;
        }

        public void Interact(GameObject interactor)
        {
            _hasBeenInspected = true;

            if (_audioSource != null && _interactChimeClip != null)
            {
                _audioSource.PlayOneShot(_interactChimeClip, 0.95f);
            }

            if (_crystalGlowLight != null)
            {
                _crystalGlowLight.intensity = 2.5f;
            }

            EventBus<OnLoreInspectedEvent>.Publish(new OnLoreInspectedEvent
            {
                Title = GetLocalizedTitle(),
                Description = GetLocalizedDescription()
            });
        }
    }
}
