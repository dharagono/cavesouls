using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Boss Arena Fog Wall.
    /// Allows the player to traverse into the arena, then seals its solid collider during the boss fight
    /// and disables permanently when the boss is defeated.
    /// </summary>
    public sealed class FogWallArenaGate : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _associatedBossId = "Boss_AbyssalWarden_01";
        [SerializeField] private Collider _blockingBarrierCollider;
        [SerializeField] private GameObject _fogVisualObject;
        [SerializeField] private Transform _arenaEntryTeleportPoint;

        private bool _isBossDefeated;
        private bool _isPlayerInsideArena;

        public string InteractionPrompt => "Traverse the White Light";

        private void OnEnable()
        {
            EventBus<OnBossFightStateChangedEvent>.Subscribe(HandleBossStateChanged);
            EventBus<OnWorldResetEvent>.Subscribe(HandleWorldReset);
        }

        private void OnDisable()
        {
            EventBus<OnBossFightStateChangedEvent>.Unsubscribe(HandleBossStateChanged);
            EventBus<OnWorldResetEvent>.Unsubscribe(HandleWorldReset);
        }

        public bool CanInteract(GameObject interactor) => !_isBossDefeated && !_isPlayerInsideArena;

        public void Interact(GameObject interactor)
        {
            if (_isBossDefeated || _isPlayerInsideArena || interactor == null) return;

            _isPlayerInsideArena = true;
            if (_arenaEntryTeleportPoint != null)
            {
                if (interactor.TryGetComponent(out CharacterController cc))
                {
                    cc.enabled = false;
                    interactor.transform.SetPositionAndRotation(_arenaEntryTeleportPoint.position, _arenaEntryTeleportPoint.rotation);
                    cc.enabled = true;
                }
                else
                {
                    interactor.transform.SetPositionAndRotation(_arenaEntryTeleportPoint.position, _arenaEntryTeleportPoint.rotation);
                }
            }

            if (_blockingBarrierCollider != null) _blockingBarrierCollider.enabled = true;
            if (_fogVisualObject != null) _fogVisualObject.SetActive(true);
        }

        private void HandleBossStateChanged(in OnBossFightStateChangedEvent evt)
        {
            if (evt.BossId != _associatedBossId) return;

            if (!evt.IsActive && evt.CurrentHealth <= 0f)
            {
                _isBossDefeated = true;
                _isPlayerInsideArena = false;
                if (_blockingBarrierCollider != null) _blockingBarrierCollider.enabled = false;
                if (_fogVisualObject != null) _fogVisualObject.SetActive(false);
            }
        }

        private void HandleWorldReset(in OnWorldResetEvent evt)
        {
            if (_isBossDefeated) return;
            _isPlayerInsideArena = false;
        }
    }
}
