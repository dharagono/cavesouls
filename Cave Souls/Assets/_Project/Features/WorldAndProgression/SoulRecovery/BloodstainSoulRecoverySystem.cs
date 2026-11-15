using CaveSouls.Core;
using CaveSouls.Features.Player;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Interactable Bloodstain Marker holding the player's dropped Souls on death.
    /// </summary>
    public sealed class BloodstainMarker : MonoBehaviour, IInteractable
    {
        private int _storedSouls;
        private bool _isActive;

        public string InteractionPrompt => "Touch Bloodstain (Recover Souls)";
        public int StoredSouls => _storedSouls;
        public bool IsActive => _isActive;

        public void PlaceBloodstain(Vector3 position, int soulsAmount)
        {
            _storedSouls = Mathf.Max(0, soulsAmount);
            _isActive = true;
            transform.position = position;
            gameObject.SetActive(true);
        }

        public void DestroyBloodstainPermanently()
        {
            _storedSouls = 0;
            _isActive = false;
            gameObject.SetActive(false);
        }

        public bool CanInteract(GameObject interactor) => _isActive;

        public void Interact(GameObject interactor)
        {
            if (!_isActive) return;

            if (interactor != null && interactor.TryGetComponent(out PlayerStatsController playerStats))
            {
                playerStats.AddSouls(_storedSouls);
            }

            DestroyBloodstainPermanently();
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "BloodstainRecovered" });
        }
    }

    /// <summary>
    /// Tracks the player's last safe grounded position every 2.0 seconds (if IsGrounded == true).
    /// On player death, destroys any previous uncollected Bloodstain and places the new Bloodstain
    /// at the last safe grounded position. Zero GC allocations.
    /// </summary>
    public sealed class BloodstainSoulRecoverySystem : MonoBehaviour
    {
        [SerializeField] private CharacterController _playerCharacterController;
        [SerializeField] private PlayerStatsController _playerStats;
        [SerializeField] private BloodstainMarker _bloodstainMarkerInstance;
        [SerializeField] private float _safePositionSampleInterval = 2.0f;

        private Vector3 _lastSafeGroundedPosition;
        private float _sampleTimer;
        private bool _hasValidSafePosition;

        public Vector3 LastSafeGroundedPosition => _lastSafeGroundedPosition;
        public BloodstainMarker ActiveMarker => _bloodstainMarkerInstance;

        private void Awake()
        {
            if (_playerCharacterController != null)
            {
                _lastSafeGroundedPosition = _playerCharacterController.transform.position;
                _hasValidSafePosition = true;
            }

            if (_bloodstainMarkerInstance != null)
            {
                _bloodstainMarkerInstance.DestroyBloodstainPermanently();
            }
        }

        private void OnEnable()
        {
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
        }

        private void OnDisable()
        {
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
        }

        private void Update()
        {
            if (_playerCharacterController == null) return;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= _safePositionSampleInterval)
            {
                _sampleTimer = 0f;
                if (_playerCharacterController.isGrounded)
                {
                    _lastSafeGroundedPosition = _playerCharacterController.transform.position;
                    _hasValidSafePosition = true;
                }
            }
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (!evt.IsPlayer || _playerStats == null || _bloodstainMarkerInstance == null) return;

            // If a previous bloodstain was not collected, its souls are lost forever
            if (_bloodstainMarkerInstance.IsActive)
            {
                _bloodstainMarkerInstance.DestroyBloodstainPermanently();
            }

            int droppedSouls = _playerStats.DropAllSoulsOnDeath();
            Vector3 dropPos = _hasValidSafePosition ? _lastSafeGroundedPosition : evt.DeathPosition;

            _bloodstainMarkerInstance.PlaceBloodstain(dropPos, droppedSouls);
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "PlayerDiedBloodstainCreated" });
        }
    }
}
