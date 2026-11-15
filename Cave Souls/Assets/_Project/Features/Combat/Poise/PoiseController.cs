using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Manages Poise (Estabilidad) and Stance Break.
    /// Regenerates Poise after a configurable delay without receiving poise damage.
    /// Zero GC allocations.
    /// </summary>
    public sealed class PoiseController : MonoBehaviour
    {
        [SerializeField] private float _maxPoise = 60f;
        [SerializeField] private float _poiseRegenDelay = 5f;
        [SerializeField] private float _poiseRegenRate = 25f;
        [SerializeField] private float _stanceBreakDuration = 2.75f;
        [SerializeField] private float _hyperArmorPoiseMultiplier = 0.4f;

        private float _currentPoise;
        private float _timeSinceLastPoiseHit;
        private float _stanceBreakTimer;
        private bool _isStanceBroken;

        public float CurrentPoise => _currentPoise;
        public float MaxPoise => _maxPoise;
        public bool IsStanceBroken => _isStanceBroken;

        private void Awake()
        {
            _currentPoise = _maxPoise;
        }

        public void Configure(float maxPoise, float regenDelay, float regenRate)
        {
            _maxPoise = Mathf.Max(1f, maxPoise);
            _poiseRegenDelay = regenDelay;
            _poiseRegenRate = regenRate;
            _currentPoise = _maxPoise;
            _isStanceBroken = false;
        }

        public void ResetPoise()
        {
            _currentPoise = _maxPoise;
            _isStanceBroken = false;
            _stanceBreakTimer = 0f;
            _timeSinceLastPoiseHit = 0f;
        }

        public void ForceStanceBreak(GameObject attacker)
        {
            _currentPoise = 0f;
            _isStanceBroken = true;
            _stanceBreakTimer = _stanceBreakDuration;

            EventBus<OnPoiseBrokenEvent>.Publish(new OnPoiseBrokenEvent
            {
                Target = gameObject,
                Attacker = attacker,
                Position = transform.position,
                StanceBreakDuration = _stanceBreakDuration
            });
        }

        /// <summary>
        /// Applies incoming poise damage and returns true if Stance Break was triggered.
        /// </summary>
        public bool ApplyPoiseDamage(float rawPoiseDamage, bool hasHyperArmor, GameObject attacker)
        {
            if (rawPoiseDamage <= 0f || _isStanceBroken) return false;

            _timeSinceLastPoiseHit = 0f;
            float effectivePoiseDamage = hasHyperArmor ? rawPoiseDamage * _hyperArmorPoiseMultiplier : rawPoiseDamage;
            _currentPoise -= effectivePoiseDamage;

            if (_currentPoise <= 0f)
            {
                ForceStanceBreak(attacker);
                return true;
            }

            return false;
        }

        public void Tick(float deltaTime)
        {
            if (_isStanceBroken)
            {
                _stanceBreakTimer -= deltaTime;
                if (_stanceBreakTimer <= 0f)
                {
                    _isStanceBroken = false;
                    _currentPoise = _maxPoise;
                }
                return;
            }

            if (_currentPoise < _maxPoise)
            {
                _timeSinceLastPoiseHit += deltaTime;
                if (_timeSinceLastPoiseHit >= _poiseRegenDelay)
                {
                    _currentPoise = Mathf.Min(_maxPoise, _currentPoise + _poiseRegenRate * deltaTime);
                }
            }
        }
    }
}
