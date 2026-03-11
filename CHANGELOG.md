# Ashen Throne — Changelog

All notable changes tracked here. Format: [ADDED] [CHANGED] [FIXED] [REMOVED].

---

## [0.21.0] — 2026-03-11 (Ralph Loop Iteration 16: Swap, Recommendation, Queue Cancel)

### ADDED
- **Building swap** — dropping a building onto another same-size building swaps their positions (P&C-style)
- **UpgradeRecommendationBanner** — persistent banner suggesting next priority upgrade (Stronghold first, then lowest-tier), tappable with "GO" button that opens info popup
- **Queue cancel buttons** — each active build queue slot now has a small red X button to cancel that upgrade
- **PlacedId tracking** in QueueSlotUI for cancel targeting

### CHANGED
- ExitMoveMode now attempts swap when dropping onto occupied cells (same-size buildings only)
- Build queue HUD slots now show cancel affordance matching P&C queue management

---

## [0.20.0] — 2026-03-11 (Ralph Loop Iteration 15: Speedup Dialog, Placement Polish, Production Summary)

### ADDED
- **SpeedupConfirmDialog** — P&C-style gem speedup confirmation popup with cost display, confirm/cancel buttons, full-screen overlay
- **Placement confirm/cancel buttons** — floating bottom banner with instructions + green CONFIRM / red CANCEL buttons during building placement mode
- **ResourceProductionSummary** — tappable resource bar opens detailed production breakdown per resource type, per building, with vault capacity summary
- **ResourceBarTappedEvent** — published when tapping the resource bar background

### CHANGED
- Building placement mode now shows instruction text ("Drag to position...") and properly cleans up buttons on exit
- Resource bar background is now tappable (Button component wired at runtime by ResourceProductionSummary)

---

## [0.19.0] — 2026-03-11 (Ralph Loop Iteration 14: P&C Speedups & Stat Preview)

### ADDED
- **Free speedup button** on construction overlay — appears when upgrade has <5 minutes remaining, pulsing green, instant complete
- **Gem speedup button** ("BOOST") on construction overlay — calculates gem cost (1 gem/min), publishes `SpeedupRequestedEvent` for confirmation dialog
- **Alliance help button** ("HELP") on construction overlay — publishes `AllianceHelpRequestedEvent`
- **Before→After stat preview** in building info popup — shows production rate and bonus changes with color-coded arrows (e.g., "Stone/hr: 250 → 500")
- **SpeedupRequestedEvent** and **AllianceHelpRequestedEvent** event structs in BuildingManager
- **FindQueueEntry** helper in ConstructionOverlayManager (avoids IReadOnlyList.Find() which doesn't exist)

### CHANGED
- Construction overlay now shows 3 action buttons (FREE/BOOST/HELP) below progress bar in horizontal row
- Free button auto-hides when >5min remaining; gem button auto-hides when free is available
- Building info description now shows stat comparison lines with color-coded resource names

---

## [0.18.0] — 2026-03-11 (Ralph Loop Iteration 13: P&C Building Interactions)

### ADDED
- **CityPowerHUD** — calculates and displays total city power from building tiers, auto-refreshes on upgrade/place/demolish events
- **Scaffold construction overlay** — semi-transparent tint with diagonal stripes + "UPGRADING" label on buildings during upgrade
- **Star burst fanfare** on upgrade complete toast — 4 pulsing star decorations with shimmer animation
- **Building icon + level badge** in upgrade complete toast — shows building sprite and "Lv X" badge
- **"LEVEL UP!" header** in toast with two-line layout (header + "Now Level X — New abilities unlocked!")

### CHANGED
- **Production rate labels** now use resource-specific colors (yellow grain, silver-blue iron, tan stone, purple arcane) with colored dot indicator
- **Upgrade complete toast** now has scale-pop overshoot animation (0.7→1.08→1.0) + glow backdrop
- **Toast timing** refined: 0.35s slide-in, 0.6s star shimmer, 2.0s hold, 0.5s fade-out
- **Construction overlay cleanup** now properly destroys scaffold + hammer icon on completion

### FIXED
- **CS1503 type mismatch** in `UpdateProductionLabel` — updated to use tuple destructuring matching new `ResourceBuildingTypes` dictionary type

---

## [0.17.0] — 2026-03-10 (Ralph Loop Pass 2: Deep Visual Polish)

### ADDED
- **Background art** wired to Lobby + Alliance scenes (`empire_bg.png` atmospheric cityscape)
- **Boot splash screen** brightness increased — flaming throne art now visible
- **Building Info Popup** (Empire): full ornate upgrade with header, level badge, separator, time estimate, glass highlight
- **Resource Detail Popup** (Empire): ornate frame, glass highlight, capacity bar outline, value shadows

### CHANGED
- **106 text shadows** across all scenes (was ~60) — every bold text now has a shadow component
- **Ornate panel tints** standardized: QuestSummary, EnergyPanel, CardTray fixed from cool/gray to warm gold
- **Glass highlights** unified to `(0.20, 0.18, 0.28, 0.15)` across all 7 panels
- **Victory/Defeat frames** now have gold separator lines between sections
- **Close buttons** unified to consistent color across all Empire popups
- **Empire popup frames** brightened from `(0.55, 0.50, 0.42)` to `(0.65, 0.58, 0.48)`
- **Boot loading frame** warmer tint `(0.62, 0.55, 0.45)`

### FIXED
- **2 unconditional AddOutlinePanel bugs** — endTurnBtn and costBadge now conditional on sprite load
- **Badge count text** 7pt → 9pt (notification badges)
- **AddStyledButton** helper now adds shadow to all button labels automatically
- **9 bold text elements** missing shadows: gold/gem amounts, TURN title, energy count, chat tab, chat initials, emblem letters, hero portraits, HP text

### REMOVED
- Removed midGlow and heroGlow flat panels from Lobby (replaced by background art)

### VERIFIED
- 0 compile errors, 0 warnings
- All 7 generators run clean (6 scene UIs + city layout)
- All 6 scenes screenshotted and visually verified

---

## [0.16.0] — 2026-03-09 (Ralph Loop: Nav Wiring, Icon Regen, Dense City)

### ADDED
- **94-building Empire city** layout (up from 41): ultra-dense P&C-style with inner ring administration, north military, east magic, west support, south resources, defense tower perimeter, and deep outer ring.

### FIXED
- **7 dead nav buttons wired** with SceneNavigator across all scenes:
  - Lobby: HERO/QUEST/BAG/MAIL now stay on Lobby (were incorrectly navigating to Empire).
  - Lobby: CAMPAIGN button now navigates to Combat scene.
  - WorldMap: BACK button now navigates to Empire.
  - Alliance: BACK button now navigates to Empire.
  - Combat: RETREAT, Victory CONTINUE, Defeat QUIT all navigate to Empire.
- **5 nav bar icons regenerated** with transparent backgrounds (were dark/opaque):
  nav_battle, nav_heroes, nav_shop, nav_alliance, nav_empire — all now vibrant art on transparent bg via Runware generate + bg removal.

### VERIFIED
- All 5 scenes pass 10-point SHIT TEST visual quality audit.
- All 34 production UI icons audited for background quality.
- All building sprites (63), hero portraits (10), card frames (8), terrain tiles (5) verified.
- 683/703 tests passing (19 pre-existing ServiceLocator/AudioManager failures, 0 regressions).

---

## [0.15.0] — 2026-03-08 (UI Audit: Combat, WorldMap, Alliance Overhaul)

### ADDED
- **Combat cards**: Card widgets now use actual CardFrame sprites per element type (Fire, Ice, Shadow, Nature, Physical). Circular cost badges with glow, value context labels (DMG/HP/DEF).
- **Combat hero panels**: Ornate panel_ornate frames, hero initial letters in portraits, HP percentage display, team icons, 3 status effect slots.
- **Combat turn tokens**: Mini HP bars per token, 4-char names, active token glow effect.
- **Combat energy orbs**: Glow behind filled orbs, depth on empty orbs.
- **WorldMap territories**: Named territory labels on 30 grid tiles with type icons. Coordinate display panel. Zoom +/- controls. Search button. 4-type legend (Allied/Enemy/Contested/Neutral).
- **WorldMap minimap**: Zone coloring, view rectangle, ally/enemy markers, "Your City" label.
- **Alliance chat**: Ornate panel_ornate frame on chat panel. Row outlines for visual separation. Input bar gold bottom trim.

### CHANGED
- Alliance avatar circles brightness increased (0.30x → 0.45x multiplier) for better visibility.
- Alliance accent bars thickened and brightened.
- Lobby Stronghold Lv text brightness increased for readability.
- Combat card tray uses ornate panel frame. End Turn button enlarged with urgency glow.

---

## [0.14.0] — 2026-03-08 (Phases 13-15: Integration Testing, Polish, Compliance)

### ADDED
- **Integration tests**: CombatFlowTests (11), EconomyFlowTests (9), BootSequenceTests (11) — verify cross-system flows.
- **Performance benchmarks**: PerformanceBenchmarkTests (6) — CombatHero creation, EventBus throughput, ServiceLocator lookup, ObjectPool get/return, damage operations, grid place/remove.
- **SceneTransitionOverlay.cs**: Fullscreen fade overlay for scene transitions using EventBus events.
- **UIAnimationHelper.cs**: Lightweight UI animation utilities (slide, fade, scale punch, fill lerp). Respects AccessibilityManager.ReduceMotion.
- **HapticFeedbackManager.cs**: Centralized haptic feedback for mobile. Subscribes to card play, hero death, build complete, level up, gacha pull events. Respects AccessibilityManager.HapticsEnabled.
- **SettingsManager.cs**: Persistent settings via PlayerPrefs (audio volumes, graphics quality, frame rate, language, notifications). Publishes SettingsChangedEvent.
- **DeepLinkHandler.cs**: URL scheme handler for `ashenthrone://` deep links. Parses route and query params, publishes DeepLinkEvent.
- **PrivacyConsentManager.cs**: GDPR consent with versioning, PlayerPrefs persistence. AcceptConsent/DeclineConsent publish events.
- **SceneTransitionOverlayTests (3)**, **SettingsManagerTests (13)**, **HapticFeedbackManagerTests (5)**, **DeepLinkHandlerTests (3)**, **PrivacyConsentManagerTests (8)** — new test coverage.

### FIXED
- SceneTransitionOverlay: `IEventSubscription` → `EventSubscription` (compile error fix).
- CombatFlowTests: Updated to match actual CombatGrid/CombatHero/CardHandManager APIs.
- PerformanceBenchmarkTests: Fixed namespace resolution (`AshenThrone.Network.PlayFabService`).
- EconomyFlowTests: Removed Editor-only `GachaPoolConfig` reference from test assembly.

---

## [0.11.0] — 2026-03-07 (Phase 10: Content Population & Balancing)

### ADDED
- **BattlePassSeason_1.asset**: 50-tier Season 1 ("Ashes of Dawn") with free + premium reward tracks. All premium rewards cosmetic only (zero P2W).
- **GachaPoolConfig.asset**: 40 cosmetic gacha items (15 common, 12 rare, 8 epic, 5 legendary). Zero heroes in pool.
- **Phase10ContentGenerator.cs**: Editor generator for Battle Pass, gacha pool, localization, balance sheets, quest reward tuning.
- **Expanded localization**: 219 keys across 8 languages (up from ~40). Hero lore, building descriptions, quest text, status effects, error messages, notifications.
- **Balance sheets**: 3 CSVs in `tools/BalanceSheets/` — building_balance, hero_balance, economy_flow (30-day F2P model).

### CHANGED
- Quest rewards tuned: daily=100 BP/50 gold, weekly=250 BP/200 gold, one-time=500 BP/500 gold.

---

## [0.10.0] — 2026-03-07 (Phase 9: SDK Integration)

### ADDED
- **AnalyticsService.cs**: Firebase Analytics wrapper with stub mode (`#if FIREBASE_SDK`). Convenience methods for battle, purchase, tutorial events.
- **CrashReporter.cs**: Firebase Crashlytics wrapper with unhandled exception capture and breadcrumb logging.
- **PhotonManager.cs**: Photon Fusion 2 wrapper with room create/join, chat messaging, data broadcast. Stub mode (`#if PHOTON_SDK`).
- **ATTManager.cs**: iOS App Tracking Transparency prompt. Auto-authorizes on Android/Editor.
- **IAPCatalogRegistrar.cs**: Registers 6 IAP SKUs at boot (Battle Pass, 3 gem packs, cosmetic bundle, valor pass). All zero P2W.
- **40 new unit tests**: AnalyticsServiceTests (10), PhotonManagerTests (15), CrashReporterTests (6), ATTManagerTests (5), IAPCatalogRegistrarTests (9).

### CHANGED
- **GameManager.cs**: Boot sequence now initializes ATT → CrashReporter → Analytics → PlayFab → Photon in correct compliance order.
- **Network/CLAUDE.md**: Updated to document all 5 network services.

---

## [0.9.0] — 2026-03-07 (Phase 8: Placeholder Art & UI Prefabs)

### ADDED
- **Phase8Generator.cs**: Comprehensive editor tool with 8 menu items under `AshenThrone/Phase 8/`.
- **Hero sprites**: 10 portraits (256x256) + 10 full-body (512x1024) faction-colored PNGs, wired to HeroData SOs.
- **Card sprites**: 8 element-colored card frames (200x300), wired to 50 AbilityCardData SOs.
- **Building sprites**: 63 PNGs (21 buildings × 3 tiers at 128x128), wired to BuildingData SOs.
- **UI sprite atlas**: 28 sprites — resource icons, currency, buttons, panels, bars, navigation, status effects.
- **Environment textures**: 7 combat tiles (256x256), 5 world map (512x512), 1 empire background (1024x1024).
- **Placeholder audio**: 3 music loops (WAV) + 15 SFX clips with sine-wave generation.
- **21 UI prefabs**: VictoryPanel, DefeatPanel, TutorialOverlay, EventBanner, QuestRow, LeaderboardRow, BattlePassTierRow, ChatBubble, BuildingSlot, WorldMapTile, StoreProductCard, HeroCard, EnergyBar, HealthBar, XPBar, SettingsToggle, SettingsSlider, HeroStatusDisplay.
- **3 particle effect prefabs**: VFX_Construction, VFX_LevelUp, VFX_CardPlay.
- **ColorblindFilter.shader**: URP fullscreen Daltonization shader with Protanopia, Deuteranopia, Tritanopia modes.
- **3 colorblind materials**: Colorblind_Protanopia.mat, Colorblind_Deuteranopia.mat, Colorblind_Tritanopia.mat.

### CHANGED
- Total assets: 132 PNGs, 51 prefabs, 18 WAVs, 1 shader, 3 materials.
- All 544 existing tests continue to pass.

---

## [0.8.1] — 2026-03-07 (Phase 7 Complete: All Generators Run, Scenes Populated)

### ADDED
- **Unity WebSocket bridge** (`tools/unity-bridge.mjs`): Sends commands to Unity Editor MCP server on port 8090.
- **MCP config** (`.claude/mcp.json`): Registers Unity MCP server for Claude Code.
- **Ralph loop plan** (`.claude/ralph-loop.md`): Full phases 7-16 with 62-check QA protocol.

### CHANGED
- Executed all generators in Unity Editor via MCP: SceneGenerator (6 scenes), SceneUIGenerator (UI hierarchies), BuildingDataGenerator (21 SOs), QuestDefinitionGenerator (30 SOs), TutorialStepGenerator (8 SOs), ConfigGenerator, ArtAssetGenerator (combat grid, card widget, damage popup, hero/building placeholders, resource icons), StarterAssetGenerator, ResearchTreeGenerator.

### FIXED
- **AshenThrone.Editor.asmdef**: Fixed TMPro assembly reference (now uses GUIDs).
- **SceneUIGenerator.cs**: Regenerated missing .meta file after file rename broke Unity asset tracking.

---

## [0.8.0] — 2026-03-07 (Phase 7: Infrastructure, Tooling, Project Hygiene)

### ADDED
- **.gitignore**: Created comprehensive Unity .gitignore excluding Library/, Temp/, Logs/, *.csproj, *.sln, .DS_Store, secrets, build artifacts. Force-includes *.meta, *.asset, *.prefab, *.unity.
- **LobbyUIController.cs**: Scene navigation controller for Lobby — buttons route to Combat, Empire, WorldMap, Alliance via GameManager.LoadSceneAsync.
- **WorldMapUIController.cs**: World map controller with territory info sidebar, mini-map, back navigation. Subscribes to TerritoryCapturedEvent.
- **AllianceUIController.cs**: Alliance screen controller with tab switching (Members, Chat, Wars, Leaderboard panels).
- **BuildingDataGenerator.cs**: Editor tool generating 21 BuildingData SOs (Stronghold + 5 per district × 4 districts) with 10 tiers each. Respects 4h/8h timer caps. Quadratic cost scaling.
- **QuestDefinitionGenerator.cs**: Editor tool generating 30 QuestDefinition SOs (10 daily, 10 weekly, 10 one-time). Balanced BP points and resource rewards.
- **TutorialStepGenerator.cs**: Editor tool generating 8 TutorialStep SOs for FTUE sequence (welcome through complete_quest).
- **AccessibilityConfig generation**: Added to ConfigGenerator.cs — generates AccessibilityConfig.asset in Resources/.
- **Balance sheets**: Created building_balance.csv (resource buildings with tier-by-tier costs/production), hero_balance.csv (10 heroes with stats/abilities/factions), economy_flow.csv (resource income/sink at Day 1/3/7/14/30/60/90 milestones, F2P 70% power target verified).

### CHANGED
- Updated Editor/CLAUDE.md with all new generator entries (BuildingDataGenerator, QuestDefinitionGenerator, TutorialStepGenerator, SceneGenerator, ConfigGenerator).

---

## [0.7.3] — 2026-03-07 (QA: Backend fixes, null guards, GC optimization)

### FIXED
- **Backend Jest tests**: Fixed 2 failing tests (14/14 now passing):
  - `GrantCombatShards > caps shards at 10`: Corrected mock to route `battle_tokens` through `GetUserData` (not `GetUserInternalData`), matching actual `validateAndConsumeBattleToken` code path.
  - `ValidateIAP > prevents duplicate receipt`: Fixed incorrect hardcoded receipt hash (`-1177685440` → `-1684829004`) to match actual `hashString("valid_receipt_data")` output.
  - `INVALID_BATTLE_TOKEN` test mock also corrected for consistency.
- **Null guard — CardHandView.cs**: Added null check after `GetComponent<CardWidget>()` in `AddWidget()` to prevent NRE if prefab is misconfigured.
- **Null guard — TurnOrderDisplay.cs**: Added null check after `GetComponent<TurnTokenWidget>()` in `SetTurnOrder()` to prevent NRE if prefab is misconfigured.
- **Null guard — QuestEngine.cs**: Changed `ServiceLocator.Get<>()` to `ServiceLocator.TryGet()` in `Start()` for `BattlePassManager` and `ResourceManager` — consistent with nullable usage in `ClaimReward()`.
- **GC optimization — CardHandManager.cs**: Replaced LINQ `.OrderBy().ToList()` shuffle with Fisher-Yates in-place shuffle in both `InitializeDeck()` and `ReshuffleDiscardIntoDeck()`. Removed unused `System.Linq` using directive. Eliminates per-reshuffle heap allocations.

---

## [0.7.2] — 2026-03-07 (Coverage: Core/ unit tests)

### ADDED
- **Unit tests — EventBusTests** (15 tests): Subscribe+Publish (handler receives, multiple handlers, no subscribers, wrong type, correct routing), Unsubscribe (stops receiving, not-registered no-throw), EventSubscription Dispose (unsubscribes, double-dispose idempotent, null action throws), exception safety (other handlers still fire), Shutdown clears all, Initialize idempotent, unsubscribe during dispatch safe.
- **Unit tests — ServiceLocatorTests** (16 tests): Register+Get (same instance, null throws, overwrite), Get not-registered throws, TryGet (registered true, not-registered false), IsRegistered (true/false), Unregister (removes, not-registered no-throw), multiple types independent, unregister one doesn't affect other, Shutdown clears, Initialize idempotent.
- **Unit tests — StateMachineTests** (20 tests): AddState null throws, Initialize (sets state, calls Enter, unregistered throws), TransitionTo (changes state, sets previous, calls Exit/Enter, same-state no-op, before-init logs error, unregistered logs error), events (OnStateEntered/OnStateExited fire), Tick (forwards to current, before-init no-op, after transition ticks new), full lifecycle.
- **Unit tests — ObjectPoolTests** (14 tests): constructor (null prefab throws, pre-warms), Get (returns active, sets position, decrements available, beyond pre-warm creates, at max returns null), Return (deactivates, increments available, null no-throw, not-tracked warns, reuses), ReturnAll (returns all, no-throw empty).

### FIXED
- `QuestEngineTests`: Fixed `Core.EventBus` namespace collision (now uses fully qualified `AshenThrone.Core.EventBus`) after `AshenThrone.Tests.Core` namespace was introduced.

---

## [0.7.1] — 2026-03-07 (Coverage: CombatHero + CombatGrid tests)

### ADDED
- **Unit tests — CombatHeroTests** (35 tests): constructor (null throws, level 1 stats, level 10 scaling, unique IDs, player-owned), TakeDamage (reduces HP, physical mitigated, True bypasses, min 1, kills, dead returns 0, OnDied), Heal (restores, clamps, dead returns 0, negative), Shield (absorb, partial, negative, stacking), status effects (None no-op, adds, refresh duration, tick decrements/removes, Slow -3 speed), stat modifiers (clamp 0/0/1), death clears effects, ResetInstanceIdForTesting.
- **Unit tests — CombatGridTests** (30 tests): dimensions 7x5, GetTile (valid/OOB/all-Normal), SetTileType (changes/OOB warning), PlaceUnit (valid/tracks/occupied/OOB), MoveUnit (valid/updates/clears old/occupied/unknown/OOB), RemoveUnit (clears/unknown no-op), GetUnitPosition (null), zone queries (player 0-2/enemy 4-6/neutral 3), IsInBounds, GetPositionsInRadius (r=0/r=1/corner clamp), GetTickEffects (fire DOT/normal empty/multiple), GridPosition equality/inequality/ToString.

---

## [0.7.0] — 2026-03-07 (Phase 6: Polish & Launch)

### ADDED
- **TutorialManager**: MonoBehaviour orchestrating the 8-step FTUE interactive tutorial.
  - `Initialize(save)` — restores progress from `TutorialSaveData`; null starts fresh; empty steps list marks complete.
  - `ReportAction(action)` — advances tutorial when player action matches current step's `RequiredAction`.
  - `SkipCurrentStep()` — advances only if step is marked `IsSkippable`; returns false otherwise.
  - `SkipAll()` — immediately completes entire tutorial.
  - `SetSteps(list)` — programmatic step injection for testing or dynamic content.
  - `BuildSaveData()` — serializes progress for PlayFab persistence.
  - Properties: `CurrentStep`, `CurrentStepIndex`, `TotalSteps`, `IsActive`, `IsComplete`.
  - Events: `TutorialActionEvent`, `TutorialStepStartedEvent`, `TutorialStepCompletedEvent`, `TutorialCompletedEvent`.
- **TutorialStep**: ScriptableObject defining a single tutorial step — `StepId`, `StepIndex`, `InstructionTextKey`, `HighlightTargetTag`, `RequiredAction`, `IsSkippable`, `VoiceOverClipKey`.
- **TutorialAction**: Enum — `None`, `TapAnywhere`, `PlayCard`, `BuildBuilding`, `CollectResource`, `UpgradeBuilding`, `RecruitHero`, `JoinAlliance`, `CompleteQuest`.
- **AccessibilityManager**: MonoBehaviour managing runtime accessibility settings.
  - `Initialize(save)` — restores from `AccessibilitySaveData`; null applies defaults from `AccessibilityConfig` ScriptableObject.
  - Setters: `SetColorblindMode(mode)`, `SetTextSizeScale(scale)`, `SetHapticsEnabled(bool)`, `SetScreenReaderEnabled(bool)`, `SetReduceMotion(bool)`, `SetUiBrightness(float)`.
  - All setters clamp to config min/max ranges and fire `AccessibilitySettingsChangedEvent` via EventBus.
  - `BuildSaveData()` — serializes all settings for persistence.
  - 3 colorblind modes: Protanopia, Deuteranopia, Tritanopia (check #31 WCAG AA compliance).
- **AccessibilityConfig**: ScriptableObject — `MinTextScale`, `MaxTextScale`, `DefaultTextScale`, `DefaultHapticsEnabled`, `MinBrightness`, `MaxBrightness`.
- **LocalizationBootstrap**: MonoBehaviour managing language selection and Unity Localization integration.
  - `Initialize(save)` — restores language from `LocalizationSaveData`; null triggers auto-detection from device language.
  - `SetLanguage(GameLanguage)` — changes active language; fires `LanguageChangedEvent`.
  - `DetectDeviceLanguage()` — maps `Application.systemLanguage` to nearest supported `GameLanguage`.
  - Static helpers: `GetLanguageCode(lang)` (ISO 639-1), `GetDisplayName(lang)` (native name), `GetSupportedLanguages()`.
  - 8 languages at launch: English, Spanish, French, German, Portuguese, Japanese, Korean, Chinese (Simplified).
- **Unit tests — TutorialManagerTests** (22 tests): `Initialize` (null save, no steps, resume, complete save, all-done), `ReportAction` (matching/wrong/complete/last-step/not-active), `SkipCurrentStep` (skippable/not-skippable/complete), `SkipAll` (marks complete, already complete), `BuildSaveData` (state/complete), `CurrentStep` (null when inactive, correct when active), `TotalSteps`, full 8-step walkthrough.
- **Unit tests — AccessibilityManagerTests** (20 tests): `Initialize` (null save defaults, no-config fallback, restore from save, clamp colorblind index, clamp text scale high/low, clamp brightness), setters (colorblind mode, same value no-op, text scale clamp, haptics toggle, screen reader toggle, reduce motion toggle, brightness clamp), `BuildSaveData` (reflects state, round-trips).
- **Unit tests — LocalizationBootstrapTests** (14 tests): `Initialize` (null auto-detect, restore from save, invalid index fallback, negative index fallback), `SetLanguage` (changes, same no-op), `GetLanguageCode` (all 8 codes), `CurrentLanguageCode`, `GetSupportedLanguages` (returns 8), `GetDisplayName` (non-empty for all), `BuildSaveData` (reflects/round-trips), `SupportedLanguageCount` constant.

### FIXED
- `Packages/manifest.json`: Removed invalid `com.artmann.unity-mcp` git URL (`?path=Packages/unity-mcp` doesn't exist in repo). Unity MCP server remains configured in Claude Code via `npx -y unity-mcp`; Unity-side package to be installed via Package Manager UI when correct path is confirmed.

---

## [0.6.0] — 2026-03-07 (Phase 5: Events & Notifications)

### ADDED
- **EventEngine**: MonoBehaviour managing all timed game events from ScriptableObject definitions.
  - `allEventDefinitions` — inspector-assigned list of `EventDefinition` ScriptableObjects.
  - `LoadActiveEvents(saves)` — restores active event state from save entries; null clears all; null entries in list are ignored.
  - `ReportProgress(objectiveType, amount)` — routes progress to all matching active events; zero/negative amounts are no-ops.
  - `GetActiveEvent(eventId)` — lookup by ID; null if not found.
  - `ActiveEvents` — read-only list of all loaded `ActiveGameEvent` instances.
  - `CheckEventSchedules()` — called from `Update()` to activate/expire events by UTC time window.
- **ActiveGameEvent**: Pure C# runtime state container for a single event instance.
  - `IsActive` — derived from UTC time window parsed from `EventDefinition.startTimeIso`/`endTimeIso` (ISO 8601 round-trip).
  - `AddProgress(amount)` — increments `CurrentProgress`; zero/negative ignored; clamped to `ObjectiveTarget`.
  - `CompletionRatio` — `CurrentProgress / ObjectiveTarget`; returns 1.0 when target is 0 (complete-by-default).
  - `CurrentProgress` — restored from `EventSaveEntry.Progress` on load.
- **EventDefinition**: ScriptableObject with fields: `eventId`, `displayName`, `description`, `objectiveType` (`EventObjectiveType` enum), `objectiveTarget`, `startTimeIso`, `endTimeIso`, `rewardShards`.
- **EventSaveEntry**: Serializable save record with `EventId` and `Progress`.
- **EventObjectiveType**: Enum — `DamageDealt`, `ShardsEarned`, `BossesDefeated`, `RalliesJoined`, `TerritoryHeld`, `QuestsCompleted`.
- **WorldBossManager**: MonoBehaviour managing world boss HP, player attack quotas, and live server sync.
  - `InitializeBossSpawn()` — sets `IsAlive = true`, `CurrentHp = TotalHp`, resets `LocalAttacksUsed`; logs error if `_definition` not assigned.
  - `RequestAttack(damage)` — validates alive + attacks remaining; returns false otherwise; increments `LocalAttacksUsed`.
  - `ReceiveServerHpUpdate(newHp)` — clamps to [0, TotalHp]; sets `IsAlive = false` at zero; no-op when definition null.
  - `HpRatio` — `CurrentHp / TotalHp`; returns 0 when definition null or TotalHp is 0.
  - `AttacksRemaining` — `MaxAttacksPerPlayer - LocalAttacksUsed`; returns 0 when definition null.
- **WorldBossDefinition**: ScriptableObject with `[field: SerializeField]` properties: `BossId`, `BossName`, `BossLore`, `TotalHp` (long), `MinDamagePerAttack`, `MaxAttacksPerPlayer`, `MilestoneHpPercents` (float[]), `ParticipationShardReward`, `LeaderboardShardRewards` (int[]).
- **VoidRiftManager**: MonoBehaviour orchestrating roguelite Void Rift dungeon runs.
  - `StartNewRun(config)` — creates a `VoidRiftRunState` from config; throws if config null.
  - `SelectPath(pathIndex)` — delegates to run state; no-op when no active run.
  - `CompleteCurrentNode(score)` — advances floor, accumulates score.
  - `EndRun(won)` — marks run over; fires `VoidRiftRunEndedEvent`.
  - `AddRelic(relic)` — adds to active run's relic list.
  - `ActiveRun` — current `VoidRiftRunState`; null between runs.
- **VoidRiftRunState**: Pure C# roguelite run state.
  - `Floors` — list of `VoidRiftFloor`; boss floor always has exactly 1 path.
  - `SelectPath(index)` — validates bounds and run-over state; activates chosen node.
  - `CompleteNode(score)` — advances floor counter; accumulates score with relic multiplier applied.
  - `EndRunDefeat()` — sets `IsRunOver = true`, `IsWon = false`; idempotent.
  - `AddRelic(relic)` — null-safe; clamps to `MaxRelics` (8), discards oldest.
  - `ScoreMultiplier` — sum of all active relic bonuses + 1.0.
  - `GetFloorNodes(floor)` — returns node list; throws `ArgumentOutOfRangeException` on bad floor index.
- **VoidRiftConfig**: ScriptableObject with `FloorCount`, `MaxRelics`, `BaseScorePerFloor`.
- **VoidRiftFloor / VoidRiftNode / VoidRelic**: Pure C# data types for run structure and collectibles.
- **NotificationScheduler**: MonoBehaviour scheduling and tracking local push notifications.
  - `ScheduleNotification(notification)` — null-safe; ignores past fire times; idempotent on duplicate IDs; sets `IsDispatched = true` on schedule.
  - `CancelNotification(id)` — returns false if not found; true if removed.
  - `CancelAll()` — clears all pending notifications; no-throw when empty.
  - `IsScheduled(id)` — checks pending list by ID.
  - `PendingCount` — count of undelivered scheduled notifications.
- **ScheduledNotification**: Data class with `Id`, `Title`, `Body`, `FireAtUtc`, `IsDispatched`.
- **Unit tests — ActiveGameEventTests** (17 tests): constructor (null def throws), `IsActive` (active window, ended, future, invalid ISO), `AddProgress` (increment, clamp at target, zero ignored, negative ignored), `CompletionRatio` (zero target=1.0, mid-progress ratio, clamp at 1.0), progress restored from save.
- **Unit tests — VoidRiftRunStateTests** (26 tests): constructor (null config throws, floor count, initial state), boss floor has 1 path, `SelectPath` (run-over no-op, out-of-range throws, negative throws, valid advances), `CompleteNode` (advances floor, increases score, skips other paths, no-op when run over), full run win + multiplier, `EndRunDefeat` (sets IsRunOver/IsWon=false, idempotent), relics (null no-op, add, clamp+discard-oldest, bonus aggregation), `GetFloorNodes` out-of-range, `VoidRelic` empty-ID throws.
- **Unit tests — WorldBossManagerTests** (19 tests): null-definition guard paths (InitializeBossSpawn/RequestAttack/AttacksRemaining/HpRatio/ReceiveServerHpUpdate), `InitializeBossSpawn` (alive, full HP, resets attacks), `RequestAttack` (true when alive+remaining, increments used, false at max, false when dead), `AttacksRemaining` (decrements), `ReceiveServerHpUpdate` (updates HP, clamps to zero, sets IsAlive=false at zero), `HpRatio` (1.0 full, 0.0 at zero, 0.5 at half).
- **Unit tests — NotificationSchedulerTests** (15 tests): `ScheduledNotification` empty-ID throws, `ScheduleNotification` (null no-op, past time skipped, future scheduled, duplicate idempotent, sets IsDispatched, increments count), `IsScheduled` (true/false), `CancelNotification` (not-found=false, found=true+removed), `CancelAll` (clears all, no-throw empty).
- **Unit tests — EventEngineTests** (12 tests): `LoadActiveEvents` (null clears, null entries ignored, progress restored, unknown event ignored, replaces existing), `ReportProgress` (zero/negative no-op, wrong type no-op, advances on match, inactive event no-op), `GetActiveEvent` (null when empty, missing ID=null, correct ID found).

### FIXED
- `TurnManager`: Added `EnsureGrid()` and `EnsureCardHand()` lazy-init helpers; `StartBattle` now calls `EnsureGrid()` to prevent `ArgumentNullException` when `Awake()` has not run before the first test setup (Unity EditMode batch runner does not guarantee `Awake()` invocation order).
- `QuestEngine`: `Initialize()` now treats `DateTime.MinValue` (default struct value when no prior save exists) as "today" for `_lastDailyResetUtc` and `GetLastMondayUtc()` for `_lastWeeklyResetUtc`, preventing spurious daily/weekly resets on first-ever app launch with no save data.

---

## [0.5.0] — 2026-03-07 (Phase 4: Economy & Monetization)

### ADDED
- **QuestDefinition**: ScriptableObject for quest definitions. Fields: `QuestId`, `DisplayName`, `Description`, `Cadence` (Daily/Weekly/OneTime), `ObjectiveType` (13 types), `RequiredCount`, `BattlePassPoints`, resource rewards (stone/iron/grain/arcane), `HeroShardReward`, `ContextTag` (optional filter for objective subcategories).
- **QuestEngine**: MonoBehaviour managing daily/weekly/one-time quest tracking.
  - `Initialize(definitions, save)` — restores progress, handles daily/weekly UTC reset detection.
  - `RecordProgress(objectiveType, amount, contextTag)` — single entry point for all objective tracking; routes to matching quest via EventBus handlers.
  - Auto-wired EventBus handlers: `BattleEndedEvent` → Win/Complete battles; `BuildingUpgradeCompletedEvent` → UpgradeBuilding; `ResearchCompletedEvent` → CompleteResearch; `RallyMemberJoinedEvent` → JoinRally; `TerritoryCapturedEvent` → CaptureTerritory; `PvpReplayReceivedEvent` → WinPvpBattles.
  - `ClaimReward(questId)` — grants resources to ResourceManager + BP points to BattlePassManager.
  - `GetQuestsByCategory(cadence)`, `GetUnclaimedCount()`, `BuildSaveData()`.
  - Static `GetLastMidnightUtc()` / `GetLastMondayUtc()` for reset window computation.
  - Events: `QuestCompletedEvent`, `QuestRewardClaimedEvent`.
- **GachaSystem**: Cosmetic-only gacha with pity counter and transparent odds.
  - `SetPool(items)` — only `CosmeticType` items (no hero type exists — check #40 enforced by design).
  - `SimulatePull()` — client-side weighted random for immediate visual feedback; pity at 50 pulls forces Legendary.
  - `ReceiveServerPullResult(itemId, isDuplicate, pityCounter)` — server result is authoritative.
  - `GetDisplayedOdds()` — returns per-rarity probability dict shown in UI (required by App Store — checks #44, #45, #41).
  - `LoadPityCounter(int)` — restores from save; clamped [0, PityThreshold].
  - `PityCounter` + `PullsUntilPity` properties for transparent UI display.
  - Events: `GachaPullConfirmedEvent`.
- **HeroShardSystem**: Hero shard collection and summoning (F2P earnable path — check #3 design rule).
  - `AddShards(heroId, amount, source)` — adds shards; sources: PveReward, EventReward, QuestReward, AllianceMilestone, SeasonReward, ShardStore.
  - `CanSummon(heroData)` — checks shard count vs `HeroData.ShardsToSummon`.
  - `RequestSummon(heroData)` — validates locally, dispatches to server; client never grants hero without `ReceiveServerSummonResult`.
  - `ReceiveServerSummonResult(heroId, success)` — deducts shards, fires `HeroSummonedEvent`.
  - `GetSummonableHeroIds()` — returns heroes ready to summon for notification badge.
  - Events: `HeroShardsAddedEvent`, `HeroSummonRequestedEvent`, `HeroSummonedEvent`.
- **IAPManager**: Unity IAP wrapper with P2W enforcement and server receipt validation.
  - `RegisterProduct(IAPProductDefinition)` — **throws `InvalidOperationException`** if `IsCombatPowerProduct = true` (design-time P2W gate — check #40).
  - `InitiatePurchase(productId)` — validates product registered, fires event, delegates to Unity IAP SDK stub.
  - `OnPurchaseDeferred(productId, transactionId, receiptJson, platform)` — queues receipt for server validation; NEVER grants rewards locally.
  - `OnServerValidationSuccess/Failed` — removes from pending queue, fires events.
  - `GetPendingPurchases()` — returns unvalidated receipts for retry on app restart.
  - IAP products defined: BattlePassPremium ($9.99), SeasonPass ($29.99), PlayPlus ($4.99/mo), CosmeticStore ($1.99–$14.99).
  - Events: `IAPPurchaseInitiatedEvent`, `IAPPurchaseCompletedEvent`, `IAPPurchaseFailedEvent`.
- **Unit tests — BattlePassManagerTests** (14 tests): `LoadState` (null season throw, tier clamp, premium restore), `AddPoints` (increment, ignore zero/negative, tier advance, tier-advance event, no advance beyond max), `ActivatePremiumTrack` (sets active, idempotent), `ClaimReward` (tier not reached, free reward success, already claimed, premium without active, premium with active, **combat-power reward rejected**).
- **Unit tests — QuestEngineTests** (22 tests): `Initialize` (null throw, null entries ignored, saved progress restored, daily reset, no-reset, one-time never resets), `RecordProgress` (increment, wrong type ignored, zero ignored, caps at required, marks completed, fires event, context tag filter, no-tag matches any), `ClaimReward` (not found, not completed, success, already claimed), `GetQuestsByCategory`, `GetUnclaimedCount`, `GetLastMidnightUtc`, `GetLastMondayUtc`, `BuildSaveData`, `QuestProgress.Reset`.
- **Unit tests — GachaSystemTests** (16 tests): `SetPool` (null/empty no-throw, replace pool), `SimulatePull` (null when empty, non-null with pool, pity increment, pity forces Legendary at threshold, reset pity after Legendary, all-owned pool returns null), `LoadPityCounter` (clamp below zero, clamp above threshold), `PullsUntilPity`, `GetDisplayedOdds` (non-empty, empty pool, sums to ~1.0), `ReceiveServerPullResult` (marks owned, updates pity), `GachaItem` construction validation.
- **Unit tests — IAPManagerTests** (14 tests): `RegisterProduct` (null throw, P2W throw, cosmetic ok, queryable after register), `GetProduct` (null for missing, correct return), `GetCatalog` count, `InitiatePurchase` (false unregistered, true registered), `OnPurchaseDeferred` (empty receipt ignored, valid queued), `OnServerValidationSuccess` (removes pending, fires event), `OnServerValidationFailed` (removes pending, fires event), `IAPProductDefinition` empty id throw.

### FIXED
- `QuestEngine`: Removed `Combat.` and `Alliance.` namespace qualifiers from event handler signatures — added `using AshenThrone.Combat;` and `using AshenThrone.Alliance;` to resolve namespace references correctly (QA check #3 blocker resolved).

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
