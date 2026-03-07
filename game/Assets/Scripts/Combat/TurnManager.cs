using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AshenThrone.Core;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Manages combat turn order, phase transitions, and energy per turn.
    /// Turn order determined by hero Speed stat. Ties broken by instanceId (lower = faster).
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public int CurrentTurnNumber { get; private set; }
        public int ActiveHeroInstanceId { get; private set; }
        public CombatPhase CurrentPhase { get; private set; }

        private readonly StateMachine<CombatPhase> _phaseMachine = new();
        private List<TurnSlot> _turnOrder = new();
        private int _turnOrderIndex;
        private CombatGrid _grid;
        private CardHandManager _cardHand;

        public event Action<int> OnTurnChanged;       // heroInstanceId
        public event Action<CombatPhase> OnPhaseChanged;
        public event Action OnBattleEnded;

        private void Awake()
        {
            _grid = GetComponent<CombatGrid>();
            _cardHand = GetComponent<CardHandManager>();
            SetupStateMachine();
        }

        private void SetupStateMachine()
        {
            _phaseMachine.AddState(CombatPhase.Initiative, new InitiativePhaseState(this));
            _phaseMachine.AddState(CombatPhase.Draw, new DrawPhaseState(this, _cardHand));
            _phaseMachine.AddState(CombatPhase.Action, new ActionPhaseState(this));
            _phaseMachine.AddState(CombatPhase.Resolve, new ResolvePhaseState(this, _grid));
            _phaseMachine.AddState(CombatPhase.End, new EndPhaseState(this, _grid));
            _phaseMachine.AddState(CombatPhase.BattleOver, new BattleOverPhaseState(this));

            _phaseMachine.OnStateEntered += (_, next) =>
            {
                CurrentPhase = next;
                OnPhaseChanged?.Invoke(next);
                EventBus.Publish(new CombatPhaseChangedEvent(next));
            };

            _phaseMachine.Initialize(CombatPhase.Initiative);
        }

        /// <summary>
        /// Begin battle with the provided hero combatants. Call once after grid is populated.
        /// </summary>
        public void StartBattle(List<CombatHero> heroes)
        {
            CurrentTurnNumber = 0;
            BuildTurnOrder(heroes);
            _phaseMachine.TransitionTo(CombatPhase.Draw);
        }

        /// <summary>
        /// Sort heroes by speed descending; ties by instanceId ascending.
        /// </summary>
        private void BuildTurnOrder(List<CombatHero> heroes)
        {
            _turnOrder = heroes
                .OrderByDescending(h => h.CurrentSpeed)
                .ThenBy(h => h.InstanceId)
                .Select(h => new TurnSlot(h.InstanceId))
                .ToList();
            _turnOrderIndex = 0;
            AdvanceToNextLivingHero();
        }

        private void AdvanceToNextLivingHero()
        {
            int safetyLimit = _turnOrder.Count + 1;
            int checked_ = 0;
            while (checked_ < safetyLimit)
            {
                if (_turnOrderIndex >= _turnOrder.Count)
                {
                    _turnOrderIndex = 0;
                    CurrentTurnNumber++;
                }
                TurnSlot slot = _turnOrder[_turnOrderIndex];
                // If unit is still alive (would be confirmed by CombatHeroManager)
                if (!slot.IsDead)
                {
                    ActiveHeroInstanceId = slot.HeroInstanceId;
                    OnTurnChanged?.Invoke(ActiveHeroInstanceId);
                    return;
                }
                _turnOrderIndex++;
                checked_++;
            }
            // All heroes on one side dead — battle over
            _phaseMachine.TransitionTo(CombatPhase.BattleOver);
        }

        /// <summary>Mark a hero as dead in the turn order (does not remove, preserves order for display).</summary>
        public void MarkHeroDead(int heroInstanceId)
        {
            TurnSlot slot = _turnOrder.FirstOrDefault(s => s.HeroInstanceId == heroInstanceId);
            if (slot != null) slot.IsDead = true;
        }

        /// <summary>Called by ActionPhaseState when the player has finished playing cards.</summary>
        public void EndActionPhase()
        {
            _phaseMachine.TransitionTo(CombatPhase.Resolve);
        }

        /// <summary>Called by ResolvePhaseState when all effects have resolved.</summary>
        public void EndResolvePhase()
        {
            _phaseMachine.TransitionTo(CombatPhase.End);
        }

        /// <summary>Called by EndPhaseState after tile ticks and status decrements.</summary>
        public void EndTurn()
        {
            _turnOrderIndex++;
            AdvanceToNextLivingHero();
            _phaseMachine.TransitionTo(CombatPhase.Draw);
        }

        private void Update() => _phaseMachine.Tick(Time.deltaTime);
    }

    public enum CombatPhase { Initiative, Draw, Action, Resolve, End, BattleOver }

    public class TurnSlot
    {
        public int HeroInstanceId;
        public bool IsDead;
        public TurnSlot(int id) { HeroInstanceId = id; }
    }

    // Phase state stubs — full implementation in Phase 1
    public class InitiativePhaseState : IState<CombatPhase> { public InitiativePhaseState(TurnManager tm) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }
    public class DrawPhaseState : IState<CombatPhase> { public DrawPhaseState(TurnManager tm, CardHandManager cm) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }
    public class ActionPhaseState : IState<CombatPhase> { public ActionPhaseState(TurnManager tm) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }
    public class ResolvePhaseState : IState<CombatPhase> { public ResolvePhaseState(TurnManager tm, CombatGrid cg) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }
    public class EndPhaseState : IState<CombatPhase> { public EndPhaseState(TurnManager tm, CombatGrid cg) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }
    public class BattleOverPhaseState : IState<CombatPhase> { public BattleOverPhaseState(TurnManager tm) { } public void Enter(CombatPhase p, CombatPhase c) { } public void Tick(float dt) { } public void Exit(CombatPhase c, CombatPhase n) { } }

    // --- Events ---
    public readonly struct CombatPhaseChangedEvent { public readonly CombatPhase Phase; public CombatPhaseChangedEvent(CombatPhase p) { Phase = p; } }
}
