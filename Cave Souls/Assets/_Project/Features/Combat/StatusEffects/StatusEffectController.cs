using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Accumulative Status Effects (Bleed, Poison, Frostbite, Curse) with time decay.
    /// Zero GC allocations in Update/Tick.
    /// </summary>
    public sealed class StatusEffectController : MonoBehaviour
    {
        [Header("Resistance Thresholds")]
        [SerializeField] private float _bleedThreshold = 100f;
        [SerializeField] private float _poisonThreshold = 100f;
        [SerializeField] private float _frostbiteThreshold = 100f;
        [SerializeField] private float _curseThreshold = 100f;

        [Header("Decay & Effect Settings")]
        [SerializeField] private float _decayRatePerSecond = 12f;
        [SerializeField] private float _bleedMaxHealthPercent = 0.15f;
        [SerializeField] private float _bleedFlatBonus = 100f;
        [SerializeField] private float _poisonDamagePerSecond = 14f;
        [SerializeField] private float _poisonDuration = 30f;
        [SerializeField] private float _frostbiteMaxHealthPercent = 0.10f;
        [SerializeField] private float _frostbiteDebuffDuration = 20f;

        private float _bleedBuildup;
        private float _poisonBuildup;
        private float _frostbiteBuildup;
        private float _curseBuildup;

        private float _poisonActiveTimer;
        private float _frostbiteActiveTimer;

        private CombatEntityVitals _vitals;

        public bool IsPoisoned => _poisonActiveTimer > 0f;
        public bool IsFrostbitten => _frostbiteActiveTimer > 0f;

        private void Awake()
        {
            _vitals = GetComponent<CombatEntityVitals>();
        }

        public void ClearAllStatusEffects()
        {
            _bleedBuildup = 0f;
            _poisonBuildup = 0f;
            _frostbiteBuildup = 0f;
            _curseBuildup = 0f;
            _poisonActiveTimer = 0f;
            _frostbiteActiveTimer = 0f;
        }

        public void AddBuildup(StatusEffectType type, float amount)
        {
            if (type == StatusEffectType.None || amount <= 0f || _vitals == null || !_vitals.IsAlive) return;

            switch (type)
            {
                case StatusEffectType.Bleed:
                    _bleedBuildup += amount;
                    bool bleedProc = _bleedBuildup >= _bleedThreshold;
                    if (bleedProc)
                    {
                        _bleedBuildup = 0f;
                        float hemorrhageDamage = (_vitals.MaxHealth * _bleedMaxHealthPercent) + _bleedFlatBonus;
                        _vitals.ApplyDirectTrueDamage(hemorrhageDamage);
                    }
                    PublishBuildup(StatusEffectType.Bleed, _bleedBuildup, _bleedThreshold, bleedProc);
                    break;

                case StatusEffectType.Poison:
                    if (_poisonActiveTimer > 0f) return;
                    _poisonBuildup += amount;
                    bool poisonProc = _poisonBuildup >= _poisonThreshold;
                    if (poisonProc)
                    {
                        _poisonBuildup = _poisonThreshold;
                        _poisonActiveTimer = _poisonDuration;
                    }
                    PublishBuildup(StatusEffectType.Poison, _poisonBuildup, _poisonThreshold, poisonProc);
                    break;

                case StatusEffectType.Frostbite:
                    if (_frostbiteActiveTimer > 0f) return;
                    _frostbiteBuildup += amount;
                    bool frostProc = _frostbiteBuildup >= _frostbiteThreshold;
                    if (frostProc)
                    {
                        _frostbiteBuildup = _frostbiteThreshold;
                        _frostbiteActiveTimer = _frostbiteDebuffDuration;
                        float frostBurst = (_vitals.MaxHealth * _frostbiteMaxHealthPercent) + 50f;
                        _vitals.ApplyDirectTrueDamage(frostBurst);
                    }
                    PublishBuildup(StatusEffectType.Frostbite, _frostbiteBuildup, _frostbiteThreshold, frostProc);
                    break;

                case StatusEffectType.Curse:
                    _curseBuildup += amount;
                    bool curseProc = _curseBuildup >= _curseThreshold;
                    if (curseProc)
                    {
                        _curseBuildup = 0f;
                        _vitals.ApplyDirectTrueDamage(_vitals.MaxHealth * 10f);
                    }
                    PublishBuildup(StatusEffectType.Curse, _curseBuildup, _curseThreshold, curseProc);
                    break;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_vitals == null || !_vitals.IsAlive) return;

            // Decay non-active buildups
            if (_bleedBuildup > 0f)
            {
                _bleedBuildup = Mathf.Max(0f, _bleedBuildup - _decayRatePerSecond * deltaTime);
            }

            if (_curseBuildup > 0f)
            {
                _curseBuildup = Mathf.Max(0f, _curseBuildup - (_decayRatePerSecond * 1.5f) * deltaTime);
            }

            if (_poisonActiveTimer > 0f)
            {
                _poisonActiveTimer -= deltaTime;
                _vitals.ApplyDirectTrueDamage(_poisonDamagePerSecond * deltaTime);
                if (_poisonActiveTimer <= 0f)
                {
                    _poisonBuildup = 0f;
                }
            }
            else if (_poisonBuildup > 0f)
            {
                _poisonBuildup = Mathf.Max(0f, _poisonBuildup - _decayRatePerSecond * deltaTime);
            }

            if (_frostbiteActiveTimer > 0f)
            {
                _frostbiteActiveTimer -= deltaTime;
                if (_frostbiteActiveTimer <= 0f)
                {
                    _frostbiteBuildup = 0f;
                }
            }
            else if (_frostbiteBuildup > 0f)
            {
                _frostbiteBuildup = Mathf.Max(0f, _frostbiteBuildup - _decayRatePerSecond * deltaTime);
            }
        }

        private void PublishBuildup(StatusEffectType type, float current, float max, bool triggered)
        {
            EventBus<OnStatusEffectBuildupEvent>.Publish(new OnStatusEffectBuildupEvent
            {
                Target = gameObject,
                EffectType = type,
                CurrentBuildUp = current,
                MaxThreshold = max,
                IsTriggered = triggered
            });
        }
    }
}
