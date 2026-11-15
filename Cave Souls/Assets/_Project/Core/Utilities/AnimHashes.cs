using UnityEngine;

namespace CaveSouls.Core
{
    /// <summary>
    /// Pre-cached Animator parameter hashes and Shader property IDs.
    /// Strictly prevents runtime string hashing in Update/LateUpdate loops.
    /// </summary>
    public static class AnimHashes
    {
        public static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        public static readonly int StrafeX = Animator.StringToHash("StrafeX");
        public static readonly int StrafeZ = Animator.StringToHash("StrafeZ");
        public static readonly int IsLockedOn = Animator.StringToHash("IsLockedOn");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int IsBlocking = Animator.StringToHash("IsBlocking");
        public static readonly int EquipLoadTier = Animator.StringToHash("EquipLoadTier");

        public static readonly int Roll = Animator.StringToHash("Roll");
        public static readonly int Backstep = Animator.StringToHash("Backstep");
        public static readonly int Attack1 = Animator.StringToHash("Attack1");
        public static readonly int Attack2 = Animator.StringToHash("Attack2");
        public static readonly int Attack3 = Animator.StringToHash("Attack3");
        public static readonly int HeavyAttack = Animator.StringToHash("HeavyAttack");
        public static readonly int Parry = Animator.StringToHash("Parry");
        public static readonly int HitStun = Animator.StringToHash("HitStun");
        public static readonly int GuardBroken = Animator.StringToHash("GuardBroken");
        public static readonly int Riposte = Animator.StringToHash("Riposte");
        public static readonly int Backstab = Animator.StringToHash("Backstab");
        public static readonly int UseConsumable = Animator.StringToHash("UseConsumable");
        public static readonly int Interact = Animator.StringToHash("Interact");
        public static readonly int Die = Animator.StringToHash("Die");
        public static readonly int PhaseTransition = Animator.StringToHash("PhaseTransition");
        public static readonly int GapCloserAttack = Animator.StringToHash("GapCloserAttack");
    }

    public static class ShaderIDs
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
        public static readonly int BloodIntensity = Shader.PropertyToID("_BloodIntensity");
        public static readonly int Opacity = Shader.PropertyToID("_Opacity");
    }
}
