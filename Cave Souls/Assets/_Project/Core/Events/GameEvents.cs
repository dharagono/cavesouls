using UnityEngine;

namespace CaveSouls.Core
{
    public struct OnDamageDealtEvent : IGameEvent
    {
        public GameObject Attacker;
        public GameObject Target;
        public DamagePayload Payload;
        public DamageResult Result;
    }

    public struct OnHitImpactEvent : IGameEvent
    {
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public WeaponMaterialType WeaponMaterial;
        public SurfaceMaterialType SurfaceMaterial;
        public float ImpactForce;
        public bool WasBlockedOrParried;
        public bool IsHeavyHit;
    }

    public struct OnPoiseBrokenEvent : IGameEvent
    {
        public GameObject Target;
        public GameObject Attacker;
        public Vector3 Position;
        public float StanceBreakDuration;
    }

    public struct OnStatusEffectBuildupEvent : IGameEvent
    {
        public GameObject Target;
        public StatusEffectType EffectType;
        public float CurrentBuildUp;
        public float MaxThreshold;
        public bool IsTriggered;
    }

    public struct OnPlayerHealStartedEvent : IGameEvent
    {
        public GameObject Player;
        public Vector3 Position;
    }

    public struct OnEntityDeathEvent : IGameEvent
    {
        public int EntityId;
        public string UniqueSaveId;
        public GameObject DeadEntity;
        public GameObject Killer;
        public Vector3 DeathPosition;
        public int SoulsReward;
        public bool IsPlayer;
        public bool IsBoss;
    }

    public struct OnWorldResetEvent : IGameEvent
    {
        public string CheckpointId;
        public Vector3 CheckpointPosition;
    }

    public struct OnCheckpointRestedEvent : IGameEvent
    {
        public string CheckpointId;
        public Vector3 RespawnPosition;
        public Quaternion RespawnRotation;
    }

    public struct OnSoulsChangedEvent : IGameEvent
    {
        public int PreviousSouls;
        public int CurrentSouls;
        public int Delta;
    }

    public struct OnLockOnTargetChangedEvent : IGameEvent
    {
        public Transform NewTarget;
        public bool IsLockedOn;
    }

    public struct OnBossFightStateChangedEvent : IGameEvent
    {
        public string BossId;
        public string BossDisplayName;
        public bool IsActive;
        public int CurrentPhase;
        public float CurrentHealth;
        public float MaxHealth;
    }

    public struct OnSoundEmittedEvent : IGameEvent
    {
        public GameObject Source;
        public Vector3 Position;
        public float NoiseRadius;
        public int TeamId;
    }

    public struct OnFootstepEvent : IGameEvent
    {
        public Vector3 FootPosition;
        public SurfaceMaterialType Surface;
        public float ArmorWeightRatio;
        public bool IsSprinting;
    }

    public struct OnVitalsChangedEvent : IGameEvent
    {
        public int EntityId;
        public bool IsPlayer;
        public float CurrentHealth;
        public float MaxHealth;
        public float CurrentFocus;
        public float MaxFocus;
        public float CurrentStamina;
        public float MaxStamina;
    }

    public struct OnGameSaveRequestedEvent : IGameEvent
    {
        public string Reason;
    }

    public struct OnInteractionPromptChangedEvent : IGameEvent
    {
        public bool IsVisible;
        public string PromptText;
    }

    public struct OnLoreInspectedEvent : IGameEvent
    {
        public string Title;
        public string Description;
    }
}
