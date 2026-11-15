using CaveSouls.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace CaveSouls.Features.Audio
{
    /// <summary>
    /// Pooled 3D Spatial AudioSource configured with Logarithmic Rolloff.
    /// Automatically returns itself to AudioPoolManager when playback finishes without coroutines or GC allocs.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PooledSpatialAudioSource : MonoBehaviour
    {
        private AudioSource _audioSource;
        private AudioPoolManager _ownerPool;
        private float _remainingPlayTime;
        private bool _isPlaying;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1.0f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = 32f;
        }

        public void Play3D(
            AudioPoolManager ownerPool,
            AudioClip clip,
            AudioMixerGroup mixerGroup,
            float volume,
            float pitch,
            float minDistance = 1.5f,
            float maxDistance = 32f)
        {
            if (clip == null)
            {
                ownerPool?.ReturnToPool(this);
                return;
            }

            _ownerPool = ownerPool;
            _audioSource.outputAudioMixerGroup = mixerGroup;
            _audioSource.clip = clip;
            _audioSource.volume = volume;
            _audioSource.pitch = pitch;
            _audioSource.minDistance = minDistance;
            _audioSource.maxDistance = maxDistance;
            _audioSource.Play();

            _remainingPlayTime = (clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch))) + 0.05f;
            _isPlaying = true;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            _remainingPlayTime -= Time.unscaledDeltaTime;
            if (_remainingPlayTime <= 0f)
            {
                _isPlaying = false;
                _audioSource.Stop();
                _audioSource.clip = null;
                _ownerPool?.ReturnToPool(this);
            }
        }
    }

    /// <summary>
    /// Manages an ObjectPool of 3D Spatial AudioSources (Logarithmic Rolloff) and AudioMixer Snapshots.
    /// Listens to OnHitImpactEvent, OnFootstepEvent, and OnVitalsChangedEvent via EventBus without coupling.
    /// </summary>
    public sealed class AudioPoolManager : MonoBehaviour
    {
        [Header("Audio Mixer & Groups")]
        [SerializeField] private AudioMixer _mainMixer;
        [SerializeField] private AudioMixerGroup _masterGroup;
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxCombatGroup;
        [SerializeField] private AudioMixerGroup _sfxEnvironmentGroup;
        [SerializeField] private AudioMixerGroup _voiceGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("AudioMixer Snapshots (Normal vs Low Health / Underwater Low-Pass)")]
        [SerializeField] private AudioMixerSnapshot _normalSnapshot;
        [SerializeField] private AudioMixerSnapshot _lowHealthLowPassSnapshot;
        [SerializeField] private AudioMixerSnapshot _underwaterSnapshot;
        [Range(0.05f, 0.4f)] [SerializeField] private float _lowHealthRatioThreshold = 0.22f;

        [Header("Audio Matrix & Pool Settings")]
        [SerializeField] private AudioMatrixSO _audioMatrix;
        [SerializeField] private PooledSpatialAudioSource _spatialSourcePrefab;
        [SerializeField] private int _initialPoolCapacity = 24;
        [SerializeField] private int _maxPoolSize = 64;

        private ComponentPool<PooledSpatialAudioSource> _spatialAudioPool;
        private bool _isLowHealthSnapshotActive;

        private void Awake()
        {
            ServiceLocator.Register(this);

            if (_spatialSourcePrefab == null)
            {
                GameObject templateObj = new GameObject("Pooled3DAudioSource_Template");
                templateObj.transform.SetParent(transform, false);
                templateObj.SetActive(false);
                _spatialSourcePrefab = templateObj.AddComponent<PooledSpatialAudioSource>();
            }

            _spatialAudioPool = new ComponentPool<PooledSpatialAudioSource>(
                _spatialSourcePrefab,
                transform,
                _initialPoolCapacity,
                _maxPoolSize,
                prewarm: true);
        }

        private void OnEnable()
        {
            EventBus<OnHitImpactEvent>.Subscribe(HandleHitImpact);
            EventBus<OnFootstepEvent>.Subscribe(HandleFootstep);
            EventBus<OnVitalsChangedEvent>.Subscribe(HandleVitalsChanged);
        }

        private void OnDisable()
        {
            EventBus<OnHitImpactEvent>.Unsubscribe(HandleHitImpact);
            EventBus<OnFootstepEvent>.Unsubscribe(HandleFootstep);
            EventBus<OnVitalsChangedEvent>.Unsubscribe(HandleVitalsChanged);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        public void PlaySpatialClip(AudioClip clip, Vector3 position, AudioMixerGroup group, float baseVolume = 1f)
        {
            if (clip == null || _spatialAudioPool == null) return;

            float pitch = _audioMatrix != null ? _audioMatrix.GetRandomPitch() : Random.Range(0.92f, 1.08f);
            float volScale = _audioMatrix != null ? _audioMatrix.GetRandomVolumeScale() : Random.Range(0.9f, 1.0f);

            PooledSpatialAudioSource source = _spatialAudioPool.Get(position, Quaternion.identity);
            source.Play3D(this, clip, group, baseVolume * volScale, pitch);
        }

        public void ReturnToPool(PooledSpatialAudioSource instance)
        {
            _spatialAudioPool?.Release(instance);
        }

        public void SetUnderwaterLowPass(bool isUnderwater, float transitionTime = 0.35f)
        {
            if (isUnderwater && _underwaterSnapshot != null)
            {
                _underwaterSnapshot.TransitionTo(transitionTime);
            }
            else if (_normalSnapshot != null)
            {
                _normalSnapshot.TransitionTo(transitionTime);
            }
        }

        private void HandleHitImpact(in OnHitImpactEvent evt)
        {
            if (_audioMatrix == null) return;

            AudioClip clip = _audioMatrix.GetCombatImpactClip(evt.WeaponMaterial, evt.SurfaceMaterial);
            float volume = evt.IsHeavyHit ? 1.0f : 0.8f;
            PlaySpatialClip(clip, evt.HitPoint, _sfxCombatGroup, volume);
        }

        private void HandleFootstep(in OnFootstepEvent evt)
        {
            if (_audioMatrix == null) return;

            AudioClip surfaceClip = _audioMatrix.GetFootstepClip(evt.Surface);
            float baseVol = evt.IsSprinting ? 0.95f : 0.65f;
            PlaySpatialClip(surfaceClip, evt.FootPosition, _sfxEnvironmentGroup, baseVol);

            if (evt.ArmorWeightRatio >= 0.30f)
            {
                AudioClip armorClink = _audioMatrix.GetRandomArmorClinkClip();
                float clinkVol = Mathf.Clamp01(evt.ArmorWeightRatio * 0.65f);
                PlaySpatialClip(armorClink, evt.FootPosition, _sfxEnvironmentGroup, clinkVol);
            }
        }

        private void HandleVitalsChanged(in OnVitalsChangedEvent evt)
        {
            if (!evt.IsPlayer) return;

            float hpRatio = evt.MaxHealth > 0f ? evt.CurrentHealth / evt.MaxHealth : 1f;
            bool shouldUseLowPass = evt.CurrentHealth > 0f && hpRatio <= _lowHealthRatioThreshold;

            if (shouldUseLowPass != _isLowHealthSnapshotActive)
            {
                _isLowHealthSnapshotActive = shouldUseLowPass;
                if (_isLowHealthSnapshotActive && _lowHealthLowPassSnapshot != null)
                {
                    _lowHealthLowPassSnapshot.TransitionTo(0.45f);
                }
                else if (!_isLowHealthSnapshotActive && _normalSnapshot != null)
                {
                    _normalSnapshot.TransitionTo(0.65f);
                }
            }
        }
    }
}
