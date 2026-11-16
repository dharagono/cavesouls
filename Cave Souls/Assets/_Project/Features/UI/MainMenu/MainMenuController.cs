using System.IO;
using CaveSouls.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Zero-Allocation Souls-like Main Menu & Pre-Menu Splash Controller.
    /// Sequence:
    /// 1. Pre-Menu Splash Sequence ("Made with Unity" -> transition to "Beyond Game Studios" black logo on white backdrop).
    /// 2. Fade-In to CAVE SOULS Title & 3D Cavern Diorama with looping "Beneath_the_Sealed_Sky.ogg" OST.
    /// 3. Interactive Menu (CONTINUE, NEW GAME, SETTINGS with EN/ES toggle, EXIT GAME) supporting Mouse, Keyboard, and Gamepad.
    /// 4. Clicking NEW GAME transitions directly into the 1:33 (93.3s) Intro Cinematic.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuState : byte
        {
            SplashUnityFadeIn = 0,
            SplashUnityHold = 1,
            SplashUnityFadeOut = 2,
            SplashStudioFadeIn = 3,
            SplashStudioHold = 4,
            SplashStudioFadeOut = 5,
            FadeInTitle = 6,
            WaitingForAnyButton = 7,
            MainButtonsActive = 8,
            SettingsPanelActive = 9,
            TransitioningToGame = 10,
            PlayingIntroCinematic = 11
        }

        [Header("Pre-Menu Splash Screen UI")]
        [SerializeField] private CanvasGroup _splashRootGroup;
        [SerializeField] private CanvasGroup _splashUnityGroup;
        [SerializeField] private RectTransform _splashUnityRect;
        [SerializeField] private CanvasGroup _splashStudioGroup;
        [SerializeField] private RectTransform _splashStudioRect;

        [Header("Main Menu UI Canvas Groups")]
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private Camera _uiCamera;
        [SerializeField] private CanvasGroup _screenBlackFadeGroup;
        [SerializeField] private CanvasGroup _titleLogoGroup;
        [SerializeField] private CanvasGroup _pressAnyButtonGroup;
        [SerializeField] private CanvasGroup _menuButtonsGroup;
        [SerializeField] private CanvasGroup _settingsPanelGroup;

        [Header("Buttons & Selection Highlights")]
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private RectTransform[] _buttonRects;
        [SerializeField] private Text[] _buttonLabels;
        [SerializeField] private Text _pressAnyButtonLabel;

        [Header("Settings & Localization UI")]
        [SerializeField] private Button _languageToggleButton;
        [SerializeField] private RectTransform _languageButtonRect;
        [SerializeField] private Text _languageButtonLabel;
        [SerializeField] private Button _statsToggleButton;
        [SerializeField] private RectTransform _statsButtonRect;
        [SerializeField] private Text _statsButtonLabel;
        [SerializeField] private Text _settingsHeaderLabel;
        [SerializeField] private Button _closeSettingsButton;
        [SerializeField] private RectTransform _closeSettingsRect;
        [SerializeField] private Text _backButtonLabel;

        [Header("Audio (Main Menu Looping OST)")]
        [SerializeField] private AudioSource _bgmAudioSource;
        [SerializeField] private float _targetBgmVolume = 0.80f;

        [Header("Audio (UI SFX)")]
        [SerializeField] private AudioSource _sfxAudioSource;
        [SerializeField] private AudioClip _hoverClip;
        [SerializeField] private AudioClip _confirmClip;

        [Header("Intro Cinematic & Scene Navigation")]
        [SerializeField] private IntroCinematicController _embeddedIntroCinematic;
        [SerializeField] private string _introCinematicSceneName = "02_IntroCinematic";
        [SerializeField] private string _gameplaySceneName = "01_MainMenu";
        [SerializeField] private string _saveFileName = "cavesouls_save01.dat";

        private MenuState _state;
        private float _stateTimer;
        private int _selectedIndex;
        private int _settingsHoverIndex = -1;
        private bool _hasSaveFile;
        private bool _isStartingNewGame;
        private bool _asyncLoadTriggered;
        private float _navCooldownTimer;

        private static readonly Color SelectedTextColor = new Color(1.0f, 0.95f, 0.82f, 1.0f);
        private static readonly Color UnselectedTextColor = new Color(0.68f, 0.68f, 0.72f, 0.85f);
        private static readonly Color DisabledTextColor = new Color(0.32f, 0.32f, 0.35f, 0.45f);

        private static readonly string[] EnglishButtonTitles =
        {
            "C O N T I N U E",
            "N E W   G A M E",
            "S E T T I N G S",
            "E X I T   G A M E"
        };

        private static readonly string[] SpanishButtonTitles =
        {
            "C O N T I N U A R",
            "N U E V O   J U E G O",
            "O P C I O N E S",
            "S A L I R   D E L   J U E G O"
        };

        private void Awake()
        {
            string savePath = Path.Combine(Application.persistentDataPath, _saveFileName);
            _hasSaveFile = File.Exists(savePath);

            if (_mainCanvas == null) TryGetComponent(out _mainCanvas);
            if (_uiCamera == null && _mainCanvas != null) _uiCamera = _mainCanvas.worldCamera;

            if (_continueButton != null)
            {
                _continueButton.interactable = _hasSaveFile;
                _continueButton.onClick.AddListener(OnContinueClicked);
            }
            if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGameClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_exitButton != null) _exitButton.onClick.AddListener(OnExitClicked);
            if (_languageToggleButton != null) _languageToggleButton.onClick.AddListener(OnToggleLanguageClicked);
            if (_closeSettingsButton != null) _closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);

            _selectedIndex = _hasSaveFile ? 0 : 1;

            SetGroup(_titleLogoGroup, 0f, false);
            SetGroup(_pressAnyButtonGroup, 0f, false);
            SetGroup(_menuButtonsGroup, 0f, false);
            SetGroup(_settingsPanelGroup, 0f, false);

            bool showPreMenuSplash = (_splashRootGroup != null) && !SplashScreenController.HasCompletedSplashThisSession;
            if (showPreMenuSplash)
            {
                _state = MenuState.SplashUnityFadeIn;
                SetGroup(_splashRootGroup, 1f, true);
                SetGroup(_splashUnityGroup, 0f, false);
                SetGroup(_splashStudioGroup, 0f, false);
                SetGroup(_screenBlackFadeGroup, 1f, false);
                if (_bgmAudioSource != null)
                {
                    _bgmAudioSource.loop = true;
                    _bgmAudioSource.volume = 0f;
                    _bgmAudioSource.Stop();
                }
            }
            else
            {
                _state = MenuState.FadeInTitle;
                SetGroup(_splashRootGroup, 0f, false);
                SetGroup(_screenBlackFadeGroup, 1f, false);
                StartMainMenuBgm();
            }

            RefreshLocalizedTexts();
            RefreshButtonVisuals();
        }

        public void PlayHoverSound()
        {
            if (_sfxAudioSource != null && _hoverClip != null)
            {
                _sfxAudioSource.PlayOneShot(_hoverClip, 0.95f);
            }
        }

        public void PlayConfirmSound()
        {
            if (_sfxAudioSource != null && _confirmClip != null)
            {
                _sfxAudioSource.PlayOneShot(_confirmClip, 1.0f);
            }
        }

        private void StartMainMenuBgm()
        {
            if (_bgmAudioSource != null)
            {
                _bgmAudioSource.loop = true;
                _bgmAudioSource.volume = 0f;
                if (!_bgmAudioSource.isPlaying && _bgmAudioSource.clip != null)
                {
                    _bgmAudioSource.Play();
                }
            }
        }

        private void Update()
        {
            if (_state == MenuState.PlayingIntroCinematic) return;

            float dt = Time.unscaledDeltaTime;
            _stateTimer += dt;
            if (_navCooldownTimer > 0f) _navCooldownTimer -= dt;

            // Fade in Main Menu BGM once past the Splash Screen sequence
            if (_state >= MenuState.FadeInTitle && _state < MenuState.TransitioningToGame && _bgmAudioSource != null)
            {
                if (_bgmAudioSource.volume < _targetBgmVolume)
                {
                    _bgmAudioSource.volume = Mathf.MoveTowards(_bgmAudioSource.volume, _targetBgmVolume, dt * 0.45f);
                }
            }

            switch (_state)
            {
                // --- STEP 1: MADE WITH UNITY SPLASH ---
                case MenuState.SplashUnityFadeIn:
                    SetGroup(_splashUnityGroup, Mathf.Clamp01(_stateTimer / 0.9f), false);
                    UpdateSplashScale(_splashUnityRect, 0.95f, 0.98f, _stateTimer / 0.9f);
                    if (_stateTimer >= 0.9f || (_stateTimer > 0.3f && WasAnyInputPressedThisFrame()))
                    {
                        SetGroup(_splashUnityGroup, 1f, false);
                        ChangeState(MenuState.SplashUnityHold);
                    }
                    break;

                case MenuState.SplashUnityHold:
                    UpdateSplashScale(_splashUnityRect, 0.98f, 1.01f, _stateTimer / 1.5f);
                    if (_stateTimer >= 1.5f || WasAnyInputPressedThisFrame())
                    {
                        ChangeState(MenuState.SplashUnityFadeOut);
                    }
                    break;

                case MenuState.SplashUnityFadeOut:
                    SetGroup(_splashUnityGroup, 1f - Mathf.Clamp01(_stateTimer / 0.8f), false);
                    if (_stateTimer >= 0.8f)
                    {
                        SetGroup(_splashUnityGroup, 0f, false);
                        ChangeState(MenuState.SplashStudioFadeIn);
                    }
                    break;

                // --- STEP 2: BEYOND GAME STUDIOS LOGO SPLASH ---
                case MenuState.SplashStudioFadeIn:
                    SetGroup(_splashStudioGroup, Mathf.Clamp01(_stateTimer / 1.0f), false);
                    UpdateSplashScale(_splashStudioRect, 0.95f, 0.99f, _stateTimer / 1.0f);
                    if (_stateTimer >= 1.0f || (_stateTimer > 0.3f && WasAnyInputPressedThisFrame()))
                    {
                        SetGroup(_splashStudioGroup, 1f, false);
                        ChangeState(MenuState.SplashStudioHold);
                    }
                    break;

                case MenuState.SplashStudioHold:
                    UpdateSplashScale(_splashStudioRect, 0.99f, 1.02f, _stateTimer / 1.8f);
                    if (_stateTimer >= 1.8f || WasAnyInputPressedThisFrame())
                    {
                        ChangeState(MenuState.SplashStudioFadeOut);
                    }
                    break;

                case MenuState.SplashStudioFadeOut:
                    float studioOut = 1f - Mathf.Clamp01(_stateTimer / 0.9f);
                    SetGroup(_splashStudioGroup, studioOut, false);
                    if (_stateTimer >= 0.9f)
                    {
                        SetGroup(_splashStudioGroup, 0f, false);
                        SetGroup(_splashRootGroup, 0f, false);
                        SplashScreenController.HasCompletedSplashThisSession = true;
                        StartMainMenuBgm();
                        ChangeState(MenuState.FadeInTitle);
                    }
                    break;

                // --- STEP 3: CAVE SOULS MAIN MENU ---
                case MenuState.FadeInTitle:
                    float fadeOutBlack = 1f - Mathf.Clamp01(_stateTimer / 1.4f);
                    float titleAlpha = Mathf.Clamp01((_stateTimer - 0.3f) / 1.4f);
                    SetGroup(_screenBlackFadeGroup, fadeOutBlack, false);
                    SetGroup(_titleLogoGroup, titleAlpha, false);

                    if (_stateTimer >= 1.8f)
                    {
                        SetGroup(_screenBlackFadeGroup, 0f, false);
                        SetGroup(_titleLogoGroup, 1f, false);
                        ChangeState(MenuState.WaitingForAnyButton);
                    }
                    break;

                case MenuState.WaitingForAnyButton:
                    float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(_stateTimer * 2.4f));
                    SetGroup(_pressAnyButtonGroup, pulse, false);

                    if (WasAnyInputPressedThisFrame())
                    {
                        PlayConfirmSound();
                        SetGroup(_pressAnyButtonGroup, 0f, false);
                        SetGroup(_menuButtonsGroup, 1f, true);
                        ChangeState(MenuState.MainButtonsActive);
                        RefreshButtonVisuals();
                    }
                    break;

                case MenuState.MainButtonsActive:
                    HandleMouseKeyboardAndGamepadMenuNavigation();
                    break;

                case MenuState.SettingsPanelActive:
                    HandleSettingsPanelInput();
                    break;

                case MenuState.TransitioningToGame:
                    float t = Mathf.Clamp01(_stateTimer / 2.0f);
                    float blackAlpha = Mathf.SmoothStep(0f, 1f, t);
                    SetGroup(_screenBlackFadeGroup, blackAlpha, true);
                    SetGroup(_titleLogoGroup, 1f - blackAlpha, false);
                    SetGroup(_menuButtonsGroup, 1f - blackAlpha, false);
                    if (_bgmAudioSource != null)
                    {
                        _bgmAudioSource.volume = Mathf.Lerp(_targetBgmVolume, 0f, blackAlpha);
                    }
                    if (_stateTimer >= 2.0f && !_asyncLoadTriggered)
                    {
                        _asyncLoadTriggered = true;
                        if (_bgmAudioSource != null) _bgmAudioSource.Stop();
                        TriggerNewGameOrContinue();
                    }
                    break;
            }
        }

        private void ChangeState(MenuState nextState)
        {
            _state = nextState;
            _stateTimer = 0f;
        }

        private static void UpdateSplashScale(RectTransform rt, float fromScale, float toScale, float t)
        {
            if (rt == null) return;
            float s = Mathf.Lerp(fromScale, toScale, Mathf.Clamp01(t));
            rt.localScale = new Vector3(s, s, 1f);
        }

        public void OnContinueClicked()
        {
            if (!_hasSaveFile || _state == MenuState.TransitioningToGame || _state == MenuState.PlayingIntroCinematic) return;
            PlayConfirmSound();
            _isStartingNewGame = false;
            if (_menuButtonsGroup != null)
            {
                _menuButtonsGroup.interactable = false;
                _menuButtonsGroup.blocksRaycasts = false;
            }
            ChangeState(MenuState.TransitioningToGame);
        }

        public void OnNewGameClicked()
        {
            if (_state == MenuState.TransitioningToGame || _state == MenuState.PlayingIntroCinematic) return;
            PlayConfirmSound();
            _isStartingNewGame = true;
            if (_menuButtonsGroup != null)
            {
                _menuButtonsGroup.interactable = false;
                _menuButtonsGroup.blocksRaycasts = false;
            }
            ChangeState(MenuState.TransitioningToGame);
        }

        public void OnSettingsClicked()
        {
            if (_state != MenuState.MainButtonsActive) return;
            PlayConfirmSound();
            SetGroup(_menuButtonsGroup, 0f, false);
            SetGroup(_settingsPanelGroup, 1f, true);
            ChangeState(MenuState.SettingsPanelActive);
            RefreshLocalizedTexts();
        }

        public void OnToggleLanguageClicked()
        {
            PlayConfirmSound();
            GameLocalizationService.ToggleLanguage();
            RefreshLocalizedTexts();
        }

        public void OnTogglePerformanceStatsClicked()
        {
            PlayConfirmSound();
            PerformanceStatsOverlay.ToggleOverlay();
            RefreshLocalizedTexts();
        }

        public void OnCloseSettingsClicked()
        {
            PlayConfirmSound();
            SetGroup(_settingsPanelGroup, 0f, false);
            SetGroup(_menuButtonsGroup, 1f, true);
            ChangeState(MenuState.MainButtonsActive);
        }

        public void OnExitClicked()
        {
            PlayConfirmSound();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleSettingsPanelInput()
        {
            // Direct Mouse Hover & Click Detection on Settings Panel Buttons (Zero-GC)
            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                int currentHover = -1;
                if (_languageButtonRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_languageButtonRect, mousePos, _uiCamera))
                {
                    currentHover = 0;
                }
                else if (_statsButtonRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_statsButtonRect, mousePos, _uiCamera))
                {
                    currentHover = 1;
                }
                else if (_closeSettingsRect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(_closeSettingsRect, mousePos, _uiCamera))
                {
                    currentHover = 2;
                }

                if (currentHover != -1 && currentHover != _settingsHoverIndex)
                {
                    _settingsHoverIndex = currentHover;
                    PlayHoverSound();
                }
                else if (currentHover == -1)
                {
                    _settingsHoverIndex = -1;
                }

                if (Mouse.current.leftButton.wasPressedThisFrame && _stateTimer > 0.1f)
                {
                    if (currentHover == 0)
                    {
                        OnToggleLanguageClicked();
                        return;
                    }
                    if (currentHover == 1)
                    {
                        OnTogglePerformanceStatsClicked();
                        return;
                    }
                    if (currentHover == 2)
                    {
                        OnCloseSettingsClicked();
                        return;
                    }
                }
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    OnCloseSettingsClicked();
                    return;
                }

                if (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                    Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                    Keyboard.current.aKey.wasPressedThisFrame ||
                    Keyboard.current.dKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    OnToggleLanguageClicked();
                }
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    OnCloseSettingsClicked();
                    return;
                }

                if (Gamepad.current.dpad.left.wasPressedThisFrame ||
                    Gamepad.current.dpad.right.wasPressedThisFrame ||
                    Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    OnToggleLanguageClicked();
                }
            }
        }

        private void HandleMouseKeyboardAndGamepadMenuNavigation()
        {
            // 1. Direct Mouse Hover & Left-Click Detection (Zero-GC, works regardless of EventSystem focus)
            if (Mouse.current != null && _buttonRects != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                for (int i = 0; i < _buttonRects.Length; i++)
                {
                    if (_buttonRects[i] == null) continue;
                    if (i == 0 && !_hasSaveFile) continue;

                    if (RectTransformUtility.RectangleContainsScreenPoint(_buttonRects[i], mousePos, _uiCamera))
                    {
                        if (_selectedIndex != i)
                        {
                            _selectedIndex = i;
                            RefreshButtonVisuals();
                            PlayHoverSound();
                        }

                        if (Mouse.current.leftButton.wasPressedThisFrame && _stateTimer > 0.1f)
                        {
                            ExecuteSelectedMenuOption();
                            return;
                        }
                        break;
                    }
                }
            }

            // 2. Keyboard & Gamepad Navigation
            int moveDir = 0;
            if (_navCooldownTimer <= 0f)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) moveDir = 1;
                    else if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) moveDir = -1;
                }

                if (Gamepad.current != null)
                {
                    if (Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.ReadValue().y < -0.6f) moveDir = 1;
                    else if (Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.ReadValue().y > 0.6f) moveDir = -1;
                }
            }

            if (moveDir != 0)
            {
                _navCooldownTimer = 0.18f;
                int minIdx = _hasSaveFile ? 0 : 1;
                int maxIdx = 3;
                int prev = _selectedIndex;
                _selectedIndex += moveDir;
                if (_selectedIndex > maxIdx) _selectedIndex = minIdx;
                if (_selectedIndex < minIdx) _selectedIndex = maxIdx;
                if (_selectedIndex != prev)
                {
                    PlayHoverSound();
                }
                RefreshButtonVisuals();
            }

            bool confirmPressed =
                (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (confirmPressed && _stateTimer > 0.1f)
            {
                ExecuteSelectedMenuOption();
            }
        }

        private void ExecuteSelectedMenuOption()
        {
            switch (_selectedIndex)
            {
                case 0: OnContinueClicked(); break;
                case 1: OnNewGameClicked(); break;
                case 2: OnSettingsClicked(); break;
                case 3: OnExitClicked(); break;
            }
        }

        private void RefreshLocalizedTexts()
        {
            bool isSpanish = GameLocalizationService.CurrentLanguage == GameLanguage.Spanish;
            string[] titles = isSpanish ? SpanishButtonTitles : EnglishButtonTitles;

            if (_buttonLabels != null)
            {
                int count = Mathf.Min(_buttonLabels.Length, titles.Length);
                for (int i = 0; i < count; i++)
                {
                    if (_buttonLabels[i] != null)
                    {
                        _buttonLabels[i].text = titles[i];
                    }
                }
            }

            if (_pressAnyButtonLabel != null)
            {
                _pressAnyButtonLabel.text = isSpanish
                    ? "-  P R E S I O N A   C U A L Q U I E R   B O T Ó N  -"
                    : "-  P R E S S   A N Y   B U T T O N  -";
            }

            if (_settingsHeaderLabel != null)
            {
                _settingsHeaderLabel.text = isSpanish
                    ? "O P C I O N E S   D E   J U E G O   Y   A U D I O"
                    : "G A M E   &   A U D I O   S E T T I N G S";
            }

            if (_languageButtonLabel != null)
            {
                _languageButtonLabel.text = isSpanish
                    ? "I D I O M A   ( L A N G U A G E ) :     <   E S P A Ñ O L   >"
                    : "L A N G U A G E   ( I D I O M A ) :     <   E N G L I S H   >";
            }

            if (_statsButtonLabel != null)
            {
                bool statsOn = PerformanceStatsOverlay.IsActive;
                if (isSpanish)
                {
                    _statsButtonLabel.text = statsOn
                        ? "E S T A D Í S T I C A S   ( F P S ) :     <   A C T I V A D O   >"
                        : "E S T A D Í S T I C A S   ( F P S ) :     <   D E S A C T I V A D O   >";
                }
                else
                {
                    _statsButtonLabel.text = statsOn
                        ? "P E R F O R M A N C E   S T A T S :     <   O N   >"
                        : "P E R F O R M A N C E   S T A T S :     <   O F F   >";
                }
            }

            if (_backButtonLabel != null)
            {
                _backButtonLabel.text = isSpanish
                    ? "V O L V E R   [ E S C ]"
                    : "B A C K   [ E S C ]";
            }
        }

        private void RefreshButtonVisuals()
        {
            if (_buttonLabels == null) return;
            for (int i = 0; i < _buttonLabels.Length; i++)
            {
                if (_buttonLabels[i] == null) continue;
                if (i == 0 && !_hasSaveFile)
                {
                    _buttonLabels[i].color = DisabledTextColor;
                }
                else
                {
                    _buttonLabels[i].color = (i == _selectedIndex) ? SelectedTextColor : UnselectedTextColor;
                }
            }
        }

        private static void SetGroup(CanvasGroup cg, float alpha, bool interactable)
        {
            if (cg == null) return;
            cg.alpha = alpha;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }

        private static bool WasAnyInputPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Gamepad.current != null &&
                (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 Gamepad.current.startButton.wasPressedThisFrame ||
                 Gamepad.current.buttonWest.wasPressedThisFrame)) return true;
            return false;
        }

        private void TriggerNewGameOrContinue()
        {
            if (_isStartingNewGame && _embeddedIntroCinematic != null)
            {
                SetGroup(_titleLogoGroup, 0f, false);
                SetGroup(_pressAnyButtonGroup, 0f, false);
                SetGroup(_menuButtonsGroup, 0f, false);
                SetGroup(_settingsPanelGroup, 0f, false);
                SetGroup(_screenBlackFadeGroup, 0f, false);
                ChangeState(MenuState.PlayingIntroCinematic);
                _embeddedIntroCinematic.PlayCinematic();
                return;
            }

            string targetScene = _isStartingNewGame ? _introCinematicSceneName : _gameplaySceneName;
            if (Application.CanStreamedLevelBeLoaded(targetScene))
            {
                SceneManager.LoadSceneAsync(targetScene);
            }
            else if (SceneManager.sceneCountInBuildSettings > 2)
            {
                SceneManager.LoadSceneAsync(2);
            }
        }
    }

    /// <summary>
    /// Subtle volumetric pulse and drift controller for the 3D God-Ray light shafts in the Main Menu.
    /// Uses MaterialPropertyBlock and pre-hashed ShaderIDs with zero GC allocations.
    /// </summary>
    public sealed class CavernGodRayPulseController : MonoBehaviour
    {
        private static readonly int BeamColorPropertyId = Shader.PropertyToID("_BeamColor");

        [SerializeField] private Renderer _godRayRenderer;
        [SerializeField] private Color _baseBeamColor = new Color(1.25f, 1.38f, 1.58f, 0.32f);
        [SerializeField] private float _pulseSpeed = 0.65f;
        [SerializeField] private float _driftAmplitude = 0.22f;

        private MaterialPropertyBlock _propBlock;
        private Vector3 _initialLocalPos;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _initialLocalPos = transform.localPosition;
            if (_godRayRenderer == null) TryGetComponent(out _godRayRenderer);
        }

        private void Update()
        {
            float t = Time.time * _pulseSpeed;
            float alphaFactor = 0.80f + 0.20f * Mathf.Sin(t);

            transform.localPosition = _initialLocalPos + new Vector3(Mathf.Sin(t * 0.5f) * _driftAmplitude, 0f, 0f);

            if (_godRayRenderer != null)
            {
                _godRayRenderer.GetPropertyBlock(_propBlock);
                Color c = _baseBeamColor;
                c.a *= alphaFactor;
                _propBlock.SetColor(BeamColorPropertyId, c);
                _propBlock.SetColor(ShaderIDs.BaseColor, c);
                _godRayRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
