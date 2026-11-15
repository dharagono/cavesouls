using CaveSouls.Core;
using CaveSouls.Features.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace CaveSouls.Features.AI
{
    /// <summary>
    /// Tactical AI Brain for standard enemies and mini-bosses.
    /// Uses zero-allocation Behavior Tree + AICombatDirector Ticket System + Tactical Strafing/Flanking
    /// + IWorldResettable (Bonfire respawn if IsUnique == false).
    /// </summary>
    [RequireComponent(typeof(CombatEntityVitals), typeof(AISensor))]
    public sealed class EnemyTacticalBrain : MonoBehaviour, IWorldResettable, ISaveable
    {
        [Header("Identity & World Reset")]
        [SerializeField] private string _uniqueSaveId;
        [SerializeField] private bool _isUniqueMiniBossOrBoss;

        [Header("References")]
        [SerializeField] private NavMeshAgent _navAgent;
        [SerializeField] private Animator _animator;
        [SerializeField] private DeterministicWeaponHitbox _weaponHitbox;
        [SerializeField] private AnimationCombatWindowSO _meleeAttackWindow;

        [Header("Combat & Tactical Parameters")]
        [SerializeField] private float _attackRange = 2.4f;
        [SerializeField] private float _tacticalStrafeDistance = 4.5f;
        [SerializeField] private float _chaseSpeed = 4.0f;
        [SerializeField] private float _strafeSpeed = 2.2f;
        [SerializeField] private float _basePhysicalDamage = 95f;
        [SerializeField] private float _basePoiseDamage = 25f;

        private CombatEntityVitals _vitals;
        private AISensor _sensor;
        private AICombatDirector _combatDirector;

        private BTNode _behaviorTreeRoot;
        private Vector3 _initialSpawnPosition;
        private Quaternion _initialSpawnRotation;
        private float _attackTimer;
        private float _attackDuration = 0.95f;
        private float _strafeDirectionSign = 1f;
        private float _strafeFlipTimer;
        private bool _isAttacking;
        private bool _wasHitboxOpen;
        private bool _permanentlyDefeated;

        public bool IsUnique => _isUniqueMiniBossOrBoss;
        public string SaveId => _uniqueSaveId;

        private void Awake()
        {
            _vitals = GetComponent<CombatEntityVitals>();
            _sensor = GetComponent<AISensor>();
            if (_navAgent == null) TryGetComponent(out _navAgent);

            _initialSpawnPosition = transform.position;
            _initialSpawnRotation = transform.rotation;

            if (_weaponHitbox != null)
            {
                _weaponHitbox.Initialize(_vitals.EntityId);
            }

            _strafeDirectionSign = (_vitals.EntityId & 1) == 0 ? 1f : -1f;
            BuildBehaviorTree();
        }

        private void Start()
        {
            ServiceLocator.TryGet(out _combatDirector);
        }

        private void OnEnable()
        {
            EventBus<OnWorldResetEvent>.Subscribe(HandleWorldReset);
            EventBus<OnEntityDeathEvent>.Subscribe(HandleEntityDeath);
        }

        private void OnDisable()
        {
            EventBus<OnWorldResetEvent>.Unsubscribe(HandleWorldReset);
            EventBus<OnEntityDeathEvent>.Unsubscribe(HandleEntityDeath);
        }

        private void Update()
        {
            if (_permanentlyDefeated || !_vitals.IsAlive) return;

            float dt = Time.deltaTime;
            _sensor.TickSensor(dt);
            _behaviorTreeRoot.Tick(dt);
        }

        private void BuildBehaviorTree()
        {
            // Zero-allocation Behavior Tree constructed once in Awake:
            // Selector:
            //  1. StanceBroken Hold
            //  2. Active Attack Execution
            //  3. Engage Target (Attack with Director Ticket OR Tactical Strafe/Chase)
            //  4. Return to Post / Idle
            _behaviorTreeRoot = new BTSelector(
                new BTSequence(
                    new BTCondition(IsStanceBrokenCondition),
                    new BTAction(TickStanceBrokenAction)
                ),
                new BTSequence(
                    new BTCondition(IsCurrentlyAttackingCondition),
                    new BTAction(TickAttackExecutionAction)
                ),
                new BTSequence(
                    new BTCondition(HasDetectedTargetCondition),
                    new BTSelector(
                        new BTSequence(
                            new BTCondition(CanStartMeleeAttackWithTicketCondition),
                            new BTAction(StartMeleeAttackAction)
                        ),
                        new BTAction(TickTacticalPositioningAction)
                    )
                ),
                new BTAction(TickIdleGuardPostAction)
            );
        }

        private bool IsStanceBrokenCondition() => _vitals.IsStanceBroken;

        private BTNodeStatus TickStanceBrokenAction(float dt)
        {
            StopNavigation();
            _weaponHitbox?.CloseHitbox();
            _isAttacking = false;
            ReleaseTicketIfHeld();
            return BTNodeStatus.Running;
        }

        private bool IsCurrentlyAttackingCondition() => _isAttacking;

        private BTNodeStatus TickAttackExecutionAction(float dt)
        {
            _attackTimer += dt;
            float normalizedTime = Mathf.Clamp01(_attackTimer / _attackDuration);

            // Track target slightly during early windup
            if (normalizedTime < 0.25f && _sensor.DetectedTarget != null)
            {
                FacePosition(_sensor.DetectedTarget.position, dt * 8f);
            }

            bool hitboxActive = _meleeAttackWindow != null
                ? _meleeAttackWindow.IsHitboxActive(normalizedTime)
                : (normalizedTime >= 0.28f && normalizedTime <= 0.52f);

            bool hyperArmor = _meleeAttackWindow != null && _meleeAttackWindow.IsHyperArmorActive(normalizedTime);
            _vitals.SetCombatWindowFlags(false, false, hyperArmor);

            if (_weaponHitbox != null)
            {
                if (hitboxActive && !_wasHitboxOpen)
                {
                    DamagePayload payload = new DamagePayload
                    {
                        Attacker = gameObject,
                        PhysicalSubType = PhysicalDamageSubType.Slash,
                        PhysicalDamage = _basePhysicalDamage,
                        PoiseDamage = _basePoiseDamage,
                        StaminaDrainOnBlock = 26f,
                        WeaponMaterial = WeaponMaterialType.IronBlade,
                        CanBeParried = _meleeAttackWindow == null || _meleeAttackWindow.CanBeParried
                    };
                    _weaponHitbox.OpenHitbox(in payload);
                }
                else if (!hitboxActive && _wasHitboxOpen)
                {
                    _weaponHitbox.CloseHitbox();
                }

                if (_weaponHitbox.IsHitboxOpen)
                {
                    _weaponHitbox.SampleHitbox();
                }

                _wasHitboxOpen = hitboxActive;
            }

            if (normalizedTime >= 1f)
            {
                _isAttacking = false;
                _wasHitboxOpen = false;
                _weaponHitbox?.CloseHitbox();
                _vitals.SetCombatWindowFlags(false, false, false);
                ReleaseTicketIfHeld();
                return BTNodeStatus.Success;
            }

            return BTNodeStatus.Running;
        }

        private bool HasDetectedTargetCondition() => _sensor.HasTarget;

        private bool CanStartMeleeAttackWithTicketCondition()
        {
            if (_sensor.DetectedTarget == null) return false;
            float distSqr = (_sensor.DetectedTarget.position - transform.position).sqrMagnitude;
            if (distSqr > _attackRange * _attackRange) return false;

            if (_combatDirector == null) ServiceLocator.TryGet(out _combatDirector);
            return _combatDirector == null || _combatDirector.TryRequestAttackTicket(_vitals.EntityId);
        }

        private BTNodeStatus StartMeleeAttackAction(float dt)
        {
            StopNavigation();
            _isAttacking = true;
            _attackTimer = 0f;
            _wasHitboxOpen = false;

            if (_animator != null)
            {
                _animator.SetTrigger(AnimHashes.Attack1);
            }

            return BTNodeStatus.Running;
        }

        private BTNodeStatus TickTacticalPositioningAction(float dt)
        {
            Vector3 targetPos = _sensor.DetectedTarget != null
                ? _sensor.DetectedTarget.position
                : _sensor.LastKnownTargetPosition;

            Vector3 toTarget = targetPos - transform.position;
            float dist = toTarget.magnitude;

            FacePosition(targetPos, dt * 10f);

            if (_combatDirector == null) ServiceLocator.TryGet(out _combatDirector);
            bool hasTicket = _combatDirector == null || _combatDirector.TryRequestAttackTicket(_vitals.EntityId);

            // If we have an attack ticket or are far away, close distance
            if (hasTicket || dist > _tacticalStrafeDistance + 1.5f)
            {
                MoveToPosition(targetPos, _chaseSpeed);
            }
            else
            {
                // Otherwise strafe/flank around the player while waiting for an attack ticket!
                _strafeFlipTimer += dt;
                if (_strafeFlipTimer >= 3.0f)
                {
                    _strafeFlipTimer = 0f;
                    _strafeDirectionSign = -_strafeDirectionSign;
                }

                Vector3 dirToPlayer = toTarget.normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirectionSign;
                Vector3 desiredCirclePos = targetPos - (dirToPlayer * _tacticalStrafeDistance) + (tangent * 2.0f);
                MoveToPosition(desiredCirclePos, _strafeSpeed);
            }

            return BTNodeStatus.Running;
        }

        private BTNodeStatus TickIdleGuardPostAction(float dt)
        {
            float distToSpawnSqr = (_initialSpawnPosition - transform.position).sqrMagnitude;
            if (distToSpawnSqr > 1.0f)
            {
                MoveToPosition(_initialSpawnPosition, _strafeSpeed);
            }
            else
            {
                StopNavigation();
            }
            return BTNodeStatus.Success;
        }

        private void MoveToPosition(Vector3 destination, float speed)
        {
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = false;
                _navAgent.speed = speed;
                _navAgent.SetDestination(destination);
            }
            else
            {
                Vector3 dir = destination - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.05f)
                {
                    transform.position += dir.normalized * (speed * Time.deltaTime);
                }
            }

            if (_animator != null)
            {
                _animator.SetFloat(AnimHashes.MoveSpeed, speed / _chaseSpeed);
            }
        }

        private void StopNavigation()
        {
            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
            }

            if (_animator != null)
            {
                _animator.SetFloat(AnimHashes.MoveSpeed, 0f);
            }
        }

        private void FacePosition(Vector3 targetPos, float step)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), step);
            }
        }

        private void ReleaseTicketIfHeld()
        {
            if (_combatDirector != null)
            {
                _combatDirector.ReleaseAttackTicket(_vitals.EntityId);
            }
        }

        public void OnWorldReset()
        {
            // Unique bosses / mini-bosses stay dead permanently once killed
            if (_isUniqueMiniBossOrBoss && _permanentlyDefeated) return;

            ReleaseTicketIfHeld();
            _isAttacking = false;
            _wasHitboxOpen = false;
            _weaponHitbox?.CloseHitbox();
            _sensor.ClearDetection();

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.Warp(_initialSpawnPosition);
            }
            else
            {
                transform.SetPositionAndRotation(_initialSpawnPosition, _initialSpawnRotation);
            }

            transform.rotation = _initialSpawnRotation;
            gameObject.SetActive(true);
            _vitals.RestoreAllVitals();
        }

        public bool CaptureState() => _permanentlyDefeated;

        public void RestoreState(bool isDefeated)
        {
            _permanentlyDefeated = isDefeated;
            if (_isUniqueMiniBossOrBoss && _permanentlyDefeated)
            {
                gameObject.SetActive(false);
            }
        }

        private void HandleWorldReset(in OnWorldResetEvent evt)
        {
            OnWorldReset();
        }

        private void HandleEntityDeath(in OnEntityDeathEvent evt)
        {
            if (evt.DeadEntity != gameObject) return;

            ReleaseTicketIfHeld();
            _weaponHitbox?.CloseHitbox();

            if (_isUniqueMiniBossOrBoss)
            {
                _permanentlyDefeated = true;
                EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "UniqueEnemyDefeated" });
            }
        }
    }
}
