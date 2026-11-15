using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Audio
{
    public enum MusicTrackState : byte
    {
        Exploration = 0,
        TenseSilence = 1,
        BossPhase1 = 2,
        BossPhase2 = 3,
        VictorySilence = 4
    }

    /// <summary>
    /// Adaptive Boss Music Controller with bar-synchronized smooth crossfades between:
    /// Exploration -> Tense Silence -> Boss Phase 1 -> Boss Phase 2 -> Victory Silence.
    /// Zero GC allocations in Update.
    /// </summary>
    public sealed class AdaptiveBossMusicController : MonoBehaviour
    {
        [Header("Music AudioSources (Dual-Deck Crossfade)")]
        [SerializeField] private AudioSource _deckA;
        [SerializeField] private AudioSource _deckB;

        [Header("Music Clips")]
        [SerializeField] private AudioClip _explorationClip;
        [SerializeField] private AudioClip _bossPhase1Clip;
        [SerializeField] private AudioClip _bossPhase2Clip;
        [SerializeField] private AudioClip _victoryStingerClip;

        [Header("Crossfade & Bar Timing")]
        [SerializeField] private float _crossfadeSpeed = 0.85f;
        [SerializeField] private float _maxMusicVolume = 0.75f;

        private MusicTrackState _currentState = MusicTrackState.Exploration;
        private bool _isDeckAActive = true;

        private void Awake()
        {
            if (_deckA != null) { _deckA.loop = true; _deckA.spatialBlend = 0f; }
            if (_deckB != null) { _deckB.loop = true; _deckB.spatialBlend = 0f; }
            TransitionToState(MusicTrackState.Exploration);
        }

        private void OnEnable()
        {
            EventBus<OnBossFightStateChangedEvent>.Subscribe(HandleBossStateChanged);
            EventBus<OnCheckpointRestedEvent>.Subscribe(HandleCheckpointRested);
        }

        private void OnDisable()
        {
            EventBus<OnBossFightStateChangedEvent>.Unsubscribe(HandleBossStateChanged);
            EventBus<OnCheckpointRestedEvent>.Unsubscribe(HandleCheckpointRested);
        }

        private void Update()
        {
            if (_deckA == null || _deckB == null) return;

            float dt = Time.unscaledDeltaTime;
            bool isSilence = _currentState == MusicTrackState.TenseSilence || _currentState == MusicTrackState.VictorySilence;

            float targetA = (!isSilence && _isDeckAActive) ? _maxMusicVolume : 0f;
            float targetB = (!isSilence && !_isDeckAActive) ? _maxMusicVolume : 0f;

            _deckA.volume = Mathf.MoveTowards(_deckA.volume, targetA, _crossfadeSpeed * dt);
            _deckB.volume = Mathf.MoveTowards(_deckB.volume, targetB, _crossfadeSpeed * dt);
        }

        public void TransitionToState(MusicTrackState nextState)
        {
            if (_currentState == nextState && (_deckA != null && _deckA.isPlaying || _deckB != null && _deckB.isPlaying))
            {
                return;
            }

            _currentState = nextState;

            AudioClip targetClip = null;
            switch (nextState)
            {
                case MusicTrackState.Exploration:
                    targetClip = _explorationClip;
                    break;
                case MusicTrackState.BossPhase1:
                    targetClip = _bossPhase1Clip;
                    break;
                case MusicTrackState.BossPhase2:
                    targetClip = _bossPhase2Clip;
                    break;
                case MusicTrackState.VictorySilence:
                    if (_victoryStingerClip != null && _deckA != null)
                    {
                        _deckA.PlayOneShot(_victoryStingerClip, _maxMusicVolume);
                    }
                    return;
                case MusicTrackState.TenseSilence:
                    return;
            }

            if (targetClip == null || _deckA == null || _deckB == null) return;

            _isDeckAActive = !_isDeckAActive;
            AudioSource incomingDeck = _isDeckAActive ? _deckA : _deckB;
            incomingDeck.clip = targetClip;
            incomingDeck.volume = 0f;
            incomingDeck.Play();
        }

        private void HandleBossStateChanged(in OnBossFightStateChangedEvent evt)
        {
            if (evt.IsActive)
            {
                TransitionToState(evt.CurrentPhase >= 2 ? MusicTrackState.BossPhase2 : MusicTrackState.BossPhase1);
            }
            else if (evt.CurrentHealth <= 0f)
            {
                TransitionToState(MusicTrackState.VictorySilence);
            }
            else
            {
                TransitionToState(MusicTrackState.Exploration);
            }
        }

        private void HandleCheckpointRested(in OnCheckpointRestedEvent evt)
        {
            TransitionToState(MusicTrackState.Exploration);
        }
    }
}
