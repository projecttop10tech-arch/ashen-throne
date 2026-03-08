# Ashen Throne — Ralph Loop Plan

> **Loop Type:** ralph-wiggum:ralph-loop
> **Completion Promise:** LAUNCH_READY
> **Max Iterations:** 100

---

## Prompt

You are the lead QA engineer and technical director for Ashen Throne, a high-quality mobile game for iOS and Android built in Unity 6. The project lives at ~/ashen-throne/. Your job is to build, review, and expand the game system-by-system through all development phases (7-16), applying BRUTAL quality standards to every single file, line of code, and design decision you encounter or create. Do not skip any check. Do not rush. Be a perfectionist.

Use the Unity MCP server (port 8090, configured in game/ProjectSettings/McpUnitySettings.json) for all Unity Editor operations: running generators, populating scenes, wiring components, creating assets.

Follow the phase plan below. Apply the 62-check QA protocol to every system.

---

## Phase Status Tracker

| Phase | Status | Notes |
|-------|--------|-------|
| 7 | COMPLETE | All generators run, scenes populated, 544 tests pass |
| 8 | COMPLETE | 132 PNGs, 51 prefabs, 18 WAVs, colorblind shader, 544 tests pass |
| 9 | NOT STARTED | SDK Integration (PlayFab, Photon, Firebase) |
| 10 | NOT STARTED | Content Population & Balancing |
| 11 | NOT STARTED | Real Art Production |
| 12 | NOT STARTED | Audio Production |
| 13 | NOT STARTED | Integration Testing & Performance |
| 14 | NOT STARTED | Polish, UX, Accessibility |
| 15 | NOT STARTED | Platform Compliance & Store Prep |
| 16 | NOT STARTED | Soft Launch → Global Release |

---

## Phase 7: Infrastructure, Tooling, Project Hygiene (Weeks 1-3)

**Goal:** Fix project hygiene, run SceneGenerator to populate empty scenes, create missing SO generators, wire scripts to scene GameObjects.

### Deliverables

- [x] **7.1** Create `.gitignore` (Unity standard: exclude Library/, Temp/, Logs/, *.csproj, *.sln, .DS_Store)
- [ ] **7.2** Run `Assets/Editor/SceneGenerator.cs` to populate all 6 scenes with Camera, Canvas, EventSystem, Directional Light, and scene-specific UI hierarchies
- [x] **7.3** Wire MonoBehaviour scripts to scene GameObjects:
  - Boot: GameManager, PlayFabService, LocalizationBootstrap, AccessibilityManager, TutorialManager
  - Lobby: LobbyUIController with navigation buttons
  - Empire: EmpireUIController, ResourceHUD, BuildingPanel, ResearchTreePanel
  - Combat: CombatUIController, CardHandView, EnergyDisplay, TurnOrderDisplay, CombatInputHandler, DamagePopupManager
  - WorldMap: WorldMapUIController
  - Alliance: AllianceUIController
- [x] **7.4** Create `Assets/Editor/BuildingDataGenerator.cs` → generate 21 BuildingData SOs
- [x] **7.5** Create `Assets/Editor/QuestDefinitionGenerator.cs` → generate 30 QuestDefinition SOs
- [x] **7.6** Create `Assets/Editor/TutorialStepGenerator.cs` → generate 8 TutorialStep SOs
- [x] **7.7** Create `Assets/Resources/AccessibilityConfig.asset` (added to ConfigGenerator)
- [x] **7.8** Create balance sheet CSVs in `tools/BalanceSheets/`
- [ ] **7.9** Run all generators in Unity Editor (BuildingData, QuestDefinition, TutorialStep, Config)

### Exit Criteria
1. All 6 scenes load without NullReferenceException
2. Scene navigation works: Boot → Lobby → Empire/Combat/WorldMap/Alliance
3. `.gitignore` prevents Library/ from staging
4. 21 BuildingData + 30 QuestDefinition + 8 TutorialStep SOs exist
5. All existing unit tests still pass

---

## Phase 8: Placeholder Art and UI Prefabs (Weeks 4-6)

**Goal:** Create colored-rectangle placeholder assets for every visual slot so all systems render in Editor. Create all missing prefabs.

### Deliverables

- [ ] **8.1** Placeholder hero art: 10 portraits (256x256), 10 full-body (512x1024), 10 Spine stubs → wire to HeroData SOs
- [ ] **8.2** Placeholder card art: 50 card frames (200x300) colored by element → wire to AbilityCardData SOs
- [ ] **8.3** Placeholder building art: 63 PNGs (21 buildings × 3 tier sprites) → wire to BuildingData SOs
- [ ] **8.4** UI sprite atlas: resource icons, currency icons, buttons, panels, status icons, navigation icons, bar fills
- [ ] **8.5** Environment art: 7 combat tile textures, 5 world map textures, empire background + district floors
- [ ] **8.6** ~30 missing UI prefabs: HeroStatusDisplay, EnergyBar, VictoryPanel, DefeatPanel, BuildingSlot, QuestRow, ChatBubble, LeaderboardRow, BattlePassTierRow, StoreProductCard, HeroCard, EventBanner, TutorialOverlay, SettingsToggle/Slider, BuildingPrefab, WorldMapTile, particle effects
- [ ] **8.7** Placeholder audio: 3 music loops (30s each), 15 SFX clips
- [ ] **8.8** Colorblind filter shader (URP fullscreen, 3 Daltonization modes) + 3 material instances

### Exit Criteria
1. Every scene renders visually (no pink materials, no invisible panels)
2. Combat scene shows: grid tiles, card hand, hero portraits, energy bar, turn order
3. Empire scene shows: resource HUD, building grid, build queue
4. All 3 colorblind modes produce visible color shifts
5. Audio plays on scene transitions

---

## Phase 9: SDK Integration — PlayFab, Photon, Firebase (Weeks 7-9)

**Goal:** Replace all SDK stubs with real working integrations.

### Deliverables

- [ ] **9.1** Install PlayFab Unity SDK, uncomment all calls in `PlayFabService.cs`
- [ ] **9.2** Deploy `backend/CloudScript/economy.js` to PlayFab title
- [ ] **9.3** Activate Unity IAP: register 6 SKUs, wire purchase flow
- [ ] **9.4** Install Photon Fusion 2, create `PhotonManager.cs`
- [ ] **9.5** Install Firebase (Analytics + Crashlytics)
- [ ] **9.6** Create `ATTManager.cs` for iOS App Tracking Transparency

### Exit Criteria
1. Boot scene authenticates with PlayFab (real PlayFabId)
2. User data round-trips (save/read via PlayFab)
3. IAP catalog loads with localized prices
4. Photon connects and can create/join a room
5. Firebase Analytics event visible in console
6. All existing tests still pass

---

## Phase 10: Content Population and Data Balancing (Weeks 10-12)

**Goal:** Fill all data-driven systems with launch content and balance the economy.

### Deliverables

- [ ] **10.1** Tune 21 buildings × 10 tiers against balance sheet
- [ ] **10.2** Tune 30 quest rewards against economy flow model
- [ ] **10.3** Hero balance pass: faction diversity, role coverage, stat distributions
- [ ] **10.4** PvE difficulty curve: Chapter 1 beatable with 1 hero at level 1
- [ ] **10.5** Expand localization from 32 keys to ~400 keys, translate 7 non-English languages
- [ ] **10.6** Battle Pass Season 1: 50 tiers free + premium, zero P2W
- [ ] **10.7** Gacha pool: 40 cosmetic items, zero heroes, pity counter at 50

### Exit Criteria
1. Complete Chapter 1 playthrough (5 PvE levels) with starter hero
2. Daily quest cycle completes correctly
3. Building Stronghold through 3 tiers with correct costs/timers
4. All 8 languages display correct text
5. Balance sheet audit passes (Day 1/7/30 resource flow matches targets)

---

## Phase 11: Real Art Asset Production (Weeks 13-17)

**Goal:** Replace all placeholder art with production dark fantasy assets.

### Deliverables

- [ ] **11.1** 10 hero portraits + full-body + Spine rigs (5 animations each)
- [ ] **11.2** 50 card illustrations + 6 element frame overlays
- [ ] **11.3** 210 building sprites (21 buildings × 10 tiers)
- [ ] **11.4** Environment: combat tiles, world map, empire city, URP skyboxes
- [ ] **11.5** UI atlas: production icons, ornate buttons/panels, app icon, splash screen
- [ ] **11.6** 20 particle effect prefabs
- [ ] **11.7** Art style guide reference sheets

### Exit Criteria
1. Zero placeholder rectangles visible
2. All 10 heroes animate correctly in combat
3. Empire shows visually distinct buildings per district/tier
4. App icon renders correctly in simulators

---

## Phase 12: Audio Production and Integration (Weeks 16-18)

**Goal:** Replace placeholder audio with production music, SFX, and voice.

### Deliverables

- [ ] **12.1** 7 music tracks (OGG 128kbps)
- [ ] **12.2** 50+ SFX clips (WAV 44.1kHz)
- [ ] **12.3** 40 hero voice lines (10 heroes × 4)
- [ ] **12.4** Create `Scripts/Core/AudioManager.cs`

### Exit Criteria
1. Music crossfades between scenes without gap/pop
2. Every player action has audio feedback
3. Combat SFX sync with Spine animations (within 100ms)
4. Volume sliders work independently per category

---

## Phase 13: Integration Testing and Performance Optimization (Weeks 19-21)

**Goal:** Make everything work together reliably, hit mobile performance targets.

### Deliverables

- [ ] **13.1** Performance benchmark suite
- [ ] **13.2** Memory optimization: ObjectPool, Addressables (4 bundles)
- [ ] **13.3** GPU optimization: URP mobile config, sprite atlas packing
- [ ] **13.4** Network optimization: PlayFab call profiling, caching
- [ ] **13.5** Full 62-check QA pass with documented results
- [ ] **13.6** 20 new PlayMode integration tests

### Exit Criteria
1. Stable 60fps in combat
2. Zero memory leaks in 30-minute session
3. Zero NullReferenceExceptions in any scene transition
4. 50+ of 62 QA checks passing
5. 60+ total tests passing
6. Build size under 150MB

---

## Phase 14: Polish, UX, and Accessibility (Weeks 22-24)

**Goal:** Add transitions, animations, haptics, tutorial polish, settings completion.

### Deliverables

- [ ] **14.1** Scene transition system: fade-to-black, loading spinner
- [ ] **14.2** UI animation: card fan, HP bar lerp, building pop, gacha reveal
- [ ] **14.3** Haptic feedback for key events
- [ ] **14.4** Notification scheduling
- [ ] **14.5** Settings panel completion
- [ ] **14.6** Tutorial polish: dim overlay, hand pointer, skip button
- [ ] **14.7** Deep link registration

### Exit Criteria
1. Smooth scene transitions
2. Every UI panel animates in/out
3. Haptics fire on all designated events
4. Notifications appear at correct times
5. Tutorial completes Steps 0-7
6. Settings persist across sessions

---

## Phase 15: Platform Compliance and Store Preparation (Weeks 25-27)

**Goal:** Meet all App Store / Google Play requirements, prepare store listings.

### Deliverables

- [ ] **15.1** iOS: ExportOptions.plist, Info.plist, screenshots, localized description
- [ ] **15.2** Android: Target API 34, AndroidManifest, keystore signing, screenshots
- [ ] **15.3** Privacy: Privacy Policy/ToS viewers, GDPR consent, COPPA
- [ ] **15.4** Age rating: IARC (expected PEGI 12 / ESRB T)
- [ ] **15.5** CI/CD final config: test gates, version bumping, signing
- [ ] **15.6** Store assets: feature graphic, promotional art, preview video
- [ ] **15.7** Final 62-check QA pass — all checks must pass

### Exit Criteria
1. TestFlight build runs on physical iPhone
2. Google Play Internal build runs on physical Android
3. All 62 QA checks pass on both platforms
4. Store listings complete in 8 languages
5. CI/CD produces signed release builds

---

## Phase 16: Soft Launch, Beta, Global Release (Weeks 28-30)

**Goal:** Beta test, soft launch, fix issues, go global.

### Deliverables

- [ ] **16.1** Closed beta: 100-200 testers
- [ ] **16.2** Soft launch: Philippines + New Zealand
- [ ] **16.3** Hotfix cycle: top 5 crashes, progression blockers
- [ ] **16.4** Global launch: simultaneous App Store + Google Play
- [ ] **16.5** Post-launch monitoring plan
- [ ] **16.6** Launch marketing assets

### Exit Criteria
1. Global launch on both stores
2. Crash-free rate >99.5% in first 48 hours
3. No progression-blocking bugs
4. D1 retention >40%
5. All 5 launch events scheduled

---

## 62-Check QA Protocol

| # | Category | Check |
|---|----------|-------|
| 1 | Code Quality | Zero compiler warnings |
| 2 | Code Quality | All public APIs have XML doc comments |
| 3 | Code Quality | Zero NullReferenceException paths |
| 4 | Code Quality | No hardcoded strings in UI (all localized) |
| 5 | Code Quality | No hardcoded magic numbers (use config SOs) |
| 6 | Code Quality | All PlayFab callbacks handle error case |
| 7 | Code Quality | No FindObjectOfType (use ServiceLocator) |
| 8 | Code Quality | No LINQ in hot paths (Update, combat resolution) |
| 9 | Code Quality | All IDisposable properly disposed |
| 10 | Code Quality | No async void (except Unity event handlers) |
| 11 | Architecture | ServiceLocator used for cross-system deps |
| 12 | Architecture | EventBus for loose coupling (all events are structs) |
| 13 | Architecture | ScriptableObjects for ALL tunable values |
| 14 | Architecture | MainThreadDispatcher for PlayFab → Unity |
| 15 | Architecture | ObjectPool for frequent spawns (>5x/session) |
| 16 | Architecture | Assembly definitions enforce boundaries |
| 17 | Performance | 60fps in combat (profiler verified) |
| 18 | Performance | Scene load < 3s on target device |
| 19 | Performance | No GC alloc in Update loops |
| 20 | Performance | Sprite atlases for all UI |
| 21 | Performance | Particle systems ≤ 200 particles each |
| 22 | Performance | Audio compressed (OGG music, WAV short SFX) |
| 23 | Performance | Addressables for large assets |
| 24 | Performance | Build size < 150MB |
| 25 | Performance | Memory stable over 30-min session |
| 26 | Visual | No pink/missing materials |
| 27 | Visual | No invisible UI panels |
| 28 | Visual | Consistent art style across scenes |
| 29 | Visual | Scene transitions smooth (no flash) |
| 30 | Visual | All heroes have idle/attack/hit/death/victory anims |
| 31 | Visual | Colorblind modes work (3 Daltonization modes) |
| 32 | Visual | UI scales correctly 16:9 through 21:9 |
| 33 | Visual | Dark fantasy theme consistent |
| 34 | Gameplay | Chapter 1 completable with starter hero |
| 35 | Gameplay | Tutorial steps 0-7 complete without error |
| 36 | Gameplay | Daily/weekly quest cycle works |
| 37 | Gameplay | Building upgrade respects timer caps (4h/8h) |
| 38 | Gameplay | Combat card play → damage → animation pipeline works |
| 39 | Gameplay | Alliance creation/join/chat functional |
| 40 | Monetization | ZERO P2W items in shop |
| 41 | Monetization | All heroes earnable F2P |
| 42 | Monetization | Gacha is cosmetic-only, pity at 50 |
| 43 | Monetization | F2P reaches 70% max power at Day 90 |
| 44 | Platform | iOS builds and runs on device |
| 45 | Platform | Android builds and runs on device |
| 46 | Platform | Notifications schedule correctly |
| 47 | Platform | Deep links resolve |
| 48 | Platform | IAP purchase flow completes |
| 49 | Security | No secrets in source control |
| 50 | Security | PlayFab calls use HTTPS |
| 51 | Security | IAP receipt validation server-side |
| 52 | Security | No client-authority for currency/resources |
| 53 | Security | GDPR/COPPA compliance |
| 54 | Testing | Unit tests pass (NUnit) |
| 55 | Testing | Integration tests pass (PlayMode) |
| 56 | Testing | All generators idempotent |
| 57 | Testing | Backend tests pass (Jest) |
| 58 | Testing | Crash-free rate > 99.5% |
| 59 | Docs | CLAUDE.md up to date |
| 60 | Docs | CHANGELOG.md current |
| 61 | Docs | Store listing complete in 8 languages |
| 62 | Docs | Privacy policy and ToS accessible |

---

## Critical Files

| File | Why |
|------|-----|
| `Assets/Editor/SceneGenerator.cs` | Must run first — populates all 6 empty scenes |
| `Assets/Scripts/Network/PlayFabService.cs` | Central stub → real SDK conversion |
| `Assets/Scripts/UI/Combat/CombatUIController.cs` | Template for wiring SerializeField refs |
| `Assets/Scripts/Data/BuildingData.cs` | Data class with zero SO instances |
| `Assets/Editor/StarterAssetGenerator.cs` | Pattern for all new SO generators |

## End-of-Iteration Protocol

After each ralph-loop iteration:
1. Run all unit + integration tests
2. Run 62-check QA protocol on modified systems
3. Update CHANGELOG.md and CLAUDE.md
4. Commit all changes
5. Push to GitHub
6. Update Phase Status Tracker above
