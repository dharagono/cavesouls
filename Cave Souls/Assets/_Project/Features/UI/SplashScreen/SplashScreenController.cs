using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Zero-Allocation Splash Screen Sequence Controller.
    /// Sequence:
    /// 1. Made with Unity Logo (Fade In -> Hold -> Fade Out)
    /// 2. Beyond Game Studios Logo (Fade In -> Hold -> Fade Out)
    /// 3. Asynchronous transition to 01_MainMenu.
    /// Driven purely by Time.unscaledDeltaTime in Update without Coroutine allocations.
    /// </summary>
    public sealed class SplashScreenController : MonoBehaviour
    {
        private enum SplashStep : byte
        {
            InitialDelay = 0,
            UnityFadeIn = 1,
            UnityHold = 2,
            UnityFadeOut = 3,
            IntermissionDelay = 4,
            StudioFadeIn = 5,
            StudioHold = 6,
            StudioFadeOut = 7,
            LoadingNextScene = 8
        }

        [Header("Canvas Groups & Transforms")]
        [SerializeField] private CanvasGroup _unityLogoGroup;
        [SerializeField] private RectTransform _unityLogoRect;
        [SerializeField] private CanvasGroup _studioLogoGroup;
        [SerializeField] private RectTransform _studioLogoRect;

        [Header("Timing Configuration (Seconds)")]
        [SerializeField] private float _initialDelay = 0.4f;
        [SerializeField] private float _unityFadeInDuration = 1.0f;
        [SerializeField] private float _unityHoldDuration = 1.8f;
        [SerializeField] private float _unityFadeOutDuration = 1.0f;
        [SerializeField] private float _intermissionDelay = 0.4f;
        [SerializeField] private float _studioFadeInDuration = 1.2f;
        [SerializeField] private float _studioHoldDuration = 2.2f;
        [SerializeField] private float _studioFadeOutDuration = 1.0f;

        [Header("Navigation")]
        [SerializeField] private string _nextSceneName = "01_MainMenu";
        [SerializeField] private bool _allowSkipInput = true;

        private SplashStep _currentStep = SplashStep.InitialDelay;
        private float _stepTimer;
        private float _totalElapsed;
        private float _unityScaleProgress;
        private float _studioScaleProgress;
        private bool _asyncLoadStarted;

        private void Awake()
        {
            SetGroupState(_unityLogoGroup, 0f);
            SetGroupState(_studioLogoGroup, 0f);

            if (_unityLogoRect != null) _unityLogoRect.localScale = new Vector3(0.95f, 0.95f, 1f);
            if (_studioLogoRect != null) _studioLogoRect.localScale = new Vector3(0.95f, 0.95f, 1f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _stepTimer += dt;
            _totalElapsed += dt;

            if (_allowSkipInput && _totalElapsed > 0.75f && WasSkipPressedThisFrame())
            {
                AdvanceOnSkip();
                return;
            }

            switch (_currentStep)
            {
                case SplashStep.InitialDelay:
                    if (_stepTimer >= _initialDelay) ChangeStep(SplashStep.UnityFadeIn);
                    break;

                case SplashStep.UnityFadeIn:
                    UpdateUnityScale(dt, _unityFadeInDuration + _unityHoldDuration + _unityFadeOutDuration);
                    SetGroupState(_unityLogoGroup, Mathf.Clamp01(_stepTimer / _unityFadeInDuration));
                    if (_stepTimer >= _unityFadeInDuration) ChangeStep(SplashStep.UnityHold);
                    break;

                case SplashStep.UnityHold:
                    UpdateUnityScale(dt, _unityFadeInDuration + _unityHoldDuration + _unityFadeOutDuration);
                    SetGroupState(_unityLogoGroup, 1f);
                    if (_stepTimer >= _unityHoldDuration) ChangeStep(SplashStep.UnityFadeOut);
                    break;

                case SplashStep.UnityFadeOut:
                    UpdateUnityScale(dt, _unityFadeInDuration + _unityHoldDuration + _unityFadeOutDuration);
                    SetGroupState(_unityLogoGroup, 1f - Mathf.Clamp01(_stepTimer / _unityFadeOutDuration));
                    if (_stepTimer >= _unityFadeOutDuration)
                    {
                        SetGroupState(_unityLogoGroup, 0f);
                        ChangeStep(SplashStep.IntermissionDelay);
                    }
                    break;

                case SplashStep.IntermissionDelay:
                    if (_stepTimer >= _intermissionDelay) ChangeStep(SplashStep.StudioFadeIn);
                    break;

                case SplashStep.StudioFadeIn:
                    UpdateStudioScale(dt, _studioFadeInDuration + _studioHoldDuration + _studioFadeOutDuration);
                    SetGroupState(_studioLogoGroup, Mathf.Clamp01(_stepTimer / _studioFadeInDuration));
                    if (_stepTimer >= _studioFadeInDuration) ChangeStep(SplashStep.StudioHold);
                    break;

                case SplashStep.StudioHold:
                    UpdateStudioScale(dt, _studioFadeInDuration + _studioHoldDuration + _studioFadeOutDuration);
                    SetGroupState(_studioLogoGroup, 1f);
                    if (_stepTimer >= _studioHoldDuration) ChangeStep(SplashStep.StudioFadeOut);
                    break;

                case SplashStep.StudioFadeOut:
                    UpdateStudioScale(dt, _studioFadeInDuration + _studioHoldDuration + _studioFadeOutDuration);
                    SetGroupState(_studioLogoGroup, 1f - Mathf.Clamp01(_stepTimer / _studioFadeOutDuration));
                    if (_stepTimer >= _studioFadeOutDuration)
                    {
                        SetGroupState(_studioLogoGroup, 0f);
                        ChangeStep(SplashStep.LoadingNextScene);
                    }
                    break;

                case SplashStep.LoadingNextScene:
                    if (!_asyncLoadStarted)
                    {
                        _asyncLoadStarted = true;
                        LoadNextSceneSafe();
                    }
                    break;
            }
        }

        private void UpdateUnityScale(float dt, float totalDuration)
        {
            if (_unityLogoRect == null || totalDuration <= 0.01f) return;
            _unityScaleProgress = Mathf.Clamp01(_unityScaleProgress + dt / totalDuration);
            float s = Mathf.Lerp(0.95f, 1.02f, _unityScaleProgress);
            _unityLogoRect.localScale = new Vector3(s, s, 1f);
        }

        private void UpdateStudioScale(float dt, float totalDuration)
        {
            if (_studioLogoRect == null || totalDuration <= 0.01f) return;
            _studioScaleProgress = Mathf.Clamp01(_studioScaleProgress + dt / totalDuration);
            float s = Mathf.Lerp(0.95f, 1.02f, _studioScaleProgress);
            _studioLogoRect.localScale = new Vector3(s, s, 1f);
        }

        private void AdvanceOnSkip()
        {
            if (_currentStep <= SplashStep.UnityFadeOut)
            {
                SetGroupState(_unityLogoGroup, 0f);
                ChangeStep(SplashStep.StudioFadeIn);
            }
            else if (_currentStep < SplashStep.LoadingNextScene)
            {
                SetGroupState(_studioLogoGroup, 0f);
                ChangeStep(SplashStep.LoadingNextScene);
            }
        }

        private void ChangeStep(SplashStep next)
        {
            _currentStep = next;
            _stepTimer = 0f;
        }

        private static void SetGroupState(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
            }
        }

        private static bool WasSkipPressedThisFrame()
        {
            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null &&
                (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 Gamepad.current.startButton.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        private void LoadNextSceneSafe()
        {
            if (Application.CanStreamedLevelBeLoaded(_nextSceneName))
            {
                SceneManager.LoadSceneAsync(_nextSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadSceneAsync(1);
            }
        }
    }
}
