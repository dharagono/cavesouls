using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;

namespace CaveSouls.Features.AI
{
    /// <summary>
    /// Multi-Phase Souls Boss Controller with Controlled Input Reading (punishing poorly-timed player heals).
    /// Transitions phases at configurable HP thresholds (e.g., 50%), activating Phase 2 move-sets,
    /// elemental weapon buffs, music transitions, and arena AoE bursts.
    /// Zero GC allocations.
    /// </summary>
    [RequireComponent(typeof(CombatEntityVitals), typeof(AISensor))]
    public sealed class BossMultiPhaseController : MonoBehaviour, IWorldResettable, ISaveable
    {
        [Header("Boss Identity")]
        [SerializeField] private string _bossId = "Boss_AbyssalWarden_01";
        [SerializeField] private string _bossDisplayName = "Warden of the Sunken Cavern";
        [Range(0.1f, 0.9f)] [SerializeField] private float _phaseTwoHealthRatioThreshold = 0.50f;

        [Header("Phase 2 Buffs & VFX")]
        [SerializeField] private GameObject _phaseTwoWeaponVfxObject;
        [SerializeField] private float _phaseTwoFireDamageBonus = 110f;
        [SerializeField] private Animator _animator;
        [SerializeField] private DeterministicWeaponHitbox _weaponHitbox;

        [Header("Controlled Input Reading (Heal Punish Lunge)")]
        [SerializeField] private bool _enableHealInputReading = true;
        [Range(0f, 1f)] [SerializeField] private float _healPunishProbability = 0.75f;
        [SerializeField] private float _minHealPunishDistance = 4.0f;
        [SerializeField] private float _maxHealPunishDistance = 18.0f;
        [SerializeField] private float _healPunishCooldown = 7.0f;
        [SerializeField] private float _lungeSpeed = 16.5f;
        [SerializeField] private float _lungeDuration = 0.65f;

        private CombatEntityVitals _vitals;
        private AISensor _sensor;

        private int _currentPhase = 1;
        private bool _isEncounterActive;
        private bool _isDefeatedPermanently;
        private bool _isPerformingGapCloser;
        private float _gapCloserTimer;
        private float _lastHealPunishTime = -100f;
        private Vector3 _lungeTargetPosition;
        private Vector3 _initialSpawnPosition;
        private Quaternion _initialSpawnRotation;

        public bool IsUnique => true;
        public string SaveId => _bossId;
        public int CurrentPhase => _currentPhase;
        public bool IsDefeatedPermanently => _isDefeatedPermanently;

        private void Awake()
        {
            _vitals = GetComponent<CombatEntityVitals>();
            _sensor = GetComponent<AISensor>();
            _initialSpawnPosition = transform.position;
            _initialSpawnRotation = transform.rotation;

            if (_phaseTwoWeaponVfxObject != null)
            {
                _phaseTwoWeaponVfxObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventBus<OnDamageDealtEvent>.Subscribe(HandleDamageDealt);
            EventBus<OnPlayerHealStartedEvent>.Subscribe(HandlePlayerHealStarted);
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
            EventBus<OnWorldResetEvent>.Subscribe(HandleWorldReset);
        }

        private void OnDisable()
        {
            EventBus<OnDamageDealtEvent>.Unsubscribe(HandleDamageDealt);
            EventBus<OnPlayerHealStartedEvent>.Unsubscribe(HandlePlayerHealStarted);
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
            EventBus<OnWorldResetEvent>.Unsubscribe(HandleWorldReset);
        }

        private void Update()
        {
            if (_isDefeatedPermanently || !_vitals.IsAlive) return;

            if (_isPerformingGapCloser)
            {
                TickGapCloserLunge(Time.deltaTime);
            }
        }

        public void BeginBossEncounter(Transform playerTransform)
        {
            if (_isDefeatedPermanently || _isEncounterActive) return;

            _isEncounterActive = true;
            _currentPhase = 1;
            _sensor.ForceAlertToTarget(playerTransform);

            PublishBossState(true);
        }

        private void TriggerPhaseTwoTransition()
        {
            _currentPhase = 2;

            if (_phaseTwoWeaponVfxObject != null)
            {
                _phaseTwoWeaponVfxObject.SetActive(true);
            }

            if (_animator != null)
            {
                _animator.SetTrigger(AnimHashes.PhaseTransition);
            }

            PublishBossState(true);
        }

        private void TickGapCloserLunge(float deltaTime)
        {
            _gapCloserTimer += deltaTime;
            float normalizedTime = Mathf.Clamp01(_gapCloserTimer / _lungeDuration);

            Vector3 toTarget = _lungeTargetPosition - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 1.2f && normalizedTime < 0.7f)
            {
                Vector3 dir = toTarget.normalized;
                transform.rotation = Quaternion.LookRotation(dir);
                transform.position += dir * (_lungeSpeed * deltaTime);
            }

            if (_weaponHitbox != null)
            {
                if (normalizedTime >= 0.35f && normalizedTime <= 0.75f)
                {
                    if (!_weaponHitbox.IsHitboxOpen)
                    {
                        DamagePayload payload = new DamagePayload
                        {
                            Attacker = gameObject,
                            PhysicalSubType = PhysicalDamageSubType.Thrust,
                            PhysicalDamage = 180f,
                            FireDamage = _currentPhase >= 2 ? _phaseTwoFireDamageBonus : 0f,
                            PoiseDamage = 55f,
                            StaminaDrainOnBlock = 48f,
                            WeaponMaterial = WeaponMaterialType.HeavySteel,
                            IsHeavyAttack = true,
                            CanBeParried = false
                        };
                        _weaponHitbox.OpenHitbox(in payload);
                    }
                    _weaponHitbox.SampleHitbox();
                }
                else if (_weaponHitbox.IsHitboxOpen)
                {
                    _weaponHitbox.CloseHitbox();
                }
            }

            if (normalizedTime >= 1f)
            {
                _isPerformingGapCloser = false;
                _weaponHitbox?.CloseHitbox();
            }
        }

        private void HandleDamageDealt(in OnDamageDealtEvent evt)
        {
            if (evt.Target != gameObject || !_vitals.IsAlive) return;

            if (!_isEncounterActive && evt.Attacker != null)
            {
                BeginBossEncounter(evt.Attacker.transform);
            }

            float healthRatio = _vitals.CurrentHealth / _vitals.MaxHealth;
            if (_currentPhase == 1 && healthRatio <= _phaseTwoHealthRatioThreshold)
            {
                TriggerPhaseTwoTransition();
            }
            else
            {
                PublishBossState(true);
            }
        }

        private void HandlePlayerHealStarted(in OnPlayerHealStartedEvent evt)
        {
            if (!_enableHealInputReading || !_isEncounterActive || !_vitals.IsAlive || _vitals.IsStanceBroken) return;
            if (_isPerformingGapCloser || (Time.time - _lastHealPunishTime) < _healPunishCooldown) return;

            float dist = Vector3.Distance(transform.position, evt.Position);
            if (dist < _minHealPunishDistance || dist > _maxHealPunishDistance) return;

            if (Random.value <= _healPunishProbability)
            {
                _lastHealPunishTime = Time.time;
                _isPerformingGapCloser = true;
                _gapCloserTimer = 0f;
                _lungeTargetPosition = evt.Position;

                if (_animator != null)
                {
                    _animator.SetTrigger(AnimHashes.GapCloserAttack);
                }
            }
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (evt.DeadEntity != gameObject) return;

            _isDefeatedPermanently = true;
            _isEncounterActive = false;
            _isPerformingGapCloser = false;
            _weaponHitbox?.CloseHitbox();

            PublishBossState(false);
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "BossDefeated" });
        }

        private void HandleWorldReset(in OnWorldResetEvent evt)
        {
            OnWorldReset();
        }

        public void OnWorldReset()
        {
            if (_isDefeatedPermanently) return;

            // If player died during boss fight, reset boss back to Phase 1 full HP
            _isEncounterActive = false;
            _isPerformingGapCloser = false;
            _currentPhase = 1;
            if (_phaseTwoWeaponVfxObject != null) _phaseTwoWeaponVfxObject.SetActive(false);
            transform.SetPositionAndRotation(_initialSpawnPosition, _initialSpawnRotation);
            _vitals.RestoreAllVitals();
            PublishBossState(false);
        }

        public bool CaptureState() => _isDefeatedPermanently;

        public void RestoreState(bool state)
        {
            _isDefeatedPermanently = state;
            if (_isDefeatedPermanently)
            {
                gameObject.SetActive(false);
            }
        }

        private void PublishBossState(bool isActive)
        {
            EventBus<OnBossFightStateChangedEvent>.Publish(new OnBossFightStateChangedEvent
            {
                BossId = _bossId,
                BossDisplayName = _bossDisplayName,
                IsActive = isActive,
                CurrentPhase = _currentPhase,
                CurrentHealth = _vitals.CurrentHealth,
                MaxHealth = _vitals.MaxHealth
            });
        }
    }
}
