using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;

namespace CaveSouls.Features.InventoryAndEquipment
{
    /// <summary>
    /// Immutable ScriptableObject for Weapons & Shields.
    /// Implements Souls-like Attribute Scaling (S, A, B, C, D, E) using AnimationCurves
    /// and Upgrade Levels (+0 to +10).
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "CaveSouls/Equipment/Weapon Data")]
    public sealed class WeaponDataSO : ItemDataSO
    {
        public const int MaxUpgradeLevel = 10;

        [Header("Base Damage & Properties")]
        [SerializeField] private PhysicalDamageSubType _physicalSubType = PhysicalDamageSubType.Slash;
        [SerializeField] private WeaponMaterialType _weaponMaterial = WeaponMaterialType.IronBlade;
        [SerializeField] private float _basePhysicalDamage = 115f;
        [SerializeField] private float _baseMagicDamage;
        [SerializeField] private float _baseFireDamage;
        [SerializeField] private float _baseLightningDamage;
        [SerializeField] private float _baseDarkDamage;
        [SerializeField] private float _basePoiseDamage = 25f;
        [SerializeField] private float _staminaDrainOnBlock = 28f;
        [SerializeField] private float _criticalMultiplier = 2.5f;
        [SerializeField] private float _weight = 4.5f;

        [Header("Status Effect Infusion")]
        [SerializeField] private StatusEffectType _statusEffectType = StatusEffectType.None;
        [SerializeField] private float _statusBuildUpPerHit;

        [Header("Attribute Scaling Grades (E to S)")]
        [SerializeField] private ScalingGrade _strengthScaling = ScalingGrade.C;
        [SerializeField] private ScalingGrade _dexterityScaling = ScalingGrade.D;
        [SerializeField] private ScalingGrade _intelligenceScaling = ScalingGrade.None;
        [SerializeField] private ScalingGrade _faithScaling = ScalingGrade.None;

        [Header("Attribute Saturation Curve (Input: Stat 1..99 -> Output: 0..1)")]
        [SerializeField] private AnimationCurve _attributeSaturationCurve = AnimationCurve.EaseInOut(1f, 0f, 99f, 1f);

        [Header("Upgrade Level Multiplier Curve (+0 to +10)")]
        [SerializeField] private AnimationCurve _upgradeDamageCurve = AnimationCurve.Linear(0f, 1.0f, 10f, 2.0f);

        [Header("Shield / Guard Absorption Profile")]
        [SerializeField] private ShieldBlockProfile _guardProfile = new ShieldBlockProfile
        {
            PhysicalBlockPercent = 0.55f,
            MagicBlockPercent = 0.35f,
            FireBlockPercent = 0.40f,
            LightningBlockPercent = 0.30f,
            DarkBlockPercent = 0.35f,
            Stability = 42f,
            ShieldMaterial = SurfaceMaterialType.Metal
        };

        [Header("Combat Move-Set Windows")]
        [SerializeField] private AnimationCombatWindowSO[] _lightAttackComboWindows;
        [SerializeField] private AnimationCombatWindowSO _heavyAttackWindow;
        [SerializeField] private AnimationCombatWindowSO _parryWindow;

        public PhysicalDamageSubType PhysicalSubType => _physicalSubType;
        public WeaponMaterialType WeaponMaterial => _weaponMaterial;
        public float BasePoiseDamage => _basePoiseDamage;
        public float StaminaDrainOnBlock => _staminaDrainOnBlock;
        public float CriticalMultiplier => _criticalMultiplier;
        public float Weight => _weight;
        public StatusEffectType StatusEffectType => _statusEffectType;
        public float StatusBuildUpPerHit => _statusBuildUpPerHit;
        public ShieldBlockProfile GuardProfile => _guardProfile;
        public AnimationCombatWindowSO[] LightAttackComboWindows => _lightAttackComboWindows;
        public AnimationCombatWindowSO HeavyAttackWindow => _heavyAttackWindow;
        public AnimationCombatWindowSO ParryWindow => _parryWindow;

        /// <summary>
        /// Computes a complete DamagePayload scaled by the player's attributes, upgrade level (+0..+10),
        /// and the attack animation's motion value. Zero GC allocations.
        /// </summary>
        public DamagePayload BuildScaledDamagePayload(
            GameObject attacker,
            int upgradeLevel,
            int strength,
            int dexterity,
            int intelligence,
            int faith,
            float motionValueMultiplier = 1f,
            float poiseMultiplier = 1f,
            bool isHeavyAttack = false,
            bool isCritical = false,
            bool canBeParried = true)
        {
            int clampedUpgrade = Mathf.Clamp(upgradeLevel, 0, MaxUpgradeLevel);
            float upgradeFactor = _upgradeDamageCurve != null
                ? _upgradeDamageCurve.Evaluate(clampedUpgrade)
                : 1f + (clampedUpgrade * 0.1f);

            // Upgrade level also slightly improves scaling coefficient (up to +25% at +10)
            float scalingUpgradeBoost = 1f + (clampedUpgrade * 0.025f);

            float strSaturation = EvaluateAttributeSaturation(strength);
            float dexSaturation = EvaluateAttributeSaturation(dexterity);
            float intSaturation = EvaluateAttributeSaturation(intelligence);
            float fthSaturation = EvaluateAttributeSaturation(faith);

            float physScalingFactor = 1f +
                ((GetGradeCoefficient(_strengthScaling) * strSaturation) +
                 (GetGradeCoefficient(_dexterityScaling) * dexSaturation)) * scalingUpgradeBoost;

            float magicScalingFactor = 1f + (GetGradeCoefficient(_intelligenceScaling) * intSaturation * scalingUpgradeBoost);
            float fireLightningScalingFactor = 1f + (GetGradeCoefficient(_faithScaling) * fthSaturation * scalingUpgradeBoost);
            float darkScalingFactor = 1f +
                ((GetGradeCoefficient(_intelligenceScaling) * intSaturation * 0.5f) +
                 (GetGradeCoefficient(_faithScaling) * fthSaturation * 0.5f)) * scalingUpgradeBoost;

            return new DamagePayload
            {
                Attacker = attacker,
                PhysicalSubType = _physicalSubType,
                PhysicalDamage = _basePhysicalDamage * upgradeFactor * physScalingFactor * motionValueMultiplier,
                MagicDamage = _baseMagicDamage * upgradeFactor * magicScalingFactor * motionValueMultiplier,
                FireDamage = _baseFireDamage * upgradeFactor * fireLightningScalingFactor * motionValueMultiplier,
                LightningDamage = _baseLightningDamage * upgradeFactor * fireLightningScalingFactor * motionValueMultiplier,
                DarkDamage = _baseDarkDamage * upgradeFactor * darkScalingFactor * motionValueMultiplier,
                PoiseDamage = _basePoiseDamage * poiseMultiplier,
                StaminaDrainOnBlock = _staminaDrainOnBlock * motionValueMultiplier,
                StatusType = _statusEffectType,
                StatusBuildUp = _statusBuildUpPerHit,
                WeaponMaterial = _weaponMaterial,
                IsHeavyAttack = isHeavyAttack,
                IsCriticalRiposteOrBackstab = isCritical,
                CanBeParried = canBeParried
            };
        }

        private float EvaluateAttributeSaturation(int statLevel)
        {
            int clamped = Mathf.Clamp(statLevel, 1, 99);
            return _attributeSaturationCurve != null
                ? Mathf.Clamp01(_attributeSaturationCurve.Evaluate(clamped))
                : clamped / 99f;
        }

        public static float GetGradeCoefficient(ScalingGrade grade)
        {
            switch (grade)
            {
                case ScalingGrade.S: return 1.45f;
                case ScalingGrade.A: return 1.15f;
                case ScalingGrade.B: return 0.88f;
                case ScalingGrade.C: return 0.62f;
                case ScalingGrade.D: return 0.38f;
                case ScalingGrade.E: return 0.18f;
                default: return 0f;
            }
        }
    }
}
