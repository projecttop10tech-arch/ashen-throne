# Scripts/Combat/ — Tactical Card Combat System

> See root `/CLAUDE.md` for project-wide rules. Grid is 7 columns × 5 rows.

Combat is a turn-based card RPG played on a 7×5 tactical grid. Each turn follows phases: Initiative → Draw → Action → Resolve → End → (repeat or BattleOver).

## Files

| File | Class | Purpose |
|------|-------|---------|
| `TurnManager.cs` | `TurnManager` | Orchestrates phase FSM and turn order |
| `CombatGrid.cs` | `CombatGrid` | 7×5 grid: unit positions, tile terrain types |
| `CardHandManager.cs` | `CardHandManager` | 15-card deck, 5-card hand, energy management |
| `CombatHero.cs` | `CombatHero` | Runtime hero state: HP, stats, status effects |
| `AbilityResolver.cs` | `AbilityResolver` | Damage/heal/status calculation engine |
| `CombatHeroFactory.cs` | `CombatHeroFactory` | Creates CombatHero from HeroRoster + ProgressionConfig |
| `PveEncounterManager.cs` | `PveEncounterManager` | Loads PveLevelData, builds squads, grants rewards |
| `GridTileIdentifier.cs` | `GridTileIdentifier` | MonoBehaviour on each tile GameObject, stores GridPosition |
| `AbilityElementExtensions.cs` | `AbilityElementExtensions` | Maps AbilityElement → DamageType / TileType |

## Grid Layout

```
Col:  0    1    2    |  3  |    4    5    6
     [P]  [P]  [P]  | [N] |   [E]  [E]  [E]
     [P]  [P]  [P]  | [N] |   [E]  [E]  [E]
     [P]  [P]  [P]  | [N] |   [E]  [E]  [E]
     [P]  [P]  [P]  | [N] |   [E]  [E]  [E]
     [P]  [P]  [P]  | [N] |   [E]  [E]  [E]

P = Player zone  N = Neutral  E = Enemy zone
Depth: col 0/6=front, col 1/5=mid, col 2/4=back
```

## Turn Phases (StateMachine)

| Phase | State Class | What Happens |
|-------|-------------|-------------|
| `Initiative` | `InitiativePhaseState` | Compute turn order by speed stat |
| `Draw` | `DrawPhaseState` | Active hero draws cards up to hand limit (5) |
| `Action` | `ActionPhaseState` | Player selects and plays cards; energy regenerates (+2, max 4) |
| `Resolve` | `ResolvePhaseState` | Queued effects resolve, DOT ticks, death checks |
| `End` | `EndPhaseState` | Status duration decrements, advance to next hero |
| `BattleOver` | `BattleOverPhaseState` | Victory/defeat, reward distribution |

## CardHandManager Rules

- **Deck size:** 15 cards (from `CombatConfig.DeckSize`)
- **Hand size:** 5 max (from `CombatConfig.HandSize`)
- **Energy:** 4 max per turn, +2 regen each Action phase
- **Reshuffle:** Discard reshuffles into deck when deck is empty
- Call `InitializeDeck(hero)` at the start of each hero's turn (DrawPhaseState does this)
- Call `DrawForTurn()` to fill hand up to 5
- Call `TryPlayCard(index, targetPos)` — returns false if insufficient energy or invalid target

## AbilityResolver — Damage Formula

All values come from `CombatConfig` (never hardcoded):
- Base damage = `card.EffectValue * caster.AttackStat`
- Crit = `base * CombatConfig.CritMultiplier` (if crit roll succeeds)
- Mitigation = `rawDamage * (1 - target.MitigationRatio)`
- Faction bonus applied if `caster.Faction == card.FactionAffinity`

## CombatHero Instance IDs

`CombatHero._nextInstanceId` is a static counter. In tests, call `CombatHero.ResetInstanceIdForTesting()` in `[SetUp]` to prevent ID drift between tests.

## Integration Points

| System | How Combat Connects |
|--------|-------------------|
| HeroRoster | `CombatHeroFactory` reads `OwnedHero` data (level, stars, loadout) |
| ResearchManager | `CombatHeroFactory` applies `ResearchBonusState` stat modifiers |
| EventBus | Publishes: `TurnChangedEvent`, `PhaseChangedEvent`, `HeroDiedEvent`, `BattleEndedEvent`, `CardPlayedEvent`, `EnergyChangedEvent` |
| UI | `CombatUIController` subscribes to EventBus events from this system |

## Adding a New Ability Effect Type

1. Add a case to `AbilityResolver.Resolve()` for the new `CardType`
2. Add a `CardType` enum value if needed (in `AbilityCardData.cs`)
3. Add any new formula constants to `CombatConfig`
4. Update `StarterAssetGenerator.cs` if the generator should produce cards of this type
5. Write tests in `Tests/Combat/AbilityResolverTests.cs`

## Tests

`Tests/Combat/` — 4 test files:
- `CardHandManagerTests.cs` — 25 tests (draw, play, energy, reshuffle, combos)
- `AbilityResolverTests.cs` — damage calculation, targeting patterns
- `CombatHeroFactoryTests.cs` — squad creation, grid placement
- `TurnManagerTests.cs` — phase transitions, turn sequencing
