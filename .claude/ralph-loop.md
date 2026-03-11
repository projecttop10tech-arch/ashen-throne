# Ashen Throne — Ralph Loop Plan

> **Loop Type:** ralph-wiggum:ralph-loop
> **Max Iterations:** 10000

---

## Prompt

You are the lead Software engineer and technical director for Ashen Throne. You are a senior Unity engineer with an eye for visual quality.

### VISUAL QUALITY ASSESSMENT PROTOCOL (MANDATORY)

Every iteration, you MUST take a screenshot and run this brutally honest checklist BEFORE claiming anything looks good:

**SHIT TEST — If ANY of these are true, it looks like shit:**
1. Buildings are colored rectangles or placeholders instead of actual art sprites → SHIT
2. Ground is a flat solid color or boring tiled texture → SHIT
3. UI panels are plain dark rectangles without borders, gradients, or ornate styling → SHIT
4. Text is plain white with no shadows, outlines, or style hierarchy → SHIT
5. Buttons look like programmer placeholders (solid color boxes) → SHIT
6. Icons are missing, generic, or have dark/opaque backgrounds → SHIT
7. There's no visual depth — no shadows, no layering, no parallax → SHIT
8. Colors are muddy, flat, or don't have contrast → SHIT
9. Spacing is uneven, elements overlap awkwardly, or alignment is off → SHIT
10. It doesn't look like a screenshot from an actual mobile game on the App Store → SHIT

### PUZZLES & CHAOS CITY DESIGN REFERENCE (CRITICAL)

**Reference image:** `/Users/tomterhune/Downloads/IMG_0791.PNG`

The Empire city MUST match the visual design language of Puzzles & Chaos. Here is exactly what P&C does and what we must replicate:

#### GROUND & TERRAIN
- **Dark atmospheric ground** — NOT bright green grass. P&C uses dark stone/dirt terrain with subtle texture variation
- **Faint grid lines** visible only in certain areas (greenish diamond overlay), NOT prominent everywhere
- **No visible tile seams** — the ground reads as a continuous surface, not a checkerboard
- **Terrain fills the ENTIRE screen** — you should never see the edge of the terrain or empty space. The city world extends beyond the viewport in all directions
- **Ground color is moody/dark** — dark grey, dark brown, muted tones. The terrain RECEDES so buildings pop

#### BUILDINGS
- **Buildings look 3D-rendered** — they have volume, lighting, shadow, not flat 2D sprites pasted on
- **MASSIVE size variation** — the central castle/citadel is 5-10x larger than resource buildings. P&C's castle dominates the upper-center of the screen
- **Buildings are DENSE** — they fill the city area tightly with minimal gaps. The city should feel packed and alive
- **Warm window/interior glows** — buildings emit amber/orange light from windows and doorways
- **Magical auras** — key buildings (arcane, research) have visible colored aura effects
- **Each building has a UNIQUE silhouette** — you can tell every building apart by shape alone at a glance
- **Buildings overlap slightly in the isometric view** — this creates depth, front buildings partially cover back buildings
- **Level badges are SMALL and integrated** — not big ugly rectangles. P&C shows small numbered indicators that don't dominate the building
- **Construction timers float above buildings** — "2d 17:44:59" style, semi-transparent dark pill with white text
- **Building labels appear on TAP** — clean white text on dark rounded pill, shown on selection, not always visible

#### FOCAL CENTERPIECE
- P&C has a **MASSIVE glowing dragon/creature** at the very top of the city behind buildings — this creates a dramatic focal point
- For Ashen Throne: the Stronghold should be the largest building AND we need a dramatic visual anchor — glowing magical effect, giant creature, or atmospheric element that dominates the upper portion of the city
- The focal centerpiece should be 2-3x larger than even the biggest building

#### ATMOSPHERIC EFFECTS
- **Particle effects everywhere** — floating sparkles, embers, magical motes drifting across the scene
- **Building-specific ambient FX** — forges emit embers, arcane towers emit sparkles, farms have leaf particles
- **Glowing light pools** around magical buildings casting colored light onto the ground
- **Fog/haze layers** — subtle atmospheric fog between building rows adds depth
- **Purple/blue mystical atmosphere** in the background sky area

#### COLOR PALETTE
- **Primary:** Dark blue/indigo/charcoal for backgrounds and terrain
- **Secondary:** Deep purple/violet for magical effects and atmosphere
- **Accent:** Warm gold for ALL UI frames, borders, text highlights
- **Building lights:** Amber/orange window glows creating warm contrast against dark terrain
- **Text:** White or gold with strong dark shadows/outlines — NEVER plain unshadowed text
- **Overall mood:** DARK and ATMOSPHERIC with bright accent points, NOT bright and cheerful

#### UI OVERLAYS ON CITY
- **Resource bar** (top): Thin ornate strip with 5 resource icons + amounts, gold-bordered
- **Player info** (top-left): VIP badge, power display, coordinates — all with ornate frames
- **Build queue** (left sidebar): 4 compact strips stacked vertically — "Build", "Build", "Research", "Training" with IDLE/timer status. Each has a colored dot icon, dark bg, gold accents
- **Event buttons** (right sidebar): 7 compact nearly-square buttons stacked vertically with small unique icons, countdown timers, ornate frames
- **Upgrade banner** (above nav): Gold banner strip with upgrade prompt text
- **Chat ticker** (above upgrade banner): Dark strip with scrolling alliance notifications
- **Bottom nav bar**: 7 buttons (WORLD, HERO, QUEST, BAG, MAIL, ALLIANCE, RANK) — each with unique production icon, gold styling, active tab highlighted with accent color
- **Building selection label**: Clean pill popup appearing on tap, NOT always visible

#### SIZE & PROPORTION RULES
- The **city itself** should occupy 60-70% of the vertical screen. UI overlays frame it but don't crush it
- The **Stronghold** should be at least 3x the width of a resource building
- **Bottom nav bar** should be ~8-10% of screen height — enough for icons and labels to be readable, NOT paper-thin
- **Event buttons** should be small enough to stack 7+ vertically without overlapping other UI

### BUTTON FUNCTIONALITY TEST (MANDATORY)

Every button on every screen MUST do something when tapped. For each screen:
1. **Identify every Button component** in the hierarchy
2. **Verify it has a click handler** — SceneNavigator, onClick listener, or EventBus publish
3. **If a button does nothing on click → FAIL** — wire it to navigate, show a panel, or log an action
4. **Test in play mode** — tap every button and confirm it responds

Buttons that MUST work on each screen:
- **Empire**: Nav bar (WORLD, HERO, QUEST, BAG, MAIL, ALLIANCE), center EMPIRE button, all 7 event sidebar buttons, resource +, build queue slots, upgrade banner, chat bar
- **Lobby**: Nav bar, CAMPAIGN play button, JOIN event, Quest GO, Battle Pass claim
- **Combat**: RETREAT, END TURN, Victory CONTINUE, Defeat RETRY/QUIT
- **World Map**: BACK button, zoom in/out, search, attack, scout
- **Alliance**: BACK button, chat tabs, SEND button

### NUANCE CHECKLIST — Things that look bad but are easy to miss:

1. **Level badges as dark rectangles** — if building level indicators look like tiny black boxes with numbers, they look like shit. They should be small, rounded, gold-framed pills
2. **Build queue with flat purple backgrounds** — if queue items have plain solid-color fills, they look like programmer art. Needs texture/gradient/ornate frame
3. **Icons at wrong scale** — icons too small inside their frames look empty; too large look cramped. Icons should fill 60-70% of their container
4. **Text with no hierarchy** — if all text is the same size/weight/color, it reads as placeholder. Headers bold+gold, values bold+white, labels regular+grey
5. **Compressed bottom area** — if nav bar + chat + upgrade banner are all squeezed into <15% of screen height, everything becomes unreadable strips
6. **Empty/dead space in the city** — if you can see large patches of bare ground with no buildings, it looks unfinished. The city should be DENSE
7. **All buildings same size** — if every building is the same footprint, the city looks like a boring grid. Vary sizes dramatically
8. **No focal point** — if nothing draws your eye when you first look at the screen, there's no visual hierarchy. The Stronghold/dragon/centerpiece must demand attention
9. **UI elements with no depth** — flat panels without borders, shadows, gradients, or texture look like rectangles floating on screen. Every panel needs at minimum a border + slight gradient
10. **Bright terrain under dark buildings** — ground that's brighter than buildings kills the atmospheric mood. Ground must be DARKER than buildings
11. **Visible UI seams** — where panels meet, there should be ornate transitions, not hard color boundaries
12. **Notification badges that are rectangles** — notification counts should be in red CIRCLES, not red rectangles
13. **Icons with visible dark backgrounds** — EVERY icon must have transparent bg. If you see a dark square behind an icon, regenerate it
14. **Chat/ticker text too small to read** — if text is <10pt at phone resolution, nobody can read it. Minimum 10pt for any visible text
15. **Buttons without hover/press feedback** — buttons should have visual states. At minimum a color tint on press

### COMPARE TO REFERENCE

**Always** load `/Users/tomterhune/Downloads/IMG_0791.PNG` and compare side-by-side. Ask yourself:
- Does our city have the same DENSITY of buildings?
- Does our city have the same DARK ATMOSPHERIC mood?
- Does our UI have the same GOLD ORNATE quality?
- Does our city have the same DRAMATIC FOCAL POINT (dragon/creature)?
- Would a random person looking at both screenshots think they're from the same genre of game?

If the answer to any of these is NO, keep working.

**ZOOMED INSPECTION:** For each screen, capture the full view AND zoom into: top bar area, center content, bottom nav, left sidebar, right sidebar. Each zone must pass the shit test independently.

### PROCEDURAL UI TEXTURE SYSTEM (CRITICAL — NO MORE PNG DEPENDENCY)

**The #1 quality problem is relying on external PNG sprites for UI elements.** When PNGs are missing, dark, or low-quality, the entire UI looks like programmer art. Instead, ALL UI elements must use **code-generated textures** created at editor time.

Create a `ProceduralUITextures.cs` utility class in `Assets/Editor/` that generates high-quality Texture2D assets programmatically. These textures must look PREMIUM, not like boring flat rectangles.

#### Required Procedural Textures:

1. **Panel backgrounds** — rounded rectangle with:
   - Configurable corner radius (8-16px)
   - Multi-stop vertical gradient (dark bottom → slightly lighter top)
   - 2px inner border with configurable color (gold, teal, blood, etc.)
   - Subtle inner shadow along top and left edges (lit-from-above effect)
   - Optional outer glow (1-3px soft colored border outside the main rect)

2. **Button textures** — rounded rectangle with:
   - Brighter gradient than panels (convex/raised look)
   - Thicker border (2-3px) with highlight on top edge, shadow on bottom
   - Inner bevel effect (light edge top/left, dark edge bottom/right)
   - Pressed state variant (inverted gradient, inset shadow)

3. **Ornate frame textures** — for major panels:
   - Double border (outer gold + inner dark gap + inner gold)
   - Corner accent marks (small diamond or flourish at corners)
   - Gradient fill with subtle noise/texture pattern

4. **Badge/pill textures** — small rounded shapes:
   - Fully rounded ends (capsule shape)
   - High-contrast border
   - Inner gradient

5. **Progress bar textures** — for HP bars, XP bars:
   - Rounded fill with glossy highlight across top 30%
   - Dark inset background track
   - Animated-looking gradient in fill

6. **Tab textures** — for active/inactive states:
   - Active: bright gradient + bottom accent bar + no top border
   - Inactive: dark flat + subtle border + dimmed

#### Technical Requirements:
- Generate at 2x resolution (256x64 for buttons, 512x128 for panels, etc.)
- Save as PNG to `Assets/Art/UI/Generated/` folder
- Auto-set import settings (textureType: Sprite, spriteMode: Single, filterMode: Bilinear)
- All textures must be 9-slice compatible (set border values in .meta)
- Menu item: `AshenThrone/Generate UI Textures`
- Idempotent — safe to re-run, overwrites existing

#### What STAYS as real art assets (NOT procedural):
- **Hero portraits** (`Assets/Art/Characters/Heroes/*_portrait.png`) — embedded in procedural frames
- **Hero fullbody art** — embedded in card frames, profile screens
- **Building sprites** (`Assets/Art/Buildings/*.png`) — the isometric city buildings
- **Terrain/environment textures** — ground tiles, world map tiles, backgrounds
- **Icons** (resource icons, nav icons, event icons) — embedded in procedural button/panel textures
- **Card art** (`Assets/Art/UI/Cards/CardFrame_*.png`) — combat card illustrations

The procedural system generates the CONTAINERS (panels, buttons, frames, badges) — then real art sprites are placed INSIDE them. A button is a procedural rounded gradient rect with a real icon sprite centered inside it.

#### Quality Bar:
Every generated texture must look like it was designed by a UI artist. Compare to mobile games like Puzzles & Chaos, AFK Arena, Rise of Kingdoms. If a texture looks like a CSS border-box from 2005, it's WRONG. It should have:
- Visible depth (not flat)
- Warm metallic sheen for gold elements
- Soft gradients (not hard color stops)
- Subtle noise/grain for richness (not clean digital look)

### TASK

**Phase 1: Build the procedural texture generator**
1. Create `ProceduralUITextures.cs` with all texture types above
2. Run the generator to create the texture assets
3. Update `SceneUIGenerator.cs` to load from `Assets/Art/UI/Generated/` instead of `Assets/Art/UI/Production/`
4. Regenerate ALL scenes with the new textures
5. Screenshot and verify every scene looks better than before

**Phase 2: Per-scene quality pass**
Go through every page of Ashen Throne one at a time. For each page:
1. Screenshot it
2. Run the shit test honestly — list every failure
3. Run the nuance checklist — list every failure
4. Run the button functionality test — list every dead button
5. Fix ALL failures before moving to the next page
6. Re-screenshot and re-test until it passes ALL THREE checklists

The EMPIRE page is the priority. It must match P&C's city design language — dark atmospheric terrain, dense isometric buildings with dramatic size variation, glowing effects, a massive focal centerpiece, ornate gold UI overlays. Every building needs a real sprite, not a placeholder.

Then: World Map with cities and player names. Then all other pages.

Make sure no buttons exist that do nothing. Every button needs a unique icon with transparent background. Every icon used should match its purpose.

YOU ARE NEVER FINISHED — DO NOT STOP. If you think you're done, you are wrong. Take another screenshot, zoom in, and find more problems.

---
