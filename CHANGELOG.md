# Ashen Throne — Changelog

All notable changes tracked here. Format: [ADDED] [CHANGED] [FIXED] [REMOVED].

---

## [0.66.0] — 2026-03-12 (Ralph Loop Iteration 61: Stat Comparison Bars, Vault Overflow Warning)

### ADDED
- **Stat comparison bars in building info panel** — P&C-style horizontal bars showing current vs next tier values for Power, Production, Troop Capacity, Defense, and Research Speed. Current value shown as solid colored bar, next tier as translucent ghost fill extending beyond current. Delta region has pulsing glow. Values displayed as "current → next" with green text when improving. Building-type-specific: resource buildings show production bars, military shows troop capacity, walls show defense, academies show research speed.
- **`GetStatComparisonData()` method** — returns list of `StatBarData` structs with label, current/next/max values, and bar color per building type and tier.
- **`AddStatComparisonBar()` method** — renders a single comparison bar row: label, dark background track, current fill, next tier ghost fill, delta glow, and "cur → next" value text.
- **`FormatStatValue()` helper** — formats stat values with K/M suffixes for bar labels.
- **Resource vault overflow warning popup** — when collecting resources pushes vault above 95% capacity, a warning popup appears with resource name, current/max amounts, fill bar, and contextual message. At 100% full, popup turns red with "WASTED!" urgency. Auto-dismisses after 4 seconds with fade-out, or tap to dismiss immediately. 30-second cooldown per resource type prevents spam.
- **`CheckVaultOverflowWarning()` method** — checks current resource vs vault cap ratio after collection events.
- **`ShowVaultOverflowWarning()` method** — creates the warning popup with fill bar, warning icon, and contextual messaging.
- **`AutoDismissVaultWarning()` coroutine** — 4s timer then 0.5s fade-out for auto-dismissal.
- **`VaultWarningCooldownDecay()` coroutine** — prevents repeated warnings within 30-second window.

### CHANGED
- `OnResourceCollected()` now calls `CheckVaultOverflowWarning()` after processing collection and streak bonuses.
- Stats section in info panel now includes visual comparison bars between STATS header and production info rows.

---

## [0.65.0] — 2026-03-12 (Ralph Loop Iteration 60: Collection Streak Bonus, Building Quick-Nav)

### ADDED
- **Resource collection streak bonus** — rapidly collecting multiple resource bubbles within 2.5 seconds triggers a streak bonus: x2 = +10%, x3-4 = +20%, x5+ = +30%. Bonus resources are added automatically via ResourceManager. The streak counter resets after the 2.5s window expires via `DecayCollectStreak()` coroutine.
- **Streak visual indicator** — floating "⚡ STREAK x3! +20%" text appears at screen center during streaks. Color escalates: green (x2), gold (x3-4), orange (x5+). Text punches in at 1.5x scale, floats upward, and fades over 1.2 seconds.
- **`ShowStreakIndicator()` / `AnimateStreakIndicator()` methods** — streak feedback creation and animation.
- **Building quick-nav sidebar** — 6 small category buttons on the right edge of the screen (SH/MIL/RES/MAG/DEF/SCI). Tapping a category smoothly scrolls to the first building of that type and flashes it 3 times for identification. Each button is color-coded to match its category (gold for stronghold, red for military, green for resource, etc.).
- **`CreateBuildingQuickNav()` method** — creates the sidebar with category buttons.
- **`ScrollToBuildingType()` method** — finds the first building of a type and smooth-scrolls to center it.
- **`SmoothScrollTo()` coroutine** — ease-in-out scroll animation for quick-nav targeting.
- **`FlashBuildingHighlight()` coroutine** — 3-pulse white flash on the targeted building for visual identification.

### CHANGED
- `OnResourceCollected()` now tracks collection streak timing, applies bonus resources, and triggers visual feedback.
- Collect toast accumulation now includes streak bonus amounts in the displayed total.

---

## [0.64.0] — 2026-03-12 (Ralph Loop Iteration 59: Resource Breakdown Popup, SH Unlock Preview)

### ADDED
- **Resource production breakdown popup** — tapping the production rate in a resource building's info panel now opens a detailed breakdown popup showing all buildings of that resource type, their individual per-hour rates, and a total summary with daily forecast. Color-coded per resource (stone=tan, iron=silver-blue, grain=gold, arcane=purple). Includes close button and fade-in animation.
- **`ShowResourceBreakdownPopup()` / `DismissResourceBreakdownPopup()` methods** — full lifecycle for the resource production detail popup.
- **Tappable production rate row in info panel** — production rate text is now a subtle button (with `▶` arrow hint) that opens the breakdown popup on tap.
- **Stronghold unlock preview in info panel** — when viewing the stronghold info panel (and it's below max level), a golden text line shows what building types unlock at the next stronghold level (e.g., "⚿ Lv.2 unlocks: Wall, Watch Tower, Marketplace").

### CHANGED
- Info panel production rate display converted from static text to interactive button with tinted background.

---

## [0.63.0] — 2026-03-12 (Ralph Loop Iteration 58: Weather Particles, Active Boost Badge)

### ADDED
- **Weather particle overlay** — ambient weather particles fall across the city screen based on time of day. Night: soft blue snow sparkles using radial gradient. Dawn/dusk: warm orange ember particles with wide drift. Daytime: subtle blue rain streaks falling fast. Max 25 particles at once, each with sinusoidal drift, fade-out lifecycle, and automatic off-screen cleanup. Created via `CreateWeatherOverlay()` + `AnimateWeatherParticles()` coroutine.
- **`AnimateWeatherParticle()` coroutine** — per-particle lifecycle with configurable fall speed, drift amplitude, fade timing. 3-6 second random lifespan.
- **Active VIP boost badge on upgrading buildings** — buildings in the upgrade queue now show a pulsing blue "⚡ VIP +10%" badge near their progress bar. Badge pulses between bright and dim blue at 3Hz via `PulseActiveBoostBadge()` coroutine. Automatically removed when upgrade completes alongside scaffolding and dust cleanup.
- **`AddActiveBoostBadge()` / `RemoveActiveBoostBadge()` / `PulseActiveBoostBadge()` methods** — lifecycle management for the upgrade speed boost visual indicator.

### CHANGED
- `RefreshUpgradeIndicators()` now also calls `AddActiveBoostBadge()` for buildings in the build queue.
- Upgrade completion handler (`OnUpgradeCompletedSfx`) now calls `RemoveActiveBoostBadge()` alongside scaffolding/dust removal.

---

## [0.62.0] — 2026-03-12 (Ralph Loop Iteration 57: Zoom Indicator, Building Unlock Previews)

### ADDED
- **Zoom level indicator** — a floating "⤢ x1.5" badge appears briefly at screen center during pinch or scroll zoom, showing the current zoom multiplier. Auto-fades after 1.5 seconds. Dark pill with gold text and border, matching P&C's zoom feedback style.
- **`ShowZoomLevelIndicator()` / `FadeZoomIndicator()` coroutine** — creates and manages the zoom indicator lifecycle with smooth fade-out.
- **Building unlock preview ghosts** — greyed-out, semi-transparent building sprites appear on the grid near the stronghold for building types that would unlock at the next stronghold level. Each ghost shows a lock icon and "⚿ SH Lv.X / BuildingName" label. Max 4 previews to avoid visual clutter. Matches P&C's "upgrade your castle to unlock" visual hints.
- **`CreateBuildingUnlockPreviews()` method** — finds empty grid spots near the stronghold and places dimmed preview sprites with lock overlay and unlock requirement labels.

### CHANGED
- `ApplyZoom()` now calls `ShowZoomLevelIndicator()` on every zoom change for real-time feedback.

---

## [0.61.0] — 2026-03-12 (Ralph Loop Iteration 56: Resource Countdown Timers, Building Count in Info Panel)

### ADDED
- **Auto-collect countdown timer on resource buildings** — each resource-producing building (grain farm, iron mine, stone quarry, arcane tower) now shows a small green-bordered timer label with seconds until the next collectible bubble spawns (e.g., "⏱ 12s"). When the building already has a bubble ready, the label switches to golden "⬆ Collect!" text. Updated every second via `UpdateResourceCountdownTimers()` coroutine.
- **`CreateCollectCountdownLabel()` method** — creates the timer label GO on each resource building during visual setup.
- **`ResourceBubbleSpawner.SecondsUntilNextSpawn` property** — exposes the time remaining until the next spawn wave for external UI consumption.
- **`ResourceBubbleSpawner.HasMaxBubbles()` method** — checks whether a specific building instance already has the maximum number of uncollected bubbles.
- **Building count/limit display in info panel** — for building types that allow multiples (e.g., grain farms 5 max, barracks 2 max), the info panel stats section now shows "Owned: 3/5" with red text when at max capacity, gray-blue otherwise.

### CHANGED
- `ResourceBubbleSpawner.SpawnInterval` changed from `private const` to `public const` to support external countdown display.

---

## [0.60.0] — 2026-03-12 (Ralph Loop Iteration 55: Info Panel Upgrade-In-Progress, Prerequisite Display)

### ADDED
- **Info panel upgrade-in-progress state** — when tapping a building that is currently upgrading, the info panel now shows a green progress bar with percentage, remaining time text ("⏱ Upgrading to Lv.X — Xh Xm"), speed-up button (green "FREE" if <60s, purple otherwise), alliance "❤ Help" button, and close button. Replaces the normal demolish/upgrade buttons for buildings in the queue. Matches P&C's building info while upgrading.
- **Prerequisite/block reason display on info panel** — when a building cannot be upgraded, the reason (e.g., "Requires Stronghold Lv.3", "Build queue full", "Not enough: Iron") is shown in red/gray text above the upgrade button, so players know what's blocking before tapping.
- **Upgrade button dimming** — the upgrade button now shows in a darker gray when prerequisites are not met, providing visual feedback that the upgrade is blocked even before tapping.

### CHANGED
- Info panel bottom section now branches into two states: upgrading (progress + controls) vs normal (demolish + upgrade + prereq text)

---

## [0.59.0] — 2026-03-12 (Ralph Loop Iteration 54: Alliance Help Visual, Category Selection Glow)

### ADDED
- **Alliance help received visual indicator** — when an AllianceHelpRequestedEvent fires for a building, a blue badge with "❤ -5m 0s" appears on the building, auto-fading after 2+1 seconds. Subscribed via EventBus in CityGridView start-up.
- **`ShowAllianceHelpReceived()` / `FadeAndDestroyHelpIndicator()` methods** — badge creation and timed fade-out coroutine for alliance help feedback.
- **Category-colored selection ring glow** — building selection rings now use the category header color (military=red, resource=green, magic=purple, etc.) at 0.45 alpha instead of uniform gold. Pulse animation preserves the category hue.
- **`GetCategorySelectionColor()` method** — derives selection ring color from `GetCategoryHeaderColor()` with adjusted alpha.

### CHANGED
- Selection ring pulse now preserves category hue (`new Color(c.r, c.g, c.b, pulse)`) instead of overwriting with gold tint.

---

## [0.58.0] — 2026-03-12 (Ralph Loop Iteration 53: Celebration Flash, Vault Glow, Category SFX)

### ADDED
- **Upgrade celebration screen flash + "LEVEL UP!" text** — when a building upgrade completes, a gold screen flash overlay appears with large "LEVEL UP!" text (28pt bold, dark outline) that scales up and fades out over 0.6 seconds. Plays after the existing ray burst and sparkle effects for a dramatic multi-stage celebration matching P&C.
- **Resource vault warning glow bar** — pulsing red glow strip appears below the resource bar when ANY resource is at 90%+ capacity. Pulses between 25-45% opacity at 3Hz. Automatically appears/disappears based on periodic vault ratio checks.
- **`PulseResourceCapGlow()` coroutine** — handles the sinusoidal opacity animation for the vault warning bar.
- **Category-specific building tap SFX** — building taps now attempt to load a category-specific audio clip (e.g., `sfx_tap_military` for barracks, `sfx_tap_magic` for arcane tower, `sfx_tap_forge` for forge) before falling back to the generic tap sound. 9 categories mapped via `GetBuildingTapSfxName()`.
- **`PlayBuildingTapSfx()` / `GetBuildingTapSfxName()` methods** — category-aware SFX dispatch with resource fallback.

### CHANGED
- Building tap handler now calls `PlayBuildingTapSfx(buildingId)` instead of generic `PlaySfx(_sfxTap)`
- Celebration burst coroutine now continues with screen flash phase after sparkle cleanup

---

## [0.57.0] — 2026-03-12 (Ralph Loop Iteration 52: Production Forecast, Radial Demolish, SH Upgrade Fix)

### ADDED
- **Daily production forecast in info panel** — resource buildings now show daily output (hourly × 24) with "☉ Daily: +6K" label, plus vault fill time estimate ("Vault full in 3h 20m"). Uses ResourceManager vault caps for accurate fill projections.
- **Demolish button in radial menu** — non-stronghold buildings now show a red "☠ Remove" button in the radial semicircle popup. Tapping it opens the existing demolish confirmation dialog with warning text and Cancel/Confirm buttons.

### CHANGED
- Stronghold upgrade banner now routes through `ShowUpgradeConfirmDialog()` instead of directly calling `BuildingManager.StartUpgrade()`. Shows resource costs, block reasons, and requires explicit confirmation — matching P&C's upgrade flow consistency.
- Radial menu now shows 4 buttons for non-stronghold buildings (Upgrade/Timer, Info, Move, Remove) arranged in wider arc.

---

## [0.56.0] — 2026-03-12 (Ralph Loop Iteration 51: Ambient Tint, Mini-Map, VIP Boost)

### ADDED
- **Time-of-day ambient tint overlay** — subtle full-screen color tint that shifts based on real-world time: dawn (warm orange), morning (clear), midday (neutral warm), afternoon (golden hour), dusk (purple-orange), night (deep blue). Smoothly interpolates every 2 seconds for seamless transitions.
- **`CreateAmbientTintOverlay()` / `GetAmbientTintColor()` / `AnimateAmbientTint()`** — ambient tint system with 6 time-of-day presets and slow lerp transitions.
- **Mini-map overview indicator** — small panel in bottom-left corner showing isometric dot representation of all placed buildings. Color-coded dots: gold (stronghold), green (resource), purple (magic), red (military), silver (defense), brown (other). Includes a white viewport rectangle that tracks current scroll position and zoom level, updating every 250ms.
- **`CreateMiniMap()` / `UpdateMiniMapViewport()` / `GetMiniMapDotColor()`** — mini-map creation, viewport tracking coroutine, and building type color mapping.
- **VIP boost indicators on buildings** — small orange badge in top-right corner of resource producers showing "↑+10%" and military buildings showing "↑SPD". Visible at close zoom level. Added to building creation pipeline alongside alliance flags.
- **`AddVIPBoostIndicator()` method** — creates VIP boost badge with gold border, filtered to resource and military building types.
- **Construction dust added to zoom visibility** — dust particles now respect far+ zoom threshold.
- **VIP boost added to zoom visibility** — badge shows at close zoom only.

### CHANGED
- Start() now creates ambient tint overlay and mini-map on initialization
- Building creation pipeline now includes VIP boost indicator after alliance flag
- Zoom detail visibility expanded with VIPBoost and ConstructionDust entries

---

## [0.55.0] — 2026-03-12 (Ralph Loop Iteration 50: Build Queue Panel, Resource Fly Animation)

### ADDED
- **Enhanced build queue panel** — tapping the builder HUD now opens a rich panel with: title bar with slot count (used/total), per-slot building sprite thumbnails, "Name → Lv.X" tier labels, progress bars with green fill, countdown timers, "FREE" speed-up button (green, for <5min remaining) or gem speed-up button (purple), and alliance "Help" button (blue) that publishes `AllianceHelpRequestedEvent`. Empty slots shown with dashed border and "Slot N — Empty" label.
- **`CreateFilledQueueSlot()` method** — builds a complete queue slot with sprite thumbnail, name, tier, progress bar, timer, speed-up and help buttons.
- **`CreateEmptyQueueSlot()` method** — renders an inactive slot placeholder with dashed-style border.
- **Resource collection bezier arc fly animation** — collected resource bubbles now fly in a smooth quadratic bezier arc toward the resource bar at the top of the screen instead of just floating upward. Animation uses ease-in-out timing, punch-then-shrink scale, and late-phase fade-out over 0.5 seconds. Target position varies by resource type (Stone→left, Iron→mid-left, Grain→mid-right, Arcane→right).

### CHANGED
- Build queue panel expanded from 0.40 to 0.48 width, 0.68 to 0.58 bottom anchor for more room
- Auto-dismiss timer increased from 5s to 8s for richer queue panel
- `ResourceCollectBubble.CollectDuration` increased from 0.3s to 0.5s for longer fly arc
- Collect animation replaced: was scale-up + fade-up, now bezier arc + ease-in-out + punch-shrink

---

## [0.54.0] — 2026-03-12 (Ralph Loop Iteration 49: Building Locks, Power HUD, Construction Dust)

### ADDED
- **Building unlock/lock state in build selector** — buildings that require a higher Stronghold level are shown grayed out with a lock icon overlay and "SH Lv.X" requirement text. Tapping a locked building shows an "Upgrade Blocked" toast with the required Stronghold level. Uses `BuildingData.strongholdLevelRequired` with fallback unlock levels.
- **`GetBuildingUnlockLevel()` helper** — returns the Stronghold level required to unlock a building type, checking BuildingData first then falling back to a hardcoded switch expression.
- **Total power rating HUD** — displays aggregate empire power below the builder count HUD. Sums `GetBuildingPowerContribution()` for all placed buildings, formatted as K/M for large values. Gold text with dark background, refreshed in the periodic update cycle.
- **`CreatePowerRatingHUD()` / `UpdatePowerRatingHUD()` methods** — power HUD creation and refresh logic.
- **Construction dust particle effect** — upgrading buildings now show 6 animated dust/spark motes rising from the building base. Motes alternate between tan dust and orange spark colors, with staggered phases, lateral drift, and fade-in/fade-out lifecycle over 3-second cycles. Uses radial gradient sprite for soft particle look.
- **`AddConstructionDustEffect()` / `RemoveConstructionDustEffect()` methods** — dust particle creation and cleanup, called alongside scaffolding overlay.
- **`AnimateConstructionDust()` coroutine** — per-frame animation loop for dust motes with sinusoidal drift, scale pulsing, and alpha lifecycle.

### CHANGED
- All 3 scaffolding call sites now also trigger construction dust particles
- Upgrade completion handler now removes dust effect alongside scaffolding
- Build selector checks current Stronghold level against building requirements before allowing placement
- Periodic refresh block now includes `UpdatePowerRatingHUD()`

---

## [0.53.0] — 2026-03-12 (Ralph Loop Iteration 48: Upgrade Confirmation Dialog)

### ADDED
- **P&C-style upgrade confirmation dialog** — tapping Upgrade now shows a full-screen dialog with: tier sprite comparison (current dimmed → next highlighted), "Lv.X → Lv.Y" header, per-resource cost breakdown with current amounts and green ✓ / red ✗ afford indicators, build time display, and Cancel/Upgrade buttons. Upgrade button is grayed out and shows "Can't Afford" when resources are insufficient.
- **`ShowUpgradeConfirmDialog()` method** — creates the confirmation dialog with resource cost rows, sprite previews, and conditional upgrade action.
- **`DismissUpgradeConfirmDialog()` method** — cleanup for upgrade confirmation dialog.

### CHANGED
- Radial menu Upgrade button now opens upgrade confirmation dialog instead of directly triggering `BuildingDoubleTappedEvent`
- Info panel Upgrade button also routes through the confirmation dialog
- Both upgrade paths now show detailed resource costs before committing to the upgrade
- Upgrade confirmation publishes `BuildingUpgradeStartedEvent` with correct `buildTimeSeconds` from `BuildingTierData`

---

## [0.52.0] — 2026-03-12 (Ralph Loop Iteration 47: Notification Badges, Build Selector Hints)

### ADDED
- **Event notification badges on buildings** — red dot badges appear on buildings with pending actions: resource buildings show "!" when vault is >90% capacity, academy/laboratory show "?" when no research is active, barracks/training show "⚔" when troops are ready (simulated cycle), marketplace shows "$" when trade is available. Badges refresh every 2.5s (periodic refresh cycle) and hide/show based on zoom level (medium+ zoom).
- **Build selector production hints** — each building button in the placement selector now shows a green hint label at the top: resource buildings show "+250/hr", military shows troop capacity, defense shows "+DEF"/"+ATK", research shows "Research"/"Tech", social shows "Trade"/"Rally"/"Diplomacy".
- **`RefreshNotificationBadge()` method** — creates/shows/hides notification dots based on building type and game state. Checks ResourceManager vault ratios and ResearchManager queue status.
- **`RefreshAllNotificationBadges()` method** — refreshes all building notification badges in periodic update loop.
- **`GetBuildingSelectorHint()` helper** — returns short production/function hint string for build selector buttons.

### CHANGED
- Building creation pipeline now calls `RefreshNotificationBadge()` after alliance flag creation
- Periodic refresh block now includes `RefreshAllNotificationBadges()`
- Notification dots added to zoom detail visibility system (medium+ zoom threshold)

---

## [0.51.0] — 2026-03-12 (Ralph Loop Iteration 46: Placement Ghost Glow, Alliance Flags)

### ADDED
- **Placement ghost green/red validity tint** — during building placement, the ghost sprite tints green when the position is valid and red when blocked. A matching glow ring aura surrounds the ghost for visual clarity.
- **Alliance territory flag markers** — military (barracks, training_ground, armory), defense (wall, watch_tower), and social (guild_hall, embassy) buildings now display a small blue shield with gold ⚔ emblem in the top-left corner, indicating alliance territory ownership. Controlled by zoom detail visibility (close zoom).
- **`CreateAllianceFlag()` method** — creates shield-shaped alliance emblem badge on relevant building types.

### CHANGED
- `UpdatePlacementPosition()` now tints the ghost sprite green/red and manages a `GlowRing` child object based on `CanPlaceAt()` validity
- Building creation pipeline now calls `CreateAllianceFlag()` after buff indicators
- Alliance flags added to zoom detail visibility system (hidden when zoomed out)

---

## [0.50.0] — 2026-03-12 (Ralph Loop Iteration 45: Radial Context Menu, Build Selector Thumbnails)

### ADDED
- **P&C radial context menu** — tapping a building now shows a semicircular arc of circular buttons (Upgrade, Info, Move) arranged above the building, replacing the old rectangular popup. Buttons are positioned on a 55px radius arc from 150° to 30°. Each button has a gold-ringed circular background with icon + label. Scale+fade animation (0.3→1.0 with ease-out-back overshoot) for snappy P&C feel.
- **Radial timer integration** — when a building is upgrading, the Upgrade button becomes a live countdown timer button (⚒ icon) within the radial layout.
- **Build selector sprite thumbnails** — each building button in the placement selector now shows the tier-1 building sprite above the name label. Uses `LoadBuildingSprite()` with `preserveAspect` for clean display.
- **`CreateRadialButton()` helper** — pixel-positioned circular button factory with icon, label, gold border, and optional click handler.
- **`AnimateRadialPopupIn()` coroutine** — scale-from-center animation with ease-out-back easing for radial popup appearance.

### CHANGED
- `OnBuildingTappedShowPopup()` completely rewritten from rectangular panel to radial semicircle layout
- Build selector buttons now have sprite thumbnail (top 64%) + name label (bottom 28%) layout instead of text-only
- Name plate in radial menu is a centered pill below the arc instead of a header row
- Upgrade cost line positioned below the name plate in the radial layout

---

## [0.49.0] — 2026-03-12 (Ralph Loop Iteration 44: Zoom Detail Levels, Tier Preview)

### ADDED
- **4-tier zoom-dependent detail levels** — city elements now show/hide based on zoom level: far (≥0.55x) shows progress bars; medium (≥0.7x) adds arrows, count badges, NEW badges, queue labels; close (≥1.0x) adds level badges, names, production rates, buff icons; inspection (≥1.4x) adds garrison labels. Keeps strategic overview clean while enabling deep inspection on zoom-in.
- **Building upgrade tier preview in info panel** — the Info panel now shows current vs next tier building sprites side-by-side with a gold arrow between them. Next tier sprite has a subtle gold glow highlight. Below the preview, stat deltas show what improves (e.g., "+250/hr", "+1.5K ⚔", "+500 troops"). Max-level buildings show only the current sprite centered.
- **`LoadBuildingSprite()` helper** — static method for loading building sprites by ID + tier, with Editor AssetDatabase fallback.
- **`GetTierUpgradeStatDelta()` helper** — computes production rate, power, and troop capacity deltas between tiers.

### CHANGED
- `UpdateZoomDetailVisibility()` expanded from 3 tiers (detail/medium/category) to 4 tiers (far/medium/close/inspection) with 10 element types controlled
- Info panel layout adjusted: tier preview occupies 0.68-0.86 vertical range, description shifted to 0.55-0.67, stats to 0.52 baseline
- Category icons now hide at close zoom (>1.3x) to avoid visual clutter with level badges

---

## [0.48.0] — 2026-03-12 (Ralph Loop Iteration 43: Buff Icons, Fly-to-Bar, Resource Particles)

### ADDED
- **Buff indicator icons on buildings** — resource buildings show a green ⬆ production boost icon, military buildings show a red ⚔ combat boost icon. Small dark-pill badges in the bottom-left area. Integrates with ResearchManager to check if bonuses are active.
- **Resource fly-to-bar particles** — collecting resource bubbles spawns 3 colored particles that fly from the building toward the resource bar at the top of the screen. Particles accelerate with an ease-in curve, arc slightly sideways, shrink as they approach the target, and fade at the end. Each particle staggered by 0.05s for a cascade effect.
- **Screen-space particle positioning** — fly particles use `WorldToScreenPoint` → normalized screen coords for accurate start position regardless of zoom/scroll state.

### CHANGED
- `OnResourceCollected` now calls `SpawnResourceFlyParticle()` alongside SFX and toast
- Building creation pipeline now calls `CreateBuffIndicators()` after garrison labels
- Fly particle uses `Screen.width/height` normalization instead of non-existent `ScreenPointToNormalizedRectanglePoint`

---

## [0.47.0] — 2026-03-12 (Ralph Loop Iteration 42: NEW Badge, Garrison Labels, Category Icons)

### ADDED
- **"NEW" badge on upgrade completion** — when a building finishes upgrading, a red "NEW" badge appears in the top-right corner. Stays visible for 8 seconds, then fades out over 2 seconds using CanvasGroup alpha. Count badges also refresh on upgrade completion.
- **Garrison troop count on military buildings** — barracks, training_ground, and armory now show a troop count label (⚔ icon + count) in the top-right. Barracks: 500×tier, Training Ground: 300×tier, Armory: 200×tier. Red-tinted text with dark pill background.
- **Category icons in build selector** — each category row in the building placement selector now shows a Unicode icon: ⚔ Military, ⛏ Resource, ⚗ Research, ✨ Magic, ⛨ Defense, ❤ Social.

### CHANGED
- `OnBuildingUpgradeCompleted` now calls `CreateNewBadge()` and refreshes count badges
- `CreateGarrisonLabel()` added to building creation pipeline alongside production labels
- Build selector category labels now include icon prefix

---

## [0.46.0] — 2026-03-12 (Ralph Loop Iteration 41: Stronghold Requirements, Offline Banner, Tap Ripple)

### ADDED
- **Stronghold level requirements** — `GetUpgradeBlockReason` now checks that the Stronghold tier is high enough before allowing building upgrades. Non-stronghold buildings need Stronghold at least as high as their target tier. Shows "Requires Stronghold Lv.X" toast when blocked.
- **Offline earnings "Welcome Back" banner** — on scene load, a P&C-style center-screen banner shows "While you were away..." with simulated resource earnings (grain/iron/stone). Has a green COLLECT button and auto-dismisses after 8 seconds.
- **Tap ripple VFX** — tapping a building spawns an expanding gold radial gradient circle that fades out over 0.4s. Uses `radial_gradient.png` sprite for smooth circle shape. Added alongside existing tap sparkles and bounce animation.

### CHANGED
- `GetUpgradeBlockReason` now iterates `PlacedBuildings` to find stronghold tier for requirement check
- `Start()` triggers `ShowOfflineEarningsBanner()` coroutine after 1.5s delay
- Building tap handler now calls `SpawnTapRipple()` alongside `SpawnTapSparkles()`

---

## [0.45.0] — 2026-03-12 (Ralph Loop Iteration 40: Demolish Dialog, Power Display, Info Panel Actions)

### ADDED
- **Demolish confirmation dialog** — tapping "Demolish" in the building info panel opens a P&C-style confirmation dialog with skull icon, warning text ("This action is irreversible. No resources will be refunded."), Cancel and DEMOLISH buttons. Red-bordered panel with fade-in animation. Cannot demolish stronghold.
- **Building power contribution** — info panel now shows "⚔ Power: +X" for each building based on type and tier. Stronghold gives 5K base, military 1.5K, defense 1K, research 600, resource 300, etc., multiplied by (tier+1).
- **Upgrade button in info panel** — full info panel now has an Upgrade button (green) alongside Demolish (red) at the bottom. Checks upgrade block reasons before proceeding. Shows "MAX LEVEL" in grey when maxed.

### CHANGED
- Building info panel layout updated with action buttons row at bottom (Demolish left, Upgrade right)
- Added `ShowDemolishConfirmDialog()` with dim overlay, warning text, Cancel/Confirm buttons
- Added `GetBuildingPowerContribution()` static helper for power calculation

---

## [0.44.0] — 2026-03-12 (Ralph Loop Iteration 39: Speed-Up Button, Queue Position Labels)

### ADDED
- **Speed-up button on progress bar** — upgrading buildings show a tappable speed-up button below the progress bar. Shows "⚡ FREE" in green when under 5min remaining, or "⚡ [time]" in purple for longer upgrades. Tapping opens the gem speed-up dialog (or applies free speedup if under threshold).
- **Queue position labels** — when multiple buildings are upgrading simultaneously, each shows a "#1/2" position label above the building. Label auto-updates on the 2s refresh cycle. Hidden when only one item is in queue. Cleaned up when upgrade completes.
- **Speed-up button live updates** — the speed-up button label updates every 2s to reflect current remaining time and switches to "FREE" when threshold is crossed.

### CHANGED
- `CreateUpgradeProgressBar` now takes `instanceId` and `remainingSeconds` params for speed-up button
- `RefreshUpgradeProgressBar` computes queue position and manages queue labels alongside progress bars
- Progress bar cleanup also removes queue position labels

---

## [0.43.0] — 2026-03-12 (Ralph Loop Iteration 38: Upgrade Progress Bar, Scaffolding Integration)

### ADDED
- **Upgrade progress bar on buildings** — buildings currently being upgraded show a green progress bar with percentage text overlaid at the building's center. Progress is calculated from `BuildQueueEntry.StartTime` and `RemainingSeconds`. Bar updates every 2s on the periodic refresh cycle.
- **Scaffolding + progress bar combo** — upgrading buildings now show both the existing scaffolding overlay (diagonal construction lines, shimmer animation) AND the new progress bar simultaneously, matching P&C's construction visual.
- **Progress bar cleanup** — progress bar and scaffolding are automatically removed when the upgrade completes.

### CHANGED
- `RefreshUpgradeArrows()` now also manages progress bars and scaffolding for upgrading buildings
- Added `RefreshUpgradeProgressBar()` method to create/update progress bar fill based on real build queue timing
- Uses existing `AddScaffoldingOverlay()` instead of duplicating scaffolding code

---

## [0.42.0] — 2026-03-11 (Ralph Loop Iteration 37: Stronghold Banner, Count Badges, Auto-Dismiss)

### ADDED
- **Stronghold recommended upgrade banner** — when no upgrades are active and stronghold isn't at max tier, a pulsing "⬆ UPGRADE STRONGHOLD" banner appears above the stronghold with dark/gold P&C styling. Tapping the banner starts the upgrade directly. Banner auto-hides when an upgrade is in progress or stronghold is maxed.
- **Building count badges** — buildings that allow multiples (grain_farm 5, iron_mine 3, stone_quarry 3, arcane_tower 2, barracks 2, training_ground 2, wall 4) show a small "#/max" badge in the bottom-left corner. Text turns red when at capacity.
- **Auto-dismiss popup after 5s** — building info popup now auto-fades out after 5 seconds of inactivity. Uses a 0.25s fade-out animation before destroying. Dismissing manually or tapping another building cancels the timer.

### CHANGED
- Added `MaxBuildingCountPerType` dictionary for building instance limits
- Added `_popupAutoDismiss` coroutine tracking field for proper cleanup
- `RefreshStrongholdUpgradeBanner()` runs on the 2s periodic refresh cycle alongside upgrade arrows

---

## [0.41.0] — 2026-03-11 (Ralph Loop Iteration 36: Popup Timer, Collect Toast, Move Coords)

### ADDED
- **Live upgrade timer in popup** — when tapping a building that's currently upgrading, the popup shows a live countdown timer (updates every 0.5s) in gold text instead of the Upgrade button. Shows "Complete!" in green when upgrade finishes while popup is open.
- **Resource collection summary toast** — collecting resource bubbles shows a floating "+500 Grain" toast in resource-themed color (green grain, blue iron, tan stone, purple arcane). Batches multiple quick collections within 0.8s window. Toast floats upward and fades over 1.2s.
- **Move mode coordinate label** — during building drag, a floating label shows the current grid snap position "(x, y)" near the ghost. Green when placement is valid, red when invalid. Destroyed on move mode exit.

### CHANGED
- `OnResourceCollected` handler replaces inline lambda — now plays SFX + triggers batched toast
- Popup `OnBuildingTappedShowPopup` checks `BuildQueue` for active upgrade before showing buttons
- `OnDrag` calls `UpdateMoveCoordLabel` during move mode
- `ExitMoveMode` calls `DestroyMoveCoordLabel` for cleanup

---

## [0.40.0] — 2026-03-11 (Ralph Loop Iteration 35: Build Selector, Queue Panel, Resource Cap Warning)

### ADDED
- **Empty cell → building placement selector** — tapping empty ground opens a category-grouped building selector panel (Military/Resource/Research/Magic/Defense/Social). Each building is a tappable button that publishes `PlacementConfirmedEvent`. Category colors match popup header scheme. Panel has grid coordinates, close button, and fade-in animation.
- **Build queue expandable panel** — tapping the Builder HUD (now a button) toggles a dropdown panel showing active queue entries with building name, target tier, and remaining time. Auto-dismisses after 5 seconds. Shows "No upgrades in progress" when empty.
- **Resource near-cap warning badges** — resource buildings (farm, mine, quarry, arcane tower) show a red "FULL" badge when their resource type is at 90%+ of vault capacity. Badges are created/removed on the 2s refresh cycle alongside upgrade arrows.

### CHANGED
- Builder count HUD is now tappable (Button component added)
- `Update()` periodic refresh now also calls `RefreshResourceCapWarnings()`
- `OnEmptyCellTapped` subscribes in `OnEnable`, wired to show build selector
- Empty cell tap event was already published; now consumed by placement UI

### FIXED
- `BuildQueueEntry.BuildTimeSeconds` → `RemainingSeconds` (correct property name from BuildingManager)

---

## [0.39.0] — 2026-03-11 (Ralph Loop Iteration 34: Sprite Swap, Arrow Upgrade, Requirement Warnings, Idle Breath)

### ADDED
- **Building sprite swap on upgrade** — when upgrade completes, building Image sprite is reloaded for the new tier (e.g., `forge_t1` → `forge_t2`). Production rate label also refreshes to show new output.
- **Quick-upgrade from arrow tap** — orange ▲ upgrade arrows are now tappable buttons with dark background. Tapping directly publishes `BuildingDoubleTappedEvent` to trigger upgrade, matching P&C one-tap upgrade flow.
- **Upgrade requirement warnings** — Upgrade button now checks affordability before proceeding. If blocked, shows a red toast at bottom with specific reason: "Not enough: Stone (2.5K), Iron (1.2K)" or "Build queue full (2/2)". Toast auto-fades after 2.5s. MAX LEVEL buildings show grayed-out "MAX" button.
- **Building idle breathing animation** — all buildings gently scale-pulse (±0.8%, stronghold ±1.5%) at randomized speeds and phases so no two buildings breathe in sync. Gives the city screen a living, organic feel matching P&C.

### CHANGED
- `OnUpgradeCompletedSfx` now calls `RefreshBuildingSprite` and `RefreshProductionLabel` alongside badge refresh
- `CreateUpgradeArrow` now accepts instanceId/buildingId/tier params and creates a tappable Button
- Upgrade popup button checks `GetUpgradeBlockReason` before publishing upgrade event
- Arrow animation reads text from child `ArrowText` GameObject instead of root

---

## [0.38.0] — 2026-03-11 (Ralph Loop Iteration 33: Free Speed-Up, Completion Banner, Info Panel)

### ADDED
- **Free speed-up for timers under 5 minutes** — P&C standard: upgrades with < 300s remaining show green "FREE" label on speed-up button and complete instantly without gem cost. Threshold constant `FreeSpeedUpThresholdSeconds`.
- **Upgrade completion banner** — gold-bordered notification banner slides in from top of screen with building name + new tier, holds 2.5s, then slides out with fade. Uses `SmoothStep` easing for polished motion.
- **Building info detail panel** — tapping "Info" button in popup opens a full overlay panel with: building name (category-colored), level with star, description text for all 21 building types, production rate for resource buildings, next upgrade costs, and close (X) button. Tap outside to dismiss. P&C-style dark panel with gold border.

### CHANGED
- Speed-up button `onClick` now checks `FreeSpeedUpThresholdSeconds` before showing gem dialog
- Speed-up button label shows "FREE" (green, 8pt) for short timers vs lightning bolt for longer ones
- Info popup "Info" button now calls `ShowBuildingInfoPanel` instead of dismissing
- `OnUpgradeCompletedSfx` now calls `ShowUpgradeCompleteBanner` with building display name

---

## [0.37.0] — 2026-03-11 (Ralph Loop Iteration 32: Upgrade Celebration, Tap Sparkles, Speed-Up Dialog)

### ADDED
- **Upgrade completion celebration** — white-gold flash overlay fades out over 0.4s, 8 golden burst particles fly outward with shrink+fade, followed by a big 15% celebratory bounce. Level badge refreshes to new tier immediately. Full P&C-style "level up" feel.
- **Tap sparkle particles** — 5 warm gold/white sparkle particles burst from building center on every tap, with random angles, speeds, and lifetimes (0.25-0.45s). Adds tactile feedback matching P&C.
- **Speed-up confirmation dialog** — tapping the speed-up button now shows a centered modal dialog with dark overlay, gold-bordered panel, gem cost display, time remaining, CONFIRM (green) and CANCEL (dark) buttons. Tap outside to dismiss. Prevents accidental gem spending.

### CHANGED
- `OnUpgradeCompletedSfx` now updates placement tier, refreshes level badge, and triggers celebration coroutine
- Speed-up button click now calls `ShowSpeedUpDialog` instead of directly publishing `SpeedupRequestedEvent`
- Added `RefreshLevelBadge`, `FormatTimeRemaining`, `AnimateBurstParticle`, `AnimateTapSparkle` helper methods

---

## [0.36.0] — 2026-03-11 (Ralph Loop Iteration 31: Stronghold Glow, Category Colors, Bounce Scale)

### ADDED
- **Stronghold special glow** — 2-layer radial gradient glow beneath stronghold building (outer amber 120px, inner gold 80px) with continuous pulse animation cycling opacity. P&C-style dramatic focal centerpiece.
- **Category-specific popup header colors** — building info popup name text now colored by category: military buildings (red), resource producers (green), research/magic (blue), defense (steel), stronghold (gold). Matches P&C visual differentiation.
- **Category-specific bounce scale** — tap bounce intensity varies by building type: stronghold 12%, military 9%, default 8%. Gives important buildings more visual weight on tap.

### CHANGED
- `CreateBuildingVisual` now calls `CreateStrongholdGlow` for stronghold placement
- `OnBuildingTappedShowPopup` uses `GetCategoryHeaderColor` for popup name coloring
- `BounceBuilding` accepts optional `bounceScale` parameter (default 0.08f)
- Tap handler passes `GetCategoryBounceScale` result to `BounceBuilding`

---

## [0.35.0] — 2026-03-11 (Ralph Loop Iteration 30: Collect-All Pulse, Alliance Help, Zoom LOD)

### ADDED
- **Collect-all button pulse animation** — COLLECT ALL button gently pulses green brightness and scales ±3% at 2.5Hz to draw attention (P&C style)
- **Alliance help button on upgrade indicator** — small blue "Help" button below speed-up, publishes `AllianceHelpRequestedEvent`
- **Enhanced zoom-dependent detail levels** — arrows and count badges hide below 0.7x; production labels hide below 1.0x; upgrade indicators always visible

### CHANGED
- `UpdateZoomDetailVisibility` manages 6 element types across zoom thresholds
- Upgrade indicator fits 3 interactive elements: timer, speed-up, alliance help

---

## [0.34.0] — 2026-03-11 (Ralph Loop Iteration 29: Upgrade Arrows, Builder HUD, Scaffolding)

### ADDED
- **Upgrade-available arrow indicators** — P&C-style orange pulsing ▲ arrows bob above buildings that can afford their next upgrade. Refreshed every 2s. Hidden when building is currently upgrading.
- **Builder count HUD** — "⚒ Builder 0/2" display in top-left corner. Gold text when slots available, red when queue full. Updates on upgrade start/complete.
- **Construction scaffolding overlay** — semi-transparent brown overlay with 3 diagonal construction lines and pulsing shimmer on buildings during upgrade. Added on upgrade start, removed on completion. Persists across scene load via RefreshUpgradeIndicators.

### CHANGED
- OnUpgradeStarted now also adds scaffolding overlay and removes upgrade arrow
- OnUpgradeCompletedSfx now removes scaffolding and updates builder count HUD
- RefreshUpgradeIndicators adds scaffolding for in-progress upgrades on scene load
- Update() runs periodic upgrade arrow + builder count refresh every 2 seconds

---

## [0.33.0] — 2026-03-11 (Ralph Loop Iteration 28: Move Confirm/Cancel, Upgrade Costs)

### ADDED
- **Move mode confirm/cancel bar** — P&C-style floating bar at bottom of screen during move mode with red Cancel and green Confirm buttons. Cancel returns building to original position without committing. Bar auto-destroyed on exit.
- **Upgrade cost preview in info popup** — popup now shows next-tier resource costs (◈Stone ♦Iron ❀Grain ✦Arcane) between building name and action buttons, with formatted K/M suffixes. Shows "MAX LEVEL" when fully upgraded.
- **Cancel preserves original position** — CancelMoveMode restores building to `_moveOriginalOrigin` without any grid state changes

### CHANGED
- Info popup expanded from 80px to 95px height to accommodate cost line
- `EnterMoveModeForBuilding` now also dims non-moving buildings (was missing from popup-triggered path)
- Added `using AshenThrone.Data` import for BuildingData access

---

## [0.32.0] — 2026-03-11 (Ralph Loop Iteration 27: Info Popup, Speed-Up Button)

### ADDED
- **Building info popup on tap** — tapping a building shows a floating popup above it with building name, level, and three action buttons: Upgrade (green), Info (blue), Move (brown). P&C-style dark panel with gold border, fade-in animation. Dismissed on deselect or empty tap.
- **Speed-up gem button on upgrade indicator** — upgrading buildings now show a green ⚡ speed-up button on the right side of the indicator. Publishes `SpeedupRequestedEvent` with estimated gem cost (1 gem per 60s remaining).
- **Popup Move button** — triggers `EnterMoveModeForBuilding` directly from the popup, matching P&C's tap → popup → move flow
- **Popup Upgrade button** — publishes `BuildingDoubleTappedEvent` to trigger upgrade via existing `QuickUpgradeHandler`

### CHANGED
- Upgrade indicator layout widened (5%-95% anchors) with timer text moved left to make room for speed-up button
- `ClearBuildingFootprint` now also dismisses info popup
- `CreateUpgradeIndicator` accepts optional `instanceId` parameter for speed-up event binding

---

## [0.31.0] — 2026-03-11 (Ralph Loop Iteration 26: Upgrade Indicators, Count Badges, Crossfade)

### ADDED
- **Instance count badges** — multi-copy buildings (e.g. 5 grain farms) now display a small "x3" badge at top-right corner, auto-refreshed on upgrade complete
- **Smooth sprite crossfade on tier upgrade** — upgrade completion flashes white (0.15s) then fades back (0.2s) instead of instant sprite swap
- **Upgrade queue indicator** — buildings currently upgrading show a dark pill overlay with hammer icon, live countdown timer, and gold progress bar that fills in real time
- **Upgrade indicator pulsing** — indicator gently pulses alpha (0.75–1.0) at 2.5Hz for a living feel
- **In-progress upgrade detection on Start** — buildings already upgrading when the scene loads get their upgrade indicator restored from BuildingManager.BuildQueue
- **Upgrade SFX hooks** — level-up SFX on upgrade start, build-complete SFX on upgrade finish, wired via dedicated event handlers

### CHANGED
- OnBuildingUpgradeCompleted now triggers crossfade animation instead of instant sprite swap
- Event subscriptions use named method references (OnUpgradeStarted, OnUpgradeCompletedSfx) instead of inline lambdas

---

## [0.30.0] — 2026-03-11 (Ralph Loop Iteration 25: Cascade Collect, Selection Outline, Move Dim)

### ADDED
- **Collect-all cascade** — "Collect All" button now triggers staggered collection (0.08s per bubble) for a satisfying P&C-style cascade instead of instant batch
- **Golden selection outline** — tapped building gets a golden Outline component (2px, 85% alpha) for clear visual selection, removed on deselect
- **Move mode dim overlay** — all non-moving buildings dim to 50% alpha with grey tint during move mode, restoring to full brightness on exit

### CHANGED
- CollectAll() now uses coroutine-based cascade instead of synchronous loop
- ClearBuildingFootprint removes Outline components from all buildings on deselect

---

## [0.29.0] — 2026-03-11 (Ralph Loop Iteration 24: Shadows, Depth Sort, Zoom Toggle)

### ADDED
- **Building drop shadows** — every building gets a subtle oval shadow (radial gradient, 30% alpha) at its base for P&C-style depth perception
- **Isometric depth sorting** — buildings sorted by grid X+Y sum so front buildings correctly overlap back ones. Re-sorts after moves/swaps.
- **Empty-ground double-tap zoom toggle** — double-tapping empty ground toggles between close (2.0x) and overview (0.6x) zoom with smooth ease-out cubic animation

### CHANGED
- CreateBuildingVisual now adds shadow as first sibling (behind building sprite)
- SortBuildingsByDepth called on Start and after every move/swap operation

---

## [0.28.0] — 2026-03-11 (Ralph Loop Iteration 23: Hold Indicator, Drop Slam, Selection Glow)

### ADDED
- **Long-press hold indicator** — radial fill ring appears above finger during 0.5s long-press, gold fill sweeps clockwise showing progress toward move mode entry. Destroyed on release or mode entry.
- **Building drop/slam animation** — placing a building plays a 3-phase animation: overshoot scale (0.12s), squash-bounce on impact (0.18s), expanding dust ring fade (0.35s)
- **Pulsing selection ring** — tapped building's golden glow ring now pulses alpha (0.35-0.50) at 3Hz for a living, breathing selection feel

### CHANGED
- Selection ring size increased 1.2→1.4x width, 0.6→0.7x height for better visibility at all zoom levels
- Hold indicator only shows after 100ms delay to avoid flash on quick taps

---

## [0.27.0] — 2026-03-11 (Ralph Loop Iteration 22: Move Mode Polish, Swap Celebration, Placement Shake)

### ADDED
- **Move mode SFX + haptic** — entering move mode (long-press) plays tap SFX with haptic vibration, exiting plays build-complete SFX
- **Swap celebration animation** — swapping two buildings bounces both with level-up SFX for satisfying feedback
- **Invalid placement shake** — dropping a building on an invalid spot shakes it horizontally (3 oscillations, 0.3s, decaying amplitude)
- **Upgrade started SFX** — starting a building upgrade plays level-up sound via EventBus subscription
- **Larger resource bubble tap area** — increased from 36×36 to 56×56 pixels for easier mobile tapping (P&C-style generous hit targets)

### CHANGED
- TrySwapBuildings now returns bool to signal success/failure for caller feedback
- CityGridView subscribes to BuildingUpgradeStartedEvent for audio integration

---

## [0.26.0] — 2026-03-11 (Ralph Loop Iteration 21: Soft-Center, Audio SFX, Production Labels)

### ADDED
- **Single-tap soft-center** — tapping a building gently nudges the viewport 60% toward centering on it (ease-out quadratic, 0.25s). P&C-style subtle focus without jarring snaps.
- **Audio SFX integration** — building tap plays `sfx_btn_click`, resource collection plays `sfx_collect_resource`, upgrade completion plays `sfx_building_complete`, all via AudioManager pool
- **Production rate labels** — verified all resource buildings (grain_farm, iron_mine, stone_quarry, arcane_tower) show persistent "+X/hr" labels in city view, color-coded by resource type

### CHANGED
- CityGridView now subscribes to ResourceCollectedEvent and BuildingUpgradeCompletedEvent for audio feedback
- Tap handler integrates soft-center after footprint highlight, before event publish

---

## [0.25.0] — 2026-03-11 (Ralph Loop Iteration 20: Double-Tap Zoom, Category Icons, Haptics)

### ADDED
- **Double-tap zoom to building** — double-tapping a building smoothly zooms in (ease-out cubic, 0.35s) and centers the viewport on it, alongside triggering quick upgrade
- **Building category mini-icons** — small symbol icons on buildings (sword=military, pickaxe=resource, star=magic, sun=research, chess=social) visible at medium zoom (0.6-1.3x) for at-a-glance identification
- **Haptic feedback** — building taps and double-taps trigger device vibration on iOS/Android

### CHANGED
- UpdateZoomDetailVisibility now manages three LOD tiers: category icons (medium zoom), level badges + name labels (close zoom)
- EnsureCategoryIcon called on both scene registration and dynamic placement

---

## [0.24.0] — 2026-03-11 (Ralph Loop Iteration 19: Smooth Zoom, Elastic Scroll, Detail LOD)

### ADDED
- **Smooth zoom interpolation** — mouse scroll zoom now lerps smoothly to target (ZoomLerpSpeed=8) instead of instant jumps
- **Zoom-level detail visibility** — level badges and name labels hide when zoomed out below 1.0x for clean strategic overview
- **Elastic scroll bounce** — scroll edges now have subtle elastic bounce (elasticity=0.08) instead of hard clamp

### CHANGED
- ScrollRect deceleration rate increased from 0.1 to 0.135 for smoother momentum (P&C-style)
- Mouse scroll zoom speed increased from 0.1 to 0.15 for more responsive zoom steps
- Pinch zoom syncs _targetZoom to prevent smooth-zoom fighting with direct pinch input

---

## [0.23.0] — 2026-03-11 (Ralph Loop Iteration 18: Quick Actions, Resource Glow, Info Flow)

### ADDED
- **BuildingQuickActionMenu** — P&C-style radial action buttons (Upgrade, Info, Move) on building tap with pop-in animation, selection ring, and auto-dismiss
- **BuildingInfoRequestedEvent** — separate event for full info popup, decoupled from building tap
- **Resource building golden glow** — pulsing radial glow on resource buildings (farms, mines, quarries, arcane towers) replaces red "!" badge for P&C visual match
- **Direct quick upgrade** — tapping "UPGRADE" in quick menu starts upgrade immediately if affordable, falls back to info popup if not

### CHANGED
- Building tap flow now matches P&C: tap → radial quick actions → full popup only via "Info" button
- BuildingInfoPopupController defers to quick action menu when present (no duplicate popup)

---

## [0.22.0] — 2026-03-11 (Ralph Loop Iteration 17: Grid Fix, Overlap Fix, Construction Overlay)

### ADDED
- **Isometric diamond move-mode grid** — during building move mode, nearby cells render as proper isometric diamonds colored by occupancy (green=empty, red=occupied)
- **BuildingConstructionOverlay** — P&C-style progress bar, timer countdown, and animated hammer icon directly on buildings during upgrade. Flashes gold when <10s remaining.
- **Level-up burst animation** — golden radial flash with "LEVEL UP" text on upgrade completion
- **ClearCellsForInstance** — safe occupancy clearing that only removes cells belonging to the specific building instance (prevents corrupting adjacent buildings)
- **Isometric diamond grid overlay texture** — proper 2:1 ratio diamond tile for the background grid

### FIXED
- **28 overlapping building positions** in EmpireCityLayoutGenerator — all 106 buildings now have clean, non-overlapping footprints
- **Occupancy corruption during move mode** — ClearCells no longer removes cells owned by other buildings
- **RegisterSceneBuildings overlap detection** — logs warnings and gracefully handles overlapping placements (first-come-first-served)

### CHANGED
- Grid overlay texture generates proper isometric diamond pattern (64x32 tile) instead of 45-degree diagonal lines
- Move mode highlight cells rebuilt as individual isometric diamonds instead of a single flat rectangle

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
