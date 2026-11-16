using CaveSouls.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Third-person Souls-like player controller for cavern exploration.
    /// Manages CharacterController movement, orbit-camera-relative orientation,
    /// dynamic deep mud slowdown debuff, surface footstep acoustics, and lore inspection.
    /// Strictly zero GC allocations in Update.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SoulsPlayerController : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float _walkSpeed = 3.8f;
        [SerializeField] private float _sprintSpeed = 6.4f;
        [SerializeField] private float _rotationSpeed = 14f;
        [SerializeField] private float _gravity = -19.6f;

        [Header("References")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private SoulsOrbitCamera _orbitCamera;
        [SerializeField] private Transform _visualModelTransform;
        [SerializeField] private AudioSource _footstepAudioSource;

        [Header("Footstep Audio Clips")]
        [SerializeField] private AudioClip[] _mudFootstepClips;
        [SerializeField] private AudioClip[] _stoneFootstepClips;
        [SerializeField] private float _walkStepInterval = 0.52f;
        [SerializeField] private float _sprintStepInterval = 0.35f;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = 2.4f;
        [SerializeField] private LayerMask _interactionLayerMask = ~0;

        private float _verticalVelocity;
        private bool _isInMudZone;
        private float _mudSpeedMultiplier = 1.0f;
        private float _stepTimer;
        private float _proceduralStepCycle;
        private IInteractable _nearbyInteractable;
        private bool _promptWasVisible;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        private readonly RaycastHit[] _groundHits = new RaycastHit[2];
        private readonly Collider[] _overlapCols = new Collider[6];

        public bool IsInMudZone => _isInMudZone;
        public float CurrentSpeedMultiplier => _isInMudZone ? _mudSpeedMultiplier : 1.0f;

        public void SetInMudZone(bool inMud, float multiplier)
        {
            _isInMudZone = inMud;
            _mudSpeedMultiplier = Mathf.Clamp(multiplier, 0.4f, 1.0f);
        }

        private void OnEnable()
        {
            EventBus<OnCheckpointRestedEvent>.Subscribe(HandleCheckpointRested);
        }

        private void OnDisable()
        {
            EventBus<OnCheckpointRestedEvent>.Unsubscribe(HandleCheckpointRested);
        }

        private void HandleCheckpointRested(in OnCheckpointRestedEvent evt)
        {
            _spawnPosition = evt.RespawnPosition;
            _spawnRotation = evt.RespawnRotation;
        }

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }
            if (_footstepAudioSource == null)
            {
                _footstepAudioSource = GetComponent<AudioSource>();
            }
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void Start()
        {
            if (_orbitCamera == null)
            {
                _orbitCamera = FindAnyObjectByType<SoulsOrbitCamera>();
            }

            // Ground snap: Ensure the player is positioned safely above the terrain mesh surface
            EnsureGroundPlacement();
        }

        public void EnsureGroundPlacement()
        {
            Vector3 origin = transform.position + Vector3.up * 4.0f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_characterController != null) _characterController.enabled = false;
                transform.position = hit.point + Vector3.up * 0.08f;
                if (_characterController != null) _characterController.enabled = true;
                _spawnPosition = transform.position;
                _verticalVelocity = -1.0f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Void fall protection: respawn if fallen below the map bounds
            if (transform.position.y < -30f)
            {
                RespawnAtSafeLocation();
                return;
            }

            HandleLocomotion(dt);
            HandleFootsteps(dt);
            HandleInteractionCheck();
        }

        private void RespawnAtSafeLocation()
        {
            if (_characterController != null) _characterController.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            if (_characterController != null) _characterController.enabled = true;
            _verticalVelocity = 0f;
        }

        private void HandleLocomotion(float dt)
        {
            Vector2 input = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) input = stick;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            bool isMoving = input.sqrMagnitude > 0.01f;

            bool isSprinting = false;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) isSprinting = true;
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.isPressed || Gamepad.current.rightTrigger.isPressed)) isSprinting = true;

            // Camera-relative movement
            float targetYaw = (_orbitCamera != null) ? _orbitCamera.CurrentYaw : transform.eulerAngles.y;
            Vector3 camForward = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0f, targetYaw, 0f) * Vector3.right;

            Vector3 moveDirection = (camForward * input.y + camRight * input.x).normalized;

            // Speed calculation with mud debuff (70% in mud)
            float baseSpeed = isSprinting ? _sprintSpeed : _walkSpeed;
            float currentSpeed = baseSpeed * CurrentSpeedMultiplier;

            // Grounding & Gravity
            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += _gravity * dt;
            }

            Vector3 velocity = moveDirection * currentSpeed;
            velocity.y = _verticalVelocity;

            _characterController.Move(velocity * dt);

            // Smooth rotation towards movement direction
            if (isMoving && moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * _rotationSpeed);
            }

            // Procedural subtle bobbing for placeholder mannequin when moving
            if (_visualModelTransform != null)
            {
                if (isMoving)
                {
                    _proceduralStepCycle += dt * (isSprinting ? 12f : 8f);
                    float bobY = Mathf.Sin(_proceduralStepCycle) * 0.04f;
                    float tiltZ = Mathf.Cos(_proceduralStepCycle * 0.5f) * 1.5f;
                    _visualModelTransform.localPosition = new Vector3(0f, bobY, 0f);
                    _visualModelTransform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
                }
                else
                {
                    _visualModelTransform.localPosition = Vector3.Lerp(_visualModelTransform.localPosition, Vector3.zero, dt * 6f);
                    _visualModelTransform.localRotation = Quaternion.Slerp(_visualModelTransform.localRotation, Quaternion.identity, dt * 6f);
                }
            }
        }

        private void HandleFootsteps(float dt)
        {
            if (!_characterController.isGrounded) return;

            Vector3 horizontalVel = _characterController.velocity;
            horizontalVel.y = 0f;
            float currentSpeed = horizontalVel.magnitude;

            if (currentSpeed < 0.2f)
            {
                _stepTimer = 0f;
                return;
            }

            bool isSprinting = currentSpeed > (_walkSpeed + 0.5f);
            float interval = isSprinting ? _sprintStepInterval : _walkStepInterval;

            _stepTimer += dt;
            if (_stepTimer >= interval)
            {
                _stepTimer = 0f;
                PlaySurfaceFootstep();
            }
        }

        private void PlaySurfaceFootstep()
        {
            if (_footstepAudioSource == null) return;

            bool isMud = _isInMudZone;

            // Downward raycast to detect actual surface material
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 1.4f, ~0, QueryTriggerInteraction.Ignore);
            if (count > 0 && _groundHits[0].collider != null)
            {
                Collider col = _groundHits[0].collider;
                if (col.TryGetComponent(out SurfaceMaterialIdentifier surfaceId))
                {
                    isMud = (surfaceId.SurfaceType == SurfaceMaterialType.Dirt || surfaceId.SurfaceType == SurfaceMaterialType.Water);
                }
                else if (col.name.IndexOf("mud", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         col.name.IndexOf("terrain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isMud = true;
                }
            }

            AudioClip[] clips = isMud ? _mudFootstepClips : _stoneFootstepClips;
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    _footstepAudioSource.pitch = Random.Range(0.92f, 1.08f);
                    float volume = isMud ? 0.88f : 0.72f;
                    _footstepAudioSource.PlayOneShot(clip, volume);
                }
            }
        }

        private void HandleInteractionCheck()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, _interactionRange, _overlapCols, _interactionLayerMask, QueryTriggerInteraction.Collide);
            IInteractable foundInteractable = null;

            for (int i = 0; i < hits; i++)
            {
                if (_overlapCols[i] != null)
                {
                    IInteractable interactable = _overlapCols[i].GetComponent<IInteractable>() ?? _overlapCols[i].GetComponentInParent<IInteractable>();
                    if (interactable != null && interactable.CanInteract(gameObject))
                    {
                        foundInteractable = interactable;
                        break;
                    }
                }
            }

            _nearbyInteractable = foundInteractable;
            bool promptVisible = _nearbyInteractable != null;

            if (promptVisible != _promptWasVisible)
            {
                _promptWasVisible = promptVisible;
                EventBus<OnInteractionPromptChangedEvent>.Publish(new OnInteractionPromptChangedEvent
                {
                    IsVisible = promptVisible,
                    PromptText = promptVisible ? _nearbyInteractable.InteractionPrompt : string.Empty
                });
            }

            if (_nearbyInteractable != null)
            {
                bool interactPressed = false;
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) interactPressed = true;
                if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) interactPressed = true;

                if (interactPressed)
                {
                    _nearbyInteractable.Interact(gameObject);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isInMudZone ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}
