using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Lord Soul Monument found at the heart of Zone A, Zone B, and Zone C.
    /// Interacting claims the Lord Soul, granting it to the Hub's ApexSealMechanism,
    /// and activates the return shortcut.
    /// </summary>
    public sealed class LordSoulAltar : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private string _altarSaveId = "Altar_LordSoul_ZoneA";
        [SerializeField] private int _zoneIndex = 0; // 0 = Spire, 1 = Abyss, 2 = City
        [SerializeField] private string _lordSoulTitle = "Soul of the Spire Lord";
        [SerializeField] private GameObject _glowingSoulVfx;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _claimClip;
        [SerializeField] private ApexSealMechanism _hubApexMechanism;

        private bool _isClaimed;
        private WorldProgressionRegistry _registry;

        public string SaveId => _altarSaveId;
        public string InteractionPrompt => _isClaimed ? string.Empty : $"Claim { _lordSoulTitle }";

        private void Start()
        {
            if (ServiceLocator.TryGet(out _registry))
            {
                _registry.RegisterSaveable(this);
            }
            if (_hubApexMechanism == null)
            {
                _hubApexMechanism = UnityEngine.Object.FindAnyObjectByType<ApexSealMechanism>();
            }
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.UnregisterSaveable(this);
            }
        }

        public bool CanInteract(GameObject interactor) => !_isClaimed;

        public void Interact(GameObject interactor)
        {
            if (_isClaimed) return;

            _isClaimed = true;
            if (_audioSource != null && _claimClip != null)
            {
                _audioSource.PlayOneShot(_claimClip);
            }

            if (_hubApexMechanism != null)
            {
                _hubApexMechanism.GrantLordSoul(_zoneIndex);
            }

            UpdateVisuals();
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = $"LordSoulClaimed_{_altarSaveId}" });
        }

        public bool CaptureState() => _isClaimed;

        public void RestoreState(bool state)
        {
            _isClaimed = state;
            if (_isClaimed && _hubApexMechanism != null)
            {
                _hubApexMechanism.GrantLordSoul(_zoneIndex);
            }
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_glowingSoulVfx != null)
            {
                _glowingSoulVfx.SetActive(!_isClaimed);
            }
        }
    }
}
