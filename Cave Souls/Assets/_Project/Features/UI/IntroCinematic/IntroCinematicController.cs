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
    /// Includes direct scene-serialized fallback references so it never stalls even if the ScriptableObject reference is unloaded.
    /// </summary>
    public sealed class IntroCinematicController : MonoBehaviour
    {
        [Header("Static Configuration")]
        [SerializeField] private IntroCinematicDataSO _cinematicData;
        [SerializeField] private string _nextGameplaySceneName = "03_PrimordialMudBed";
        [SerializeField] private bool _playOnStart = true;

        [Header("Direct Scene Asset References (Fail-Safe)")]
        [SerializeField] private Texture2D[] _directSlideTextures;
        [SerializeField] private AudioClip _directBgmClip;
        [SerializeField] private AudioClip _directVocalsEnglishClip;
        [SerializeField] private AudioClip _directVocalsSpanishClip;

        [Header("UI References")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;
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

        private bool _isPlaying;
        private float _elapsedTime;
        private int _activeSlideIndex = -1;
        private int _activeSubtitleIndex = -1;
        private float _skipHoldTimer;
        private bool _isTransitioningOut;
        private float _outroFadeTimer;
        private bool _asyncLoadTriggered;

        private const float TotalTimelineSeconds = 93.3f;
        private const float DefaultFadeSeconds = 2.0f;
        private const float SkipHoldRequiredSeconds = 1.25f;
        private const float SubtitleFadeSeconds = 0.35f;

        // Built-in acoustic-matched 10-slide windows across the 93.3s (1:33) timeline
        private static readonly float[] FallbackSlideStarts = { 0.0f, 10.5f, 20.5f, 30.8f, 38.6f, 46.5f, 56.8f, 66.6f, 74.5f, 83.6f };
        private static readonly float[] FallbackSlideEnds   = { 10.5f, 20.5f, 30.8f, 38.6f, 46.5f, 56.8f, 66.6f, 74.5f, 83.6f, 93.3f };

        private static readonly IntroSubtitleCue[] FallbackSubtitles =
        {
            // Clip 1 (0.0s - 10.5s)
            new IntroSubtitleCue { StartTime = 0.7f,  EndTime = 3.3f,  TextEnglish = "In the beginning...", TextSpanish = "En el principio..." },
            new IntroSubtitleCue { StartTime = 4.0f,  EndTime = 6.3f,  TextEnglish = "there was no weight.", TextSpanish = "no había peso." },
            new IntroSubtitleCue { StartTime = 6.8f,  EndTime = 10.1f, TextEnglish = "Only... the flow.", TextSpanish = "Solo... el flujo." },
            // Clip 2 (10.5s - 20.5s)
            new IntroSubtitleCue { StartTime = 11.2f, EndTime = 14.3f, TextEnglish = "But light breeds shadows...", TextSpanish = "Pero la luz engendra sombras..." },
            new IntroSubtitleCue { StartTime = 14.8f, EndTime = 16.5f, TextEnglish = "and fear...", TextSpanish = "y el miedo..." },
            new IntroSubtitleCue { StartTime = 16.8f, EndTime = 20.1f, TextEnglish = "...is the poison of rulers.", TextSpanish = "...es el veneno de los gobernantes." },
            // Clip 3 (20.5s - 30.8s)
            new IntroSubtitleCue { StartTime = 21.1f, EndTime = 27.1f, TextEnglish = "To protect his own domain... he decreed the end of the cycle...", TextSpanish = "Para proteger su propio dominio... decretó el fin del ciclo..." },
            new IntroSubtitleCue { StartTime = 27.5f, EndTime = 30.3f, TextEnglish = "...and sealed our sky.", TextSpanish = "...y selló nuestro cielo." },
            // Clip 4 (30.8s - 38.6s)
            new IntroSubtitleCue { StartTime = 31.5f, EndTime = 35.3f, TextEnglish = "He left behind his most loyal Sentinel...", TextSpanish = "Dejó atrás a su Centinela más leal..." },
            new IntroSubtitleCue { StartTime = 35.6f, EndTime = 38.3f, TextEnglish = "...to guard the heights.", TextSpanish = "...para custodiar las alturas." },
            // Clip 5 (38.6s - 46.5s)
            new IntroSubtitleCue { StartTime = 39.0f, EndTime = 42.1f, TextEnglish = "Trapped beneath the seal...", TextSpanish = "Atrapados bajo el sello..." },
            new IntroSubtitleCue { StartTime = 42.6f, EndTime = 46.1f, TextEnglish = "...we were stripped of birth.", TextSpanish = "...fuimos despojados del nacimiento." },
            // Clip 6 (46.5s - 56.8s)
            new IntroSubtitleCue { StartTime = 47.2f, EndTime = 49.6f, TextEnglish = "We grew dense.", TextSpanish = "Nos volvimos densos." },
            new IntroSubtitleCue { StartTime = 50.2f, EndTime = 56.4f, TextEnglish = "solidifying our essence into prisons of crystal... mud... and stone.", TextSpanish = "solidificando nuestra esencia en prisiones de cristal... lodo... y piedra." },
            // Clip 7 (56.8s - 66.6s)
            new IntroSubtitleCue { StartTime = 57.3f, EndTime = 59.3f, TextEnglish = "And yet...", TextSpanish = "Y sin embargo..." },
            new IntroSubtitleCue { StartTime = 59.7f, EndTime = 62.7f, TextEnglish = "in the lowest stratum...", TextSpanish = "en el estrato más profundo..." },
            new IntroSubtitleCue { StartTime = 63.0f, EndTime = 66.2f, TextEnglish = "...where despair reigns...", TextSpanish = "...donde reina la desesperación..." },
            // Clip 8 (66.6s - 74.5s)
            new IntroSubtitleCue { StartTime = 67.1f, EndTime = 71.5f, TextEnglish = "...an ancient spark refuses to fade.", TextSpanish = "...una chispa ancestral se niega a apagarse." },
            new IntroSubtitleCue { StartTime = 72.0f, EndTime = 74.1f, TextEnglish = "A Lumen.", TextSpanish = "Un Lumen." },
            // Clip 9 (74.5s - 83.6s)
            new IntroSubtitleCue { StartTime = 75.3f, EndTime = 77.0f, TextEnglish = "Rise.", TextSpanish = "Álzate." },
            new IntroSubtitleCue { StartTime = 77.3f, EndTime = 79.9f, TextEnglish = "Defy the Sentinel.", TextSpanish = "Desafía al Centinela." },
            new IntroSubtitleCue { StartTime = 80.3f, EndTime = 83.1f, TextEnglish = "Reclaim the heights.", TextSpanish = "Reclama las alturas." },
            // Clip 10 (83.6s - 93.3s)
            new IntroSubtitleCue { StartTime = 84.4f, EndTime = 86.4f, TextEnglish = "And discover...", TextSpanish = "Y descubre..." },
            new IntroSubtitleCue { StartTime = 86.7f, EndTime = 89.2f, TextEnglish = "what was stolen from us...", TextSpanish = "...lo que nos fue arrebatado..." },
            new IntroSubtitleCue { StartTime = 89.5f, EndTime = 92.6f, TextEnglish = "...beyond the sky of stone.", TextSpanish = "...más allá del cielo de piedra." }
        };

        private void Awake()
        {
            if (_slideCanvasGroup != null) _slideCanvasGroup.alpha = 0f;
            if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
            if (_skipPromptGroup != null) _skipPromptGroup.alpha = 0f;
            if (_finalBlackFadeGroup != null) _finalBlackFadeGroup.alpha = 0f;

            if (!_playOnStart && _rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
            }
        }

        private void Start()
        {
            if (_playOnStart)
            {
                PlayCinematic();
            }
        }

        public void PlayCinematic()
        {
            gameObject.SetActive(true);
            AudioListener.pause = false;
            if (AudioListener.volume <= 0.01f) AudioListener.volume = 1.0f;

            _isPlaying = true;
            _elapsedTime = 0f;
            _activeSlideIndex = -1;
            _activeSubtitleIndex = -1;
            _skipHoldTimer = 0f;
            _isTransitioningOut = false;
            _outroFadeTimer = 0f;
            _asyncLoadTriggered = false;

            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 1f;
                _rootCanvasGroup.interactable = true;
                _rootCanvasGroup.blocksRaycasts = true;
            }

            if (_finalBlackFadeGroup != null)
            {
                _finalBlackFadeGroup.alpha = 0f;
            }

            if (_skipPromptText != null)
            {
                _skipPromptText.text = GameLocalizationService.Select(
                    "HOLD [SPACE / ESC] TO SKIP",
                    "MANTÉN [ESPACIO / ESC] PARA OMITIR");
            }

            AudioClip bgmClip = (_cinematicData != null && _cinematicData.BgmClip != null)
                ? _cinematicData.BgmClip
                : _directBgmClip;

            if (_bgmAudioSource != null && bgmClip != null)
            {
                _bgmAudioSource.Stop();
                _bgmAudioSource.clip = bgmClip;
                _bgmAudioSource.loop = false;
                _bgmAudioSource.volume = 0.82f;
                _bgmAudioSource.Play();
            }

            if (_vocalsAudioSource != null)
            {
                _vocalsAudioSource.Stop();
                bool isSpanish = GameLocalizationService.CurrentLanguage == GameLanguage.Spanish;
                AudioClip voClip = isSpanish
                    ? ((_cinematicData != null && _cinematicData.VocalsSpanishClip != null) ? _cinematicData.VocalsSpanishClip : _directVocalsSpanishClip)
                    : ((_cinematicData != null && _cinematicData.VocalsEnglishClip != null) ? _cinematicData.VocalsEnglishClip : _directVocalsEnglishClip);

                if (voClip != null)
                {
                    _vocalsAudioSource.clip = voClip;
                    _vocalsAudioSource.loop = false;
                    _vocalsAudioSource.volume = 1.0f;
                    _vocalsAudioSource.Play();
                }
            }

            UpdateSlideState(0.05f);
            UpdateSubtitleState(0.05f);
        }

        private void Update()
        {
            if (!_isPlaying) return;

            float dt = Time.unscaledDeltaTime;

            if (_isTransitioningOut)
            {
                _outroFadeTimer += dt;
                float tOut = Mathf.Clamp01(_outroFadeTimer / 1.5f);
                if (_finalBlackFadeGroup != null) _finalBlackFadeGroup.alpha = tOut;
                if (_bgmAudioSource != null) _bgmAudioSource.volume = Mathf.Lerp(0.82f, 0f, tOut);
                if (_vocalsAudioSource != null) _vocalsAudioSource.volume = Mathf.Lerp(1.0f, 0f, tOut);

                if (_outroFadeTimer >= 1.55f && !_asyncLoadTriggered)
                {
                    _asyncLoadTriggered = true;
                    LoadGameplayScene();
                }
                return;
            }

            // Advance timeline strictly via unscaledDeltaTime so muted Editor Game View or MP3 buffer never stalls the slides
            _elapsedTime += dt;

            UpdateSlideState(_elapsedTime);
            UpdateSubtitleState(_elapsedTime);
            HandleSkipInput(dt);

            float totalDur = (_cinematicData != null && _cinematicData.TotalDuration > 10f)
                ? _cinematicData.TotalDuration
                : TotalTimelineSeconds;

            if (_elapsedTime >= totalDur)
            {
                _isTransitioningOut = true;
                _outroFadeTimer = 0f;
            }
        }

        private void UpdateSlideState(float time)
        {
            IntroSlideEntry[] soSlides = (_cinematicData != null) ? _cinematicData.Slides : null;
            int slideCount = (soSlides != null && soSlides.Length > 0)
                ? soSlides.Length
                : FallbackSlideStarts.Length;

            int foundIdx = -1;
            float startTime = 0f;
            float endTime = 10.5f;

            for (int i = 0; i < slideCount; i++)
            {
                float s = (soSlides != null && i < soSlides.Length) ? soSlides[i].StartTime : FallbackSlideStarts[i];
                float e = (soSlides != null && i < soSlides.Length) ? soSlides[i].EndTime : FallbackSlideEnds[i];
                if (time >= s && time < e)
                {
                    foundIdx = i;
                    startTime = s;
                    endTime = e;
                    break;
                }
            }

            if (foundIdx == -1)
            {
                if (_slideCanvasGroup != null) _slideCanvasGroup.alpha = 0f;
                return;
            }

            if (foundIdx != _activeSlideIndex)
            {
                _activeSlideIndex = foundIdx;
                if (_slideRawImage != null)
                {
                    Texture2D tex = null;
                    if (soSlides != null && foundIdx < soSlides.Length && soSlides[foundIdx].SlideTexture != null)
                    {
                        tex = soSlides[foundIdx].SlideTexture;
                    }
                    else if (_directSlideTextures != null && foundIdx < _directSlideTextures.Length)
                    {
                        tex = _directSlideTextures[foundIdx];
                    }

                    if (tex != null)
                    {
                        _slideRawImage.texture = tex;
                        _slideRawImage.color = Color.white;
                    }
                }
            }

            float clipDuration = Mathf.Max(0.1f, endTime - startTime);
            float localTime = time - startTime;
            float remainingTime = endTime - time;

            float fadeInDur = (_cinematicData != null && _cinematicData.SlideFadeInDuration > 0.05f)
                ? _cinematicData.SlideFadeInDuration
                : DefaultFadeSeconds;
            float fadeOutDur = (_cinematicData != null && _cinematicData.SlideFadeOutDuration > 0.05f)
                ? _cinematicData.SlideFadeOutDuration
                : DefaultFadeSeconds;

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
            if (_subtitleGroup == null || _subtitleText == null) return;

            IntroSubtitleCue[] cues = (_cinematicData != null && _cinematicData.SubtitleCues != null && _cinematicData.SubtitleCues.Length > 0)
                ? _cinematicData.SubtitleCues
                : FallbackSubtitles;

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
            string target = !string.IsNullOrEmpty(_nextGameplaySceneName) ? _nextGameplaySceneName : "01_MainMenu";
            if (Application.CanStreamedLevelBeLoaded(target))
            {
                SceneManager.LoadSceneAsync(target);
            }
            else
            {
                SceneManager.LoadSceneAsync(1); // 01_MainMenu index
            }
        }
    }
}
