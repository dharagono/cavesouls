using System;
using UnityEngine;

namespace CaveSouls.Core
{
    public enum PhysicalDamageSubType : byte
    {
        Standard = 0,
        Strike = 1,
        Slash = 2,
        Thrust = 3
    }

    public enum DamageElementType : byte
    {
        Physical = 0,
        Magic = 1,
        Fire = 2,
        Lightning = 3,
        Dark = 4
    }

    public enum StatusEffectType : byte
    {
        None = 0,
        Bleed = 1,
        Poison = 2,
        Frostbite = 3,
        Curse = 4
    }

    public enum SurfaceMaterialType : byte
    {
        Stone = 0,
        Water = 1,
        Wood = 2,
        Dirt = 3,
        Metal = 4,
        Flesh = 5,
        WoodShield = 6,
        PlateArmor = 7
    }

    public enum WeaponMaterialType : byte
    {
        IronBlade = 0,
        HeavySteel = 1,
        WoodClub = 2,
        MagicCrystal = 3
    }

    public enum EquipLoadTier : byte
    {
        FastRoll = 0,       // < 30%
        MediumRoll = 1,     // 30% - 70%
        FatRoll = 2,        // 70% - 100%
        Overencumbered = 3  // > 100%
    }

    public enum AttributeType : byte
    {
        Vigor = 0,
        Mind = 1,
        Endurance = 2,
        Strength = 3,
        Dexterity = 4,
        Intelligence = 5,
        Faith = 6
    }

    public enum ScalingGrade : byte
    {
        None = 0,
        E = 1,
        D = 2,
        C = 3,
        B = 4,
        A = 5,
        S = 6
    }

    public enum BufferedActionType : byte
    {
        None = 0,
        LightAttack = 1,
        HeavyAttack = 2,
        Roll = 3,
        Backstep = 4,
        Parry = 5,
        UseConsumable = 6,
        Interact = 7,
        WeaponSkill = 8
    }

    [Serializable]
    public struct DamagePayload
    {
        public GameObject Attacker;
        public PhysicalDamageSubType PhysicalSubType;
        public float PhysicalDamage;
        public float MagicDamage;
        public float FireDamage;
        public float LightningDamage;
        public float DarkDamage;
        public float PoiseDamage;
        public float StaminaDrainOnBlock;
        public StatusEffectType StatusType;
        public float StatusBuildUp;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public Vector3 HitDirection;
        public WeaponMaterialType WeaponMaterial;
        public bool IsHeavyAttack;
        public bool IsCriticalRiposteOrBackstab;
        public bool CanBeParried;
    }

    [Serializable]
    public struct DefenseProfile
    {
        [Header("Flat Defense")]
        public float FlatPhysical;
        public float FlatMagic;
        public float FlatFire;
        public float FlatLightning;
        public float FlatDark;

        [Header("Percentage Absorption (0 to 1)")]
        [Range(0f, 0.99f)] public float AbsorbStandard;
        [Range(0f, 0.99f)] public float AbsorbStrike;
        [Range(0f, 0.99f)] public float AbsorbSlash;
        [Range(0f, 0.99f)] public float AbsorbThrust;
        [Range(0f, 0.99f)] public float AbsorbMagic;
        [Range(0f, 0.99f)] public float AbsorbFire;
        [Range(0f, 0.99f)] public float AbsorbLightning;
        [Range(0f, 0.99f)] public float AbsorbDark;
    }

    [Serializable]
    public struct ShieldBlockProfile
    {
        [Range(0f, 1f)] public float PhysicalBlockPercent;
        [Range(0f, 1f)] public float MagicBlockPercent;
        [Range(0f, 1f)] public float FireBlockPercent;
        [Range(0f, 1f)] public float LightningBlockPercent;
        [Range(0f, 1f)] public float DarkBlockPercent;
        [Range(0f, 100f)] public float Stability; // Reduces stamina drain on block
        public SurfaceMaterialType ShieldMaterial;
    }

    public struct DamageResult
    {
        public float TotalDamageDealt;
        public float PoiseDamageDealt;
        public float StaminaDrained;
        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodgedByIFrames;
        public bool TriggeredGuardBreak;
        public bool TriggeredStanceBreak;
        public bool KilledTarget;
        public SurfaceMaterialType ImpactedMaterial;
    }

    public interface IDamageable
    {
        int EntityId { get; }
        Transform Transform { get; }
        bool IsAlive { get; }
        bool IsInvulnerable { get; }
        bool IsInParryWindow { get; }
        bool IsBlocking { get; }
        bool HasHyperArmor { get; }
        bool IsStanceBroken { get; }
        SurfaceMaterialType SurfaceMaterial { get; }
        DamageResult ReceiveDamage(in DamagePayload payload);
        void NotifyParriedBy(GameObject defender);
    }

    public interface ITargetable
    {
        Transform LockOnPivot { get; }
        bool CanBeLockedOn { get; }
    }

    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }

    public interface IWorldResettable
    {
        bool IsUnique { get; }
        void OnWorldReset();
    }

    public interface ISaveable
    {
        string SaveId { get; }
        bool CaptureState();
        void RestoreState(bool state);
    }
}
