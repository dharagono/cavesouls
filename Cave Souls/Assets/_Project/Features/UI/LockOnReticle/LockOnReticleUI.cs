using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Screen-space Lock-On Reticle tracking the active target in LateUpdate.
    /// Zero GC allocations.
    /// </summary>
    public sealed class LockOnReticleUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _reticleRectTransform;
        [SerializeField] private GameObject _reticleVisualRoot;
        [SerializeField] private Camera _uiCamera;

        private Transform _lockedTargetPivot;
        private bool _isLockedOn;

        private void Awake()
        {
            if (_uiCamera == null) _uiCamera = Camera.main;
            if (_reticleVisualRoot != null) _reticleVisualRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<OnLockOnTargetChangedEvent>.Subscribe(HandleLockOnTargetChanged);
        }

        private void OnDisable()
        {
            EventBus<OnLockOnTargetChangedEvent>.Unsubscribe(HandleLockOnTargetChanged);
        }

        private void LateUpdate()
        {
            if (!_isLockedOn || _lockedTargetPivot == null || _uiCamera == null || _reticleRectTransform == null)
            {
                if (_reticleVisualRoot != null && _reticleVisualRoot.activeSelf)
                {
                    _reticleVisualRoot.SetActive(false);
                }
                return;
            }

            Vector3 screenPos = _uiCamera.WorldToScreenPoint(_lockedTargetPivot.position);
            bool inFrontOfCamera = screenPos.z > 0f;

            if (_reticleVisualRoot != null && _reticleVisualRoot.activeSelf != inFrontOfCamera)
            {
                _reticleVisualRoot.SetActive(inFrontOfCamera);
            }

            if (inFrontOfCamera)
            {
                _reticleRectTransform.position = screenPos;
            }
        }

        private void HandleLockOnTargetChanged(in OnLockOnTargetChangedEvent evt)
        {
            _isLockedOn = evt.IsLockedOn && evt.NewTarget != null;
            _lockedTargetPivot = evt.NewTarget;

            if (_reticleVisualRoot != null)
            {
                _reticleVisualRoot.SetActive(_isLockedOn);
            }
        }
    }
}
