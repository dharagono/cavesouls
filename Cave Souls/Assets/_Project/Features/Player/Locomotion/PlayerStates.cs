using CaveSouls.Core;
using CaveSouls.Features.Combat;
using CaveSouls.Features.InventoryAndEquipment;
using UnityEngine;

namespace CaveSouls.Features.Player
{
    // =========================================================================================
    // 1. IDLE STATE
    // =========================================================================================
    public sealed class PlayerIdleState : HierarchicalState<PlayerStateMachineController>
    {
        public PlayerIdleState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            Context.Vitals.SetBlocking(false);
            if (Context.Animator != null)
            {
                Context.Animator.SetFloat(AnimHashes.MoveSpeed, 0f);
                Context.Animator.SetFloat(AnimHashes.StrafeX, 0f);
                Context.Animator.SetFloat(AnimHashes.StrafeZ, 0f);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (Context.TryConsumeBufferedAction(out _)) return;

            if (Context.Input.IsBlockHeld && Context.Vitals.CurrentStamina > 5f)
            {
                Context.TransitionToState(Context.GuardBlockState, PlayerLocomotionStateId.GuardBlock);
                return;
            }

            if (Context.Input.MoveInput.sqrMagnitude > 0.02f)
            {
                Context.TransitionToState(Context.MoveState, PlayerLocomotionStateId.Move);
            }
        }
    }

    // =========================================================================================
    // 2. MOVE STATE
    // =========================================================================================
    public sealed class PlayerMoveState : HierarchicalState<PlayerStateMachineController>
    {
        public PlayerMoveState(PlayerStateMachineController context) : base(context) { }

        public override void Tick(float deltaTime)
        {
            if (Context.TryConsumeBufferedAction(out _)) return;

            if (Context.Input.IsBlockHeld && Context.Vitals.CurrentStamina > 5f)
            {
                Context.TransitionToState(Context.GuardBlockState, PlayerLocomotionStateId.GuardBlock);
                return;
            }

            Vector2 moveInput = Context.Input.MoveInput;
            if (moveInput.sqrMagnitude < 0.01f)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
                return;
            }

            if (Context.Input.IsSprintHeld && Context.Stats.EquipLoadTier != EquipLoadTier.Overencumbered && Context.Vitals.CurrentStamina > 5f)
            {
                Context.TransitionToState(Context.SprintState, PlayerLocomotionStateId.Sprint);
                return;
            }

            Vector3 moveDir = Context.GetCameraRelativeMovementDirection(moveInput);
            float speed = Context.Input.IsCrouchActive ? Context.CrouchSpeed : Context.WalkSpeed;
            if (Context.Stats.EquipLoadTier == EquipLoadTier.Overencumbered)
            {
                speed *= 0.45f;
            }

            Context.CharacterController.Move(moveDir * (speed * deltaTime));
            Context.RotateTowardsMovementOrLockOn(moveDir, deltaTime);

            if (Context.Animator != null)
            {
                Context.Animator.SetFloat(AnimHashes.MoveSpeed, moveInput.magnitude, 0.08f, deltaTime);
                Context.Animator.SetFloat(AnimHashes.StrafeX, moveInput.x, 0.08f, deltaTime);
                Context.Animator.SetFloat(AnimHashes.StrafeZ, moveInput.y, 0.08f, deltaTime);
            }
        }
    }

    // =========================================================================================
    // 3. SPRINT STATE
    // =========================================================================================
    public sealed class PlayerSprintState : HierarchicalState<PlayerStateMachineController>
    {
        public PlayerSprintState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            if (Context.Animator != null)
            {
                Context.Animator.SetBool(AnimHashes.IsSprinting, true);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (Context.TryConsumeBufferedAction(out _)) return;

            Vector2 moveInput = Context.Input.MoveInput;
            if (!Context.Input.IsSprintHeld || moveInput.sqrMagnitude < 0.05f ||
                !Context.Vitals.TryConsumeStamina(Context.SprintStaminaDrainPerSec * deltaTime))
            {
                Context.TransitionToState(Context.MoveState, PlayerLocomotionStateId.Move);
                return;
            }

            Vector3 moveDir = Context.GetCameraRelativeMovementDirection(moveInput);
            Context.CharacterController.Move(moveDir * (Context.SprintSpeed * deltaTime));

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                Context.transform.rotation = Quaternion.Slerp(Context.transform.rotation, targetRot, Context.RotationSharpness * deltaTime);
            }
        }

        public override void Exit()
        {
            if (Context.Animator != null)
            {
                Context.Animator.SetBool(AnimHashes.IsSprinting, false);
            }
        }
    }

    // =========================================================================================
    // 4. ROLL / BACKSTEP STATE (Equip Load Tier + I-Frames + Cancel Window)
    // =========================================================================================
    public sealed class PlayerRollBackstepState : HierarchicalState<PlayerStateMachineController>
    {
        private bool _isBackstep;
        private Vector3 _dodgeDirection;
        private float _duration;
        private float _elapsed;
        private float _dodgeSpeed;
        private AnimationCombatWindowSO _activeWindow;

        public PlayerRollBackstepState(PlayerStateMachineController context) : base(context) { }

        public void PrepareDodge(bool isBackstep, Vector2 moveSnapshot)
        {
            _isBackstep = isBackstep;
            if (isBackstep)
            {
                _dodgeDirection = -Context.transform.forward;
            }
            else
            {
                Vector3 dir = Context.GetCameraRelativeMovementDirection(moveSnapshot);
                _dodgeDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : Context.transform.forward;
            }
        }

        public override void Enter()
        {
            _elapsed = 0f;
            Context.WeaponHitbox?.CloseHitbox();

            if (_isBackstep)
            {
                _activeWindow = Context.BackstepWindow;
                _dodgeSpeed = Context.BackstepSpeed;
                _duration = 0.45f;
                if (Context.Animator != null) Context.Animator.SetTrigger(AnimHashes.Backstep);
            }
            else
            {
                EquipLoadTier tier = Context.Stats.EquipLoadTier;
                switch (tier)
                {
                    case EquipLoadTier.FastRoll:
                        _activeWindow = Context.FastRollWindow;
                        _dodgeSpeed = Context.FastRollSpeed;
                        _duration = 0.55f;
                        break;
                    case EquipLoadTier.FatRoll:
                        _activeWindow = Context.FatRollWindow;
                        _dodgeSpeed = Context.FatRollSpeed;
                        _duration = 0.85f;
                        break;
                    default:
                        _activeWindow = Context.MediumRollWindow;
                        _dodgeSpeed = Context.MediumRollSpeed;
                        _duration = 0.65f;
                        break;
                }

                if (!_isBackstep && _dodgeDirection.sqrMagnitude > 0.01f)
                {
                    Context.transform.rotation = Quaternion.LookRotation(_dodgeDirection);
                }

                if (Context.Animator != null)
                {
                    Context.Animator.SetInteger(AnimHashes.EquipLoadTier, (int)tier);
                    Context.Animator.SetTrigger(AnimHashes.Roll);
                }
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(_elapsed / _duration);

            bool iFrames = _activeWindow != null
                ? _activeWindow.IsIFrameActive(normalizedTime)
                : (normalizedTime >= 0.05f && normalizedTime <= 0.52f);

            Context.Vitals.SetCombatWindowFlags(iFrames, false, false);

            // Smooth velocity curve over the dodge
            float speedFactor = 1f - (normalizedTime * 0.65f);
            Context.CharacterController.Move(_dodgeDirection * (_dodgeSpeed * speedFactor * deltaTime));

            bool inCancelWindow = _activeWindow != null
                ? _activeWindow.IsInCancelWindow(normalizedTime)
                : normalizedTime >= 0.68f;

            if (inCancelWindow && Context.TryConsumeBufferedAction(out _))
            {
                return;
            }

            if (normalizedTime >= 1f)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }

        public override void Exit()
        {
            Context.Vitals.SetCombatWindowFlags(false, false, false);
        }
    }

    // =========================================================================================
    // 5. ATTACKING STATE (Combo Chains, Deterministic Hitbox Interpolation, Hyper-Armor, Cancel Window)
    // =========================================================================================
    public sealed class PlayerAttackingState : HierarchicalState<PlayerStateMachineController>
    {
        private bool _isHeavy;
        private int _comboIndex;
        private float _elapsed;
        private float _attackDuration = 0.82f;
        private AnimationCombatWindowSO _currentWindow;
        private bool _wasHitboxOpenLastFrame;

        public PlayerAttackingState(PlayerStateMachineController context) : base(context) { }

        public void ConfigureAttack(bool isHeavy, int comboIndex)
        {
            _isHeavy = isHeavy;
            _comboIndex = comboIndex;
        }

        public override void Enter()
        {
            _elapsed = 0f;
            _wasHitboxOpenLastFrame = false;

            WeaponDataSO weapon = Context.Equipment != null ? Context.Equipment.RightHandWeapon : null;
            if (_isHeavy)
            {
                _currentWindow = weapon != null ? weapon.HeavyAttackWindow : null;
                _attackDuration = 1.05f;
                if (Context.Animator != null) Context.Animator.SetTrigger(AnimHashes.HeavyAttack);
            }
            else
            {
                AnimationCombatWindowSO[] combo = weapon != null ? weapon.LightAttackComboWindows : null;
                if (combo != null && combo.Length > 0)
                {
                    _comboIndex = _comboIndex % combo.Length;
                    _currentWindow = combo[_comboIndex];
                }
                else
                {
                    _currentWindow = null;
                }

                _attackDuration = 0.72f;
                if (Context.Animator != null)
                {
                    int hash = _comboIndex == 0 ? AnimHashes.Attack1 : (_comboIndex == 1 ? AnimHashes.Attack2 : AnimHashes.Attack3);
                    Context.Animator.SetTrigger(hash);
                }
            }

            float staminaCost = _currentWindow != null ? _currentWindow.StaminaCost : (_isHeavy ? 28f : 18f);
            Context.Vitals.TryConsumeStamina(staminaCost);
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(_elapsed / _attackDuration);

            // Rotate slightly toward Lock-On target or input during early wind-up (0..0.22)
            if (normalizedTime < 0.22f)
            {
                Vector3 moveDir = Context.GetCameraRelativeMovementDirection(Context.Input.MoveInput);
                Context.RotateTowardsMovementOrLockOn(moveDir, deltaTime);
            }

            bool isHitboxActive = _currentWindow != null
                ? _currentWindow.IsHitboxActive(normalizedTime)
                : (normalizedTime >= 0.25f && normalizedTime <= 0.50f);

            bool hasHyperArmor = _currentWindow != null && _currentWindow.IsHyperArmorActive(normalizedTime);
            Context.Vitals.SetCombatWindowFlags(false, false, hasHyperArmor);

            if (Context.WeaponHitbox != null)
            {
                if (isHitboxActive && !_wasHitboxOpenLastFrame)
                {
                    DamagePayload payload = Context.Stats.BuildCurrentWeaponDamagePayload(_currentWindow, isCritical: false);
                    Context.WeaponHitbox.OpenHitbox(in payload);
                }
                else if (!isHitboxActive && _wasHitboxOpenLastFrame)
                {
                    Context.WeaponHitbox.CloseHitbox();
                }

                if (Context.WeaponHitbox.IsHitboxOpen)
                {
                    Context.WeaponHitbox.SampleHitbox();
                }

                _wasHitboxOpenLastFrame = isHitboxActive;
            }

            bool inCancelWindow = _currentWindow != null
                ? _currentWindow.IsInCancelWindow(normalizedTime)
                : normalizedTime >= 0.62f;

            if (inCancelWindow)
            {
                // Check if player buffered another Light Attack for the next combo step
                if (Context.Input.ActionBuffer.TryConsumeNext(Time.time, out BufferedActionType nextAction, out Vector2 moveSnapshot))
                {
                    if (nextAction == BufferedActionType.LightAttack && !_isHeavy && Context.Vitals.CurrentStamina > 1f)
                    {
                        ConfigureAttack(isHeavy: false, comboIndex: _comboIndex + 1);
                        Enter();
                        return;
                    }

                    // Re-enqueue non-combo action and let standard handler process it
                    Context.Input.ActionBuffer.Enqueue(nextAction, moveSnapshot, Time.time);
                    if (Context.TryConsumeBufferedAction(out _))
                    {
                        return;
                    }
                }
            }

            if (normalizedTime >= 1f)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }

        public override void Exit()
        {
            Context.WeaponHitbox?.CloseHitbox();
            Context.Vitals.SetCombatWindowFlags(false, false, false);
        }
    }

    // =========================================================================================
    // 6. PARRYING STATE
    // =========================================================================================
    public sealed class PlayerParryingState : HierarchicalState<PlayerStateMachineController>
    {
        private float _elapsed;
        private const float ParryTotalDuration = 0.65f;
        private AnimationCombatWindowSO _parryWindow;

        public PlayerParryingState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            _elapsed = 0f;
            WeaponDataSO leftHand = Context.Equipment != null ? Context.Equipment.LeftHandShieldOrWeapon : null;
            _parryWindow = leftHand != null ? leftHand.ParryWindow : null;

            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.Parry);
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(_elapsed / ParryTotalDuration);

            bool parryActive = _parryWindow != null
                ? _parryWindow.IsParryActive(normalizedTime)
                : (normalizedTime >= 0.14f && normalizedTime <= 0.36f);

            Context.Vitals.SetCombatWindowFlags(false, parryActive, false);

            if (normalizedTime >= 0.72f && Context.TryConsumeBufferedAction(out _))
            {
                return;
            }

            if (normalizedTime >= 1f)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }

        public override void Exit()
        {
            Context.Vitals.SetCombatWindowFlags(false, false, false);
        }
    }

    // =========================================================================================
    // 7. GUARD / BLOCK STATE
    // =========================================================================================
    public sealed class PlayerGuardBlockState : HierarchicalState<PlayerStateMachineController>
    {
        public PlayerGuardBlockState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            Context.Vitals.SetBlocking(true);
            if (Context.Animator != null)
            {
                Context.Animator.SetBool(AnimHashes.IsBlocking, true);
            }
        }

        public override void Tick(float deltaTime)
        {
            if (Context.TryConsumeBufferedAction(out _)) return;

            if (!Context.Input.IsBlockHeld || Context.Vitals.CurrentStamina <= 0f)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
                return;
            }

            Vector2 moveInput = Context.Input.MoveInput;
            Vector3 moveDir = Context.GetCameraRelativeMovementDirection(moveInput);
            Context.CharacterController.Move(moveDir * (Context.WalkSpeed * 0.65f * deltaTime));
            Context.RotateTowardsMovementOrLockOn(moveDir, deltaTime);
        }

        public override void Exit()
        {
            Context.Vitals.SetBlocking(false);
            if (Context.Animator != null)
            {
                Context.Animator.SetBool(AnimHashes.IsBlocking, false);
            }
        }
    }

    // =========================================================================================
    // 8. HIT STUN STATE
    // =========================================================================================
    public sealed class PlayerHitStunState : HierarchicalState<PlayerStateMachineController>
    {
        private float _elapsed;
        private const float HitStunDuration = 0.48f;

        public PlayerHitStunState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            _elapsed = 0f;
            Context.WeaponHitbox?.CloseHitbox();
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.HitStun);
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= HitStunDuration * 0.75f && Context.TryConsumeBufferedAction(out _))
            {
                return;
            }

            if (_elapsed >= HitStunDuration)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }
    }

    // =========================================================================================
    // 9. GUARD BROKEN / STANCE BROKEN STATE
    // =========================================================================================
    public sealed class PlayerGuardBrokenState : HierarchicalState<PlayerStateMachineController>
    {
        private float _elapsed;
        private const float GuardBreakStunDuration = 1.65f;

        public PlayerGuardBrokenState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            _elapsed = 0f;
            Context.WeaponHitbox?.CloseHitbox();
            Context.Vitals.SetBlocking(false);
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.GuardBroken);
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= GuardBreakStunDuration)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }
    }

    // =========================================================================================
    // 10. RIPOSTE / BACKSTAB STATE (Invulnerable Critical Execution)
    // =========================================================================================
    public sealed class PlayerRiposteBackstabState : HierarchicalState<PlayerStateMachineController>
    {
        private IDamageable _target;
        private bool _isRiposte;
        private bool _hasDealtDamage;
        private float _elapsed;
        private const float CriticalAnimationDuration = 1.45f;
        private const float DamageStrikeTime = 0.62f;

        public PlayerRiposteBackstabState(PlayerStateMachineController context) : base(context) { }

        public void ConfigureCritical(IDamageable target, bool isRiposte)
        {
            _target = target;
            _isRiposte = isRiposte;
        }

        public override void Enter()
        {
            _elapsed = 0f;
            _hasDealtDamage = false;
            // Player has full I-Frames during Riposte / Backstab execution
            Context.Vitals.SetCombatWindowFlags(true, false, true);

            if (_target != null && _target.Transform != null)
            {
                Vector3 toTarget = _target.Transform.position - Context.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    Context.transform.rotation = Quaternion.LookRotation(toTarget.normalized);
                }
            }

            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(_isRiposte ? AnimHashes.Riposte : AnimHashes.Backstab);
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (!_hasDealtDamage && _elapsed >= DamageStrikeTime)
            {
                _hasDealtDamage = true;
                if (_target != null && _target.IsAlive)
                {
                    DamagePayload critPayload = Context.Stats.BuildCurrentWeaponDamagePayload(null, isCritical: true);
                    critPayload.HitPoint = _target.Transform.position + Vector3.up;
                    critPayload.HitNormal = -Context.transform.forward;
                    critPayload.HitDirection = Context.transform.forward;
                    _target.ReceiveDamage(in critPayload);
                }
            }

            if (_elapsed >= CriticalAnimationDuration)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }

        public override void Exit()
        {
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            _target = null;
        }
    }

    // =========================================================================================
    // 11. USING CONSUMABLE STATE (Estus Flask / Item + Boss Input-Reading Event)
    // =========================================================================================
    public sealed class PlayerUsingConsumableState : HierarchicalState<PlayerStateMachineController>
    {
        private float _elapsed;
        private bool _appliedEffect;
        private const float DrinkDuration = 0.95f;
        private const float HealApplicationTime = 0.42f;

        public PlayerUsingConsumableState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            _elapsed = 0f;
            _appliedEffect = false;

            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.UseConsumable);
            }

            // Broadcast immediately on heal animation start so Bosses with Input Reading can react!
            int charges = 0;
            ConsumableItemSO item = Context.Equipment != null ? Context.Equipment.GetActiveConsumable(out charges) : null;
            if (item != null && charges > 0 && item.TriggersBossHealPunishEvent)
            {
                EventBus<OnPlayerHealStartedEvent>.Publish(new OnPlayerHealStartedEvent
                {
                    Player = Context.gameObject,
                    Position = Context.transform.position
                });
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            // Slow walk allowed while drinking Estus
            Vector3 moveDir = Context.GetCameraRelativeMovementDirection(Context.Input.MoveInput);
            Context.CharacterController.Move(moveDir * (Context.WalkSpeed * 0.35f * deltaTime));

            if (!_appliedEffect && _elapsed >= HealApplicationTime)
            {
                _appliedEffect = true;
                Context.Equipment?.TryConsumeActiveQuickSlot(Context.Vitals);
            }

            if (_elapsed >= DrinkDuration * 0.78f && Context.TryConsumeBufferedAction(out _))
            {
                return;
            }

            if (_elapsed >= DrinkDuration)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }
    }

    // =========================================================================================
    // 12. INTERACTING STATE (Bonfire, FogWall, Lever, Door)
    // =========================================================================================
    public sealed class PlayerInteractingState : HierarchicalState<PlayerStateMachineController>
    {
        private IInteractable _interactable;
        private float _elapsed;
        private const float InteractionDuration = 0.65f;

        public PlayerInteractingState(PlayerStateMachineController context) : base(context) { }

        public void ConfigureInteraction(IInteractable interactable)
        {
            _interactable = interactable;
        }

        public override void Enter()
        {
            _elapsed = 0f;
            // Grant I-Frames while opening doors / lighting bonfires / traversing fog walls
            Context.Vitals.SetCombatWindowFlags(true, false, false);

            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.Interact);
            }

            _interactable?.Interact(Context.gameObject);
        }

        public override void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed >= InteractionDuration)
            {
                Context.TransitionToState(Context.IdleState, PlayerLocomotionStateId.Idle);
            }
        }

        public override void Exit()
        {
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            _interactable = null;
        }
    }

    // =========================================================================================
    // 13. DEAD STATE
    // =========================================================================================
    public sealed class PlayerDeadState : HierarchicalState<PlayerStateMachineController>
    {
        public PlayerDeadState(PlayerStateMachineController context) : base(context) { }

        public override void Enter()
        {
            Context.WeaponHitbox?.CloseHitbox();
            Context.LockOn?.ClearLockOn();
            Context.Vitals.SetCombatWindowFlags(false, false, false);
            if (Context.Animator != null)
            {
                Context.Animator.SetTrigger(AnimHashes.Die);
            }
        }
    }
}
