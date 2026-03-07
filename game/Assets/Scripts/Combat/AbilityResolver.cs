using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Resolves ability card effects against combat targets.
    /// Single responsibility: given a card, caster, and target position, compute and apply all effects.
    /// No side effects outside of CombatHero mutations and EventBus events.
    /// All damage formulas sourced from CombatConfig — no magic numbers in this class.
    /// </summary>
    public class AbilityResolver
    {
        private readonly CombatGrid _grid;
        private readonly Dictionary<int, CombatHero> _heroById;
        private readonly CombatConfig _config;

        public AbilityResolver(CombatGrid grid, Dictionary<int, CombatHero> heroById, CombatConfig config)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _heroById = heroById ?? throw new ArgumentNullException(nameof(heroById));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Resolve a card played by the caster at the given target position.
        /// Handles: damage, healing, status application, terrain effects, area-of-effect, combo bonuses.
        /// Returns a summary of all effects that occurred.
        /// </summary>
        public AbilityResolutionResult Resolve(
            AbilityCardData card,
            CombatHero caster,
            GridPosition targetPosition,
            bool comboActivated)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (caster == null) throw new ArgumentNullException(nameof(caster));

            var result = new AbilityResolutionResult(card.cardId, caster.InstanceId);

            // Gather all targets based on card targeting type
            List<CombatHero> targets = GatherTargets(card, caster, targetPosition);

            foreach (CombatHero target in targets)
            {
                if (target == null || !target.IsAlive) continue;

                switch (card.cardType)
                {
                    case CardType.Attack:
                        ResolveAttack(card, caster, target, comboActivated, result);
                        break;

                    case CardType.Heal:
                        ResolveHeal(card, caster, target, result);
                        break;

                    case CardType.Defense:
                        ResolveDefense(card, caster, target, result);
                        break;

                    case CardType.Buff:
                        ResolveStatusApplication(card, target, isDebuff: false, result);
                        break;

                    case CardType.Debuff:
                        ResolveStatusApplication(card, target, isDebuff: true, result);
                        break;

                    case CardType.TerrainEffect:
                        ResolveTerrainEffect(card, targetPosition, result);
                        break;

                    case CardType.Utility:
                        ResolveUtility(card, caster, target, result);
                        break;
                }
            }

            EventBus.Publish(new AbilityResolvedEvent(card.cardId, caster.InstanceId, result));
            return result;
        }

        // ─── Attack Resolution ────────────────────────────────────────────────────

        private void ResolveAttack(
            AbilityCardData card,
            CombatHero caster,
            CombatHero target,
            bool comboActivated,
            AbilityResolutionResult result)
        {
            // Miss check: Shadow Veil tile effect
            GridPosition? targetPos = _grid.GetUnitPosition(target.InstanceId);
            if (targetPos.HasValue)
            {
                CombatTile tile = _grid.GetTile(targetPos.Value);
                if (tile?.TileType == TileType.ShadowVeil &&
                    UnityEngine.Random.value < _config.ShadowVeilMissChance)
                {
                    result.AddMiss(target.InstanceId);
                    EventBus.Publish(new AttackMissedEvent(caster.InstanceId, target.InstanceId));
                    return;
                }
            }

            // Base damage = card.baseEffectValue * statMultiplier * relevant caster stat
            float baseStat = GetScalingStat(caster, card.scalingStat);
            float rawDamage = card.baseEffectValue + (card.statMultiplier * baseStat);

            // Combo bonus
            if (comboActivated && card.comboBonusMultiplier > 1f)
                rawDamage *= card.comboBonusMultiplier;

            // Terrain bonus: High Ground boosts ranged attacks
            GridPosition? casterPos = _grid.GetUnitPosition(caster.InstanceId);
            if (casterPos.HasValue)
            {
                CombatTile casterTile = _grid.GetTile(casterPos.Value);
                if (casterTile?.TileType == TileType.HighGround)
                    rawDamage *= _config.HighGroundDamageMultiplier;
            }

            // Marked target bonus
            if (target.HasStatus(StatusEffectType.Marked))
                rawDamage *= _config.MarkedDamageBonus;

            // Critical hit
            bool isCrit = UnityEngine.Random.value < caster.CurrentCritChance;
            if (isCrit) rawDamage *= _config.CriticalHitMultiplier;

            int finalDamage = Mathf.Max(_config.MinimumDamage, Mathf.RoundToInt(rawDamage));
            int actualDealt = target.TakeDamage(finalDamage, card.element.ToDamageType());

            result.AddDamage(target.InstanceId, actualDealt, isCrit);

            // Apply secondary status effect (with proc chance)
            if (card.applyStatusEffect != StatusEffectType.None && card.statusDuration > 0)
                ResolveStatusApplication(card, target, isDebuff: true, result);
        }

        // ─── Heal Resolution ──────────────────────────────────────────────────────

        private void ResolveHeal(
            AbilityCardData card,
            CombatHero caster,
            CombatHero target,
            AbilityResolutionResult result)
        {
            float baseStat = GetScalingStat(caster, card.scalingStat);
            int healAmount = Mathf.Max(_config.MinimumHeal, Mathf.RoundToInt(card.baseEffectValue + card.statMultiplier * baseStat));
            int actualHealed = target.Heal(healAmount);
            result.AddHeal(target.InstanceId, actualHealed);
        }

        // ─── Defense / Shield Resolution ─────────────────────────────────────────

        private void ResolveDefense(
            AbilityCardData card,
            CombatHero caster,
            CombatHero target,
            AbilityResolutionResult result)
        {
            int shieldAmount = Mathf.Max(0, Mathf.RoundToInt(card.baseEffectValue + card.statMultiplier * GetScalingStat(caster, card.scalingStat)));
            target.ApplyShield(shieldAmount);
            result.AddShield(target.InstanceId, shieldAmount);
        }

        // ─── Status Effect Application ────────────────────────────────────────────

        private void ResolveStatusApplication(
            AbilityCardData card,
            CombatHero target,
            bool isDebuff,
            AbilityResolutionResult result)
        {
            if (card.applyStatusEffect == StatusEffectType.None) return;
            target.ApplyStatus(card.applyStatusEffect, card.statusDuration, card.statusProcChance);
            result.AddStatusApplied(target.InstanceId, card.applyStatusEffect, isDebuff);
        }

        // ─── Terrain Effect ───────────────────────────────────────────────────────

        private void ResolveTerrainEffect(
            AbilityCardData card,
            GridPosition targetPosition,
            AbilityResolutionResult result)
        {
            // Terrain effect cards change tile type. The tile type to apply is encoded
            // in the card's AbilityElement field for terrain cards.
            TileType newType = card.element.ToTileType();
            if (newType == TileType.Normal) return; // No-op if element doesn't map to terrain
            _grid.SetTileType(targetPosition, newType);
            result.AddTerrainChanged(targetPosition, newType);
        }

        // ─── Utility ─────────────────────────────────────────────────────────────

        private void ResolveUtility(
            AbilityCardData card,
            CombatHero caster,
            CombatHero target,
            AbilityResolutionResult result)
        {
            // Utility effects (reposition, buff, chain) are contextual.
            // Encoded via status effects on self or target. Extend per specific card design.
            if (card.applyStatusEffect != StatusEffectType.None)
                ResolveStatusApplication(card, target, isDebuff: false, result);
        }

        // ─── Target Gathering ─────────────────────────────────────────────────────

        private List<CombatHero> GatherTargets(AbilityCardData card, CombatHero caster, GridPosition targetPosition)
        {
            var targets = new List<CombatHero>();

            switch (card.targetType)
            {
                case CardTargetType.SingleEnemy:
                    AddOccupant(targetPosition, targets);
                    if (card.splashRadius > 0)
                        AddSplashTargets(targetPosition, card.splashRadius, caster, targets, enemiesOnly: true);
                    break;

                case CardTargetType.SingleAlly:
                    AddOccupant(targetPosition, targets);
                    break;

                case CardTargetType.AllEnemies:
                    foreach (var kvp in _heroById)
                    {
                        if (kvp.Value.IsAlive && kvp.Value.IsPlayerOwned != caster.IsPlayerOwned)
                            targets.Add(kvp.Value);
                    }
                    break;

                case CardTargetType.AllAllies:
                    foreach (var kvp in _heroById)
                    {
                        if (kvp.Value.IsAlive && kvp.Value.IsPlayerOwned == caster.IsPlayerOwned)
                            targets.Add(kvp.Value);
                    }
                    break;

                case CardTargetType.Self:
                    targets.Add(caster);
                    break;

                case CardTargetType.TargetTile:
                    // Terrain effect cards — no hero target needed
                    break;

                case CardTargetType.RandomEnemy:
                    var enemies = new List<CombatHero>();
                    foreach (var kvp in _heroById)
                    {
                        if (kvp.Value.IsAlive && kvp.Value.IsPlayerOwned != caster.IsPlayerOwned)
                            enemies.Add(kvp.Value);
                    }
                    if (enemies.Count > 0)
                        targets.Add(enemies[UnityEngine.Random.Range(0, enemies.Count)]);
                    break;

                case CardTargetType.LowestHpAlly:
                    CombatHero lowestHp = null;
                    foreach (var kvp in _heroById)
                    {
                        if (!kvp.Value.IsAlive || kvp.Value.IsPlayerOwned != caster.IsPlayerOwned) continue;
                        if (lowestHp == null || kvp.Value.CurrentHealth < lowestHp.CurrentHealth)
                            lowestHp = kvp.Value;
                    }
                    if (lowestHp != null) targets.Add(lowestHp);
                    break;
            }

            return targets;
        }

        private void AddOccupant(GridPosition pos, List<CombatHero> targets)
        {
            CombatTile tile = _grid.GetTile(pos);
            if (tile?.OccupantId == null) return;
            if (_heroById.TryGetValue(tile.OccupantId.Value, out CombatHero hero) && hero.IsAlive)
                targets.Add(hero);
        }

        private void AddSplashTargets(GridPosition center, int radius, CombatHero caster, List<CombatHero> targets, bool enemiesOnly)
        {
            List<GridPosition> splashPositions = _grid.GetPositionsInRadius(center, radius);
            foreach (GridPosition pos in splashPositions)
            {
                if (pos.Equals(center)) continue; // Primary target already added
                CombatTile tile = _grid.GetTile(pos);
                if (tile?.OccupantId == null) continue;
                if (!_heroById.TryGetValue(tile.OccupantId.Value, out CombatHero hero) || !hero.IsAlive) continue;
                if (enemiesOnly && hero.IsPlayerOwned == caster.IsPlayerOwned) continue;
                if (!targets.Contains(hero))
                    targets.Add(hero);
            }
        }

        // ─── Stat Helpers ─────────────────────────────────────────────────────────

        private static float GetScalingStat(CombatHero hero, StatType stat) => stat switch
        {
            StatType.Attack   => hero.CurrentAttack,
            StatType.Defense  => hero.CurrentDefense,
            StatType.Speed    => hero.CurrentSpeed,
            StatType.MaxHealth => hero.MaxHealth,
            _                  => hero.CurrentAttack
        };
    }

    /// <summary>
    /// Immutable summary of all effects from a single ability card resolution.
    /// Used for UI feedback, replay recording, and analytics.
    /// </summary>
    public class AbilityResolutionResult
    {
        public string CardId { get; }
        public int CasterInstanceId { get; }

        private readonly List<DamageEntry> _damages = new();
        private readonly List<HealEntry> _heals = new();
        private readonly List<ShieldEntry> _shields = new();
        private readonly List<StatusEntry> _statuses = new();
        private readonly List<MissEntry> _misses = new();
        private readonly List<TerrainEntry> _terrainChanges = new();

        public IReadOnlyList<DamageEntry> Damages => _damages;
        public IReadOnlyList<HealEntry> Heals => _heals;
        public IReadOnlyList<ShieldEntry> Shields => _shields;
        public IReadOnlyList<StatusEntry> Statuses => _statuses;
        public IReadOnlyList<MissEntry> Misses => _misses;
        public IReadOnlyList<TerrainEntry> TerrainChanges => _terrainChanges;

        public AbilityResolutionResult(string cardId, int casterId) { CardId = cardId; CasterInstanceId = casterId; }

        public void AddDamage(int targetId, int amount, bool isCrit) => _damages.Add(new DamageEntry(targetId, amount, isCrit));
        public void AddHeal(int targetId, int amount) => _heals.Add(new HealEntry(targetId, amount));
        public void AddShield(int targetId, int amount) => _shields.Add(new ShieldEntry(targetId, amount));
        public void AddStatusApplied(int targetId, StatusEffectType type, bool isDebuff) => _statuses.Add(new StatusEntry(targetId, type, isDebuff));
        public void AddMiss(int targetId) => _misses.Add(new MissEntry(targetId));
        public void AddTerrainChanged(GridPosition pos, TileType newType) => _terrainChanges.Add(new TerrainEntry(pos, newType));
    }

    public readonly struct DamageEntry { public readonly int TargetId; public readonly int Amount; public readonly bool IsCrit; public DamageEntry(int t, int a, bool c) { TargetId = t; Amount = a; IsCrit = c; } }
    public readonly struct HealEntry { public readonly int TargetId; public readonly int Amount; public HealEntry(int t, int a) { TargetId = t; Amount = a; } }
    public readonly struct ShieldEntry { public readonly int TargetId; public readonly int Amount; public ShieldEntry(int t, int a) { TargetId = t; Amount = a; } }
    public readonly struct StatusEntry { public readonly int TargetId; public readonly StatusEffectType Type; public readonly bool IsDebuff; public StatusEntry(int t, StatusEffectType st, bool d) { TargetId = t; Type = st; IsDebuff = d; } }
    public readonly struct MissEntry { public readonly int TargetId; public MissEntry(int t) { TargetId = t; } }
    public readonly struct TerrainEntry { public readonly GridPosition Position; public readonly TileType NewType; public TerrainEntry(GridPosition p, TileType t) { Position = p; NewType = t; } }

    // --- Events ---
    public readonly struct AbilityResolvedEvent { public readonly string CardId; public readonly int CasterId; public readonly AbilityResolutionResult Result; public AbilityResolvedEvent(string c, int id, AbilityResolutionResult r) { CardId = c; CasterId = id; Result = r; } }
    public readonly struct AttackMissedEvent { public readonly int AttackerId; public readonly int TargetId; public AttackMissedEvent(int a, int t) { AttackerId = a; TargetId = t; } }
}
