# Ashen Throne — CLAUDE.md

Instructions for Claude agents working on this project. Read this file at the start of every session.

---

## Project Identity
- **Game:** Ashen Throne — dark fantasy mobile game (iOS + Android)
- **Genre:** True Hybrid 4X Strategy + Tactical Card Combat RPG
- **Engine:** Unity 6 LTS + Universal Render Pipeline
- **Language:** C# (game) + Node.js (PlayFab Cloud Scripts)
- **Backend:** Microsoft PlayFab + Photon Fusion 2

## Repository
- **GitHub:** https://github.com/projecttop10tech-arch/ashen-throne
- **Local:** ~/ashen-throne/
- **Branch strategy:** `main` = production-ready. `develop` = integration. Feature branches per system.

## Critical Design Rules (Never Violate)
1. **Max build timer:** 4h for normal buildings, 8h for Stronghold. HARD CODED CAP in `BuildingManager.cs`. Never raise this.
2. **Zero P2W:** No item in the cash shop improves combat power. Enforced via `BattlePassReward.IsCombatPowerReward` gate in code.
3. **All heroes earnable F2P.** Cosmetic gacha only — heroes are never in the gacha.
4. **No energy system blocking core gameplay.** Players play freely.
5. **No server merges.** Cross-server events instead.

## Architecture Patterns
- **ServiceLocator** for cross-system dependencies (not FindObjectOfType)
- **EventBus** for loose coupling between systems (all events are structs)
- **ScriptableObjects** for ALL tunable values (CombatConfig, EmpireConfig, ProgressionConfig, etc.)
- **MainThreadDispatcher** for any PlayFab callbacks that touch Unity APIs
- **ObjectPool<T>** for any GameObject spawned more than 5x per session

## Key File Locations
| System | File |
|---|---|
| Combat grid | `game/Assets/Scripts/Combat/CombatGrid.cs` |
| Damage calculator | `game/Assets/Scripts/Combat/AbilityResolver.cs` |
| Card hand | `game/Assets/Scripts/Combat/CardHandManager.cs` |
| Turn order | `game/Assets/Scripts/Combat/TurnManager.cs` |
| Hero runtime state | `game/Assets/Scripts/Combat/CombatHero.cs` |
| Building system | `game/Assets/Scripts/Empire/BuildingManager.cs` |
| Resource system | `game/Assets/Scripts/Empire/ResourceManager.cs` |
| Hero collection | `game/Assets/Scripts/Heroes/HeroRoster.cs` |
| Battle Pass | `game/Assets/Scripts/Economy/BattlePassManager.cs` |
| Alliance | `game/Assets/Scripts/Alliance/AllianceManager.cs` |
| Events | `game/Assets/Scripts/Events/EventEngine.cs` |
| PlayFab backend | `backend/CloudScript/economy.js` |
| Backend tests | `backend/Tests/economy.test.js` |

## Combat Grid Layout
- **7 columns × 5 rows** (corrected in Phase 0 QA)
- Columns 0-2: player zone (front/mid/back depth)
- Column 3: neutral
- Columns 4-6: enemy zone (front/mid/back depth)

## Config ScriptableObjects
- `CombatConfig` — deck size, energy, tile effects, crit multiplier, faction bonuses
- `EmpireConfig` — vault capacities, build timers, offline earnings cap, queue slots
- `ProgressionConfig` — XP curves, stat growth per level, max hero level

## Development Phase Status
| Phase | Status |
|---|---|
| Phase 0: Foundation | COMPLETE (QA passed, committed) |
| Phase 1: Combat Core | COMPLETE (QA passed, committed) |
| Phase 2: Empire System | COMPLETE (QA passed, committed) |
| Phase 3: Alliance & Social | COMPLETE (QA passed, committed) |
| Phase 4: Economy & Monetization | COMPLETE (QA passed, committed) |
| Phase 5: Events | COMPLETE (QA passed, committed) |
| Phase 6: Polish & Launch | COMPLETE (QA passed, committed) |
| Phase 7: Infrastructure & Tooling | COMPLETE (all generators run, scenes populated, 544 tests pass) |
| Phase 8: Placeholder Art & UI Prefabs | COMPLETE (132 PNGs, 51 prefabs, 18 WAVs, shader + 3 materials, 544 tests pass) |
| Phase 9: SDK Integration | NOT STARTED |

## Phase 1 Remaining Work
- [x] TurnManager phase state full implementations (DrawPhaseState, ActionPhaseState, ResolvePhaseState, EndPhaseState, BattleOverPhaseState)
- [x] 10 starter heroes as ScriptableObject assets (generated via Assets/Editor/StarterAssetGenerator.cs)
- [x] 50 ability cards as ScriptableObject assets (generated via Assets/Editor/StarterAssetGenerator.cs)
- [x] 20 PvE story levels as data assets (4 chapters, 5 levels each)
- [x] PveEncounterManager (loads and runs story levels)
- [x] CombatHeroFactory (creates CombatHero from HeroRoster + ProgressionConfig)
- [x] Unit tests for CardHandManager (25 tests — CardHandManagerTests.cs)
- [x] Combat UI: CombatUIController, CardHandView, CardWidget, HeroStatusDisplay, StatusIconWidget, EnergyDisplay, TurnOrderDisplay, TurnTokenWidget
- [x] CombatInputHandler (player card selection + target tap → CardHandManager.TryPlayCard)
- [x] DamagePopupManager (floating damage numbers, pooled, per-damage-type color)
- [x] GridTileIdentifier (raycast-to-GridPosition helper for input)
- [x] Unit tests: CombatHero (35 tests), CombatGrid (30 tests)

## End-of-Iteration Checklist (MANDATORY)
At the end of every ralph loop iteration:
1. Run the 62-check QA protocol on all modified systems
2. Fix all blockers found
3. Update CHANGELOG.md with what was added/changed/fixed
4. Update this CLAUDE.md if any architectural decisions changed
5. Commit all changes: `git add -A && git commit -m "Phase X: [description] — QA passed"`
6. Push to GitHub: `git push`

## Known Issues / Tech Debt
- `TurnManager.cs`: Phase states fully implemented. ActionPhaseState assumes CardHandManager is initialized with the active hero's deck before each Draw phase. Multi-hero deck switching (per turn) is Phase 2 work.
- `PlayFabService.cs`: Running in stub mode. Install PlayFab Unity SDK and uncomment real auth code.
- `CombatHero._nextInstanceId`: Static, never fully reset. Use `ResetInstanceIdForTesting()` in test setup.
- `BattlePassSeason.StartDate/EndDate`: Uses `DateTime` field which Unity Inspector doesn't serialize well. Consider string ISO format with a custom property drawer.

## QA Protocol Reference
62 checks across: Code Quality (1-10), Architecture (11-16), Performance (17-25), Visual Quality (26-33), Gameplay (34-39), Monetization Ethics (40-43), Platform Compliance (44-48), Security (49-53), Testing (54-58), Documentation (59-62).

**Hard blockers that stop shipping:**
- Check #40: Any P2W item
- Check #6: Unhandled exceptions in PlayFab callbacks
- Check #3: NullReferenceException paths
- Check #58: Crash-free rate < 99.5%
