using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Core combat vitals & damage receiver implementing IDamageable and ITargetable.
    /// Integrates Health, FP (Focus), Stamina, Poise, Status Effects, I-Frames, Parry windows, and Shield Blocking.
    /// Zero GC allocations in Update.
    /// </summary>
    [RequireComponent(typeof(PoiseController), typeof(StatusEffectController))]
    public sealed class CombatEntityVitals : MonoBehaviour, IDamageable, ITargetable
    {
        [Header("Identity")]
        [SerializeField] private string _uniqueSaveId;
        [SerializeField] private bool _isPlayer;
        [SerializeField] private bool _isBoss;
        [SerializeField] private int _soulsReward = 150;
        [SerializeField] private SurfaceMaterialType _surfaceMaterial = SurfaceMaterialType.Flesh;
        [SerializeField] private Transform _lockOnPivot;

        [Header("Base Vitals")]
        [SerializeField] private float _maxHealth = 500f;
        [SerializeField] private float _maxFocus = 100f;
        [SerializeField] private float _maxStamina = 120f;
        [SerializeField] private float _staminaRegenRate = 32f;
        [SerializeField] private float _staminaRegenDelay = 0.85f;
        [SerializeField] private float _blockingStaminaRegenMultiplier = 0.25f;

        [Header("Defense & Shield Profiles")]
        [SerializeField] private DefenseProfile _defenseProfile;
        [SerializeField] private ShieldBlockProfile _shieldBlockProfile;

        private PoiseController _poiseController;
        private StatusEffectController _statusEffectController;

        private float _currentHealth;
        private float _currentFocus;
        private float _currentStamina;
        private float _staminaRegenCooldown;

        private bool _isInvulnerable;
        private bool _isInParryWindow;
        private bool _isBlocking;
        private bool _hasHyperArmor;
        private static int _nextEntityIdCounter;
        private int _cachedEntityId;

        public int EntityId => _cachedEntityId;
        public string UniqueSaveId => _uniqueSaveId;
        public Transform Transform => transform;
        public bool IsAlive => _currentHealth > 0f;
        public bool IsPlayer => _isPlayer;
        public bool IsBoss => _isBoss;
        public bool IsInvulnerable => _isInvulnerable;
        public bool IsInParryWindow => _isInParryWindow;
        public bool IsBlocking => _isBlocking;
        public bool HasHyperArmor => _hasHyperArmor;
        public bool IsStanceBroken => _poiseController != null && _poiseController.IsStanceBroken;
        public SurfaceMaterialType SurfaceMaterial => _surfaceMaterial;

        public Transform LockOnPivot => _lockOnPivot != null ? _lockOnPivot : transform;
        public bool CanBeLockedOn => IsAlive;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float CurrentFocus => _currentFocus;
        public float MaxFocus => _maxFocus;
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => _maxStamina;

        private void Awake()
        {
            _cachedEntityId = ++_nextEntityIdCounter;
            _poiseController = GetComponent<PoiseController>();
            _statusEffectController = GetComponent<StatusEffectController>();

            RestoreAllVitals();
        }

        private void Update()
        {
            if (!IsAlive) return;

            float dt = Time.deltaTime;
            _poiseController.Tick(dt);
            _statusEffectController.Tick(dt);
            TickStaminaRegeneration(dt);
        }

        public void ConfigureDerivedStats(float maxHealth, float maxFocus, float maxStamina, in DefenseProfile defense, in ShieldBlockProfile shield)
        {
            float healthRatio = _maxHealth > 0f ? _currentHealth / _maxHealth : 1f;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _maxFocus = Mathf.Max(0f, maxFocus);
            _maxStamina = Mathf.Max(1f, maxStamina);
            _currentHealth = Mathf.Clamp(_maxHealth * healthRatio, 1f, _maxHealth);
            _currentFocus = Mathf.Min(_currentFocus, _maxFocus);
            _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
            _defenseProfile = defense;
            _shieldBlockProfile = shield;
            BroadcastVitalsChanged();
        }

        public void RestoreAllVitals()
        {
            _currentHealth = _maxHealth;
            _currentFocus = _maxFocus;
            _currentStamina = _maxStamina;
            _staminaRegenCooldown = 0f;
            _poiseController?.ResetPoise();
            _statusEffectController?.ClearAllStatusEffects();
            BroadcastVitalsChanged();
        }

        public void SetCombatWindowFlags(bool iFrames, bool parryWindow, bool hyperArmor)
        {
            _isInvulnerable = iFrames;
            _isInParryWindow = parryWindow;
            _hasHyperArmor = hyperArmor;
        }

        public void SetBlocking(bool isBlocking)
        {
            _isBlocking = isBlocking;
        }

        public bool TryConsumeStamina(float amount)
        {
            if (_currentStamina <= 0f) return false;
            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _staminaRegenCooldown = _staminaRegenDelay;
            BroadcastVitalsChanged();
            return true;
        }

        public bool TryConsumeFocus(float amount)
        {
            if (_currentFocus < amount) return false;
            _currentFocus -= amount;
            BroadcastVitalsChanged();
            return true;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            BroadcastVitalsChanged();
        }

        public void RestoreFocus(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _currentFocus = Mathf.Min(_maxFocus, _currentFocus + amount);
            BroadcastVitalsChanged();
        }

        public void ApplyDirectTrueDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            BroadcastVitalsChanged();

            if (_currentHealth <= 0f)
            {
                HandleDeath(null);
            }
        }

        public DamageResult ReceiveDamage(in DamagePayload payload)
        {
            DamageResult result = default;
            result.ImpactedMaterial = _surfaceMaterial;

            if (!IsAlive) return result;

            if (_isInvulnerable && !payload.IsCriticalRiposteOrBackstab)
            {
                result.WasDodgedByIFrames = true;
                return result;
            }

            bool isFacingAttacker = false;
            if (payload.Attacker != null)
            {
                Vector3 toAttacker = (payload.Attacker.transform.position - transform.position).normalized;
                toAttacker.y = 0f;
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                isFacingAttacker = Vector3.Dot(fwd.normalized, toAttacker.normalized) > 0.25f;
            }

            result = DamageCalculator.Evaluate(
                in payload,
                in _defenseProfile,
                in _shieldBlockProfile,
                _isBlocking,
                isFacingAttacker,
                _currentStamina);

            if (!result.WasBlocked)
            {
                result.ImpactedMaterial = _surfaceMaterial;
            }

            if (result.StaminaDrained > 0f)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - result.StaminaDrained);
                _staminaRegenCooldown = _staminaRegenDelay;
            }

            if (result.TriggeredGuardBreak)
            {
                _isBlocking = false;
                _poiseController.ForceStanceBreak(payload.Attacker);
            }
            else if (result.PoiseDamageDealt > 0f)
            {
                result.TriggeredStanceBreak = _poiseController.ApplyPoiseDamage(
                    result.PoiseDamageDealt,
                    _hasHyperArmor,
                    payload.Attacker);
            }

            if (result.TotalDamageDealt > 0f)
            {
                _currentHealth = Mathf.Max(0f, _currentHealth - result.TotalDamageDealt);
            }

            // Status Effect Buildup (reduced by 60% if blocked)
            if (payload.StatusType != StatusEffectType.None && payload.StatusBuildUp > 0f)
            {
                float buildup = result.WasBlocked ? payload.StatusBuildUp * 0.4f : payload.StatusBuildUp;
                _statusEffectController.AddBuildup(payload.StatusType, buildup);
            }

            BroadcastVitalsChanged();

            if (_currentHealth <= 0f)
            {
                result.KilledTarget = true;
                HandleDeath(payload.Attacker);
            }

            EventBus<OnDamageDealtEvent>.Publish(new OnDamageDealtEvent
            {
                Attacker = payload.Attacker,
                Target = gameObject,
                Payload = payload,
                Result = result
            });

            return result;
        }

        public void NotifyParriedBy(GameObject defender)
        {
            if (defender == gameObject) return;
            _poiseController.ForceStanceBreak(defender);
        }

        private void TickStaminaRegeneration(float deltaTime)
        {
            if (_staminaRegenCooldown > 0f)
            {
                _staminaRegenCooldown -= deltaTime;
                return;
            }

            if (_currentStamina < _maxStamina)
            {
                float rate = _staminaRegenRate;
                if (_isBlocking) rate *= _blockingStaminaRegenMultiplier;
                if (_statusEffectController != null && _statusEffectController.IsFrostbitten) rate *= 0.65f;

                _currentStamina = Mathf.Min(_maxStamina, _currentStamina + rate * deltaTime);
                BroadcastVitalsChanged();
            }
        }

        private void HandleDeath(GameObject killer)
        {
            _isBlocking = false;
            _isInvulnerable = false;
            _isInParryWindow = false;

            EventBus<OnEntityDeathEvent>.Publish(new OnEntityDeathEvent
            {
                EntityId = _cachedEntityId,
                UniqueSaveId = _uniqueSaveId,
                DeadEntity = gameObject,
                Killer = killer,
                DeathPosition = transform.position,
                SoulsReward = _soulsReward,
                IsPlayer = _isPlayer,
                IsBoss = _isBoss
            });
        }

        private void BroadcastVitalsChanged()
        {
            EventBus<OnVitalsChangedEvent>.Publish(new OnVitalsChangedEvent
            {
                EntityId = _cachedEntityId,
                IsPlayer = _isPlayer,
                CurrentHealth = _currentHealth,
                MaxHealth = _maxHealth,
                CurrentFocus = _currentFocus,
                MaxFocus = _maxFocus,
                CurrentStamina = _currentStamina,
                MaxStamina = _maxStamina
            });
        }
    }
}
