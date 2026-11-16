using UnityEngine;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Volume trigger placed over deep mud pools in "El Lecho de Lodo Primordial".
    /// Forces deep mud locomotion debuff (30% speed reduction / 70% speed multiplier)
    /// and deep mud footstep acoustics.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MudDebuffZone : MonoBehaviour
    {
        [SerializeField] private float _speedMultiplier = 0.70f;
        public float SpeedMultiplier => _speedMultiplier;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out SoulsPlayerController player))
            {
                player.SetInMudZone(true, _speedMultiplier);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out SoulsPlayerController player))
            {
                player.SetInMudZone(false, 1.0f);
            }
        }
    }
}
