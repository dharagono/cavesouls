using CaveSouls.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CaveSouls.Features.UI
{
    /// <summary>
    /// Pooled world-space floating damage text instance.
    /// Uses pre-cached integer string lookup table (0..9999) to avoid string allocations on hit!
    /// </summary>
    public sealed class FloatingDamageTextInstance : MonoBehaviour
    {
        private static readonly string[] PreAllocatedIntStrings = new string[4096];
        private static bool _tableInitialized;

        [SerializeField] private Text _textComponent;
        [SerializeField] private float _floatUpSpeed = 1.25f;
        [SerializeField] private float _lifetime = 0.85f;

        private FloatingDamageTextPool _ownerPool;
        private Transform _cameraTransform;
        private float _timer;

        public static void EnsureStringTableInitialized()
        {
            if (_tableInitialized) return;
            for (int i = 0; i < PreAllocatedIntStrings.Length; i++)
            {
                PreAllocatedIntStrings[i] = i.ToString();
            }
            _tableInitialized = true;
        }

        public void Activate(FloatingDamageTextPool pool, Transform camTransform, int damageValue, bool isCritical)
        {
            EnsureStringTableInitialized();
            _ownerPool = pool;
            _cameraTransform = camTransform;
            _timer = _lifetime;

            if (_textComponent != null)
            {
                int clampedIndex = Mathf.Clamp(damageValue, 0, PreAllocatedIntStrings.Length - 1);
                _textComponent.text = PreAllocatedIntStrings[clampedIndex];
                _textComponent.color = isCritical ? new Color(1f, 0.82f, 0.18f, 1f) : Color.white;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _timer -= dt;

            transform.position += Vector3.up * (_floatUpSpeed * dt);
            if (_cameraTransform != null)
            {
                transform.rotation = _cameraTransform.rotation;
            }

            if (_timer <= 0f)
            {
                _ownerPool?.ReleaseInstance(this);
            }
        }
    }

    /// <summary>
    /// Object-Pooled Floating Damage Text Manager listening to OnDamageDealtEvent.
    /// Zero GC allocations during combat.
    /// </summary>
    public sealed class FloatingDamageTextPool : MonoBehaviour
    {
        [SerializeField] private FloatingDamageTextInstance _textPrefab;
        [SerializeField] private int _defaultCapacity = 16;
        [SerializeField] private int _maxSize = 64;

        private ComponentPool<FloatingDamageTextInstance> _pool;
        private Transform _mainCameraTransform;

        private void Awake()
        {
            FloatingDamageTextInstance.EnsureStringTableInitialized();

            if (Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
            }

            if (_textPrefab != null)
            {
                _pool = new ComponentPool<FloatingDamageTextInstance>(_textPrefab, transform, _defaultCapacity, _maxSize, prewarm: true);
            }
        }

        private void OnEnable()
        {
            EventBus<OnDamageDealtEvent>.Subscribe(HandleDamageDealt);
        }

        private void OnDisable()
        {
            EventBus<OnDamageDealtEvent>.Unsubscribe(HandleDamageDealt);
        }

        public void ReleaseInstance(FloatingDamageTextInstance instance)
        {
            _pool?.Release(instance);
        }

        private void HandleDamageDealt(in OnDamageDealtEvent evt)
        {
            if (_pool == null || evt.Result.TotalDamageDealt < 1f) return;

            int roundedDamage = Mathf.RoundToInt(evt.Result.TotalDamageDealt);
            Vector3 spawnPos = evt.Payload.HitPoint != Vector3.zero
                ? evt.Payload.HitPoint + Vector3.up * 0.35f
                : evt.Target.transform.position + Vector3.up * 1.6f;

            FloatingDamageTextInstance instance = _pool.Get(spawnPos, Quaternion.identity);
            instance.Activate(this, _mainCameraTransform, roundedDamage, evt.Payload.IsCriticalRiposteOrBackstab);
        }
    }
}
