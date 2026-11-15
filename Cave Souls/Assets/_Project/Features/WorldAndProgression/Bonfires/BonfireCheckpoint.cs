using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Souls Bonfire / Sanctuary Checkpoint.
    /// Lighting & resting restores HP/FP/Stamina, refills Estus flasks, clears status effects,
    /// fires OnWorldResetEvent (respawning common enemies while keeping unique bosses dead),
    /// and triggers continuous auto-save.
    /// </summary>
    public sealed class BonfireCheckpoint : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private string _bonfireId = "Bonfire_CavernEntrance";
        [SerializeField] private Transform _respawnPoint;
        [SerializeField] private GameObject _litFlameVfx;
        [SerializeField] private bool _isLitByDefault;

        private bool _isLit;
        private WorldProgressionRegistry _registry;

        public string SaveId => _bonfireId;
        public string InteractionPrompt => _isLit ? "Rest at Bonfire" : "Light Bonfire";

        private void Awake()
        {
            _isLit = _isLitByDefault;
            UpdateVisualState();
        }

        private void Start()
        {
            if (ServiceLocator.TryGet(out _registry))
            {
                _registry.RegisterSaveable(this);
            }
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.UnregisterSaveable(this);
            }
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            if (!_isLit)
            {
                _isLit = true;
                UpdateVisualState();
            }

            // 1. Restore HP, FP, Stamina & Clear Status Effects on the player
            if (interactor != null && interactor.TryGetComponent(out CombatEntityVitals vitals))
            {
                vitals.RestoreAllVitals();
            }

            Vector3 spawnPos = _respawnPoint != null ? _respawnPoint.position : transform.position + transform.forward * 1.5f;
            Quaternion spawnRot = _respawnPoint != null ? _respawnPoint.rotation : transform.rotation;

            // 2. Broadcast Checkpoint Rested (Refills Estus Flasks & updates respawn point)
            EventBus<OnCheckpointRestedEvent>.Publish(new OnCheckpointRestedEvent
            {
                CheckpointId = _bonfireId,
                RespawnPosition = spawnPos,
                RespawnRotation = spawnRot
            });

            // 3. Broadcast World Reset (Respawns all non-unique common enemies)
            EventBus<OnWorldResetEvent>.Publish(new OnWorldResetEvent
            {
                CheckpointId = _bonfireId,
                CheckpointPosition = spawnPos
            });

            // 4. Request Auto-Save
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent
            {
                Reason = "BonfireRest"
            });
        }

        public bool CaptureState() => _isLit;

        public void RestoreState(bool state)
        {
            _isLit = state || _isLitByDefault;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_litFlameVfx != null)
            {
                _litFlameVfx.SetActive(_isLit);
            }
        }
    }
}
