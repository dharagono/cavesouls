using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.WorldAndProgression
{
    /// <summary>
    /// Souls-like One-Way Shortcut Door ("Does not open from this side") and Persistent Elevator.
    /// Both implement IInteractable and ISaveable for continuous world state persistence.
    /// </summary>
    public sealed class OneWayShortcutDoor : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private string _doorSaveId = "ShortcutDoor_Catacombs_01";
        [SerializeField] private Transform _doorHingeTransform;
        [SerializeField] private float _openYawAngle = 95f;

        private bool _isUnlockedAndOpen;
        private string _lastAttemptMessage = "Open Door";
        private WorldProgressionRegistry _registry;

        public string SaveId => _doorSaveId;
        public string InteractionPrompt => _isUnlockedAndOpen ? string.Empty : _lastAttemptMessage;

        private void Start()
        {
            if (ServiceLocator.TryGet(out _registry))
            {
                _registry.RegisterSaveable(this);
            }
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.UnregisterSaveable(this);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (_isUnlockedAndOpen || interactor == null) return false;

            bool isOnUnlockableSide = IsInteractorOnUnlockableSide(interactor.transform.position);
            _lastAttemptMessage = isOnUnlockableSide ? "Open Contraption" : "Does not open from this side";
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (_isUnlockedAndOpen || interactor == null) return;

            if (!IsInteractorOnUnlockableSide(interactor.transform.position))
            {
                _lastAttemptMessage = "Does not open from this side";
                return;
            }

            SetDoorOpen(true);
            EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "ShortcutDoorUnlocked" });
        }

        public bool CaptureState() => _isUnlockedAndOpen;

        public void RestoreState(bool state)
        {
            SetDoorOpen(state);
        }

        private bool IsInteractorOnUnlockableSide(Vector3 interactorPos)
        {
            Vector3 toInteractor = (interactorPos - transform.position).normalized;
            // Door's forward vector points toward the valid shortcut unlock side
            return Vector3.Dot(transform.forward, toInteractor) > 0f;
        }

        private void SetDoorOpen(bool open)
        {
            _isUnlockedAndOpen = open;
            if (_doorHingeTransform != null)
            {
                _doorHingeTransform.localRotation = open
                    ? Quaternion.Euler(0f, _openYawAngle, 0f)
                    : Quaternion.identity;
            }
        }
    }

    public sealed class PersistentElevator : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private string _elevatorSaveId = "Elevator_AbyssShaft_01";
        [SerializeField] private Transform _platformTransform;
        [SerializeField] private Vector3 _bottomLocalPosition;
        [SerializeField] private Vector3 _topLocalPosition = new Vector3(0f, 18f, 0f);
        [SerializeField] private float _travelSpeed = 3.5f;

        private bool _isAtTopState;
        private bool _isMoving;
        private WorldProgressionRegistry _registry;

        public string SaveId => _elevatorSaveId;
        public string InteractionPrompt => _isMoving ? string.Empty : "Step on Pressure Plate";

        private void Start()
        {
            if (ServiceLocator.TryGet(out _registry))
            {
                _registry.RegisterSaveable(this);
            }
        }

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.UnregisterSaveable(this);
            }
        }

        private void Update()
        {
            if (!_isMoving || _platformTransform == null) return;

            Vector3 targetPos = _isAtTopState ? _topLocalPosition : _bottomLocalPosition;
            _platformTransform.localPosition = Vector3.MoveTowards(
                _platformTransform.localPosition,
                targetPos,
                _travelSpeed * Time.deltaTime);

            if ((_platformTransform.localPosition - targetPos).sqrMagnitude < 0.0001f)
            {
                _platformTransform.localPosition = targetPos;
                _isMoving = false;
                EventBus<OnGameSaveRequestedEvent>.Publish(new OnGameSaveRequestedEvent { Reason = "ElevatorArrived" });
            }
        }

        public bool CanInteract(GameObject interactor) => !_isMoving;

        public void Interact(GameObject interactor)
        {
            if (_isMoving) return;
            _isAtTopState = !_isAtTopState;
            _isMoving = true;
        }

        public bool CaptureState() => _isAtTopState;

        public void RestoreState(bool isAtTop)
        {
            _isAtTopState = isAtTop;
            _isMoving = false;
            if (_platformTransform != null)
            {
                _platformTransform.localPosition = _isAtTopState ? _topLocalPosition : _bottomLocalPosition;
            }
        }
    }
}
