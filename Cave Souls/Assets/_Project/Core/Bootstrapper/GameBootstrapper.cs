using UnityEngine;

namespace CaveSouls.Core
{
    public enum GlobalGameState : byte
    {
        Boot = 0,
        Exploration = 1,
        BossEncounter = 2,
        RestingAtBonfire = 3,
        PlayerDead = 4
    }

    /// <summary>
    /// Central Bootstrapper and Game State Machine.
    /// Ensures core systems are initialized cleanly without singletons scattered across scenes.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private GlobalGameState _currentGameState = GlobalGameState.Boot;
        public GlobalGameState CurrentGameState => _currentGameState;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register(this);
            _currentGameState = GlobalGameState.Exploration;
        }

        private void OnEnable()
        {
            EventBus<OnBossFightStateChangedEvent>.Subscribe(HandleBossStateChanged);
            EventBus<OnCheckpointRestedEvent>.Subscribe(HandleCheckpointRested);
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
        }

        private void OnDisable()
        {
            EventBus<OnBossFightStateChangedEvent>.Unsubscribe(HandleBossStateChanged);
            EventBus<OnCheckpointRestedEvent>.Unsubscribe(HandleCheckpointRested);
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        public void SetGameState(GlobalGameState newState)
        {
            _currentGameState = newState;
        }

        private void HandleBossStateChanged(in OnBossFightStateChangedEvent evt)
        {
            _currentGameState = evt.IsActive ? GlobalGameState.BossEncounter : GlobalGameState.Exploration;
        }

        private void HandleCheckpointRested(in OnCheckpointRestedEvent evt)
        {
            _currentGameState = GlobalGameState.RestingAtBonfire;
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (evt.IsPlayer)
            {
                _currentGameState = GlobalGameState.PlayerDead;
            }
        }
    }
}
