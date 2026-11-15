using System;
using System.Collections.Generic;

namespace CaveSouls.Core
{
    /// <summary>
    /// Fast, zero-allocation runtime Service Locator for decoupled architecture.
    /// Services are registered in Awake/Bootstrap and cached by consumers in Awake.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>(32);

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            Type type = typeof(T);
            if (Services.TryGetValue(type, out object existing) && ReferenceEquals(existing, service))
            {
                Services.Remove(type);
            }
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out object service))
            {
                return (T)service;
            }
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered in ServiceLocator.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        public static void Clear()
        {
            Services.Clear();
        }
    }
}
