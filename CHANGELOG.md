# Ashen Throne — Changelog

All notable changes tracked here. Format: [ADDED] [CHANGED] [FIXED] [REMOVED].

---

## [0.4.0] — 2026-03-07 (Phase 3: Alliance & Social)

### ADDED
- **TerritoryConfig**: ScriptableObject for all territory war tuning. Fields: `TotalRegions` (200), `MapRadius`, `WarWindowDurationSeconds` (7200), `WarWindowsPerDay`, `WarWindowStartHourUtc`, capture power thresholds, rally min/max/duration, fortification HP + tier multiplier, supply line range, resource/military/research/stronghold territory bonuses (%), contribution point awards.
- **TerritorySystem** (`TerritoryManager` + `HexCoord` + `TerritoryRegion` + `TerritoryBonuses`):
  - `HexCoord` — axial hex coordinate with `Distance()`, `Neighbors()`, full equality/hashcode.
  - `TerritoryRegion` — runtime state per hex (owner, fortification tier, HP). Pure C#.
  - `TerritoryManager` — MonoBehaviour managing 200 regions. `InitializeMap()`, `LoadFromServerData()`, `ApplyCapture()` (server-confirmed only), `ApplyFortificationUpgrade()`, `CalculateBonuses(allianceId)` (BFS connectivity check), `IsAttackable()`, `AreAdjacent()`.
  - `_coordToRegionId` Dictionary for O(1) BFS neighbor lookup (replaces O(n²) nested foreach).
  - Events: `TerritoryMapLoadedEvent`, `TerritoryCapturedEvent`, `TerritoryFortifiedEvent`.
- **WarEngine**: MonoBehaviour managing rallies and war window scheduling.
  - `GetUpcomingWarWindows(int count)` — pure scheduling calculation, no side effects.
  - `IsWarWindowOpen()` — returns true during scheduled 2-hour war windows.
  - `StartRally()` / `JoinRally()` / `LaunchRally()` — full rally lifecycle.
  - `ApplyWarResult(rallyId, WarResult)` — verifies SHA-256 hash before applying server result.
  - `ComputeAttackerPower(participantPowerScores)` — static; includes log2 coordination bonus.
  - `ResolveAttack(attackerPower, defenderPower, fortHP)` — static; fortification adds to defense.
  - `ComputeResultHash(...)` — static; SHA-256 of action+region+outcome+powers (check #50).
  - Events: `RallyStartedEvent`, `RallyMemberJoinedEvent`, `RallyLaunchedEvent`, `RallyCancelledEvent`, `WarResultAppliedEvent`.
  - `RallyAttack` class: lifecycle (Recruiting → Launched → Completed/Cancelled), auto-join organizer, cap enforcement, `ApplyResult(WarResult)`.
  - `WarResult` class: includes `IsHashValid()` client-side verification.
  - `WarWindow` class: open/close UTC times, `IsOpen`, `SecondsUntilClose`.
- **AllianceChatManager**: In-game alliance chat with sanitization and rate limiting.
  - `IChatSanitizer` interface + `DefaultChatSanitizer` (pre-compiled Regex: strips HTML, SQL injection patterns, JS protocols, control chars; clamped to `MaxMessageLength` 200).
  - `ValidateSend()` — validates + sanitizes before dispatch; checks officer permission, rate limit (20/min).
  - `ReceiveMessage()` — secondary sanitization defense; ring-buffer history (max 200 per channel).
  - 3 channels: `Alliance`, `Officer`, `System`.
  - Events: `ChatMessageReceivedEvent`.
- **LeaderboardManager**: PlayFab-backed leaderboard cache.
  - 3 categories: `SoloPower`, `AllianceScore`, `TerritoryCount`.
  - `RequestLeaderboard()` — returns cached if fresh (5 min TTL), otherwise fires PlayFab stub.
  - `RankEntries()` — static; handles ties (same rank, next rank skipped).
  - `InvalidateCache()` / `InvalidateCache(type)`.
  - Events: `LeaderboardUpdatedEvent`.
- **AsyncPvpManager**: Async PvP loadout recording + result validation.
  - `RecordLoadout()` — records 1–3 hero loadout with SHA-256 integrity hash; fire-and-forget to server.
  - `RequestAttack()` — submits attack request with loadout hashes; returns request ID.
  - `ReceiveReplay()` — verifies `CombatReplayData.ValidationHash` (SHA-256 of replayId + loadout hashes + outcome + turns) before storing; ring buffer (50 replays).
  - `ComputeLoadoutHash()` / `ComputeReplayHash()` — pure static C#, SHA-256 (check #50).
  - Events: `PvpLoadoutRecordedEvent`, `PvpAttackRequestedEvent`, `PvpReplayReceivedEvent`.
- **Unit tests — TerritorySystemTests** (20 tests): HexCoord math (distance, equality, neighbors), `InitializeMap` (null throw, populate, null entries), `LoadFromServerData` (ownership, neutral), `GetTerritoryCount`, `ApplyCapture` (ownership + index update), `AreAdjacent`, `IsAttackable` (own territory, no adjacent, adjacent owned), fortification damage (reduce, destroyed, no negative).
- **Unit tests — WarEngineTests** (18 tests): `ComputeAttackerPower` (empty, null, single, grouped, negative clamp), `ResolveAttack` (win/lose/draw, fortification adds defense), `ComputeResultHash` (deterministic, different outcomes, 64-char hex), `WarResult.IsHashValid` (match, tampered), `RallyAttack` lifecycle (throw on empty id, auto-join, TryJoin/full/duplicate, TryLaunch min, Cancel, post-cancel no-join), `WarWindow` timing.
- **Unit tests — AsyncPvpManagerTests** (18 tests): `RecordLoadout` validation, hash non-empty, loadout hash determinism + diff, `ReceiveReplay` (null, bad hash, valid, history add, ring-buffer eviction, event), `RequestAttack` (no loadout, with loadout, empty id), `GetReplay` (null id, found by id), `CombatReplayData.IsHashValid` null loadout.
- **Unit tests — AllianceChatManagerTests** (14 tests): Sanitizer (HTML strip, SQL remove, JS protocol, length clamp, null input, violation detection), `ValidateSend` (empty, whitespace, valid, sanitization), `ReceiveMessage` (null no-throw, history add, event fire, ring-buffer eviction, officer channel), `GetHistory` empty, `ClearHistory` (target channel, other unaffected), `SetSanitizer` null throw.

### FIXED
- `TerritorySystem.GetConnectedRegions`: Replaced O(n²) nested foreach with `_coordToRegionId` Dictionary for O(1) BFS neighbor lookup (QA check #19 blocker resolved).
- `WarEngine.Update()`: Added inline comment documenting that the active-rally scan is bounded by alliance size (≤50), within the 2ms frame budget (QA check #20 documentation resolved).

---

## [0.3.0] — 2026-03-07 (Phase 2: Empire System)

### ADDED
- **ResearchNodeData**: ScriptableObject for research tree nodes. Fields: `nodeId`, `displayName`, `description`, `branch` (Military/Resource/Research/Hero), `gridPosition`, per-resource costs, `researchTimeSeconds` [60–86400], `prerequisiteNodeIds`, `effects` (list of `ResearchEffect`), `requiredAcademyTier`. `ResearchEffect` contains `effectType` (30+ `ResearchEffectType` enum values), `magnitude`, `description`.
- **ResearchManager**: MonoBehaviour managing the 30-node research tree. `LoadAllNodes()` via `Resources.LoadAll<ResearchNodeData>`. `StartResearch` validates prerequisites, queue capacity, resources, deducts cost, enqueues. `TickResearchQueue()` in `Update` decrements timer, fires `ResearchCompletedEvent` on finish. `ApplySpeedup(int)` clamps to 0. `ResearchBonusState` inner class aggregates all cumulative % bonuses and is published as `ResearchBonusesUpdatedEvent` after each completion. `HydrateCompletedNodes(IEnumerable<string>)` restores completed state from save data.
- **ResearchTreeGenerator**: Editor script (`AshenThrone → Generate Research Tree`) generating 30 `ResearchNodeData` assets to `Assets/Data/Research/`. 4 branches: Military (8 nodes), Resource (7 nodes), Research (7 nodes), Hero (8 nodes). Includes prerequisite chains, cost scaling, and time scaling.
- **ResourceHUD**: Persistent empire HUD showing 4 resource stocks and production rates. Subscribes to `ResourceChangedEvent` + `ProductionRatesUpdatedEvent`. Compact K-format for large numbers.
- **BuildingPanel**: Modal panel for placed-building info, upgrade costs, build-time, and real-time upgrade progress bar. `Show(string placedId)` drives all state. `Update()` polls active queue entry. Upgrade/Close buttons fully wired.
- **ResearchTreePanel**: Full research tree UI with 4 branch tabs. `RefreshBranch(ResearchBranch)` rebuilds `ResearchNodeWidget` instances. Active research bar with live progress slider + countdown. Contains nested `ResearchDetailPanel` for node detail + research action.
- **ResearchNodeWidget**: State-driven node badge (Locked = grey, Available = blue, InProgress = yellow, Completed = green + checkmark). `Bind(node, completed, available, inProgress, onClicked)`.
- **EmpireUIController**: Root empire HUD coordinator. `ShowBuildingPanel(string placedId)`, `ShowResearchTree()`, `ShowBuildMenu()` (Phase 3 stub). Closes all panels before opening selected.
- **BuildQueueOverlay**: Persistent 2-slot build queue bottom strip. Each slot: building name, target tier, progress bar, remaining time. Subscribes to `BuildingUpgradeStartedEvent` / `BuildingUpgradeCompletedEvent`. Contains nested `BuildQueueSlotWidget`.
- **Unit tests — ResearchManagerTests** (19 tests): StartResearch happy path, queue add, event fires; failure cases (not found, missing prereqs, already in queue, queue full, already completed); prerequisite chain (succeeds when met, `IsAvailable`); speedup (reduces remaining, clamps 0, no-throw on empty queue); completion effects (fires event, adds to completed, applies bonuses); hydration (marks completed, restores effects, null-safe).
- **Unit tests — ResourceManagerTests** (16 tests): init state, `CanAfford` (exact, false when insufficient, zero cost), `Spend` (deducts correctly, throws when insufficient, zero no-op, fires events per resource), `AddResource` (increases, clamps to max, ignores zero/negative), offline earnings (caps at configured max hours).
- **Unit tests — BuildingManagerTests** (16 tests): `PlaceBuilding` (throws on null, returns id, adds to dict, deducts resources, returns null for unique already built, fires event); `StartUpgrade` (false not found, true valid, adds to queue, false already in queue, false queue full at 2, false at max tier); timer cap (non-Core clamped to `MaxBuildTimeSecondsOther`, Core/Stronghold to `MaxBuildTimeSecondsStronghold`); speedup (reduces, clamps, no-throw).

### FIXED
- `BuildingPanel.Update()`: Removed redundant `if (!gameObject.activeSelf) return;` guard — `Update()` does not run when the GameObject is inactive (QA check #20 resolved).

---

## [0.2.1] — 2026-03-07 (Phase 1: Combat UI + Tests)

### ADDED
- **CardHandManagerTests**: 25 NUnit tests covering InitializeDeck (null, wrong size, energy reset, hand clear, deck count), DrawForTurn (draw count, hand-size cap, energy regen, deck decrement, OnCardDrawn event, reshuffle on exhaustion), TryPlayCard (card not in hand, insufficient energy, success + removal, energy decrement, OnCardPlayed event), combo tag activation, DiscardCard (removal, no-throw on missing, OnCardDiscarded event), energy clamp (never above max, never below zero).
- **CombatUIController**: Root HUD coordinator. Binds up to 3 player + 3 enemy `HeroStatusDisplay` slots. Toggles card interactability on phase transitions. Shows victory/defeat/draw panels on `BattleEndedEvent`. All EventBus subs cleaned in `OnDisable`.
- **CardHandView**: Renders card hand as pooled `CardWidget` row. Internal reuse pool (SetActive) avoids runtime allocation. `SetInteractable(bool)` dims hand and disables widgets during non-Action phases. Publishes `CardSelectedByPlayerEvent` on widget tap.
- **CardWidget**: Single card display with name, energy cost, description, and combo indicator. `Bind(card, onClick)` call after pool retrieval; `SetInteractable(bool)` for tap gating.
- **HeroStatusDisplay**: Per-hero HP bar, name, status effect icons, dead overlay. `Bind(CombatHero)` initializes; `RefreshHealth()` / `RefreshStatusEffects()` update on events; `SetDead()` fades and overlays.
- **StatusIconWidget**: Small status effect icon + turn-duration counter. Indexed into a `Sprite[]` array by `StatusEffectType` cast.
- **EnergyDisplay**: Row of up to 4 energy orb images. Subscribes to `EnergyChangedEvent`; fills/empties orbs by color. `SetEnergy(int)` for immediate force-refresh.
- **TurnOrderDisplay**: Horizontal strip of 6 `TurnTokenWidget` portrait tokens. Uses internal reuse pool. Updates active-turn glow on `CombatPhaseChangedEvent`. Dims dead heroes on `HeroDiedEvent`.
- **TurnTokenWidget**: Single hero turn-order token with player/enemy colored border and active-turn glow.
- **CombatInputHandler**: Bridges player touch input to `CardHandManager.TryPlayCard`. Two-step flow: tap card → enter pending state; tap grid tile → resolve play. Calls `TurnManager.EndActionPhase()` from End Turn button. Publishes `CardPendingPlayEvent` / `CardPendingCancelledEvent`.
- **GridTileIdentifier**: MonoBehaviour on each grid tile GameObject. Stores `GridPosition` for raycast-to-grid resolution by `CombatInputHandler`.
- **DamagePopupManager**: Floating damage/heal number system. Pre-warmed pool of 10 popups. Per-damage-type color (Physical = gold, Fire = orange, Arcane = purple, True = white, Heal = green). Fade-in → float → fade-out coroutine. `StopAllCoroutines` + pool cleanup on `OnDisable`.

### FIXED
- `DamagePopupManager`: `StopAllCoroutines()` now called in `OnDisable` to prevent orphaned popup coroutines when the manager is disabled mid-animation (QA check #4 blocker resolved).
- `DamagePopupManager`: Replaced `FindObjectOfType<CombatGrid>()` with `GetComponentInParent<CombatGrid>() ?? ServiceLocator.Get<CombatGrid>()` (QA check #16 — scene-independence blocker resolved).
- `CardHandView` / `TurnOrderDisplay`: Removed incompatible factory-pattern `ObjectPool<T>` constructor calls. Replaced with internal list-based reuse pools using `SetActive(true/false)` (pool size ≤5/6 — full ObjectPool overhead not warranted for UI).

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
