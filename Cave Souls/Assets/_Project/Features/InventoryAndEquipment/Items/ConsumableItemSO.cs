using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    public enum ConsumableEffectType : byte
    {
        RestoreHealth = 0,
        RestoreFocus = 1,
        CurePoison = 2,
        CureBleedAndFrost = 3,
        GrantSouls = 4
    }

    /// <summary>
    /// Immutable ScriptableObject for Estus Flasks and tactical consumables.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_Consumable_", menuName = "CaveSouls/Items/Consumable Item")]
    public sealed class ConsumableItemSO : ItemDataSO
    {
        [Header("Consumable Settings")]
        [SerializeField] private ConsumableEffectType _effectType = ConsumableEffectType.RestoreHealth;
        [SerializeField] private float _effectAmount = 350f;
        [SerializeField] private bool _isRefillableEstus = true;
        [SerializeField] private int _maxEstusCharges = 5;
        [SerializeField] private bool _triggersBossHealPunishEvent = true;

        public ConsumableEffectType EffectType => _effectType;
        public float EffectAmount => _effectAmount;
        public bool IsRefillableEstus => _isRefillableEstus;
        public int MaxEstusCharges => _maxEstusCharges;
        public bool TriggersBossHealPunishEvent => _triggersBossHealPunishEvent;
    }
}
