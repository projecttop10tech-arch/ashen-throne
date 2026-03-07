using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenThrone.Core
{
    /// <summary>
    /// Generic finite state machine. Used by Combat (turn phases), Empire (build states),
    /// UI (panel stacks), and any system needing explicit state transitions.
    /// TState must be an enum or value type with equality comparison.
    /// </summary>
    public class StateMachine<TState> where TState : struct
    {
        private static readonly EqualityComparer<TState> _comparer = EqualityComparer<TState>.Default;
        private readonly Dictionary<TState, IState<TState>> _states = new();
        private IState<TState> _currentStateHandler;

        public TState CurrentState { get; private set; }
        public TState PreviousState { get; private set; }

        public event Action<TState, TState> OnStateEntered;
        public event Action<TState, TState> OnStateExited;

        private bool _isInitialized;

        /// <summary>
        /// Register a state handler. Must register all states before calling Initialize.
        /// </summary>
        public void AddState(TState state, IState<TState> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _states[state] = handler;
        }

        /// <summary>
        /// Set the initial state without triggering Enter. Call before the first Tick.
        /// </summary>
        public void Initialize(TState initialState)
        {
            if (!_states.TryGetValue(initialState, out IState<TState> handler))
                throw new InvalidOperationException($"[StateMachine] State '{initialState}' has no registered handler.");

            CurrentState = initialState;
            _currentStateHandler = handler;
            _currentStateHandler.Enter(default, initialState);
            _isInitialized = true;
        }

        /// <summary>
        /// Transition to a new state. Calls Exit on current and Enter on new.
        /// No-op if already in the requested state.
        /// </summary>
        public void TransitionTo(TState newState)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[StateMachine] Cannot transition before Initialize is called.");
                return;
            }
            if (_comparer.Equals(CurrentState, newState)) return;
            if (!_states.TryGetValue(newState, out IState<TState> nextHandler))
            {
                Debug.LogError($"[StateMachine] No handler registered for state '{newState}'.");
                return;
            }

            TState previous = CurrentState;
            _currentStateHandler?.Exit(previous, newState);
            OnStateExited?.Invoke(previous, newState);

            PreviousState = previous;
            CurrentState = newState;
            _currentStateHandler = nextHandler;

            _currentStateHandler.Enter(previous, newState);
            OnStateEntered?.Invoke(previous, newState);
        }

        /// <summary>
        /// Forward Unity Update tick to the current state handler.
        /// Call from a MonoBehaviour Update that owns this state machine.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isInitialized) return;
            _currentStateHandler?.Tick(deltaTime);
        }
    }

    /// <summary>
    /// Interface all state handlers must implement.
    /// </summary>
    public interface IState<TState> where TState : struct
    {
        /// <summary>Called when this state is entered. previousState may be default if first state.</summary>
        void Enter(TState previousState, TState currentState);

        /// <summary>Called every frame while this is the active state.</summary>
        void Tick(float deltaTime);

        /// <summary>Called when transitioning away from this state.</summary>
        void Exit(TState currentState, TState nextState);
    }
}
