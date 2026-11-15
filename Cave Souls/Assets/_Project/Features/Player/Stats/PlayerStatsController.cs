using CaveSouls.Core;
using CaveSouls.Features.Combat;
using CaveSouls.Features.InventoryAndEquipment;
using UnityEngine;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Runtime RPG Attribute, Equip Load Ratio (<30% Fast, 30-70% Medium, 70-100% Fat, >100% Overencumbered),
    /// Weapon Scaling integrator, and Souls currency manager.
    /// Zero GC allocations in Update.
    /// </summary>
    [RequireComponent(typeof(CombatEntityVitals), typeof(EquipmentAndInventoryManager))]
    public sealed class PlayerStatsController : MonoBehaviour
    {
        [SerializeField] private PlayerStatsSO _statsConfig;

        [Header("Runtime Attributes")]
        [SerializeField] private int _vigor = 12;
        [SerializeField] private int _mind = 10;
        [SerializeField] private int _endurance = 11;
        [SerializeField] private int _strength = 13;
        [SerializeField] private int _dexterity = 12;
        [SerializeField] private int _intelligence = 9;
        [SerializeField] private int _faith = 9;

        private CombatEntityVitals _vitals;
        private PoiseController _poiseController;
        private EquipmentAndInventoryManager _equipmentManager;

        private int _currentSouls;
        private float _currentEquipWeight;
        private float _maxEquipLoad = 50f;
        private float _equipLoadRatio;
        private EquipLoadTier _equipLoadTier;

        public int Vigor => _vigor;
        public int Mind => _mind;
        public int Endurance => _endurance;
        public int Strength => _strength;
        public int Dexterity => _dexterity;
        public int Intelligence => _intelligence;
        public int Faith => _faith;

        public int CurrentSouls => _currentSouls;
        public float CurrentEquipWeight => _currentEquipWeight;
        public float MaxEquipLoad => _maxEquipLoad;
        public float EquipLoadRatio => _equipLoadRatio;
        public EquipLoadTier EquipLoadTier => _equipLoadTier;

        private void Awake()
        {
            _vitals = GetComponent<CombatEntityVitals>();
            _poiseController = GetComponent<PoiseController>();
            _equipmentManager = GetComponent<EquipmentAndInventoryManager>();

            if (_statsConfig != null)
            {
                _vigor = _statsConfig.StartingVigor;
                _mind = _statsConfig.StartingMind;
                _endurance = _statsConfig.StartingEndurance;
                _strength = _statsConfig.StartingStrength;
                _dexterity = _statsConfig.StartingDexterity;
                _intelligence = _statsConfig.StartingIntelligence;
                _faith = _statsConfig.StartingFaith;
            }

            RecalculateAllDerivedStats();
        }

        private void OnEnable()
        {
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
        }

        private void OnDisable()
        {
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
        }

        public void RecalculateAllDerivedStats()
        {
            float maxHp = _statsConfig != null ? _statsConfig.EvaluateMaxHealth(_vigor) : 300f + (_vigor * 25f);
            float maxFp = _statsConfig != null ? _statsConfig.EvaluateMaxFocus(_mind) : 50f + (_mind * 6f);
            float maxStamina = _statsConfig != null ? _statsConfig.EvaluateMaxStamina(_endurance) : 80f + (_endurance * 2.5f);
            _maxEquipLoad = _statsConfig != null ? _statsConfig.EvaluateMaxEquipLoad(_endurance) : 45f + (_endurance * 1.5f);

            _currentEquipWeight = _equipmentManager != null ? _equipmentManager.CalculateTotalEquippedWeight() : 0f;
            _equipLoadRatio = _maxEquipLoad > 0f ? _currentEquipWeight / _maxEquipLoad : 0f;
            _equipLoadTier = EvaluateEquipLoadTier(_equipLoadRatio);

            int totalLevel = _vigor + _mind + _endurance + _strength + _dexterity + _intelligence + _faith;
            DefenseProfile baseDefense = new DefenseProfile
            {
                FlatPhysical = 30f + (totalLevel * 0.8f) + (_strength * 0.6f),
                FlatMagic = 25f + (totalLevel * 0.6f) + (_intelligence * 0.8f),
                FlatFire = 25f + (totalLevel * 0.6f) + (_vigor * 0.7f),
                FlatLightning = 25f + (totalLevel * 0.6f) + (_endurance * 0.6f),
                FlatDark = 25f + (totalLevel * 0.6f) + (_faith * 0.8f)
            };

            DefenseProfile finalDefense = _equipmentManager != null
                ? _equipmentManager.CalculateTotalDefenseProfile(in baseDefense)
                : baseDefense;

            ShieldBlockProfile guardProfile = _equipmentManager != null
                ? _equipmentManager.GetActiveGuardProfile()
                : default;

            _vitals.ConfigureDerivedStats(maxHp, maxFp, maxStamina, in finalDefense, in guardProfile);

            if (_poiseController != null)
            {
                float armorPoise = _equipmentManager != null ? _equipmentManager.CalculateTotalArmorPoiseBonus() : 0f;
                _poiseController.Configure(20f + armorPoise, 5f, 25f);
            }
        }

        public static EquipLoadTier EvaluateEquipLoadTier(float ratio)
        {
            if (ratio < 0.30f) return EquipLoadTier.FastRoll;
            if (ratio <= 0.70f) return EquipLoadTier.MediumRoll;
            if (ratio <= 1.00f) return EquipLoadTier.FatRoll;
            return EquipLoadTier.Overencumbered;
        }

        public DamagePayload BuildCurrentWeaponDamagePayload(AnimationCombatWindowSO animWindow, bool isCritical = false)
        {
            WeaponDataSO weapon = _equipmentManager != null ? _equipmentManager.RightHandWeapon : null;
            int upgradeLevel = _equipmentManager != null ? _equipmentManager.RightHandUpgradeLevel : 0;

            float motionVal = animWindow != null ? animWindow.MotionValueMultiplier : 1f;
            float poiseMult = animWindow != null ? animWindow.PoiseDamageMultiplier : 1f;
            bool isHeavy = animWindow != null && animWindow.IsHeavyAttack;
            bool canBeParried = animWindow == null || animWindow.CanBeParried;

            if (weapon != null)
            {
                return weapon.BuildScaledDamagePayload(
                    gameObject,
                    upgradeLevel,
                    _strength,
                    _dexterity,
                    _intelligence,
                    _faith,
                    motionVal,
                    poiseMult,
                    isHeavy,
                    isCritical,
                    canBeParried);
            }

            // Unarmed fallback
            return new DamagePayload
            {
                Attacker = gameObject,
                PhysicalSubType = PhysicalDamageSubType.Strike,
                PhysicalDamage = (20f + _strength * 1.2f) * motionVal,
                PoiseDamage = 10f * poiseMult,
                StaminaDrainOnBlock = 12f,
                WeaponMaterial = WeaponMaterialType.IronBlade,
                IsHeavyAttack = isHeavy,
                IsCriticalRiposteOrBackstab = isCritical,
                CanBeParried = canBeParried
            };
        }

        public void AddSouls(int amount)
        {
            if (amount <= 0) return;
            int prev = _currentSouls;
            _currentSouls += amount;
            EventBus<OnSoulsChangedEvent>.Publish(new OnSoulsChangedEvent
            {
                PreviousSouls = prev,
                CurrentSouls = _currentSouls,
                Delta = amount
            });
        }

        public int DropAllSoulsOnDeath()
        {
            int lost = _currentSouls;
            _currentSouls = 0;
            EventBus<OnSoulsChangedEvent>.Publish(new OnSoulsChangedEvent
            {
                PreviousSouls = lost,
                CurrentSouls = 0,
                Delta = -lost
            });
            return lost;
        }

        public void SetAttributesAndSouls(int vig, int mnd, int end, int str, int dex, int intel, int fth, int souls)
        {
            _vigor = vig;
            _mind = mnd;
            _endurance = end;
            _strength = str;
            _dexterity = dex;
            _intelligence = intel;
            _faith = fth;
            _currentSouls = Mathf.Max(0, souls);
            RecalculateAllDerivedStats();
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (!evt.IsPlayer && evt.SoulsReward > 0)
            {
                AddSouls(evt.SoulsReward);
            }
        }
    }
}
