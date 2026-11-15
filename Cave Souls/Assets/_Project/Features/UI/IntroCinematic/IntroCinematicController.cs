using CaveSouls.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Zero-Allocation Souls-like Intro Cinematic Controller (1:33 total duration).
    /// Plays the 10 sequential concept art slides with strict 2.0s Fade-In and 2.0s Fade-Out transitions,
    /// plays "Weight_of_the_Seal.mp3" alongside the localized voice-over ("intro_vocals_english.mp3" or "intro_vocals_spanish.mp3"),
    /// and displays synchronized subtitles in the player's selected language.
    /// </summary>
    public sealed class IntroCinematicController : MonoBehaviour
    {
        [Header("Static Configuration")]
        [SerializeField] private IntroCinematicDataSO _cinematicData;
        [SerializeField] private string _nextGameplaySceneName = "SampleScene";

        [Header("UI References")]
        [SerializeField] private RawImage _slideRawImage;
        [SerializeField] private RectTransform _slideRect;
        [SerializeField] private CanvasGroup _slideCanvasGroup;
        [SerializeField] private CanvasGroup _subtitleGroup;
        [SerializeField] private Text _subtitleText;
        [SerializeField] private CanvasGroup _skipPromptGroup;
        [SerializeField] private Text _skipPromptText;
        [SerializeField] private CanvasGroup _finalBlackFadeGroup;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmAudioSource;
        [SerializeField] private AudioSource _vocalsAudioSource;

        private float _elapsedTime;
        private int _activeSlideIndex = -1;
        private int _activeSubtitleIndex = -1;
        private float _skipHoldTimer;
        private bool _isTransitioningOut;
        private float _outroFadeTimer;
        private bool _asyncLoadTriggered;

        private const float SkipHoldRequiredSeconds = 1.25f;
        private const float SubtitleFadeSeconds = 0.40f;

        private void Awake()
        {
            if (_slideCanvasGroup != null) _slideCanvasGroup.alpha = 0f;
            if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
            if (_skipPromptGroup != null) _skipPromptGroup.alpha = 0f;
            if (_finalBlackFadeGroup != null) _finalBlackFadeGroup.alpha = 0f;

            if (_skipPromptText != null)
            {
                _skipPromptText.text = GameLocalizationService.Select(
                    "HOLD [SPACE / ESC] TO SKIP",
                    "MANTÉN [ESPACIO / ESC] PARA OMITIR");
            }
        }

        private void Start()
        {
            if (_cinematicData == null) return;

            if (_bgmAudioSource != null && _cinematicData.BgmClip != null)
            {
                _bgmAudioSource.clip = _cinematicData.BgmClip;
                _bgmAudioSource.loop = false;
                _bgmAudioSource.volume = 0.78f;
                _bgmAudioSource.Play();
            }

            if (_vocalsAudioSource != null)
            {
                AudioClip voClip = (GameLocalizationService.CurrentLanguage == GameLanguage.Spanish)
                    ? _cinematicData.VocalsSpanishClip
                    : _cinematicData.VocalsEnglishClip;

                if (voClip != null)
                {
                    _vocalsAudioSource.clip = voClip;
                    _vocalsAudioSource.loop = false;
                    _vocalsAudioSource.volume = 1.0f;
                    _vocalsAudioSource.Play();
                }
            }

            UpdateSlideState(0f);
            UpdateSubtitleState(0f);
        }

        private void Update()
        {
            if (_cinematicData == null) return;

            float dt = Time.unscaledDeltaTime;

            if (_isTransitioningOut)
            {
                _outroFadeTimer += dt;
                float tOut = Mathf.Clamp01(_outroFadeTimer / 1.5f);
                if (_finalBlackFadeGroup != null) _finalBlackFadeGroup.alpha = tOut;
                if (_bgmAudioSource != null) _bgmAudioSource.volume = Mathf.Lerp(0.78f, 0f, tOut);
                if (_vocalsAudioSource != null) _vocalsAudioSource.volume = Mathf.Lerp(1.0f, 0f, tOut);

                if (_outroFadeTimer >= 1.55f && !_asyncLoadTriggered)
                {
                    _asyncLoadTriggered = true;
                    LoadGameplayScene();
                }
                return;
            }

            if (_bgmAudioSource != null && _bgmAudioSource.isPlaying)
            {
                float audioClock = _bgmAudioSource.time;
                if (audioClock > _elapsedTime || Mathf.Abs(audioClock - _elapsedTime) > 0.25f)
                {
                    _elapsedTime = audioClock;
                }
                else
                {
                    _elapsedTime += dt;
                }
            }
            else
            {
                _elapsedTime += dt;
            }

            UpdateSlideState(_elapsedTime);
            UpdateSubtitleState(_elapsedTime);
            HandleSkipInput(dt);

            if (_elapsedTime >= _cinematicData.TotalDuration)
            {
                _isTransitioningOut = true;
                _outroFadeTimer = 0f;
            }
        }

        private void UpdateSlideState(float time)
        {
            IntroSlideEntry[] slides = _cinematicData.Slides;
            if (slides == null || slides.Length == 0) return;

            int foundIdx = -1;
            for (int i = 0; i < slides.Length; i++)
            {
                if (time >= slides[i].StartTime && time < slides[i].EndTime)
                {
                    foundIdx = i;
                    break;
                }
            }

            if (foundIdx == -1)
            {
                if (_slideCanvasGroup != null) _slideCanvasGroup.alpha = 0f;
                return;
            }

            IntroSlideEntry slide = slides[foundIdx];
            if (foundIdx != _activeSlideIndex)
            {
                _activeSlideIndex = foundIdx;
                if (_slideRawImage != null)
                {
                    _slideRawImage.texture = slide.SlideTexture;
                }
            }

            float clipDuration = Mathf.Max(0.1f, slide.EndTime - slide.StartTime);
            float localTime = time - slide.StartTime;
            float remainingTime = slide.EndTime - time;

            float fadeInDur = Mathf.Max(0.01f, _cinematicData.SlideFadeInDuration);
            float fadeOutDur = Mathf.Max(0.01f, _cinematicData.SlideFadeOutDuration);

            // Strict 2.0s Fade-In and 2.0s Fade-Out per image transition
            float alpha = 1.0f;
            if (localTime < fadeInDur)
            {
                alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(localTime / fadeInDur));
            }
            else if (remainingTime < fadeOutDur)
            {
                alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remainingTime / fadeOutDur));
            }

            if (_slideCanvasGroup != null)
            {
                _slideCanvasGroup.alpha = alpha;
            }

            // Subtle Ken Burns slow camera push (1.00 -> 1.045) with zero allocations
            if (_slideRect != null)
            {
                float progress = Mathf.Clamp01(localTime / clipDuration);
                float scale = 1.0f + (0.045f * progress);
                _slideRect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void UpdateSubtitleState(float time)
        {
            IntroSubtitleCue[] cues = _cinematicData.SubtitleCues;
            if (cues == null || cues.Length == 0 || _subtitleGroup == null || _subtitleText == null) return;

            int foundCue = -1;
            for (int i = 0; i < cues.Length; i++)
            {
                if (time >= cues[i].StartTime && time <= cues[i].EndTime)
                {
                    foundCue = i;
                    break;
                }
            }

            if (foundCue == -1)
            {
                _subtitleGroup.alpha = Mathf.MoveTowards(_subtitleGroup.alpha, 0f, Time.unscaledDeltaTime * 3.5f);
                return;
            }

            IntroSubtitleCue cue = cues[foundCue];
            if (foundCue != _activeSubtitleIndex)
            {
                _activeSubtitleIndex = foundCue;
                _subtitleText.text = (GameLocalizationService.CurrentLanguage == GameLanguage.Spanish)
                    ? cue.TextSpanish
                    : cue.TextEnglish;
            }

            float localCueTime = time - cue.StartTime;
            float remCueTime = cue.EndTime - time;
            float cueAlpha = 1.0f;

            if (localCueTime < SubtitleFadeSeconds)
            {
                cueAlpha = Mathf.Clamp01(localCueTime / SubtitleFadeSeconds);
            }
            else if (remCueTime < SubtitleFadeSeconds)
            {
                cueAlpha = Mathf.Clamp01(remCueTime / SubtitleFadeSeconds);
            }

            _subtitleGroup.alpha = cueAlpha;
        }

        private void HandleSkipInput(float dt)
        {
            bool skipHeld = false;
            if (Keyboard.current != null)
            {
                skipHeld = Keyboard.current.escapeKey.isPressed || Keyboard.current.spaceKey.isPressed || Keyboard.current.enterKey.isPressed;
            }
            if (!skipHeld && Gamepad.current != null)
            {
                skipHeld = Gamepad.current.buttonSouth.isPressed || Gamepad.current.startButton.isPressed;
            }

            if (skipHeld)
            {
                _skipHoldTimer += dt;
                if (_skipPromptGroup != null)
                {
                    _skipPromptGroup.alpha = Mathf.MoveTowards(_skipPromptGroup.alpha, 0.9f, dt * 3f);
                }

                if (_skipHoldTimer >= SkipHoldRequiredSeconds)
                {
                    _isTransitioningOut = true;
                    _outroFadeTimer = 0f;
                }
            }
            else
            {
                _skipHoldTimer = Mathf.Max(0f, _skipHoldTimer - dt * 2f);
                if (_skipPromptGroup != null)
                {
                    _skipPromptGroup.alpha = Mathf.MoveTowards(_skipPromptGroup.alpha, 0f, dt * 2f);
                }
            }
        }

        private void LoadGameplayScene()
        {
            if (Application.CanStreamedLevelBeLoaded(_nextGameplaySceneName))
            {
                SceneManager.LoadSceneAsync(_nextGameplaySceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 3)
            {
                SceneManager.LoadSceneAsync(3);
            }
        }
    }
}
