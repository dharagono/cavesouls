using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.Player
{
    /// <summary>
    /// Zero-allocation Ring Buffer for Souls-like Action Input Buffering.
    /// Stores discrete combat/dodge inputs for a configurable window (default 0.25s)
    /// and executes them on the first valid Cancel Window frame.
    /// </summary>
    public sealed class ActionInputBuffer
    {
        private struct BufferedEntry
        {
            public BufferedActionType Action;
            public Vector2 MoveDirectionSnapshot;
            public float Timestamp;
            public bool IsValid;
        }

        private const int BufferCapacity = 8;
        private readonly BufferedEntry[] _entries = new BufferedEntry[BufferCapacity];
        private float _bufferWindowSeconds;

        public ActionInputBuffer(float bufferWindowSeconds = 0.25f)
        {
            _bufferWindowSeconds = Mathf.Max(0.05f, bufferWindowSeconds);
        }

        public void SetBufferWindow(float seconds)
        {
            _bufferWindowSeconds = Mathf.Max(0.05f, seconds);
        }

        public void Enqueue(BufferedActionType action, Vector2 moveDirSnapshot, float currentTime)
        {
            if (action == BufferedActionType.None) return;

            // Overwrite oldest or expired slot
            int targetSlot = 0;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < BufferCapacity; i++)
            {
                if (!_entries[i].IsValid || (currentTime - _entries[i].Timestamp) > _bufferWindowSeconds)
                {
                    targetSlot = i;
                    break;
                }

                if (_entries[i].Timestamp < oldestTime)
                {
                    oldestTime = _entries[i].Timestamp;
                    targetSlot = i;
                }
            }

            _entries[targetSlot] = new BufferedEntry
            {
                Action = action,
                MoveDirectionSnapshot = moveDirSnapshot,
                Timestamp = currentTime,
                IsValid = true
            };
        }

        /// <summary>
        /// Peeks and consumes the earliest non-expired buffered action within the window.
        /// Returns false if no valid action is buffered.
        /// </summary>
        public bool TryConsumeNext(float currentTime, out BufferedActionType action, out Vector2 moveDirection)
        {
            int bestIndex = -1;
            float earliestValidTimestamp = float.MaxValue;

            for (int i = 0; i < BufferCapacity; i++)
            {
                if (!_entries[i].IsValid) continue;

                float age = currentTime - _entries[i].Timestamp;
                if (age > _bufferWindowSeconds)
                {
                    _entries[i].IsValid = false;
                    continue;
                }

                if (_entries[i].Timestamp < earliestValidTimestamp)
                {
                    earliestValidTimestamp = _entries[i].Timestamp;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                action = _entries[bestIndex].Action;
                moveDirection = _entries[bestIndex].MoveDirectionSnapshot;
                _entries[bestIndex].IsValid = false;
                return true;
            }

            action = BufferedActionType.None;
            moveDirection = Vector2.zero;
            return false;
        }

        public void Clear()
        {
            for (int i = 0; i < BufferCapacity; i++)
            {
                _entries[i].IsValid = false;
            }
        }
    }
}
