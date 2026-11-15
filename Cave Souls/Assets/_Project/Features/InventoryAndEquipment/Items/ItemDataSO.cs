using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    public enum ItemCategory : byte
    {
        Consumable = 0,
        Weapon = 1,
        Armor = 2,
        KeyItem = 3,
        UpgradeMaterial = 4
    }

    /// <summary>
    /// Base immutable ScriptableObject for all inventory items.
    /// </summary>
    public abstract class ItemDataSO : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [TextArea(2, 4)] [SerializeField] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ItemCategory _category;
        [SerializeField] private int _maxStack = 99;

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public ItemCategory Category => _category;
        public int MaxStack => _maxStack;
    }
}
