namespace CaveSouls.Core
{
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void FixedTick(float fixedDeltaTime);
        void LateTick(float deltaTime);
        void Exit();
    }

    /// <summary>
    /// Base Hierarchical State (HFSM) supporting parent/child sub-state machines without GC allocations.
    /// </summary>
    public abstract class HierarchicalState<TContext> : IState where TContext : class
    {
        protected readonly TContext Context;
        private IState _ActiveSubState;

        public IState ActiveSubState => _ActiveSubState;

        protected HierarchicalState(TContext context)
        {
            Context = context;
        }

        public virtual void Enter() { }

        public virtual void Tick(float deltaTime)
        {
            _ActiveSubState?.Tick(deltaTime);
        }

        public virtual void FixedTick(float fixedDeltaTime)
        {
            _ActiveSubState?.FixedTick(fixedDeltaTime);
        }

        public virtual void LateTick(float deltaTime)
        {
            _ActiveSubState?.LateTick(deltaTime);
        }

        public virtual void Exit()
        {
            if (_ActiveSubState != null)
            {
                _ActiveSubState.Exit();
                _ActiveSubState = null;
            }
        }

        protected void SetSubState(IState newSubState)
        {
            if (ReferenceEquals(_ActiveSubState, newSubState)) return;
            _ActiveSubState?.Exit();
            _ActiveSubState = newSubState;
            _ActiveSubState?.Enter();
        }
    }

    public sealed class StateMachine
    {
        private IState _currentState;
        public IState CurrentState => _currentState;

        public void Initialize(IState startingState)
        {
            _currentState = startingState;
            _currentState?.Enter();
        }

        public void ChangeState(IState nextState)
        {
            if ( nextState == null || ReferenceEquals(_currentState, nextState)) return;
            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();
        }

        public void Tick(float deltaTime) => _currentState?.Tick(deltaTime);
        public void FixedTick(float fixedDeltaTime) => _currentState?.FixedTick(fixedDeltaTime);
        public void LateTick(float deltaTime) => _currentState?.LateTick(deltaTime);
    }
}
