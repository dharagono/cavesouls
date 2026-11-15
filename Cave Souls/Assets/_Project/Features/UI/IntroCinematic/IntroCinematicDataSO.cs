using System;
using UnityEngine;

namespace CaveSouls.Features.UI
{
    [Serializable]
    public struct IntroSlideEntry
    {
        public Texture2D SlideTexture;
        public float StartTime;
        public float EndTime;
    }

    [Serializable]
    public struct IntroSubtitleCue
    {
        public float StartTime;
        public float EndTime;
        [TextArea(2, 4)] public string TextEnglish;
        [TextArea(2, 4)] public string TextSpanish;
    }

    /// <summary>
    /// Immutable ScriptableObject holding all static configuration for the 1:33 (93.3s) Cave Souls Intro Cinematic:
    /// - 10 sequential concept art slides with 2.0s Fade-In and 2.0s Fade-Out transitions.
    /// - Background music ("Weight_of_the_Seal.mp3") + localized voice tracks (English & Spanish).
    /// - Acoustic-synchronized subtitle cues for both languages.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_IntroCinematicData", menuName = "CaveSouls/UI/Intro Cinematic Data")]
    public sealed class IntroCinematicDataSO : ScriptableObject
    {
        [Header("Global Timing (1:33 = 93.3s)")]
        [SerializeField] private float _totalDuration = 93.3f;
        [SerializeField] private float _slideFadeInDuration = 2.0f;
        [SerializeField] private float _slideFadeOutDuration = 2.0f;

        [Header("Audio Tracks (1:33 Duration)")]
        [SerializeField] private AudioClip _bgmClip;
        [SerializeField] private AudioClip _vocalsEnglishClip;
        [SerializeField] private AudioClip _vocalsSpanishClip;

        [Header("Sequential Artwork Slides (01.png - 10.png)")]
        [SerializeField] private IntroSlideEntry[] _slides;

        [Header("Localized Subtitle Script Cues")]
        [SerializeField] private IntroSubtitleCue[] _subtitleCues;

        public float TotalDuration => _totalDuration;
        public float SlideFadeInDuration => _slideFadeInDuration;
        public float SlideFadeOutDuration => _slideFadeOutDuration;
        public AudioClip BgmClip => _bgmClip;
        public AudioClip VocalsEnglishClip => _vocalsEnglishClip;
        public AudioClip VocalsSpanishClip => _vocalsSpanishClip;
        public IntroSlideEntry[] Slides => _slides;
        public IntroSubtitleCue[] SubtitleCues => _subtitleCues;
    }
}
