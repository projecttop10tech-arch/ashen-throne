using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Combat;
using AshenThrone.Data;

namespace AshenThrone.UI.Combat
{
    /// <summary>
    /// Root coordinator for all combat HUD panels.
    /// Listens to combat lifecycle events and activates/deactivates sub-panels accordingly.
    /// Does NOT own game logic — it only reads state and delegates display to sub-controllers.
    /// </summary>
    public class CombatUIController : MonoBehaviour
    {
        [Header("Sub-panels")]
        [SerializeField] private CardHandView _cardHandView;
        [SerializeField] private EnergyDisplay _energyDisplay;
        [SerializeField] private TurnOrderDisplay _turnOrderDisplay;

        [Header("Hero Status Displays")]
        [SerializeField] private HeroStatusDisplay[] _playerStatusDisplays;   // Max 3
        [SerializeField] private HeroStatusDisplay[] _enemyStatusDisplays;    // Max 3

        [Header("Battle Result Panels")]
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private GameObject _defeatPanel;
        [SerializeField] private GameObject _drawPanel;

        [Header("HUD Root")]
        [SerializeField] private GameObject _hudRoot;

        private EventSubscription _phaseChangedSub;
        private EventSubscription _battleEndedSub;
        private EventSubscription _heroDamagedSub;
        private EventSubscription _heroHealedSub;
        private EventSubscription _heroStatusAppliedSub;
        private EventSubscription _heroStatusRemovedSub;
        private EventSubscription _heroDiedSub;

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            _phaseChangedSub    = EventBus.Subscribe<CombatPhaseChangedEvent>(OnPhaseChanged);
            _battleEndedSub     = EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
            _heroDamagedSub     = EventBus.Subscribe<HeroDamagedEvent>(OnHeroDamaged);
            _heroHealedSub      = EventBus.Subscribe<HeroHealedEvent>(OnHeroHealed);
            _heroStatusAppliedSub  = EventBus.Subscribe<StatusAppliedEvent>(OnStatusApplied);
            _heroStatusRemovedSub  = EventBus.Subscribe<StatusRemovedEvent>(OnStatusRemoved);
            _heroDiedSub        = EventBus.Subscribe<HeroDiedEvent>(OnHeroDied);
        }

        private void OnDisable()
        {
            _phaseChangedSub?.Dispose();
            _battleEndedSub?.Dispose();
            _heroDamagedSub?.Dispose();
            _heroHealedSub?.Dispose();
            _heroStatusAppliedSub?.Dispose();
            _heroStatusRemovedSub?.Dispose();
            _heroDiedSub?.Dispose();
        }

        /// <summary>
        /// Called by PveEncounterManager (or combat scene manager) to bind hero data to status displays.
        /// </summary>
        public void BindCombatants(IReadOnlyList<CombatHero> playerHeroes, IReadOnlyList<CombatHero> enemyHeroes)
        {
            for (int i = 0; i < _playerStatusDisplays.Length; i++)
            {
                if (_playerStatusDisplays[i] == null) continue;
                if (i < playerHeroes.Count)
                    _playerStatusDisplays[i].Bind(playerHeroes[i]);
                else
                    _playerStatusDisplays[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < _enemyStatusDisplays.Length; i++)
            {
                if (_enemyStatusDisplays[i] == null) continue;
                if (i < enemyHeroes.Count)
                    _enemyStatusDisplays[i].Bind(enemyHeroes[i]);
                else
                    _enemyStatusDisplays[i].gameObject.SetActive(false);
            }
        }

        private void OnPhaseChanged(CombatPhaseChangedEvent evt)
        {
            bool isActionPhase = evt.Phase == CombatPhase.Action;
            _cardHandView?.SetInteractable(isActionPhase);
        }

        private void OnBattleEnded(BattleEndedEvent evt)
        {
            if (_hudRoot != null) _hudRoot.SetActive(false);

            switch (evt.Outcome)
            {
                case BattleOutcome.PlayerVictory:
                    _victoryPanel?.SetActive(true);
                    break;
                case BattleOutcome.PlayerDefeat:
                    _defeatPanel?.SetActive(true);
                    break;
                case BattleOutcome.Draw:
                    _drawPanel?.SetActive(true);
                    break;
            }
        }

        private void OnHeroDamaged(HeroDamagedEvent evt)
        {
            FindStatusDisplay(evt.HeroId)?.RefreshHealth();
        }

        private void OnHeroHealed(HeroHealedEvent evt)
        {
            FindStatusDisplay(evt.HeroId)?.RefreshHealth();
        }

        private void OnStatusApplied(StatusAppliedEvent evt)
        {
            FindStatusDisplay(evt.HeroId)?.RefreshStatusEffects();
        }

        private void OnStatusRemoved(StatusRemovedEvent evt)
        {
            FindStatusDisplay(evt.HeroId)?.RefreshStatusEffects();
        }

        private void OnHeroDied(HeroDiedEvent evt)
        {
            FindStatusDisplay(evt.HeroId)?.SetDead();
        }

        private HeroStatusDisplay FindStatusDisplay(int heroInstanceId)
        {
            foreach (var d in _playerStatusDisplays)
                if (d != null && d.BoundHeroInstanceId == heroInstanceId) return d;
            foreach (var d in _enemyStatusDisplays)
                if (d != null && d.BoundHeroInstanceId == heroInstanceId) return d;
            return null;
        }

        private void ValidateReferences()
        {
            if (_cardHandView == null)
                Debug.LogError("[CombatUIController] CardHandView not assigned.", this);
            if (_energyDisplay == null)
                Debug.LogError("[CombatUIController] EnergyDisplay not assigned.", this);
            if (_turnOrderDisplay == null)
                Debug.LogError("[CombatUIController] TurnOrderDisplay not assigned.", this);
        }
    }
}
