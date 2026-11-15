using System.IO;
using CaveSouls.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Zero-Allocation Souls-like Main Menu Controller.
    /// Manages the "PRESS ANY BUTTON" title state, the main menu button list (CONTINUE, NEW GAME, SETTINGS, EXIT),
    /// English / Spanish language selection in Settings, looping Main Menu OST fade-in/out,
    /// save file detection, and transitions to 02_IntroCinematic (New Game) or SampleScene (Continue).
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuState : byte
        {
            FadeInTitle = 0,
            WaitingForAnyButton = 1,
            MainButtonsActive = 2,
            SettingsPanelActive = 3,
            TransitioningToGame = 4
        }

        [Header("UI Canvas Groups")]
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
        [SerializeField] private Text[] _buttonLabels;
        [SerializeField] private Text _pressAnyButtonLabel;

        [Header("Settings & Localization UI")]
        [SerializeField] private Button _languageToggleButton;
        [SerializeField] private Text _languageButtonLabel;
        [SerializeField] private Text _settingsHeaderLabel;
        [SerializeField] private Text _backButtonLabel;

        [Header("Audio (Main Menu Looping OST)")]
        [SerializeField] private AudioSource _bgmAudioSource;
        [SerializeField] private float _targetBgmVolume = 0.80f;

        [Header("Scene Navigation")]
        [SerializeField] private string _introCinematicSceneName = "02_IntroCinematic";
        [SerializeField] private string _gameplaySceneName = "SampleScene";
        [SerializeField] private string _saveFileName = "cavesouls_save01.dat";

        private MenuState _state = MenuState.FadeInTitle;
        private float _stateTimer;
        private int _selectedIndex;
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

            if (_continueButton != null)
            {
                _continueButton.interactable = _hasSaveFile;
                _continueButton.onClick.AddListener(OnContinueClicked);
            }
            if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGameClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_exitButton != null) _exitButton.onClick.AddListener(OnExitClicked);
            if (_languageToggleButton != null) _languageToggleButton.onClick.AddListener(OnToggleLanguageClicked);

            _selectedIndex = _hasSaveFile ? 0 : 1;

            if (_bgmAudioSource != null)
            {
                _bgmAudioSource.loop = true;
                _bgmAudioSource.volume = 0f;
                if (!_bgmAudioSource.isPlaying && _bgmAudioSource.clip != null)
                {
                    _bgmAudioSource.Play();
                }
            }

            SetGroup(_screenBlackFadeGroup, 1f, false);
            SetGroup(_titleLogoGroup, 0f, false);
            SetGroup(_pressAnyButtonGroup, 0f, false);
            SetGroup(_menuButtonsGroup, 0f, false);
            SetGroup(_settingsPanelGroup, 0f, false);

            RefreshLocalizedTexts();
            RefreshButtonVisuals();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _stateTimer += dt;
            if (_navCooldownTimer > 0f) _navCooldownTimer -= dt;

            if (_bgmAudioSource != null && _state != MenuState.TransitioningToGame)
            {
                if (_bgmAudioSource.volume < _targetBgmVolume)
                {
                    _bgmAudioSource.volume = Mathf.MoveTowards(_bgmAudioSource.volume, _targetBgmVolume, dt * 0.45f);
                }
            }

            switch (_state)
            {
                case MenuState.FadeInTitle:
                    float fadeOutBlack = 1f - Mathf.Clamp01(_stateTimer / 1.4f);
                    float titleAlpha = Mathf.Clamp01((_stateTimer - 0.3f) / 1.4f);
                    SetGroup(_screenBlackFadeGroup, fadeOutBlack, false);
                    SetGroup(_titleLogoGroup, titleAlpha, false);

                    if (_stateTimer >= 1.8f)
                    {
                        SetGroup(_screenBlackFadeGroup, 0f, false);
                        SetGroup(_titleLogoGroup, 1f, false);
                        _state = MenuState.WaitingForAnyButton;
                        _stateTimer = 0f;
                    }
                    break;

                case MenuState.WaitingForAnyButton:
                    float pulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(_stateTimer * 2.4f));
                    SetGroup(_pressAnyButtonGroup, pulse, false);

                    if (WasAnyInputPressedThisFrame())
                    {
                        SetGroup(_pressAnyButtonGroup, 0f, false);
                        SetGroup(_menuButtonsGroup, 1f, true);
                        _state = MenuState.MainButtonsActive;
                        _stateTimer = 0f;
                        RefreshButtonVisuals();
                    }
                    break;

                case MenuState.MainButtonsActive:
                    HandleKeyboardAndGamepadMenuNavigation();
                    break;

                case MenuState.SettingsPanelActive:
                    HandleSettingsPanelInput();
                    break;

                case MenuState.TransitioningToGame:
                    float blackAlpha = Mathf.Clamp01(_stateTimer / 1.1f);
                    SetGroup(_screenBlackFadeGroup, blackAlpha, true);
                    if (_bgmAudioSource != null)
                    {
                        _bgmAudioSource.volume = Mathf.Lerp(_targetBgmVolume, 0f, blackAlpha);
                    }
                    if (_stateTimer >= 1.15f && !_asyncLoadTriggered)
                    {
                        _asyncLoadTriggered = true;
                        LoadTargetScene();
                    }
                    break;
            }
        }

        public void OnContinueClicked()
        {
            if (!_hasSaveFile || _state == MenuState.TransitioningToGame) return;
            _isStartingNewGame = false;
            _state = MenuState.TransitioningToGame;
            _stateTimer = 0f;
        }

        public void OnNewGameClicked()
        {
            if (_state == MenuState.TransitioningToGame) return;
            _isStartingNewGame = true;
            _state = MenuState.TransitioningToGame;
            _stateTimer = 0f;
        }

        public void OnSettingsClicked()
        {
            if (_state != MenuState.MainButtonsActive) return;
            SetGroup(_menuButtonsGroup, 0f, false);
            SetGroup(_settingsPanelGroup, 1f, true);
            _state = MenuState.SettingsPanelActive;
            _stateTimer = 0f;
            RefreshLocalizedTexts();
        }

        public void OnToggleLanguageClicked()
        {
            GameLocalizationService.ToggleLanguage();
            RefreshLocalizedTexts();
        }

        public void OnCloseSettingsClicked()
        {
            SetGroup(_settingsPanelGroup, 0f, false);
            SetGroup(_menuButtonsGroup, 1f, true);
            _state = MenuState.MainButtonsActive;
            _stateTimer = 0f;
        }

        public void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleSettingsPanelInput()
        {
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

        private void HandleKeyboardAndGamepadMenuNavigation()
        {
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
                _selectedIndex += moveDir;
                if (_selectedIndex > maxIdx) _selectedIndex = minIdx;
                if (_selectedIndex < minIdx) _selectedIndex = maxIdx;
                RefreshButtonVisuals();
            }

            bool confirmPressed =
                (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (confirmPressed)
            {
                switch (_selectedIndex)
                {
                    case 0: OnContinueClicked(); break;
                    case 1: OnNewGameClicked(); break;
                    case 2: OnSettingsClicked(); break;
                    case 3: OnExitClicked(); break;
                }
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

        private void LoadTargetScene()
        {
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
