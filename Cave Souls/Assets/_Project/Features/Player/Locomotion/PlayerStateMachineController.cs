using CaveSouls.Core;
using CaveSouls.Features.Combat;
using CaveSouls.Features.InventoryAndEquipment;
using UnityEngine;

namespace CaveSouls.Features.Player
{
    public enum PlayerLocomotionStateId : byte
    {
        Idle = 0,
        Move = 1,
        Sprint = 2,
        RollOrBackstep = 3,
        Attacking = 4,
        Parrying = 5,
        GuardBlock = 6,
        HitStun = 7,
        GuardBroken = 8,
        RiposteOrBackstab = 9,
        UsingConsumable = 10,
        Interacting = 11,
        Dead = 12
    }

    /// <summary>
    /// Hierarchical Finite State Machine (HFSM) controller for the Souls Player.
    /// Coordinates all 13 states, ActionInputBuffer Cancel Windows, DeterministicWeaponHitbox,
    /// Lock-On strafing, EquipLoad dodge tiers, and acoustic noise emission.
    /// Strictly zero GC allocations in Update/FixedUpdate/LateUpdate.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(CombatEntityVitals), typeof(PlayerStatsController))]
    [RequireComponent(typeof(PlayerInputReader), typeof(LockOnSystem), typeof(ParryAndCriticalSystem))]
    public sealed class PlayerStateMachineController : MonoBehaviour
    {
        private const int MaxInteractableColliders = 8;

        [Header("References")]
        [SerializeField] private Animator _animator;
        [SerializeField] private DeterministicWeaponHitbox _weaponHitbox;
        [SerializeField] private Transform _cameraTransform;

        [Header("Locomotion Settings")]
        [SerializeField] private float _walkSpeed = 4.2f;
        [SerializeField] private float _sprintSpeed = 6.8f;
        [SerializeField] private float _crouchSpeed = 2.4f;
        [SerializeField] private float _rotationSharpness = 14f;
        [SerializeField] private float _gravity = -24f;
        [SerializeField] private float _sprintStaminaDrainPerSec = 14f;

        [Header("Dodge Settings per Equip Load Tier")]
        [SerializeField] private AnimationCombatWindowSO _fastRollWindow;
        [SerializeField] private AnimationCombatWindowSO _mediumRollWindow;
        [SerializeField] private AnimationCombatWindowSO _fatRollWindow;
        [SerializeField] private AnimationCombatWindowSO _backstepWindow;
        [SerializeField] private float _fastRollSpeed = 9.5f;
        [SerializeField] private float _mediumRollSpeed = 7.8f;
        [SerializeField] private float _fatRollSpeed = 5.4f;
        [SerializeField] private float _backstepSpeed = 6.2f;

        [Header("Interaction & Acoustic Stealth")]
        [SerializeField] private float _interactionRadius = 2.0f;
        [SerializeField] private LayerMask _interactableMask = ~0;
        [SerializeField] private float _noiseBroadcastInterval = 0.35f;

        private readonly Collider[] _interactBuffer = new Collider[MaxInteractableColliders];

        private CharacterController _characterController;
        private CombatEntityVitals _vitals;
        private PlayerStatsController _stats;
        private EquipmentAndInventoryManager _equipment;
        private PlayerInputReader _input;
        private LockOnSystem _lockOnSystem;
        private ParryAndCriticalSystem _parryAndCriticalSystem;

        private StateMachine _hfsm;

        // Pre-allocated HFSM State instances (Zero GC allocations on state transitions!)
        private PlayerIdleState _idleState;
        private PlayerMoveState _moveState;
        private PlayerSprintState _sprintState;
        private PlayerRollBackstepState _rollBackstepState;
        private PlayerAttackingState _attackingState;
        private PlayerParryingState _parryingState;
        private PlayerGuardBlockState _guardBlockState;
        private PlayerHitStunState _hitStunState;
        private PlayerGuardBrokenState _guardBrokenState;
        private PlayerRiposteBackstabState _riposteBackstabState;
        private PlayerUsingConsumableState _usingConsumableState;
        private PlayerInteractingState _interactingState;
        private PlayerDeadState _deadState;

        private PlayerLocomotionStateId _currentStateId;
        private float _verticalVelocity;
        private float _noiseTimer;

        public PlayerLocomotionStateId CurrentStateId => _currentStateId;
        public CharacterController CharacterController => _characterController;
        public CombatEntityVitals Vitals => _vitals;
        public PlayerStatsController Stats => _stats;
        public EquipmentAndInventoryManager Equipment => _equipment;
        public PlayerInputReader Input => _input;
        public LockOnSystem LockOn => _lockOnSystem;
        public ParryAndCriticalSystem ParryAndCritical => _parryAndCriticalSystem;
        public Animator Animator => _animator;
        public DeterministicWeaponHitbox WeaponHitbox => _weaponHitbox;
        public Transform CameraTransform => _cameraTransform;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public float CrouchSpeed => _crouchSpeed;
        public float RotationSharpness => _rotationSharpness;
        public float SprintStaminaDrainPerSec => _sprintStaminaDrainPerSec;
        public float FastRollSpeed => _fastRollSpeed;
        public float MediumRollSpeed => _mediumRollSpeed;
        public float FatRollSpeed => _fatRollSpeed;
        public float BackstepSpeed => _backstepSpeed;

        public AnimationCombatWindowSO FastRollWindow => _fastRollWindow;
        public AnimationCombatWindowSO MediumRollWindow => _mediumRollWindow;
        public AnimationCombatWindowSO FatRollWindow => _fatRollWindow;
        public AnimationCombatWindowSO BackstepWindow => _backstepWindow;

        public PlayerIdleState IdleState => _idleState;
        public PlayerMoveState MoveState => _moveState;
        public PlayerSprintState SprintState => _sprintState;
        public PlayerRollBackstepState RollBackstepState => _rollBackstepState;
        public PlayerAttackingState AttackingState => _attackingState;
        public PlayerParryingState ParryingState => _parryingState;
        public PlayerGuardBlockState GuardBlockState => _guardBlockState;
        public PlayerHitStunState HitStunState => _hitStunState;
        public PlayerGuardBrokenState GuardBrokenState => _guardBrokenState;
        public PlayerRiposteBackstabState RiposteBackstabState => _riposteBackstabState;
        public PlayerUsingConsumableState UsingConsumableState => _usingConsumableState;
        public PlayerInteractingState InteractingState => _interactingState;
        public PlayerDeadState DeadState => _deadState;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _vitals = GetComponent<CombatEntityVitals>();
            _stats = GetComponent<PlayerStatsController>();
            _equipment = GetComponent<EquipmentAndInventoryManager>();
            _input = GetComponent<PlayerInputReader>();
            _lockOnSystem = GetComponent<LockOnSystem>();
            _parryAndCriticalSystem = GetComponent<ParryAndCriticalSystem>();

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_weaponHitbox != null)
            {
                _weaponHitbox.Initialize(_vitals.EntityId);
            }

            // Instantiate all states once in Awake (Zero GC during gameplay)
            _hfsm = new StateMachine();
            _idleState = new PlayerIdleState(this);
            _moveState = new PlayerMoveState(this);
            _sprintState = new PlayerSprintState(this);
            _rollBackstepState = new PlayerRollBackstepState(this);
            _attackingState = new PlayerAttackingState(this);
            _parryingState = new PlayerParryingState(this);
            _guardBlockState = new PlayerGuardBlockState(this);
            _hitStunState = new PlayerHitStunState(this);
            _guardBrokenState = new PlayerGuardBrokenState(this);
            _riposteBackstabState = new PlayerRiposteBackstabState(this);
            _usingConsumableState = new PlayerUsingConsumableState(this);
            _interactingState = new PlayerInteractingState(this);
            _deadState = new PlayerDeadState(this);

            _hfsm.Initialize(_idleState);
        }

        private void OnEnable()
        {
            _input.OnLockOnTogglePressed += HandleLockOnToggle;
            EventBus<OnDamageDealtEvent>.Subscribe(HandleDamageDealt);
            EventBus<OnPoiseBrokenEvent>.Subscribe(HandlePoiseBroken);
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
        }

        private void OnDisable()
        {
            _input.OnLockOnTogglePressed -= HandleLockOnToggle;
            EventBus<OnDamageDealtEvent>.Unsubscribe(HandleDamageDealt);
            EventBus<OnPoiseBrokenEvent>.Unsubscribe(HandlePoiseBroken);
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_lockOnSystem.IsLockedOn)
            {
                _lockOnSystem.ValidateCurrentLockOn();
                _lockOnSystem.TrySwitchTarget(_input.LookInput);
            }

            _hfsm.Tick(dt);
            ApplyGravityAndVerticalMovement(dt);
            TickAcousticNoiseEmission(dt);
        }

        private void FixedUpdate()
        {
            _hfsm.FixedTick(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _hfsm.LateTick(Time.deltaTime);
        }

        public void TransitionToState(IState targetState, PlayerLocomotionStateId stateId)
        {
            _currentStateId = stateId;
            _hfsm.ChangeState(targetState);
        }

        public Vector3 GetCameraRelativeMovementDirection(Vector2 inputVector)
        {
            if (inputVector.sqrMagnitude < 0.001f) return Vector3.zero;

            Vector3 camFwd = _cameraTransform != null ? _cameraTransform.forward : transform.forward;
            Vector3 camRight = _cameraTransform != null ? _cameraTransform.right : transform.right;
            camFwd.y = 0f;
            camRight.y = 0f;
            camFwd.Normalize();
            camRight.Normalize();

            Vector3 moveDir = (camFwd * inputVector.y + camRight * inputVector.x);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
            return moveDir;
        }

        public void RotateTowardsMovementOrLockOn(Vector3 moveWorldDir, float deltaTime)
        {
            if (_lockOnSystem.IsLockedOn && _lockOnSystem.CurrentTargetPivot != null && _currentStateId != PlayerLocomotionStateId.Sprint)
            {
                Vector3 toTarget = _lockOnSystem.CurrentTargetPivot.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSharpness * deltaTime);
                }
            }
            else if (moveWorldDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveWorldDir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSharpness * deltaTime);
            }
        }

        public bool TryConsumeBufferedAction(out BufferedActionType consumedAction)
        {
            consumedAction = BufferedActionType.None;
            if (!_input.ActionBuffer.TryConsumeNext(Time.time, out BufferedActionType action, out Vector2 moveSnapshot))
            {
                return false;
            }

            switch (action)
            {
                case BufferedActionType.Roll:
                case BufferedActionType.Backstep:
                    if (_stats.EquipLoadTier == EquipLoadTier.Overencumbered) return false;
                    if (!_vitals.TryConsumeStamina(GetDodgeStaminaCost())) return false;
                    _rollBackstepState.PrepareDodge(action == BufferedActionType.Backstep, moveSnapshot);
                    TransitionToState(_rollBackstepState, PlayerLocomotionStateId.RollOrBackstep);
                    consumedAction = action;
                    return true;

                case BufferedActionType.LightAttack:
                    // First check if a Front Riposte or Rear Backstab is available!
                    if (_parryAndCriticalSystem.TryFindCriticalTarget(transform, _vitals.EntityId, out IDamageable critTarget, out bool isRiposte))
                    {
                        _riposteBackstabState.ConfigureCritical(critTarget, isRiposte);
                        TransitionToState(_riposteBackstabState, PlayerLocomotionStateId.RiposteOrBackstab);
                        consumedAction = action;
                        return true;
                    }

                    if (_vitals.CurrentStamina <= 1f) return false;
                    _attackingState.ConfigureAttack(isHeavy: false, comboIndex: 0);
                    TransitionToState(_attackingState, PlayerLocomotionStateId.Attacking);
                    consumedAction = action;
                    return true;

                case BufferedActionType.HeavyAttack:
                    if (_vitals.CurrentStamina <= 1f) return false;
                    _attackingState.ConfigureAttack(isHeavy: true, comboIndex: 0);
                    TransitionToState(_attackingState, PlayerLocomotionStateId.Attacking);
                    consumedAction = action;
                    return true;

                case BufferedActionType.Parry:
                    if (!_vitals.TryConsumeStamina(15f)) return false;
                    TransitionToState(_parryingState, PlayerLocomotionStateId.Parrying);
                    consumedAction = action;
                    return true;

                case BufferedActionType.UseConsumable:
                    TransitionToState(_usingConsumableState, PlayerLocomotionStateId.UsingConsumable);
                    consumedAction = action;
                    return true;

                case BufferedActionType.Interact:
                    if (TryFindNearestInteractable(out IInteractable interactable))
                    {
                        _interactingState.ConfigureInteraction(interactable);
                        TransitionToState(_interactingState, PlayerLocomotionStateId.Interacting);
                        consumedAction = action;
                        return true;
                    }
                    return false;
            }

            return false;
        }

        public float GetDodgeStaminaCost()
        {
            // Fat Roll has stamina penalty as specified in section 2.D
            return _stats.EquipLoadTier == EquipLoadTier.FatRoll ? 26f : 18f;
        }

        private bool TryFindNearestInteractable(out IInteractable nearest)
        {
            nearest = null;
            int count = Physics.OverlapSphereNonAlloc(transform.position, _interactionRadius, _interactBuffer, _interactableMask, QueryTriggerInteraction.Collide);
            float bestDistSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _interactBuffer[i];
                if (col == null) continue;
                if (!col.TryGetComponent(out IInteractable candidate)) continue;
                if (!candidate.CanInteract(gameObject)) continue;

                float dSqr = (col.transform.position - transform.position).sqrMagnitude;
                if (dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    nearest = candidate;
                }
            }

            return nearest != null;
        }

        private void ApplyGravityAndVerticalMovement(float deltaTime)
        {
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += _gravity * deltaTime;
            }

            _characterController.Move(new Vector3(0f, _verticalVelocity * deltaTime, 0f));

            if (_animator != null)
            {
                _animator.SetBool(AnimHashes.IsGrounded, _characterController.isGrounded);
                _animator.SetBool(AnimHashes.IsLockedOn, _lockOnSystem.IsLockedOn);
            }
        }

        private void TickAcousticNoiseEmission(float deltaTime)
        {
            if (!_vitals.IsAlive || _currentStateId == PlayerLocomotionStateId.Idle) return;

            _noiseTimer += deltaTime;
            if (_noiseTimer < _noiseBroadcastInterval) return;
            _noiseTimer = 0f;

            float baseRadius = 0f;
            if (_currentStateId == PlayerLocomotionStateId.Sprint)
            {
                baseRadius = 14f;
            }
            else if (_currentStateId == PlayerLocomotionStateId.Move)
            {
                baseRadius = _input.IsCrouchActive ? 2.0f : 6.5f;
            }
            else if (_currentStateId == PlayerLocomotionStateId.Attacking || _currentStateId == PlayerLocomotionStateId.RollOrBackstep)
            {
                baseRadius = 9f;
            }

            if (baseRadius > 0f)
            {
                // Heavy armor increases acoustic emission radius
                float armorNoiseMultiplier = 1f + (_stats.EquipLoadRatio * 0.5f);
                EventBus<OnSoundEmittedEvent>.Publish(new OnSoundEmittedEvent
                {
                    Source = gameObject,
                    Position = transform.position,
                    NoiseRadius = baseRadius * armorNoiseMultiplier,
                    TeamId = 0
                });
            }
        }

        private void HandleLockOnToggle()
        {
            _lockOnSystem.ToggleLockOn();
        }

        private void HandleDamageDealt(in OnDamageDealtEvent evt)
        {
            if (evt.Target != gameObject || !_vitals.IsAlive) return;

            if (evt.Result.TriggeredGuardBreak)
            {
                TransitionToState(_guardBrokenState, PlayerLocomotionStateId.GuardBroken);
            }
            else if (!evt.Result.WasBlocked && !evt.Result.WasDodgedByIFrames && !_vitals.HasHyperArmor)
            {
                TransitionToState(_hitStunState, PlayerLocomotionStateId.HitStun);
            }
        }

        private void HandlePoiseBroken(in OnPoiseBrokenEvent evt)
        {
            if (evt.Target == gameObject && _vitals.IsAlive)
            {
                TransitionToState(_guardBrokenState, PlayerLocomotionStateId.GuardBroken);
            }
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (evt.DeadEntity == gameObject)
            {
                TransitionToState(_deadState, PlayerLocomotionStateId.Dead);
            }
        }
    }
}
