using System;

namespace CaveSouls.Core
{
    public interface IGameEvent { }

    /// <summary>
    /// Zero-allocation, struct-based Event Bus using `in T` references.
    /// Avoids boxing and allocations during Update/FixedUpdate loops.
    /// </summary>
    public static class EventBus<T> where T : struct, IGameEvent
    {
        public delegate void EventCallback(in T eventData);

        private static EventCallback[] _listeners = new EventCallback[16];
        private static int _count;

        public static void Subscribe(EventCallback listener)
        {
            if (listener == null) return;

            for (int i = 0; i < _count; i++)
            {
                if (_listeners[i] == listener) return;
            }

            if (_count == _listeners.Length)
            {
                Array.Resize(ref _listeners, _listeners.Length * 2);
            }

            _listeners[_count++] = listener;
        }

        public static void Unsubscribe(EventCallback listener)
        {
            if (listener == null) return;

            for (int i = 0; i < _count; i++)
            {
                if (_listeners[i] == listener)
                {
                    _count--;
                    _listeners[i] = _listeners[_count];
                    _listeners[_count] = null;
                    return;
                }
            }
        }

        public static void Publish(in T eventData)
        {
            // Iterate backwards so unsubscribing during callback is safe and zero-alloc
            for (int i = _count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke(in eventData);
            }
        }

        public static void Clear()
        {
            Array.Clear(_listeners, 0, _count);
            _count = 0;
        }
    }
}
