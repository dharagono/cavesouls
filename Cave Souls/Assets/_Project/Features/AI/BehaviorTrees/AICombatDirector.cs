using CaveSouls.Core;
using UnityEngine;

namespace CaveSouls.Features.AI
{
    /// <summary>
    /// Combat Director (Ticket System).
    /// Prevents multiple enemies from attacking on the exact same frame; enemies request an attack ticket
    /// while non-ticket holders strafe, flank, and maintain tactical spacing.
    /// Zero GC allocations.
    /// </summary>
    public sealed class AICombatDirector : MonoBehaviour
    {
        private struct TicketSlot
        {
            public int HolderEntityId;
            public float ExpirationTime;
            public bool IsOccupied;
        }

        private const int MaxTicketCapacity = 8;

        [SerializeField] private int _maxSimultaneousAttackers = 2;
        [SerializeField] private float _maxTicketHoldDuration = 3.2f;
        [SerializeField] private float _postAttackCooldownPerEnemy = 1.15f;

        private readonly TicketSlot[] _tickets = new TicketSlot[MaxTicketCapacity];
        private readonly int[] _cooldownEntityIds = new int[32];
        private readonly float[] _cooldownEndTimes = new float[32];
        private int _cooldownCount;

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister(this);
        }

        private void Update()
        {
            float now = Time.time;
            int activeCap = Mathf.Clamp(_maxSimultaneousAttackers, 1, MaxTicketCapacity);

            for (int i = 0; i < activeCap; i++)
            {
                if (_tickets[i].IsOccupied && now >= _tickets[i].ExpirationTime)
                {
                    _tickets[i].IsOccupied = false;
                    _tickets[i].HolderEntityId = 0;
                }
            }
        }

        public bool HasTicket(int entityId)
        {
            int activeCap = Mathf.Clamp(_maxSimultaneousAttackers, 1, MaxTicketCapacity);
            for (int i = 0; i < activeCap; i++)
            {
                if (_tickets[i].IsOccupied && _tickets[i].HolderEntityId == entityId)
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryRequestAttackTicket(int entityId)
        {
            float now = Time.time;

            if (HasTicket(entityId)) return true;
            if (IsEntityOnCooldown(entityId, now)) return false;

            int activeCap = Mathf.Clamp(_maxSimultaneousAttackers, 1, MaxTicketCapacity);
            for (int i = 0; i < activeCap; i++)
            {
                if (!_tickets[i].IsOccupied)
                {
                    _tickets[i].IsOccupied = true;
                    _tickets[i].HolderEntityId = entityId;
                    _tickets[i].ExpirationTime = now + _maxTicketHoldDuration;
                    return true;
                }
            }

            return false;
        }

        public void ReleaseAttackTicket(int entityId)
        {
            int activeCap = Mathf.Clamp(_maxSimultaneousAttackers, 1, MaxTicketCapacity);
            for (int i = 0; i < activeCap; i++)
            {
                if (_tickets[i].IsOccupied && _tickets[i].HolderEntityId == entityId)
                {
                    _tickets[i].IsOccupied = false;
                    _tickets[i].HolderEntityId = 0;
                    RegisterCooldown(entityId, Time.time + _postAttackCooldownPerEnemy);
                    return;
                }
            }
        }

        private bool IsEntityOnCooldown(int entityId, float now)
        {
            for (int i = 0; i < _cooldownCount; i++)
            {
                if (_cooldownEntityIds[i] == entityId)
                {
                    return now < _cooldownEndTimes[i];
                }
            }
            return false;
        }

        private void RegisterCooldown(int entityId, float endTime)
        {
            for (int i = 0; i < _cooldownCount; i++)
            {
                if (_cooldownEntityIds[i] == entityId)
                {
                    _cooldownEndTimes[i] = endTime;
                    return;
                }
            }

            if (_cooldownCount < _cooldownEntityIds.Length)
            {
                _cooldownEntityIds[_cooldownCount] = entityId;
                _cooldownEndTimes[_cooldownCount] = endTime;
                _cooldownCount++;
            }
            else
            {
                _cooldownEntityIds[0] = entityId;
                _cooldownEndTimes[0] = endTime;
            }
        }
    }
}
