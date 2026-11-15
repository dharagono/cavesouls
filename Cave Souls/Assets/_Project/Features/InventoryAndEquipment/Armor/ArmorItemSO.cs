using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    public enum ArmorSlot : byte
    {
        Head = 0,
        Chest = 1,
        Gauntlets = 2,
        Leggings = 3
    }

    /// <summary>
    /// Immutable ScriptableObject for Armor pieces.
    /// Contributes DefenseProfile, Poise bonus, Equip Weight, and acoustic noise modifier.
    /// </summary>
    [CreateAssetMenu(fileName = "Armor_", menuName = "CaveSouls/Equipment/Armor Item")]
    public sealed class ArmorItemSO : ItemDataSO
    {
        [Header("Armor Slot & Weight")]
        [SerializeField] private ArmorSlot _slot = ArmorSlot.Chest;
        [SerializeField] private float _weight = 6.5f;
        [SerializeField] private float _poiseBonus = 14f;
        [Range(0f, 1f)] [SerializeField] private float _armorClinkIntensity = 0.5f;

        [Header("Mitigation & Absorption")]
        [SerializeField] private DefenseProfile _defenseContribution;

        public ArmorSlot Slot => _slot;
        public float Weight => _weight;
        public float PoiseBonus => _poiseBonus;
        public float ArmorClinkIntensity => _armorClinkIntensity;
        public DefenseProfile DefenseContribution => _defenseContribution;
    }
}
