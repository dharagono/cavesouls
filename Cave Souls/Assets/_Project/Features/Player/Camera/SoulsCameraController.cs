using CaveSouls.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Dynamic Cinemachine Camera Controller switching between FreeLook (Exploration)
    /// and TargetGroup / LockOn (Combat). Zero GC allocations.
    /// </summary>
    public sealed class SoulsCameraController : MonoBehaviour
    {
        [Header("Cinemachine Virtual Cameras")]
        [SerializeField] private CinemachineCamera _freeLookCamera;
        [SerializeField] private CinemachineCamera _lockOnCamera;
        [SerializeField] private CinemachineTargetGroup _lockOnTargetGroup;
        [SerializeField] private Transform _playerCameraFollowTarget;

        [Header("Priorities")]
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority = 5;

        private void OnEnable()
        {
            EventBus<OnLockOnTargetChangedEvent>.Subscribe(HandleLockOnTargetChanged);
        }

        private void OnDisable()
        {
            EventBus<OnLockOnTargetChangedEvent>.Unsubscribe(HandleLockOnTargetChanged);
        }

        private void HandleLockOnTargetChanged(in OnLockOnTargetChangedEvent evt)
        {
            if (evt.IsLockedOn && evt.NewTarget != null)
            {
                if (_lockOnTargetGroup != null && _lockOnTargetGroup.Targets.Count >= 2)
                {
                    var targets = _lockOnTargetGroup.Targets;
                    targets[0].Object = _playerCameraFollowTarget;
                    targets[1].Object = evt.NewTarget;
                }

                if (_lockOnCamera != null) _lockOnCamera.Priority = _activePriority;
                if (_freeLookCamera != null) _freeLookCamera.Priority = _inactivePriority;
            }
            else
            {
                if (_freeLookCamera != null) _freeLookCamera.Priority = _activePriority;
                if (_lockOnCamera != null) _lockOnCamera.Priority = _inactivePriority;
            }
        }
    }
}
