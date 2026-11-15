using System.Collections.Generic;
using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Central runtime registry for all ISaveable world objects (Bosses, Bonfires, Shortcut Doors, Elevators, Chests).
    /// Avoids FindObjectsByType calls during runtime auto-saves.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class WorldProgressionRegistry : MonoBehaviour
    {
        private readonly List<ISaveable> _saveables = new List<ISaveable>(128);
        private Vector3 _lastRestedBonfirePosition;
        private Quaternion _lastRestedBonfireRotation = Quaternion.identity;
        private string _lastRestedBonfireId = "Bonfire_Start";

        public IReadOnlyList<ISaveable> Saveables => _saveables;
        public Vector3 LastRestedBonfirePosition => _lastRestedBonfirePosition;
        public Quaternion LastRestedBonfireRotation => _lastRestedBonfireRotation;
        public string LastRestedBonfireId => _lastRestedBonfireId;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            EventBus<OnCheckpointRestedEvent>.Subscribe(HandleCheckpointRested);
        }

        private void OnDisable()
        {
            EventBus<OnCheckpointRestedEvent>.Unsubscribe(HandleCheckpointRested);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        public void RegisterSaveable(ISaveable saveable)
        {
            if (saveable == null || string.IsNullOrEmpty(saveable.SaveId)) return;
            if (!_saveables.Contains(saveable))
            {
                _saveables.Add(saveable);
            }
        }

        public void UnregisterSaveable(ISaveable saveable)
        {
            if (saveable != null)
            {
                _saveables.Remove(saveable);
            }
        }

        public void SetLastCheckpoint(string checkpointId, Vector3 pos, Quaternion rot)
        {
            _lastRestedBonfireId = checkpointId;
            _lastRestedBonfirePosition = pos;
            _lastRestedBonfireRotation = rot;
        }

        private void HandleCheckpointRested(in OnCheckpointRestedEvent evt)
        {
            _lastRestedBonfireId = evt.CheckpointId;
            _lastRestedBonfirePosition = evt.RespawnPosition;
            _lastRestedBonfireRotation = evt.RespawnRotation;
        }
    }
}
