using System;
using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    [Serializable]
    public struct InventorySlotState
    {
        public ItemDataSO Item;
        public int Quantity;
        public int UpgradeLevel;
    }

    /// <summary>
    /// Manages equipped Weapons, Armor, QuickSlots (Estus/Consumables), Total Equip Weight,
    /// combined DefenseProfile, and Checkpoint Estus refilling.
    /// Zero GC allocations during gameplay loops.
    /// </summary>
    public sealed class EquipmentAndInventoryManager : MonoBehaviour
    {
        private const int MaxQuickSlots = 4;
        private const int MaxInventoryCapacity = 64;

        [Header("Equipped Weapons")]
        [SerializeField] private WeaponDataSO _rightHandWeapon;
        [SerializeField] private int _rightHandUpgradeLevel;
        [SerializeField] private WeaponDataSO _leftHandShieldOrWeapon;
        [SerializeField] private int _leftHandUpgradeLevel;

        [Header("Equipped Armor")]
        [SerializeField] private ArmorItemSO _headArmor;
        [SerializeField] private ArmorItemSO _chestArmor;
        [SerializeField] private ArmorItemSO _gauntletsArmor;
        [SerializeField] private ArmorItemSO _leggingsArmor;

        [Header("QuickSlots (Estus & Consumables)")]
        [SerializeField] private ConsumableItemSO[] _quickSlotItems = new ConsumableItemSO[MaxQuickSlots];
        [SerializeField] private int[] _quickSlotCharges = new int[MaxQuickSlots];

        private readonly InventorySlotState[] _inventory = new InventorySlotState[MaxInventoryCapacity];
        private int _inventoryCount;
        private int _activeQuickSlotIndex;

        public WeaponDataSO RightHandWeapon => _rightHandWeapon;
        public int RightHandUpgradeLevel => _rightHandUpgradeLevel;
        public WeaponDataSO LeftHandShieldOrWeapon => _leftHandShieldOrWeapon;
        public int ActiveQuickSlotIndex => _activeQuickSlotIndex;
        public int InventoryCount => _inventoryCount;

        private void Awake()
        {
            RefillAllEstusFlasks();
        }

        private void OnEnable()
        {
            EventBus<OnCheckpointRestedEvent>.Subscribe(HandleCheckpointRested);
        }

        private void OnDisable()
        {
            EventBus<OnCheckpointRestedEvent>.Unsubscribe(HandleCheckpointRested);
        }

        public float CalculateTotalEquippedWeight()
        {
            float weight = 0f;
            if (_rightHandWeapon != null) weight += _rightHandWeapon.Weight;
            if (_leftHandShieldOrWeapon != null) weight += _leftHandShieldOrWeapon.Weight;
            if (_headArmor != null) weight += _headArmor.Weight;
            if (_chestArmor != null) weight += _chestArmor.Weight;
            if (_gauntletsArmor != null) weight += _gauntletsArmor.Weight;
            if (_leggingsArmor != null) weight += _leggingsArmor.Weight;
            return weight;
        }

        public float CalculateTotalArmorPoiseBonus()
        {
            float poise = 0f;
            if (_headArmor != null) poise += _headArmor.PoiseBonus;
            if (_chestArmor != null) poise += _chestArmor.PoiseBonus;
            if (_gauntletsArmor != null) poise += _gauntletsArmor.PoiseBonus;
            if (_leggingsArmor != null) poise += _leggingsArmor.PoiseBonus;
            return poise;
        }

        public float CalculateAverageArmorClinkIntensity()
        {
            float sum = 0f;
            int count = 0;
            if (_headArmor != null) { sum += _headArmor.ArmorClinkIntensity; count++; }
            if (_chestArmor != null) { sum += _chestArmor.ArmorClinkIntensity; count++; }
            if (_gauntletsArmor != null) { sum += _gauntletsArmor.ArmorClinkIntensity; count++; }
            if (_leggingsArmor != null) { sum += _leggingsArmor.ArmorClinkIntensity; count++; }
            return count > 0 ? sum / count : 0f;
        }

        public DefenseProfile CalculateTotalDefenseProfile(in DefenseProfile baseStatDefense)
        {
            DefenseProfile total = baseStatDefense;
            AccumulateArmorDefense(ref total, _headArmor);
            AccumulateArmorDefense(ref total, _chestArmor);
            AccumulateArmorDefense(ref total, _gauntletsArmor);
            AccumulateArmorDefense(ref total, _leggingsArmor);
            return total;
        }

        public ShieldBlockProfile GetActiveGuardProfile()
        {
            if (_leftHandShieldOrWeapon != null) return _leftHandShieldOrWeapon.GuardProfile;
            if (_rightHandWeapon != null) return _rightHandWeapon.GuardProfile;
            return new ShieldBlockProfile
            {
                PhysicalBlockPercent = 0.4f,
                Stability = 25f,
                ShieldMaterial = SurfaceMaterialType.Metal
            };
        }

        public void CycleQuickSlot()
        {
            if (_quickSlotItems == null || _quickSlotItems.Length == 0) return;
            _activeQuickSlotIndex = (_activeQuickSlotIndex + 1) % _quickSlotItems.Length;
        }

        public ConsumableItemSO GetActiveConsumable(out int remainingCharges)
        {
            if (_quickSlotItems == null || _activeQuickSlotIndex < 0 || _activeQuickSlotIndex >= _quickSlotItems.Length)
            {
                remainingCharges = 0;
                return null;
            }

            remainingCharges = _quickSlotCharges[_activeQuickSlotIndex];
            return _quickSlotItems[_activeQuickSlotIndex];
        }

        public bool TryConsumeActiveQuickSlot(CombatEntityVitals targetVitals)
        {
            ConsumableItemSO item = GetActiveConsumable(out int charges);
            if (item == null || charges <= 0 || targetVitals == null || !targetVitals.IsAlive) return false;

            _quickSlotCharges[_activeQuickSlotIndex] = charges - 1;

            if (item.TriggersBossHealPunishEvent)
            {
                EventBus<OnPlayerHealStartedEvent>.Publish(new OnPlayerHealStartedEvent
                {
                    Player = targetVitals.gameObject,
                    Position = targetVitals.transform.position
                });
            }

            switch (item.EffectType)
            {
                case ConsumableEffectType.RestoreHealth:
                    targetVitals.Heal(item.EffectAmount);
                    break;
                case ConsumableEffectType.RestoreFocus:
                    targetVitals.RestoreFocus(item.EffectAmount);
                    break;
                case ConsumableEffectType.CurePoison:
                case ConsumableEffectType.CureBleedAndFrost:
                    if (targetVitals.TryGetComponent(out StatusEffectController statusCtrl))
                    {
                        statusCtrl.ClearAllStatusEffects();
                    }
                    break;
            }

            return true;
        }

        public void RefillAllEstusFlasks()
        {
            if (_quickSlotItems == null || _quickSlotCharges == null) return;
            int len = Mathf.Min(_quickSlotItems.Length, _quickSlotCharges.Length);
            for (int i = 0; i < len; i++)
            {
                if (_quickSlotItems[i] != null && _quickSlotItems[i].IsRefillableEstus)
                {
                    _quickSlotCharges[i] = _quickSlotItems[i].MaxEstusCharges;
                }
            }
        }

        public bool UpgradeRightHandWeapon()
        {
            if (_rightHandWeapon == null || _rightHandUpgradeLevel >= WeaponDataSO.MaxUpgradeLevel) return false;
            _rightHandUpgradeLevel++;
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "WeaponUpgraded" });
            return true;
        }

        public void SetRightHandUpgradeLevel(int level)
        {
            _rightHandUpgradeLevel = Mathf.Clamp(level, 0, WeaponDataSO.MaxUpgradeLevel);
        }

        public bool AddItemToInventory(ItemDataSO item, int quantity = 1, int upgradeLevel = 0)
        {
            if (item == null || quantity <= 0) return false;

            for (int i = 0; i < _inventoryCount; i++)
            {
                if (_inventory[i].Item == item && _inventory[i].UpgradeLevel == upgradeLevel && _inventory[i].Quantity < item.MaxStack)
                {
                    _inventory[i].Quantity = Mathf.Min(item.MaxStack, _inventory[i].Quantity + quantity);
                    return true;
                }
            }

            if (_inventoryCount >= MaxInventoryCapacity) return false;

            _inventory[_inventoryCount++] = new InventorySlotState
            {
                Item = item,
                Quantity = Mathf.Min(item.MaxStack, quantity),
                UpgradeLevel = upgradeLevel
            };
            return true;
        }

        public InventorySlotState GetInventorySlot(int index) => _inventory[index];

        private void HandleCheckpointRested(in OnCheckpointRestedEvent evt)
        {
            RefillAllEstusFlasks();
        }

        private static void AccumulateArmorDefense(ref DefenseProfile total, ArmorItemSO armor)
        {
            if (armor == null) return;
            DefenseProfile c = armor.DefenseContribution;
            total.FlatPhysical += c.FlatPhysical;
            total.FlatMagic += c.FlatMagic;
            total.FlatFire += c.FlatFire;
            total.FlatLightning += c.FlatLightning;
            total.FlatDark += c.FlatDark;

            // Multiplicative absorption stacking: 1 - (1 - a1)*(1 - a2)
            total.AbsorbStandard = 1f - ((1f - total.AbsorbStandard) * (1f - c.AbsorbStandard));
            total.AbsorbStrike = 1f - ((1f - total.AbsorbStrike) * (1f - c.AbsorbStrike));
            total.AbsorbSlash = 1f - ((1f - total.AbsorbSlash) * (1f - c.AbsorbSlash));
            total.AbsorbThrust = 1f - ((1f - total.AbsorbThrust) * (1f - c.AbsorbThrust));
            total.AbsorbMagic = 1f - ((1f - total.AbsorbMagic) * (1f - c.AbsorbMagic));
            total.AbsorbFire = 1f - ((1f - total.AbsorbFire) * (1f - c.AbsorbFire));
            total.AbsorbLightning = 1f - ((1f - total.AbsorbLightning) * (1f - c.AbsorbLightning));
            total.AbsorbDark = 1f - ((1f - total.AbsorbDark) * (1f - c.AbsorbDark));
        }
    }
}
