using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Combat
{
    /// <summary>
    /// Immutable ScriptableObject defining normalized frame windows [0..1] for an attack, dodge, or parry animation.
    /// Controls Hitboxes, I-Frames, Parry windows, Hyper-Armor, and Action Buffer Cancel Windows.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimCombatWindow_", menuName = "CaveSouls/Combat/Animation Combat Window")]
    public sealed class AnimationCombatWindowSO : ScriptableObject
    {
        [Header("Animation Metadata")]
        [SerializeField] private string _animationStateName = "Attack1";
        [SerializeField] private float _staminaCost = 18f;
        [SerializeField] private float _motionValueMultiplier = 1.0f;
        [SerializeField] private float _poiseDamageMultiplier = 1.0f;
        [SerializeField] private bool _isHeavyAttack;
        [SerializeField] private bool _canBeParried = true;

        [Header("Hitbox Active Window (Normalized 0..1)")]
        [SerializeField] private bool _hasHitboxWindow = true;
        [Range(0f, 1f)] [SerializeField] private float _hitboxStart = 0.24f;
        [Range(0f, 1f)] [SerializeField] private float _hitboxEnd = 0.48f;

        [Header("I-Frames Invulnerability Window (Normalized 0..1)")]
        [SerializeField] private bool _hasIFrameWindow;
        [Range(0f, 1f)] [SerializeField] private float _iFrameStart = 0.05f;
        [Range(0f, 1f)] [SerializeField] private float _iFrameEnd = 0.52f;

        [Header("Parry Active Window (Normalized 0..1)")]
        [SerializeField] private bool _hasParryWindow;
        [Range(0f, 1f)] [SerializeField] private float _parryStart = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float _parryEnd = 0.34f;

        [Header("Hyper-Armor Window (Normalized 0..1)")]
        [SerializeField] private bool _hasHyperArmorWindow;
        [Range(0f, 1f)] [SerializeField] private float _hyperArmorStart = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float _hyperArmorEnd = 0.65f;

        [Header("Input Buffer Cancel Window (Normalized 0..1)")]
        [Range(0f, 1f)] [SerializeField] private float _cancelWindowStart = 0.62f;

        private int _cachedStateHash;

        public int StateHash
        {
            get
            {
                if (_cachedStateHash == 0)
                {
                    _cachedStateHash = Animator.StringToHash(_animationStateName);
                }
                return _cachedStateHash;
            }
        }

        public float StaminaCost => _staminaCost;
        public float MotionValueMultiplier => _motionValueMultiplier;
        public float PoiseDamageMultiplier => _poiseDamageMultiplier;
        public bool IsHeavyAttack => _isHeavyAttack;
        public bool CanBeParried => _canBeParried;

        public bool IsHitboxActive(float normalizedTime) =>
            _hasHitboxWindow && normalizedTime >= _hitboxStart && normalizedTime <= _hitboxEnd;

        public bool IsIFrameActive(float normalizedTime) =>
            _hasIFrameWindow && normalizedTime >= _iFrameStart && normalizedTime <= _iFrameEnd;

        public bool IsParryActive(float normalizedTime) =>
            _hasParryWindow && normalizedTime >= _parryStart && normalizedTime <= _parryEnd;

        public bool IsHyperArmorActive(float normalizedTime) =>
            _hasHyperArmorWindow && normalizedTime >= _hyperArmorStart && normalizedTime <= _hyperArmorEnd;

        public bool IsInCancelWindow(float normalizedTime) =>
            normalizedTime >= _cancelWindowStart;
    }
}
