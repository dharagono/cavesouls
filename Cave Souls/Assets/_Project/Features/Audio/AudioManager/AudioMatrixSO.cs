using System;
using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Audio
{
    [Serializable]
    public struct CombatImpactAudioEntry
    {
        public WeaponMaterialType WeaponMaterial;
        public SurfaceMaterialType ImpactedSurface;
        public AudioClip[] ImpactClips;
    }

    [Serializable]
    public struct SurfaceFootstepAudioEntry
    {
        public SurfaceMaterialType Surface;
        public AudioClip[] FootstepClips;
    }

    /// <summary>
    /// Immutable ScriptableObject storing the [WeaponMaterial x SurfaceMaterial] combat audio matrix,
    /// surface footstep banks, and armor foley clink clips.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioMatrixDatabase", menuName = "CaveSouls/Audio/Combat & Surface Audio Matrix SO")]
    public sealed class AudioMatrixSO : ScriptableObject
    {
        [Header("Combat Impact Matrix [WeaponMaterial x SurfaceMaterial]")]
        [SerializeField] private CombatImpactAudioEntry[] _combatImpactMatrix;

        [Header("Surface Footstep Banks")]
        [SerializeField] private SurfaceFootstepAudioEntry[] _surfaceFootsteps;

        [Header("Armor Foley Clips")]
        [SerializeField] private AudioClip[] _armorClinkClips;

        [Header("Pitch & Volume Randomization (Anti-Machine-Gun Effect)")]
        [SerializeField] private float _minPitch = 0.92f;
        [SerializeField] private float _maxPitch = 1.08f;
        [SerializeField] private float _minVolumeScale = 0.88f;
        [SerializeField] private float _maxVolumeScale = 1.00f;

        public float GetRandomPitch() => UnityEngine.Random.Range(_minPitch, _maxPitch);
        public float GetRandomVolumeScale() => UnityEngine.Random.Range(_minVolumeScale, _maxVolumeScale);

        public AudioClip GetCombatImpactClip(WeaponMaterialType weaponMat, SurfaceMaterialType surfaceMat)
        {
            if (_combatImpactMatrix == null) return null;

            for (int i = 0; i < _combatImpactMatrix.Length; i++)
            {
                if (_combatImpactMatrix[i].WeaponMaterial == weaponMat &&
                    _combatImpactMatrix[i].ImpactedSurface == surfaceMat)
                {
                    AudioClip[] clips = _combatImpactMatrix[i].ImpactClips;
                    if (clips != null && clips.Length > 0)
                    {
                        return clips[UnityEngine.Random.Range(0, clips.Length)];
                    }
                }
            }

            // Fallback to first available clip in matrix
            for (int i = 0; i < _combatImpactMatrix.Length; i++)
            {
                AudioClip[] clips = _combatImpactMatrix[i].ImpactClips;
                if (clips != null && clips.Length > 0)
                {
                    return clips[UnityEngine.Random.Range(0, clips.Length)];
                }
            }

            return null;
        }

        public AudioClip GetFootstepClip(SurfaceMaterialType surface)
        {
            if (_surfaceFootsteps == null) return null;

            for (int i = 0; i < _surfaceFootsteps.Length; i++)
            {
                if (_surfaceFootsteps[i].Surface == surface)
                {
                    AudioClip[] clips = _surfaceFootsteps[i].FootstepClips;
                    if (clips != null && clips.Length > 0)
                    {
                        return clips[UnityEngine.Random.Range(0, clips.Length)];
                    }
                }
            }

            return null;
        }

        public AudioClip GetRandomArmorClinkClip()
        {
            if (_armorClinkClips == null || _armorClinkClips.Length == 0) return null;
            return _armorClinkClips[UnityEngine.Random.Range(0, _armorClinkClips.Length)];
        }
    }
}
