using UnityEngine;

namespace CaveSouls.Core
{
    public enum GameLanguage : byte
    {
        English = 0,
        Spanish = 1
    }

    public readonly struct OnLanguageChangedEvent : IGameEvent
    {
        public readonly GameLanguage Language;

        public OnLanguageChangedEvent(GameLanguage language)
        {
            Language = language;
        }
    }

    /// <summary>
    /// Zero-Allocation static localization service for English / Spanish switching.
    /// Persists the player's language preference in PlayerPrefs and broadcasts struct events via EventBus.
    /// </summary>
    public static class GameLocalizationService
    {
        private const string LanguagePrefKey = "CaveSouls_Language";
        private static bool _initialized;
        private static GameLanguage _currentLanguage;

        public static GameLanguage CurrentLanguage
        {
            get
            {
                if (!_initialized)
                {
                    int saved = PlayerPrefs.GetInt(LanguagePrefKey, (int)GameLanguage.English);
                    _currentLanguage = (saved == (int)GameLanguage.Spanish) ? GameLanguage.Spanish : GameLanguage.English;
                    _initialized = true;
                }
                return _currentLanguage;
            }
        }

        public static void SetLanguage(GameLanguage language)
        {
            if (_initialized && _currentLanguage == language) return;

            _currentLanguage = language;
            _initialized = true;
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            PlayerPrefs.Save();

            EventBus<OnLanguageChangedEvent>.Publish(new OnLanguageChangedEvent(language));
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == GameLanguage.English ? GameLanguage.Spanish : GameLanguage.English);
        }

        public static string Select(string englishText, string spanishText)
        {
            return CurrentLanguage == GameLanguage.Spanish ? spanishText : englishText;
        }
    }
}
