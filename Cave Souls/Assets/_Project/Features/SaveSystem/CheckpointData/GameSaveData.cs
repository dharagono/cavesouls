using System;
using UnityEngine;

namespace CaveSouls.Features.SaveSystem
{
    [Serializable]
    public struct WorldStateFlagEntry
    {
        public string SaveId;
        public bool State;
    }

    [Serializable]
    public struct SavedInventoryItemEntry
    {
        public string ItemId;
        public int Quantity;
        public int UpgradeLevel;
    }

    /// <summary>
    /// Clean serializable GameSaveData payload for JSON/Binary serialization.
    /// Stores last checkpoint position, player attributes, souls, bloodstain state,
    /// upgraded weapons, inventory, and <string, bool> world flags (bosses defeated, doors unlocked, bonfires lit).
    /// </summary>
    [Serializable]
    public sealed class GameSaveData
    {
        public int SaveVersion = 1;
        public string LastCheckpointId = "Bonfire_Start";
        public Vector3 LastCheckpointPosition;
        public Quaternion LastCheckpointRotation = Quaternion.identity;

        // Player RPG Attributes & Currency
        public int Vigor = 12;
        public int Mind = 10;
        public int Endurance = 11;
        public int Strength = 13;
        public int Dexterity = 12;
        public int Intelligence = 9;
        public int Faith = 9;
        public int CurrentSouls;
        public int RightHandWeaponUpgradeLevel;

        // Bloodstain Persistence
        public bool HasActiveBloodstain;
        public Vector3 BloodstainPosition;
        public int BloodstainSouls;

        // Inventory & World Progression Flags (<string, bool> serialized as array for Unity JsonUtility)
        public SavedInventoryItemEntry[] InventoryEntries;
        public WorldStateFlagEntry[] WorldStateFlags;
    }
}
