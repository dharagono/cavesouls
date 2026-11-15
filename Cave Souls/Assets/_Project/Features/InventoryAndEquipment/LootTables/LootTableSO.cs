using System;
using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    [Serializable]
    public struct LootDropEntry
    {
        public ItemDataSO Item;
        [Range(0f, 1f)] public float DropChance;
        public int MinQuantity;
        public int MaxQuantity;
    }

    /// <summary>
    /// Immutable ScriptableObject defining enemy loot drop tables.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable_", menuName = "CaveSouls/Items/Loot Table")]
    public sealed class LootTableSO : ScriptableObject
    {
        [SerializeField] private LootDropEntry[] _entries;

        public LootDropEntry[] Entries => _entries;

        /// <summary>
        /// Evaluates loot drops into a pre-allocated buffer without GC allocations.
        /// Returns the number of dropped entries written to resultsBuffer.
        /// </summary>
        public int RollDropsNonAlloc(ItemDataSO[] itemResults, int[] quantityResults)
        {
            if (_entries == null || itemResults == null || quantityResults == null) return 0;

            int maxOut = Mathf.Min(itemResults.Length, quantityResults.Length);
            int dropCount = 0;

            for (int i = 0; i < _entries.Length && dropCount < maxOut; i++)
            {
                LootDropEntry entry = _entries[i];
                if (entry.Item == null) continue;

                if (UnityEngine.Random.value <= entry.DropChance)
                {
                    itemResults[dropCount] = entry.Item;
                    quantityResults[dropCount] = UnityEngine.Random.Range(
                        Mathf.Max(1, entry.MinQuantity),
                        Mathf.Max(1, entry.MaxQuantity) + 1);
                    dropCount++;
                }
            }

            return dropCount;
        }
    }
}
