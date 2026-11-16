using UnityEngine;

namespace CaveSouls.Core
{
    /// <summary>
    /// Attach to environment colliders or terrain objects to explicitly tag their SurfaceMaterialType
    /// (Stone, Water, Wood, Dirt, Metal).
    /// </summary>
    public sealed class SurfaceMaterialIdentifier : MonoBehaviour
    {
        [SerializeField] private SurfaceMaterialType _surfaceType = SurfaceMaterialType.Stone;
        public SurfaceMaterialType SurfaceType => _surfaceType;

        public void SetSurfaceType(SurfaceMaterialType surfaceType)
        {
            _surfaceType = surfaceType;
        }
    }
}
