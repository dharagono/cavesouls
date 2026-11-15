using CaveSouls.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Reads player input from the New Unity Input System and feeds discrete combat actions
    /// into the ActionInputBuffer with a configurable buffer window (0.25s).
    /// Zero GC allocations in Update.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Input Buffer Configuration")]
        [SerializeField] private float _inputBufferWindowSeconds = 0.25f;
        [SerializeField] private InputActionAsset _inputActionsAsset;

        private ActionInputBuffer _actionBuffer;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _lightAttackAction;
        private InputAction _heavyAttackAction;
        private InputAction _rollOrSprintAction;
        private InputAction _blockAction;
        private InputAction _parryAction;
        private InputAction _useItemAction;
        private InputAction _interactAction;
        private InputAction _lockOnAction;
        private InputAction _crouchAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _isSprintHeld;
        private bool _isBlockHeld;
        private bool _isCrouchToggled;
        private float _rollSprintHoldTimer;
        private bool _rollButtonDown;

        private const float SprintHoldThreshold = 0.22f;

        public ActionInputBuffer ActionBuffer => _actionBuffer;
        public Vector2 MoveInput => _moveInput;
        public Vector2 LookInput => _lookInput;
        public bool IsSprintHeld => _isSprintHeld;
        public bool IsBlockHeld => _isBlockHeld;
        public bool IsCrouchActive => _isCrouchToggled;

        public event System.Action OnLockOnTogglePressed;

        private void Awake()
        {
            _actionBuffer = new ActionInputBuffer(_inputBufferWindowSeconds);
            BindInputActions();
        }

        private void OnEnable()
        {
            if (_inputActionsAsset != null)
            {
                _inputActionsAsset.Enable();
            }
        }

        private void OnDisable()
        {
            if (_inputActionsAsset != null)
            {
                _inputActionsAsset.Disable();
            }
        }

        private void Update()
        {
            _moveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _lookInput = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
            _isBlockHeld = _blockAction != null && _blockAction.IsPressed();

            // Souls-style shared Dodge (Tap) / Sprint (Hold) logic
            if (_rollButtonDown)
            {
                _rollSprintHoldTimer += Time.deltaTime;
                if (_rollSprintHoldTimer >= SprintHoldThreshold && _moveInput.sqrMagnitude > 0.1f)
                {
                    _isSprintHeld = true;
                }
            }
            else
            {
                _isSprintHeld = false;
            }
        }

        private void BindInputActions()
        {
            if (_inputActionsAsset == null) return;

            InputActionMap playerMap = _inputActionsAsset.FindActionMap("Player", false);
            if (playerMap == null) return;

            _moveAction = playerMap.FindAction("Move", false);
            _lookAction = playerMap.FindAction("Look", false);
            _lightAttackAction = playerMap.FindAction("Attack", false);
            _heavyAttackAction = playerMap.FindAction("HeavyAttack", false);
            _rollOrSprintAction = playerMap.FindAction("Sprint", false);
            _blockAction = playerMap.FindAction("Block", false);
            _parryAction = playerMap.FindAction("Parry", false);
            _useItemAction = playerMap.FindAction("Previous", false);
            _interactAction = playerMap.FindAction("Interact", false);
            _lockOnAction = playerMap.FindAction("Next", false);
            _crouchAction = playerMap.FindAction("Crouch", false);

            if (_lightAttackAction != null) _lightAttackAction.performed += OnLightAttackPerformed;
            if (_heavyAttackAction != null) _heavyAttackAction.performed += OnHeavyAttackPerformed;
            if (_rollOrSprintAction != null)
            {
                _rollOrSprintAction.started += OnRollSprintStarted;
                _rollOrSprintAction.canceled += OnRollSprintCanceled;
            }
            if (_parryAction != null) _parryAction.performed += OnParryPerformed;
            if (_useItemAction != null) _useItemAction.performed += OnUseItemPerformed;
            if (_interactAction != null) _interactAction.performed += OnInteractPerformed;
            if (_lockOnAction != null) _lockOnAction.performed += OnLockOnPerformed;
            if (_crouchAction != null) _crouchAction.performed += OnCrouchPerformed;
        }

        private void OnDestroy()
        {
            if (_lightAttackAction != null) _lightAttackAction.performed -= OnLightAttackPerformed;
            if (_heavyAttackAction != null) _heavyAttackAction.performed -= OnHeavyAttackPerformed;
            if (_rollOrSprintAction != null)
            {
                _rollOrSprintAction.started -= OnRollSprintStarted;
                _rollOrSprintAction.canceled -= OnRollSprintCanceled;
            }
            if (_parryAction != null) _parryAction.performed -= OnParryPerformed;
            if (_useItemAction != null) _useItemAction.performed -= OnUseItemPerformed;
            if (_interactAction != null) _interactAction.performed -= OnInteractPerformed;
            if (_lockOnAction != null) _lockOnAction.performed -= OnLockOnPerformed;
            if (_crouchAction != null) _crouchAction.performed -= OnCrouchPerformed;
        }

        private void OnLightAttackPerformed(InputAction.CallbackContext ctx)
        {
            _actionBuffer.Enqueue(BufferedActionType.LightAttack, _moveInput, Time.time);
        }

        private void OnHeavyAttackPerformed(InputAction.CallbackContext ctx)
        {
            _actionBuffer.Enqueue(BufferedActionType.HeavyAttack, _moveInput, Time.time);
        }

        private void OnRollSprintStarted(InputAction.CallbackContext ctx)
        {
            _rollButtonDown = true;
            _rollSprintHoldTimer = 0f;
        }

        private void OnRollSprintCanceled(InputAction.CallbackContext ctx)
        {
            if (_rollButtonDown && _rollSprintHoldTimer < SprintHoldThreshold)
            {
                BufferedActionType dodgeType = _moveInput.sqrMagnitude > 0.05f
                    ? BufferedActionType.Roll
                    : BufferedActionType.Backstep;
                _actionBuffer.Enqueue(dodgeType, _moveInput, Time.time);
            }
            _rollButtonDown = false;
            _isSprintHeld = false;
        }

        private void OnParryPerformed(InputAction.CallbackContext ctx)
        {
            _actionBuffer.Enqueue(BufferedActionType.Parry, _moveInput, Time.time);
        }

        private void OnUseItemPerformed(InputAction.CallbackContext ctx)
        {
            _actionBuffer.Enqueue(BufferedActionType.UseConsumable, _moveInput, Time.time);
        }

        private void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            _actionBuffer.Enqueue(BufferedActionType.Interact, _moveInput, Time.time);
        }

        private void OnLockOnPerformed(InputAction.CallbackContext ctx)
        {
            OnLockOnTogglePressed?.Invoke();
        }

        private void OnCrouchPerformed(InputAction.CallbackContext ctx)
        {
            _isCrouchToggled = !_isCrouchToggled;
        }
    }
}
