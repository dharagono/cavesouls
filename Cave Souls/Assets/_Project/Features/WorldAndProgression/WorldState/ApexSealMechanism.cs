using System;
using System.Collections.Generic;
using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Mechanism in Layer 1 (The Hub - El Santuario Neutral) that bars passage to Layer 3 (El Ápice).
    /// Requires claiming the 3 Lord Souls from Zone A (Las Agujas), Zone B (El Abismo), and Zone C (La Ciudad).
    /// Once all 3 souls are offered, unseals the Great Apex Elevator.
    /// Implements ISaveable for auto-save persistence.
    /// </summary>
    public sealed class ApexSealMechanism : MonoBehaviour, IInteractable, ISaveable
    {
        public const string SaveIdentifier = "Mechanism_ApexSeal_Hub";

        [Header("Pedestals & Runes")]
        [SerializeField] private GameObject _runeSpireZoneA;
        [SerializeField] private GameObject _runeAbyssZoneB;
        [SerializeField] private GameObject _runeCityZoneC;

        [Header("Door & Elevator Access")]
        [SerializeField] private GameObject _blockingSealBarrier;
        [SerializeField] private PersistentElevator _apexElevator;
        [SerializeField] private Animator _sealDoorAnimator;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _runeActivateClip;
        [SerializeField] private AudioClip _sealBrokenClip;

        private bool _hasLordSoulA;
        private bool _hasLordSoulB;
        private bool _hasLordSoulC;
        private bool _isSealBroken;

        private WorldProgressionRegistry _registry;

        public string SaveId => SaveIdentifier;
        public bool IsSealBroken => _isSealBroken;

        public string InteractionPrompt
        {
            get
            {
                if (_isSealBroken) return string.Empty;
                int count = (_hasLordSoulA ? 1 : 0) + (_hasLordSoulB ? 1 : 0) + (_hasLordSoulC ? 1 : 0);
                if (count == 3) return "Commune with the Apex Seal (Unseal the Heights)";
                return $"The Seal of Stone bars the sky ({count}/3 Lord Souls offered)";
            }
        }

        private void Start()
        {
            if (ServiceLocator.TryGet(out _registry))
            {
                _registry.RegisterSaveable(this);
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

        public bool CanInteract(GameObject interactor) => !_isSealBroken;

        public void Interact(GameObject interactor)
        {
            if (_isSealBroken) return;

            int count = (_hasLordSoulA ? 1 : 0) + (_hasLordSoulB ? 1 : 0) + (_hasLordSoulC ? 1 : 0);
            if (count >= 3)
            {
                BreakSeal();
            }
            else
            {
                if (_audioSource != null && _runeActivateClip != null)
                {
                    _audioSource.PlayOneShot(_runeActivateClip);
                }
            }
        }

        public void GrantLordSoul(int zoneIndex)
        {
            bool newlyClaimed = false;
            if (zoneIndex == 0 && !_hasLordSoulA) { _hasLordSoulA = true; newlyClaimed = true; }
            if (zoneIndex == 1 && !_hasLordSoulB) { _hasLordSoulB = true; newlyClaimed = true; }
            if (zoneIndex == 2 && !_hasLordSoulC) { _hasLordSoulC = true; newlyClaimed = true; }

            if (newlyClaimed)
            {
                if (_audioSource != null && _runeActivateClip != null)
                {
                    _audioSource.PlayOneShot(_runeActivateClip);
                }
                UpdateVisuals();
                EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = $"LordSoulClaimed_{zoneIndex}" });
            }
        }

        private void BreakSeal()
        {
            _isSealBroken = true;
            if (_blockingSealBarrier != null) _blockingSealBarrier.SetActive(false);
            if (_audioSource != null && _sealBrokenClip != null)
            {
                _audioSource.PlayOneShot(_sealBrokenClip);
            }
            if (_sealDoorAnimator != null)
            {
                _sealDoorAnimator.SetTrigger("Open");
            }

            UpdateVisuals();
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "ApexSealBroken" });
        }

        private void UpdateVisuals()
        {
            if (_runeSpireZoneA != null) _runeSpireZoneA.SetActive(_hasLordSoulA);
            if (_runeAbyssZoneB != null) _runeAbyssZoneB.SetActive(_hasLordSoulB);
            if (_runeCityZoneC != null) _runeCityZoneC.SetActive(_hasLordSoulC);
            if (_blockingSealBarrier != null) _blockingSealBarrier.SetActive(!_isSealBroken);
        }

        public bool CaptureState() => _isSealBroken;

        public void RestoreState(bool state)
        {
            _isSealBroken = state;
            if (_isSealBroken)
            {
                _hasLordSoulA = true;
                _hasLordSoulB = true;
                _hasLordSoulC = true;
            }
            UpdateVisuals();
        }
    }
}
