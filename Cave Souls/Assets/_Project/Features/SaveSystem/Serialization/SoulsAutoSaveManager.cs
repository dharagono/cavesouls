using System.Collections.Generic;
using System.IO;
using CaveSouls.Core;
using CaveSouls.Features.InventoryAndEquipment;
using CaveSouls.Features.Player;
using CaveSouls.Features.WorldAndProgression;
using UnityEngine;

namespace CaveSouls.Features.SaveSystem
{
    /// <summary>
    /// Continuous Auto-Save Manager (Souls-style anti-Save-Scumming).
    /// Automatically serializes and encrypts GameSaveData upon resting at Bonfires,
    /// defeating bosses, unlocking doors, upgrading weapons, or dying.
    /// </summary>
    public sealed class SoulsAutoSaveManager : MonoBehaviour
    {
        [SerializeField] private string _saveFileName = "cavesouls_save01.dat";
        [SerializeField] private bool _useEncryption = true;
        [SerializeField] private PlayerStatsController _playerStats;
        [SerializeField] private EquipmentAndInventoryManager _equipmentManager;
        [SerializeField] private BloodstainSoulRecoverySystem _bloodstainSystem;

        private readonly Dictionary<string, bool> _worldFlagsLookup = new Dictionary<string, bool>(128);
        private string _saveFilePath;

        private void Awake()
        {
            _saveFilePath = Path.Combine(Application.persistentDataPath, _saveFileName);
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            EventBus<OnGameSaveRequestedEvent>.Subscribe(HandleGameSaveRequested);
        }

        private void OnDisable()
        {
            EventBus<OnGameSaveRequestedEvent>.Unsubscribe(HandleGameSaveRequested);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        public void SaveGameNow()
        {
            GameSaveData data = new GameSaveData();

            if (ServiceLocator.TryGet(out WorldProgressionRegistry worldRegistry))
            {
                data.LastCheckpointId = worldRegistry.LastRestedBonfireId;
                data.LastCheckpointPosition = worldRegistry.LastRestedBonfirePosition;
                data.LastCheckpointRotation = worldRegistry.LastRestedBonfireRotation;

                IReadOnlyList<ISaveable> saveables = worldRegistry.Saveables;
                for (int i = 0; i < saveables.Count; i++)
                {
                    ISaveable s = saveables[i];
                    if (s != null && !string.IsNullOrEmpty(s.SaveId))
                    {
                        _worldFlagsLookup[s.SaveId] = s.CaptureState();
                    }
                }
            }

            if (_playerStats != null)
            {
                data.Vigor = _playerStats.Vigor;
                data.Mind = _playerStats.Mind;
                data.Endurance = _playerStats.Endurance;
                data.Strength = _playerStats.Strength;
                data.Dexterity = _playerStats.Dexterity;
                data.Intelligence = _playerStats.Intelligence;
                data.Faith = _playerStats.Faith;
                data.CurrentSouls = _playerStats.CurrentSouls;
            }

            if (_equipmentManager != null)
            {
                data.RightHandWeaponUpgradeLevel = _equipmentManager.RightHandUpgradeLevel;
                int invCount = _equipmentManager.InventoryCount;
                data.InventoryEntries = new SavedInventoryItemEntry[invCount];
                for (int i = 0; i < invCount; i++)
                {
                    InventorySlotState slot = _equipmentManager.GetInventorySlot(i);
                    data.InventoryEntries[i] = new SavedInventoryItemEntry
                    {
                        ItemId = slot.Item != null ? slot.Item.ItemId : string.Empty,
                        Quantity = slot.Quantity,
                        UpgradeLevel = slot.UpgradeLevel
                    };
                }
            }

            if (_bloodstainSystem != null && _bloodstainSystem.ActiveMarker != null)
            {
                data.HasActiveBloodstain = _bloodstainSystem.ActiveMarker.IsActive;
                data.BloodstainPosition = _bloodstainSystem.ActiveMarker.transform.position;
                data.BloodstainSouls = _bloodstainSystem.ActiveMarker.StoredSouls;
            }

            data.WorldStateFlags = new WorldStateFlagEntry[_worldFlagsLookup.Count];
            int idx = 0;
            foreach (KeyValuePair<string, bool> kvp in _worldFlagsLookup)
            {
                data.WorldStateFlags[idx++] = new WorldStateFlagEntry
                {
                    SaveId = kvp.Key,
                    State = kvp.Value
                };
            }

            string json = JsonUtility.ToJson(data, prettyPrint: false);
            if (_useEncryption)
            {
                byte[] encrypted = SaveCryptoUtility.EncryptJsonToBytes(json);
                File.WriteAllBytes(_saveFilePath, encrypted);
            }
            else
            {
                File.WriteAllText(_saveFilePath, json);
            }
        }

        public bool TryLoadGame()
        {
            if (!File.Exists(_saveFilePath)) return false;

            string json;
            if (_useEncryption)
            {
                byte[] bytes = File.ReadAllBytes(_saveFilePath);
                json = SaveCryptoUtility.DecryptBytesToJson(bytes);
            }
            else
            {
                json = File.ReadAllText(_saveFilePath);
            }

            if (string.IsNullOrEmpty(json)) return false;

            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
            if (data == null) return false;

            _worldFlagsLookup.Clear();
            if (data.WorldStateFlags != null)
            {
                for (int i = 0; i < data.WorldStateFlags.Length; i++)
                {
                    _worldFlagsLookup[data.WorldStateFlags[i].SaveId] = data.WorldStateFlags[i].State;
                }
            }

            if (ServiceLocator.TryGet(out WorldProgressionRegistry worldRegistry))
            {
                worldRegistry.SetLastCheckpoint(data.LastCheckpointId, data.LastCheckpointPosition, data.LastCheckpointRotation);
                IReadOnlyList<ISaveable> saveables = worldRegistry.Saveables;
                for (int i = 0; i < saveables.Count; i++)
                {
                    ISaveable s = saveables[i];
                    if (s != null && !string.IsNullOrEmpty(s.SaveId) && _worldFlagsLookup.TryGetValue(s.SaveId, out bool savedState))
                    {
                        s.RestoreState(savedState);
                    }
                }
            }

            if (_equipmentManager != null)
            {
                _equipmentManager.SetRightHandUpgradeLevel(data.RightHandWeaponUpgradeLevel);
            }

            if (_playerStats != null)
            {
                _playerStats.SetAttributesAndSouls(
                    data.Vigor,
                    data.Mind,
                    data.Endurance,
                    data.Strength,
                    data.Dexterity,
                    data.Intelligence,
                    data.Faith,
                    data.CurrentSouls);
            }

            if (_bloodstainSystem != null && _bloodstainSystem.ActiveMarker != null)
            {
                if (data.HasActiveBloodstain)
                {
                    _bloodstainSystem.ActiveMarker.PlaceBloodstain(data.BloodstainPosition, data.BloodstainSouls);
                }
                else
                {
                    _bloodstainSystem.ActiveMarker.DestroyBloodstainPermanently();
                }
            }

            return true;
        }

        private void HandleGameSaveRequested(in OnGameSaveRequestedEvent evt)
        {
            SaveGameNow();
        }
    }
}
