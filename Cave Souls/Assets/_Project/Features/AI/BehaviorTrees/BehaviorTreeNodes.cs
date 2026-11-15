namespace CaveSouls.Features.AI
{
    public enum BTNodeStatus : byte
    {
        Success = 0,
        Failure = 1,
        Running = 2
    }

    /// <summary>
    /// Zero-allocation Behavior Tree node base class.
    /// All nodes are instantiated once in Awake and ticked without allocations.
    /// </summary>
    public abstract class BTNode
    {
        public abstract BTNodeStatus Tick(float deltaTime);
    }

    public sealed class BTSequence : BTNode
    {
        private readonly BTNode[] _children;

        public BTSequence(params BTNode[] children)
        {
            _children = children;
        }

        public override BTNodeStatus Tick(float deltaTime)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BTNodeStatus status = _children[i].Tick(deltaTime);
                if (status != BTNodeStatus.Success)
                {
                    return status;
                }
            }
            return BTNodeStatus.Success;
        }
    }

    public sealed class BTSelector : BTNode
    {
        private readonly BTNode[] _children;

        public BTSelector(params BTNode[] children)
        {
            _children = children;
        }

        public override BTNodeStatus Tick(float deltaTime)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BTNodeStatus status = _children[i].Tick(deltaTime);
                if (status != BTNodeStatus.Failure)
                {
                    return status;
                }
            }
            return BTNodeStatus.Failure;
        }
    }

    public sealed class BTCondition : BTNode
    {
        public delegate bool ConditionPredicate();
        private readonly ConditionPredicate _predicate;

        public BTCondition(ConditionPredicate predicate)
        {
            _predicate = predicate;
        }

        public override BTNodeStatus Tick(float deltaTime)
        {
            return _predicate != null && _predicate() ? BTNodeStatus.Success : BTNodeStatus.Failure;
        }
    }

    public sealed class BTAction : BTNode
    {
        public delegate BTNodeStatus ActionTickDelegate(float deltaTime);
        private readonly ActionTickDelegate _action;

        public BTAction(ActionTickDelegate action)
        {
            _action = action;
        }

        public override BTNodeStatus Tick(float deltaTime)
        {
            return _action != null ? _action(deltaTime) : BTNodeStatus.Failure;
        }
    }
}
