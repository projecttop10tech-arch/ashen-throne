using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Manages combat turn order, phase transitions, card resolution, and win condition detection.
    /// Coordinates between CardHandManager, AbilityResolver, and CombatGrid.
    /// Turn order: speed descending, instance ID ascending (lower = faster on ties).
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private CombatConfig _config;

        public int CurrentTurnNumber { get; private set; }
        public int ActiveHeroInstanceId { get; private set; }
        public CombatPhase CurrentPhase { get; private set; }

        // Exposed to phase states (internal = Combat assembly only)
        internal CombatGrid Grid => _grid;
        internal CardHandManager CardHand => _cardHand;
        internal Dictionary<int, CombatHero> HeroById => _heroById;
        internal AbilityResolver Resolver => _resolver;
        internal CombatConfig Config => _config;

        private StateMachine<CombatPhase> _phaseMachine;
        private List<TurnSlot> _turnOrder = new();
        private int _turnOrderIndex;
        private CombatGrid _grid;
        private CardHandManager _cardHand;
        private readonly Dictionary<int, CombatHero> _heroById = new();
        private AbilityResolver _resolver;
        private EventSubscription _deathSub;

        public event Action<int> OnTurnChanged;
        public event Action<CombatPhase> OnPhaseChanged;
        public event Action<BattleOutcome> OnBattleEnded;

        private void Awake()
        {
            _grid = GetComponent<CombatGrid>();
            _cardHand = GetComponent<CardHandManager>();
        }

        private CombatGrid EnsureGrid()
        {
            if (_grid == null) _grid = GetComponent<CombatGrid>();
            return _grid;
        }

        private CardHandManager EnsureCardHand()
        {
            if (_cardHand == null) _cardHand = GetComponent<CardHandManager>();
            return _cardHand;
        }

        private void OnDestroy()
        {
            _deathSub?.Dispose();
        }

        /// <summary>
        /// Begin a combat encounter with the provided heroes.
        /// All heroes must be placed on the grid before calling this.
        /// </summary>
        /// <param name="heroes">All combatants (player-owned and AI-owned combined).</param>
        /// <exception cref="ArgumentException">Heroes list is null or empty.</exception>
        /// <exception cref="InvalidOperationException">CombatConfig not assigned via Inspector.</exception>
        public void StartBattle(List<CombatHero> heroes)
        {
            if (heroes == null || heroes.Count == 0)
                throw new ArgumentException("[TurnManager] Cannot start battle with empty hero list.", nameof(heroes));
            if (_config == null)
                throw new InvalidOperationException("[TurnManager] CombatConfig not assigned in Inspector. Assign the CombatConfig ScriptableObject.");

            CurrentTurnNumber = 0;
            _heroById.Clear();
            foreach (var h in heroes) _heroById[h.InstanceId] = h;

            _resolver = new AbilityResolver(EnsureGrid(), _heroById, _config);

            _deathSub?.Dispose();
            _deathSub = EventBus.Subscribe<HeroDiedEvent>(evt => MarkHeroDead(evt.HeroId));

            _phaseMachine = new StateMachine<CombatPhase>();
            SetupStateMachine();

            BuildTurnOrder(heroes);
            _phaseMachine.TransitionTo(CombatPhase.Draw);
        }

        private void SetupStateMachine()
        {
            _phaseMachine.AddState(CombatPhase.Initiative, new InitiativePhaseState(this));
            _phaseMachine.AddState(CombatPhase.Draw, new DrawPhaseState(this));
            _phaseMachine.AddState(CombatPhase.Action, new ActionPhaseState(this));
            _phaseMachine.AddState(CombatPhase.Resolve, new ResolvePhaseState(this));
            _phaseMachine.AddState(CombatPhase.End, new EndPhaseState(this));
            _phaseMachine.AddState(CombatPhase.BattleOver, new BattleOverPhaseState(this));

            _phaseMachine.OnStateEntered += (_, next) =>
            {
                CurrentPhase = next;
                OnPhaseChanged?.Invoke(next);
                EventBus.Publish(new CombatPhaseChangedEvent(next));
            };

            _phaseMachine.Initialize(CombatPhase.Initiative);
        }

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
                if (!slot.IsDead)
                {
                    ActiveHeroInstanceId = slot.HeroInstanceId;
                    OnTurnChanged?.Invoke(ActiveHeroInstanceId);
                    return;
                }
                _turnOrderIndex++;
                checked_++;
            }
            // All heroes in turn order are dead — the battle is over
            TransitionToBattleOver();
        }

        /// <summary>Mark a hero slot as dead so it is skipped in future turn advance calls.</summary>
        public void MarkHeroDead(int heroInstanceId)
        {
            for (int i = 0; i < _turnOrder.Count; i++)
            {
                if (_turnOrder[i].HeroInstanceId == heroInstanceId)
                {
                    _turnOrder[i].IsDead = true;
                    return;
                }
            }
        }

        // ─── Phase Transition API ─────────────────────────────────────────────────

        /// <summary>Called by UI or AI when the active hero has finished playing cards.</summary>
        public void EndActionPhase() => _phaseMachine.TransitionTo(CombatPhase.Resolve);

        internal void CompleteDrawPhase() => _phaseMachine.TransitionTo(CombatPhase.Action);

        internal void CompleteResolvePhase() => _phaseMachine.TransitionTo(CombatPhase.End);

        internal void EndTurn()
        {
            _turnOrderIndex++;
            AdvanceToNextLivingHero();
            if (CurrentPhase != CombatPhase.BattleOver)
                _phaseMachine.TransitionTo(CombatPhase.Draw);
        }

        internal void TransitionToBattleOver()
        {
            if (CurrentPhase != CombatPhase.BattleOver)
                _phaseMachine.TransitionTo(CombatPhase.BattleOver);
        }

        internal void NotifyBattleEnded(BattleOutcome outcome)
        {
            _deathSub?.Dispose();
            OnBattleEnded?.Invoke(outcome);
            EventBus.Publish(new BattleEndedEvent(outcome));
        }

        private void Update() => _phaseMachine?.Tick(Time.deltaTime);
    }

    // ─── Phase States ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initiative phase: a logical starting point set by Initialize. No-op because
    /// BuildTurnOrder runs in StartBattle before the phase machine begins ticking.
    /// StartBattle immediately transitions to Draw.
    /// </summary>
    public class InitiativePhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        public InitiativePhaseState(TurnManager tm) { _tm = tm; }
        public void Enter(CombatPhase prev, CombatPhase curr) { }
        public void Tick(float dt) { }
        public void Exit(CombatPhase curr, CombatPhase next) { }
    }

    /// <summary>
    /// Draw phase: CardHandManager draws cards and regens energy automatically via
    /// CombatPhaseChangedEvent (fires after this Enter). This state defers transition
    /// to Action by one Tick so card-draw animations have a frame to begin.
    /// </summary>
    public class DrawPhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        private bool _pendingTransition;

        public DrawPhaseState(TurnManager tm) { _tm = tm; }

        public void Enter(CombatPhase prev, CombatPhase curr)
        {
            // CardHandManager.DrawForTurn() will fire via CombatPhaseChangedEvent which
            // publishes AFTER this Enter returns. Set flag so Tick advances to Action.
            _pendingTransition = true;
        }

        public void Tick(float dt)
        {
            if (!_pendingTransition) return;
            _pendingTransition = false;
            _tm.CompleteDrawPhase();
        }

        public void Exit(CombatPhase curr, CombatPhase next)
        {
            _pendingTransition = false;
        }
    }

    /// <summary>
    /// Action phase: player or AI hero plays ability cards.
    ///
    /// Player heroes: waits for EndActionPhase() from UI. 30-second safety timeout auto-ends.
    /// AI heroes: executes 3-tactic decision system with 0.6s delays between actions for
    ///            visual readability. Tactics: (1) target lowest HP ratio, (2) apply Marked
    ///            before attacking, (3) fall back to highest-value attack card.
    ///
    /// Each CardPlayedEvent triggers immediate AbilityResolver.Resolve() for the active hero.
    /// Freeze/Stun statuses skip the hero's action phase entirely.
    /// </summary>
    public class ActionPhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        private EventSubscription _cardPlayedSub;
        private bool _isAiTurn;
        private float _aiTimer;
        private float _playerTimer;
        private bool _aiDone;

        public ActionPhaseState(TurnManager tm) { _tm = tm; }

        public void Enter(CombatPhase prev, CombatPhase curr)
        {
            _aiDone = false;
            _playerTimer = 0f;
            _aiTimer = _tm.Config != null ? _tm.Config.AiActionDelaySeconds : 0.6f;

            if (!_tm.HeroById.TryGetValue(_tm.ActiveHeroInstanceId, out CombatHero active))
            {
                _tm.EndActionPhase(); // Fallback: hero not found
                return;
            }

            // Freeze/Stun: skip this hero's turn entirely
            if (active.HasStatus(StatusEffectType.Freeze) || active.HasStatus(StatusEffectType.Stun))
            {
                EventBus.Publish(new HeroTurnSkippedEvent(_tm.ActiveHeroInstanceId));
                _tm.EndActionPhase();
                return;
            }

            _isAiTurn = !active.IsPlayerOwned;

            // Subscribe before any card plays so every CardPlayedEvent is resolved
            _cardPlayedSub = EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        }

        public void Tick(float dt)
        {
            if (_isAiTurn && !_aiDone)
            {
                _aiTimer -= dt;
                if (_aiTimer <= 0f)
                {
                    _aiTimer = _tm.Config != null ? _tm.Config.AiActionDelaySeconds : 0.6f;
                    if (!TryExecuteAiAction())
                    {
                        _aiDone = true;
                        _tm.EndActionPhase();
                    }
                }
            }
            else if (!_isAiTurn)
            {
                _playerTimer += dt;
                float timeout = _tm.Config != null ? _tm.Config.PlayerActionTimeoutSeconds : 30f;
                if (_playerTimer >= timeout)
                {
                    Debug.LogWarning("[ActionPhaseState] Player action timeout — auto-ending turn.");
                    _tm.EndActionPhase();
                }
            }
        }

        public void Exit(CombatPhase curr, CombatPhase next)
        {
            _cardPlayedSub?.Dispose();
            _cardPlayedSub = null;
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (evt.Card == null) return;
            if (!_tm.HeroById.TryGetValue(_tm.ActiveHeroInstanceId, out CombatHero caster)) return;
            _tm.Resolver.Resolve(evt.Card, caster, evt.Target, evt.ComboActivated);
        }

        /// <summary>
        /// Execute one AI action. Returns true if a card was played, false if no action possible.
        /// Uses 3 tactics: target priority, Marked debuff setup, highest damage selection.
        /// </summary>
        private bool TryExecuteAiAction()
        {
            if (!_tm.HeroById.TryGetValue(_tm.ActiveHeroInstanceId, out CombatHero active)) return false;
            if (!active.IsAlive) return false;

            var hand = _tm.CardHand.Hand;
            int energy = _tm.CardHand.CurrentEnergy;
            if (hand.Count == 0 || energy <= 0) return false;

            // Gather living enemies and allies
            var enemies = new List<CombatHero>();
            var allies = new List<CombatHero>();
            foreach (var kvp in _tm.HeroById)
            {
                if (!kvp.Value.IsAlive) continue;
                if (kvp.Value.IsPlayerOwned != active.IsPlayerOwned) enemies.Add(kvp.Value);
                else allies.Add(kvp.Value);
            }
            if (enemies.Count == 0) return false;

            // Filter to affordable cards
            var playable = new List<AbilityCardData>();
            foreach (var card in hand)
            {
                if (card.energyCost <= energy) playable.Add(card);
            }
            if (playable.Count == 0) return false;

            // TACTIC 1 — Target lowest HP ratio enemy (prioritise kill shots)
            CombatHero primaryEnemy = enemies[0];
            float lowestRatio = (float)primaryEnemy.CurrentHealth / primaryEnemy.MaxHealth;
            foreach (var e in enemies)
            {
                float r = (float)e.CurrentHealth / e.MaxHealth;
                if (r < lowestRatio) { lowestRatio = r; primaryEnemy = e; }
            }

            // TACTIC 2 — Apply Marked debuff first if primary target lacks it
            AbilityCardData chosen = null;
            if (!primaryEnemy.HasStatus(StatusEffectType.Marked))
            {
                foreach (var card in playable)
                {
                    if (card.applyStatusEffect == StatusEffectType.Marked)
                    { chosen = card; break; }
                }
            }

            // TACTIC 3 — Play highest damage attack card
            if (chosen == null)
            {
                float bestDmg = -1f;
                foreach (var card in playable)
                {
                    if (card.cardType != CardType.Attack) continue;
                    float dmg = card.baseEffectValue + card.statMultiplier * active.CurrentAttack;
                    if (dmg > bestDmg) { bestDmg = dmg; chosen = card; }
                }
            }

            // Fallback — heal lowest-HP ally if below 50%
            if (chosen == null)
            {
                CombatHero mostHurtAlly = FindLowestHpHero(allies);
                if (mostHurtAlly != null && (float)mostHurtAlly.CurrentHealth / mostHurtAlly.MaxHealth < 0.5f)
                {
                    foreach (var card in playable)
                    {
                        if (card.cardType == CardType.Heal) { chosen = card; primaryEnemy = mostHurtAlly; break; }
                    }
                }
            }

            // Last resort — play any affordable card
            if (chosen == null) chosen = playable[0];

            // Determine target hero based on card targeting rules
            CombatHero targetHero = ResolveAiTarget(chosen, active, enemies, allies, primaryEnemy);
            if (targetHero == null) return false;

            GridPosition? targetPos = _tm.Grid.GetUnitPosition(targetHero.InstanceId);
            if (!targetPos.HasValue) return false;

            return _tm.CardHand.TryPlayCard(chosen, targetPos.Value);
        }

        private CombatHero ResolveAiTarget(
            AbilityCardData card, CombatHero caster,
            List<CombatHero> enemies, List<CombatHero> allies, CombatHero defaultEnemy)
        {
            switch (card.targetType)
            {
                case CardTargetType.SingleEnemy:
                case CardTargetType.AllEnemies:
                case CardTargetType.RandomEnemy:
                    return defaultEnemy;

                case CardTargetType.SingleAlly:
                case CardTargetType.AllAllies:
                case CardTargetType.LowestHpAlly:
                    return allies.Count > 0 ? FindLowestHpHero(allies) : caster;

                case CardTargetType.Self:
                    return caster;

                case CardTargetType.TargetTile:
                    return defaultEnemy; // Terrain cards use position; hero reference is a hint

                default:
                    return defaultEnemy;
            }
        }

        private static CombatHero FindLowestHpHero(List<CombatHero> heroes)
        {
            if (heroes.Count == 0) return null;
            CombatHero lowest = heroes[0];
            float lowestRatio = (float)lowest.CurrentHealth / lowest.MaxHealth;
            for (int i = 1; i < heroes.Count; i++)
            {
                float r = (float)heroes[i].CurrentHealth / heroes[i].MaxHealth;
                if (r < lowestRatio) { lowestRatio = r; lowest = heroes[i]; }
            }
            return lowest;
        }
    }

    /// <summary>
    /// Resolve phase: card effects have already been applied during Action phase.
    /// This phase removes dead heroes from the grid and checks win conditions.
    /// If all enemies (or all players) are dead the battle is over.
    /// </summary>
    public class ResolvePhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        public ResolvePhaseState(TurnManager tm) { _tm = tm; }

        public void Enter(CombatPhase prev, CombatPhase curr)
        {
            // Remove dead heroes from grid so they no longer block positions
            foreach (var kvp in _tm.HeroById)
            {
                if (!kvp.Value.IsAlive)
                    _tm.Grid.RemoveUnit(kvp.Key);
            }

            if (CheckBattleOver()) return;
            _tm.CompleteResolvePhase();
        }

        public void Tick(float dt) { }
        public void Exit(CombatPhase curr, CombatPhase next) { }

        private bool CheckBattleOver()
        {
            bool anyPlayerAlive = false;
            bool anyEnemyAlive = false;
            foreach (var kvp in _tm.HeroById)
            {
                if (!kvp.Value.IsAlive) continue;
                if (kvp.Value.IsPlayerOwned) anyPlayerAlive = true;
                else anyEnemyAlive = true;
            }
            if (!anyPlayerAlive || !anyEnemyAlive)
            {
                _tm.TransitionToBattleOver();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// End phase: applies tile-based damage, status DOT effects, and decrements status
    /// durations for the active hero. Checks for DOT-induced deaths, then advances the
    /// turn order to the next living hero.
    /// </summary>
    public class EndPhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        public EndPhaseState(TurnManager tm) { _tm = tm; }

        public void Enter(CombatPhase prev, CombatPhase curr)
        {
            if (!_tm.HeroById.TryGetValue(_tm.ActiveHeroInstanceId, out CombatHero active))
            {
                _tm.EndTurn();
                return;
            }

            CombatConfig cfg = _tm.Config;

            // 1. Terrain tile effects (fire DOT, etc.) for ALL heroes on affected tiles
            var tileEffects = _tm.Grid.GetTickEffects();
            foreach (var effect in tileEffects)
            {
                if (_tm.HeroById.TryGetValue(effect.TargetHeroId, out CombatHero hero) && hero.IsAlive)
                    hero.TakeDamage(effect.Damage, effect.Type);
            }

            // 2. Status DOT damage for active hero
            ApplyStatusDot(active, cfg);

            // 3. Decrement status effect durations (removes expired effects)
            active.TickStatusEffects();

            // 4. Check if any DOT kills ended the battle
            if (CheckBattleOver()) return;

            _tm.EndTurn();
        }

        public void Tick(float dt) { }
        public void Exit(CombatPhase curr, CombatPhase next) { }

        private static void ApplyStatusDot(CombatHero hero, CombatConfig cfg)
        {
            // Iterate over a snapshot to avoid modification during enumeration
            var effects = new List<ActiveStatusEffect>(hero.StatusEffects);
            foreach (var status in effects)
            {
                switch (status.Type)
                {
                    case StatusEffectType.Burn:
                        hero.TakeDamage(cfg.BurnDamagePerTurn, DamageType.Fire);
                        break;
                    case StatusEffectType.Bleed:
                        hero.TakeDamage(cfg.BleedDamagePerTurn, DamageType.Physical);
                        break;
                    case StatusEffectType.Poison:
                        hero.TakeDamage(cfg.PoisonDamagePerTurn, DamageType.Arcane);
                        break;
                    case StatusEffectType.Regenerating:
                        hero.Heal(cfg.RegenerationHealPerTurn);
                        break;
                }
            }
        }

        private bool CheckBattleOver()
        {
            bool anyPlayerAlive = false;
            bool anyEnemyAlive = false;
            foreach (var kvp in _tm.HeroById)
            {
                if (!kvp.Value.IsAlive) continue;
                if (kvp.Value.IsPlayerOwned) anyPlayerAlive = true;
                else anyEnemyAlive = true;
            }
            if (!anyPlayerAlive || !anyEnemyAlive)
            {
                _tm.TransitionToBattleOver();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Battle over phase: determines the outcome, fires BattleEndedEvent, and
    /// notifies OnBattleEnded listeners. Terminal state — no further transitions.
    /// </summary>
    public class BattleOverPhaseState : IState<CombatPhase>
    {
        private readonly TurnManager _tm;
        public BattleOverPhaseState(TurnManager tm) { _tm = tm; }

        public void Enter(CombatPhase prev, CombatPhase curr)
        {
            BattleOutcome outcome = DetermineOutcome();
            _tm.NotifyBattleEnded(outcome);
        }

        public void Tick(float dt) { }
        public void Exit(CombatPhase curr, CombatPhase next) { }

        private BattleOutcome DetermineOutcome()
        {
            bool anyPlayerAlive = false;
            bool anyEnemyAlive = false;
            foreach (var kvp in _tm.HeroById)
            {
                if (!kvp.Value.IsAlive) continue;
                if (kvp.Value.IsPlayerOwned) anyPlayerAlive = true;
                else anyEnemyAlive = true;
            }
            if (anyPlayerAlive && !anyEnemyAlive) return BattleOutcome.PlayerVictory;
            if (!anyPlayerAlive && anyEnemyAlive) return BattleOutcome.PlayerDefeat;
            return BattleOutcome.Draw; // Both sides eliminated simultaneously
        }
    }

    // ─── Supporting Types ─────────────────────────────────────────────────────────

    public enum CombatPhase { Initiative, Draw, Action, Resolve, End, BattleOver }

    public enum BattleOutcome { PlayerVictory, PlayerDefeat, Draw }

    public class TurnSlot
    {
        public readonly int HeroInstanceId;
        public bool IsDead;
        public TurnSlot(int id) { HeroInstanceId = id; }
    }

    // --- Events ---
    public readonly struct CombatPhaseChangedEvent { public readonly CombatPhase Phase; public CombatPhaseChangedEvent(CombatPhase p) { Phase = p; } }
    public readonly struct BattleEndedEvent { public readonly BattleOutcome Outcome; public BattleEndedEvent(BattleOutcome o) { Outcome = o; } }
    public readonly struct HeroTurnSkippedEvent { public readonly int HeroInstanceId; public HeroTurnSkippedEvent(int id) { HeroInstanceId = id; } }
}
