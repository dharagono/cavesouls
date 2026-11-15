using UnityEngine;
using UnityEngine.Pool;

namespace CaveSouls.Core
{
    /// <summary>
    /// Generic zero-allocation ObjectPool wrapper around UnityEngine.Pool.ObjectPool&lt;T&gt;.
    /// Used for projectiles, blood decals, impact sparks, floating damage numbers, and 3D audio sources.
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly ObjectPool<T> _pool;

        public ComponentPool(T prefab, Transform parent, int defaultCapacity = 16, int maxSize = 128, bool prewarm = true)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new ObjectPool<T>(
                createFunc: CreateInstance,
                actionOnGet: OnGetInstance,
                actionOnRelease: OnReleaseInstance,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);

            if (prewarm && _prefab != null)
            {
                T[] prewarmBuffer = new T[defaultCapacity];
                for (int i = 0; i < defaultCapacity; i++)
                {
                    prewarmBuffer[i] = _pool.Get();
                }
                for (int i = 0; i < defaultCapacity; i++)
                {
                    _pool.Release(prewarmBuffer[i]);
                }
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = _pool.Get();
            Transform tr = instance.transform;
            tr.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public void Release(T instance)
        {
            if (instance != null)
            {
                _pool.Release(instance);
            }
        }

        private T CreateInstance()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private static void OnGetInstance(T instance)
        {
            instance.gameObject.SetActive(true);
        }

        private void OnReleaseInstance(T instance)
        {
            instance.gameObject.SetActive(false);
            if (_parent != null)
            {
                instance.transform.SetParent(_parent, false);
            }
        }

        private static void OnDestroyInstance(T instance)
        {
            if (instance != null)
            {
                Object.Destroy(instance.gameObject);
            }
        }
    }
}
