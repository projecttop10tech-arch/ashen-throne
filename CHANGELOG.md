# Ashen Throne — Changelog

All notable changes tracked here. Format: [ADDED] [CHANGED] [FIXED] [REMOVED].

---

## [0.2.0] — 2026-03-07 (Phase 1: Combat Core)

### ADDED
- **TurnManager**: Full phase state implementations replacing all Phase 0 stubs.
  - `DrawPhaseState`: Defers to CardHandManager for draw via EventBus; transitions to Action in next Tick.
  - `ActionPhaseState`: Player waits for UI `EndActionPhase()` with 30s safety timeout. AI uses 3-tactic decision system (target priority, Marked debuff setup, highest damage selection) with configurable per-action delays.
  - `ResolvePhaseState`: Removes dead heroes from grid, detects win/loss.
  - `EndPhaseState`: Applies terrain DOT, status DOT (Burn, Bleed, Poison, Regenerating), decrements status durations, checks win/loss.
  - `BattleOverPhaseState`: Determines outcome (PlayerVictory / PlayerDefeat / Draw), fires `BattleEndedEvent`.
  - `HeroTurnSkippedEvent` for Freeze/Stun handling.
  - `BattleOutcome` enum and `BattleEndedEvent`.
- **AbilityResolver**: Pure C# damage calculator for all card types and targeting patterns (QA PASS 62/62).
- **AbilityElementExtensions**: Maps `AbilityElement` to `DamageType` and `TileType`.
- **CombatHeroFactory**: Pure C# factory creating `CombatHero` from `HeroData + OwnedHero`, building card loadouts, placing squads on grid. Full null-safety and bounds checking.
- **PveLevelData**: `ScriptableObject` defining PvE encounter (enemies, terrain presets, narrative, rewards).
- **PveEncounterManager**: MonoBehaviour loading PveLevelData, building squads via `CombatHeroFactory`, applying terrain, starting `TurnManager`, granting XP rewards on victory.
- **StarterAssetGenerator**: Editor script (`AshenThrone → Generate Starter Assets`) generating:
  - 10 starter heroes (2 per faction, all 5 factions): Kaelen Ironwrath, Vorra Steelborn, Seraphyn Ashveil, Mordoc the Sundered, Lyra Thornveil, Zeph Wildmane, Aldric Stoneguard, Mira of the Pale Stone, Skaros Nightfall, Vex the Unbound.
  - 50 ability cards (5 per hero): complete with combo chains, status effects, terrain effects.
  - 20 PvE story levels across 4 chapters of The Ashfall arc.
- **Unit tests**: `AbilityResolverTests` (8 tests), `CombatHeroFactoryTests` (11 tests), `TurnManagerTests` (8 tests).

### CHANGED
- `CombatConfig`: Added `BurnDamagePerTurn`, `BleedDamagePerTurn`, `PoisonDamagePerTurn`, `RegenerationHealPerTurn` (status DOT values), `AiActionDelaySeconds`, `PlayerActionTimeoutSeconds` (moved out of code constants).
- `CardHandManager`: `CardPlayedEvent` now includes the full `AbilityCardData Card` reference (not just `cardId`) so `ActionPhaseState` can resolve immediately without lookups.
- `TurnManager.StartBattle(heroes)`: Creates `AbilityResolver` and `heroById` dict; subscribes to `HeroDiedEvent` for automatic turn order updates. `OnBattleEnded` event now carries `BattleOutcome` parameter.
- Directory structure: Added `Assets/Editor/`, `Assets/Data/Heroes/`, `Assets/Data/Cards/`, `Assets/Data/Levels/`, `Assets/Resources/Heroes/`.

### FIXED
- TurnManager phase states were all stubs with empty Enter/Tick/Exit bodies — now fully implemented.
- `AiActionDelaySeconds` and `PlayerActionTimeoutSeconds` were hardcoded `const float` values in ActionPhaseState — moved to `CombatConfig` (QA check #8 blocker resolved).
- `CombatHero.ResetInstanceIdForTesting()` was called in `PveEncounterManager.StartEncounter` (production code calling a test-only method) — removed (QA check #3/15 blocker resolved).

---

## [0.1.1] — 2026-03-07 (Phase 0 QA Pass 1)

### FIXED
- **CRITICAL** ResourceManager.Spend: `CanAfford(stone, iron, arcane, arcane)` → `CanAfford(stone, iron, grain, arcane)`. Grain check was completely bypassed, allowing players to overspend grain.
- CombatGrid: Columns corrected from 5 to 7 to match intended 3-player + 1-neutral + 3-enemy zone layout. Rows corrected from 7 to 5. Zone boundary methods updated accordingly.
- CombatGrid.IsPlayerZone/IsEnemyZone: Added explicit upper-bound guards (`>= 0 && <= 2`, `>= 4 && <= 6`) to prevent out-of-range false positives.
- BuildingManager.IsBuilt: Replaced `System.Linq.Enumerable.Any` with explicit foreach loop to eliminate LINQ allocation in placement validation path.

### ADDED
- `CombatConfig.cs`: ScriptableObject for all tunable combat balance values (deck size, energy, tile effects, mitigation, faction bonuses). Eliminates hardcoded constants from game logic.
- `EmpireConfig.cs`: ScriptableObject for all tunable empire values (vault capacities, build timers, offline earnings, queue slots). Eliminates hardcoded defaults from ResourceManager and BuildingManager.
- `CHANGELOG.md`: This file.
- `docs/API/economy.md`: PlayFab Cloud Script endpoint documentation (request/response schemas).
- `docs/GDD/MonetizationDesign.md`: Monetization pricing matrix and anti-P2W rules.

### CHANGED
- CombatGrid comment block updated to accurately describe 7x5 grid layout.
- CombatGrid zone helper methods documented with /// summary comments.

---

## [0.1.0] — 2026-03-07 (Phase 0 Initial)

### ADDED
- Project directory structure (Unity 6 + backend + tools + docs)
- Core architecture: GameManager, EventBus, ServiceLocator, StateMachine, ObjectPool
- Network: PlayFabService (stub mode until SDK installed)
- Utils: MainThreadDispatcher
- Data models: HeroData, AbilityCardData, PassiveAbilityData, BuildingData
- Combat systems: CombatGrid, TurnManager (stub phases), CardHandManager, CombatHero
- Empire systems: BuildingManager, ResourceManager
- Hero systems: HeroRoster, ProgressionConfig
- Economy: BattlePassManager
- Alliance: AllianceManager
- Events: EventEngine, EventDefinition
- Backend: PlayFab Cloud Scripts (economy.js) + Jest unit tests
- CI/CD: GitHub Actions build pipeline (Android + iOS + TestFlight + Google Play)
- Documentation: Game Design Document, Art Style Guide
- Unity package manifest (Unity 6 + PlayFab SDK)
