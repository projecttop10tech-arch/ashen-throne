using System;
using System.Collections.Generic;
using UnityEngine;
using AshenThrone.Core;
using AshenThrone.Data;

namespace AshenThrone.Combat
{
    /// <summary>
    /// Runtime state of a hero during a single combat encounter.
    /// Derived from HeroData + player progression. Destroyed at battle end.
    /// Does NOT persist — all persistent data lives in Heroes.HeroRoster.
    /// </summary>
    public class CombatHero
    {
        public int InstanceId { get; }
        public HeroData Data { get; }
        public bool IsPlayerOwned { get; }

        // --- Current Stats ---
        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentAttack { get; private set; }
        public int CurrentDefense { get; private set; }
        public int CurrentSpeed { get; private set; }
        public float CurrentCritChance { get; private set; }

        public bool IsAlive => CurrentHealth > 0;

        // --- Status Effects ---
        private readonly List<ActiveStatusEffect> _statusEffects = new();
        public IReadOnlyList<ActiveStatusEffect> StatusEffects => _statusEffects;

        // --- Shield ---
        private int _shieldAmount;

        public event Action<int, int> OnHealthChanged;      // (oldHp, newHp)
        public event Action<StatusEffectType, int> OnStatusApplied;  // (type, duration)
        public event Action<StatusEffectType> OnStatusRemoved;
        public event Action<int> OnDied;                    // instanceId

        private static int _nextInstanceId = 1;

        /// <param name="data">Hero definition ScriptableObject.</param>
        /// <param name="playerLevel">Hero's current level (1–80).</param>
        /// <param name="isPlayerOwned">True for player squad, false for AI enemies.</param>
        /// <param name="progressionConfig">Config owning the stat growth rate. Must not be null.</param>
        public CombatHero(HeroData data, int playerLevel, bool isPlayerOwned, Heroes.ProgressionConfig progressionConfig)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (progressionConfig == null) throw new ArgumentNullException(nameof(progressionConfig));
            InstanceId = _nextInstanceId++;
            IsPlayerOwned = isPlayerOwned;
            InitializeStats(playerLevel, progressionConfig);
        }

        /// <summary>Reset static instance ID counter. Call ONLY from test setup for deterministic IDs.</summary>
        internal static void ResetInstanceIdForTesting() => _nextInstanceId = 1;

        private void InitializeStats(int playerLevel, Heroes.ProgressionConfig config)
        {
            // Stat growth rate sourced from ProgressionConfig.StatGrowthPerLevel — tune there, not here.
            float levelMultiplier = 1f + config.StatGrowthPerLevel * (playerLevel - 1);
            MaxHealth = Mathf.RoundToInt(Data.baseHealth * levelMultiplier);
            CurrentHealth = MaxHealth;
            CurrentAttack = Mathf.RoundToInt(Data.baseAttack * levelMultiplier);
            CurrentDefense = Mathf.RoundToInt(Data.baseDefense * levelMultiplier);
            CurrentSpeed = Data.baseSpeed;
            CurrentCritChance = Data.baseCritChance;
        }

        /// <summary>
        /// Apply damage after defense mitigation and shield absorption.
        /// Returns actual damage dealt.
        /// </summary>
        public int TakeDamage(int rawDamage, DamageType damageType)
        {
            if (!IsAlive) return 0;

            int mitigated = damageType == DamageType.True
                ? rawDamage
                : Mathf.Max(1, rawDamage - CurrentDefense / 4);

            int afterShield = AbsorbWithShield(mitigated);
            int oldHp = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - afterShield);
            OnHealthChanged?.Invoke(oldHp, CurrentHealth);
            EventBus.Publish(new HeroDamagedEvent(InstanceId, afterShield, damageType, CurrentHealth));

            if (CurrentHealth == 0)
                Die();

            return afterShield;
        }

        /// <summary>
        /// Restore HP up to MaxHealth. Returns actual amount healed.
        /// </summary>
        public int Heal(int amount)
        {
            if (!IsAlive) return 0;
            int oldHp = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + Mathf.Max(0, amount));
            int healed = CurrentHealth - oldHp;
            if (healed > 0)
            {
                OnHealthChanged?.Invoke(oldHp, CurrentHealth);
                EventBus.Publish(new HeroHealedEvent(InstanceId, healed, CurrentHealth));
            }
            return healed;
        }

        /// <summary>
        /// Apply a shield that absorbs the next N damage.
        /// </summary>
        public void ApplyShield(int amount) => _shieldAmount += Mathf.Max(0, amount);

        /// <summary>
        /// Apply a status effect. Refreshes duration if already active.
        /// </summary>
        public void ApplyStatus(StatusEffectType type, int duration, float procChance = 1f)
        {
            if (type == StatusEffectType.None) return;
            if (UnityEngine.Random.value > procChance) return;

            ActiveStatusEffect existing = _statusEffects.Find(s => s.Type == type);
            if (existing != null)
            {
                existing.RemainingTurns = Mathf.Max(existing.RemainingTurns, duration);
            }
            else
            {
                _statusEffects.Add(new ActiveStatusEffect(type, duration));
                ApplyStatModifications(type, true);
            }
            OnStatusApplied?.Invoke(type, duration);
            EventBus.Publish(new StatusAppliedEvent(InstanceId, type, duration));
        }

        /// <summary>
        /// Decrement status durations. Called at end of each of this hero's turns.
        /// Removes expired effects.
        /// </summary>
        public void TickStatusEffects()
        {
            for (int i = _statusEffects.Count - 1; i >= 0; i--)
            {
                _statusEffects[i].RemainingTurns--;
                if (_statusEffects[i].RemainingTurns <= 0)
                {
                    StatusEffectType removed = _statusEffects[i].Type;
                    _statusEffects.RemoveAt(i);
                    ApplyStatModifications(removed, false);
                    OnStatusRemoved?.Invoke(removed);
                    EventBus.Publish(new StatusRemovedEvent(InstanceId, removed));
                }
            }
        }

        public bool HasStatus(StatusEffectType type) => _statusEffects.Exists(s => s.Type == type);

        public void ModifyAttack(int delta) => CurrentAttack = Mathf.Max(0, CurrentAttack + delta);
        public void ModifyDefense(int delta) => CurrentDefense = Mathf.Max(0, CurrentDefense + delta);
        public void ModifySpeed(int delta) => CurrentSpeed = Mathf.Max(1, CurrentSpeed + delta);

        private int AbsorbWithShield(int damage)
        {
            if (_shieldAmount <= 0) return damage;
            int absorbed = Mathf.Min(_shieldAmount, damage);
            _shieldAmount -= absorbed;
            EventBus.Publish(new ShieldAbsorbedEvent(InstanceId, absorbed, _shieldAmount));
            return damage - absorbed;
        }

        private void ApplyStatModifications(StatusEffectType type, bool applying)
        {
            int multiplier = applying ? 1 : -1;
            switch (type)
            {
                case StatusEffectType.Slow:   ModifySpeed(-3 * multiplier); break;
                case StatusEffectType.Enraged: ModifyAttack(CurrentAttack / 2 * multiplier); ModifyDefense(-CurrentDefense / 3 * multiplier); break;
                case StatusEffectType.Marked: break; // Handled by attacker
            }
        }

        private void Die()
        {
            _statusEffects.Clear();
            _shieldAmount = 0;
            OnDied?.Invoke(InstanceId);
            EventBus.Publish(new HeroDiedEvent(InstanceId, IsPlayerOwned));
        }
    }

    public class ActiveStatusEffect
    {
        public StatusEffectType Type;
        public int RemainingTurns;
        public ActiveStatusEffect(StatusEffectType t, int d) { Type = t; RemainingTurns = d; }
    }

    // --- Events ---
    public readonly struct HeroDamagedEvent { public readonly int HeroId; public readonly int Damage; public readonly DamageType DamageType; public readonly int RemainingHp; public HeroDamagedEvent(int id, int d, DamageType t, int hp) { HeroId = id; Damage = d; DamageType = t; RemainingHp = hp; } }
    public readonly struct HeroHealedEvent { public readonly int HeroId; public readonly int Amount; public readonly int CurrentHp; public HeroHealedEvent(int id, int a, int hp) { HeroId = id; Amount = a; CurrentHp = hp; } }
    public readonly struct HeroDiedEvent { public readonly int HeroId; public readonly bool WasPlayerOwned; public HeroDiedEvent(int id, bool p) { HeroId = id; WasPlayerOwned = p; } }
    public readonly struct StatusAppliedEvent { public readonly int HeroId; public readonly StatusEffectType Type; public readonly int Duration; public StatusAppliedEvent(int id, StatusEffectType t, int d) { HeroId = id; Type = t; Duration = d; } }
    public readonly struct StatusRemovedEvent { public readonly int HeroId; public readonly StatusEffectType Type; public StatusRemovedEvent(int id, StatusEffectType t) { HeroId = id; Type = t; } }
    public readonly struct ShieldAbsorbedEvent { public readonly int HeroId; public readonly int Absorbed; public readonly int RemainingShield; public ShieldAbsorbedEvent(int id, int a, int r) { HeroId = id; Absorbed = a; RemainingShield = r; } }
}
