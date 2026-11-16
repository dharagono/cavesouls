using System;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Persistent, Zero-GC runtime performance & telemetry HUD.
    /// Displays FPS, frame time (ms), Mono/Heap memory, Total Allocated RAM, and GC collection counters.
    /// Can be toggled on/off in the Settings menu (persisted via PlayerPrefs).
    /// </summary>
    public sealed class PerformanceStatsOverlay : MonoBehaviour
    {
        public const string PrefKey = "CaveSouls_ShowPerformanceStats";

        private static PerformanceStatsOverlay _instance;
        public static PerformanceStatsOverlay Instance => _instance;

        [Header("UI Canvas References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _statsText;
        [SerializeField] private Image _backgroundPanel;

        [Header("Update Interval")]
        [SerializeField] private float _updateInterval = 0.25f;

        private bool _isEnabled;
        private float _intervalTimer;
        private int _frameCount;
        private float _timeAccumulator;
        private float _currentFps;
        private float _currentFrameMs;

        // Pre-allocated StringBuilder to guarantee strictly Zero-GC string generation
        private readonly StringBuilder _sb = new StringBuilder(512);

        public static bool IsActive => PlayerPrefs.GetInt(PrefKey, 0) == 1;

        public static void SetActiveState(bool active)
        {
            PlayerPrefs.SetInt(PrefKey, active ? 1 : 0);
            PlayerPrefs.Save();

            if (_instance != null)
            {
                _instance.ApplyActiveState(active);
            }
            else if (active)
            {
                EnsureInstanceExists();
            }
        }

        public static bool ToggleOverlay()
        {
            bool newState = !IsActive;
            SetActiveState(newState);
            return newState;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeInit()
        {
            if (IsActive)
            {
                EnsureInstanceExists();
            }
        }

        public static PerformanceStatsOverlay EnsureInstanceExists()
        {
            if (_instance != null) return _instance;

            GameObject go = new GameObject("SYSTEM_PerformanceStatsOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PerformanceStatsOverlay>();
            _instance.BuildUiHierarchy();
            _instance.ApplyActiveState(IsActive);
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (_canvas == null)
            {
                BuildUiHierarchy();
            }
            ApplyActiveState(IsActive);
        }

        public void ApplyActiveState(bool active)
        {
            _isEnabled = active;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = active ? 1f : 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            if (_canvas != null)
            {
                _canvas.enabled = active;
            }
        }

        private void Update()
        {
            if (!_isEnabled) return;

            _frameCount++;
            _timeAccumulator += Time.unscaledDeltaTime;
            _intervalTimer += Time.unscaledDeltaTime;

            if (_intervalTimer >= _updateInterval)
            {
                _currentFps = _frameCount / _timeAccumulator;
                _currentFrameMs = (_timeAccumulator / _frameCount) * 1000f;

                RefreshStatsText();

                _frameCount = 0;
                _timeAccumulator = 0f;
                _intervalTimer = 0f;
            }
        }

        private void RefreshStatsText()
        {
            if (_statsText == null) return;

            long monoUsed = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            long monoHeap = Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
            long totalAlloc = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);

            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            _sb.Clear();
            _sb.Append("<color=#E0D0B0><b>CAVE SOULS</b> | ENGINE TELEMETRY</color>\n");
            _sb.Append("FPS: ");
            _sb.Append(_currentFps.ToString("F1"));
            _sb.Append(" (");
            _sb.Append(_currentFrameMs.ToString("F2"));
            _sb.Append(" ms)\n");

            _sb.Append("Mono Heap: ");
            _sb.Append(monoUsed);
            _sb.Append(" / ");
            _sb.Append(monoHeap);
            _sb.Append(" MB\n");

            _sb.Append("Total Alloc: ");
            _sb.Append(totalAlloc);
            _sb.Append(" MB\n");

            _sb.Append("GC Gen: [0: ");
            _sb.Append(gc0);
            _sb.Append(" | 1: ");
            _sb.Append(gc1);
            _sb.Append(" | 2: ");
            _sb.Append(gc2);
            _sb.Append("]\n");

            _sb.Append("<color=#A09070>Pipeline: URP 6000.6 (SRP Batcher ON)</color>");

            _statsText.text = _sb.ToString();
        }

        private void BuildUiHierarchy()
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Background panel at top-right
            GameObject panelGo = new GameObject("StatsPanel");
            panelGo.transform.SetParent(transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1, 1);
            panelRt.anchorMax = new Vector2(1, 1);
            panelRt.pivot = new Vector2(1, 1);
            panelRt.anchoredPosition = new Vector2(-24, -24);
            panelRt.sizeDelta = new Vector2(340, 150);

            _backgroundPanel = panelGo.AddComponent<Image>();
            _backgroundPanel.color = new Color(0.04f, 0.05f, 0.06f, 0.88f);

            // Text
            GameObject textGo = new GameObject("StatsText");
            textGo.transform.SetParent(panelGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(14, 10);
            textRt.offsetMax = new Vector2(-14, -10);

            _statsText = textGo.AddComponent<Text>();
            _statsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statsText.fontSize = 15;
            _statsText.lineSpacing = 1.25f;
            _statsText.color = new Color(0.88f, 0.88f, 0.84f, 1f);
            _statsText.alignment = TextAnchor.UpperLeft;
        }
    }
}
