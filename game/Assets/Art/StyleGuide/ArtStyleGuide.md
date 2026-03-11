# Ashen Throne — Art Style Guide

## Visual Identity

**Genre:** Dark Fantasy
**Mood:** Ominous, atmospheric, richly colored
**Influences:** Dark Souls, Hades, Darkest Dungeon, Magic: The Gathering

## Color Palette

### Primary Colors
| Name | Hex | Usage |
|------|-----|-------|
| Ashen Gray | #2D2D3D | UI backgrounds, shadows |
| Ember Orange | #E8651A | Fire elements, alerts, damage |
| Void Purple | #6B2FA0 | Magic, arcane, premium items |
| Dawn Gold | #D4A84B | Holy, currency, highlights |
| Blood Crimson | #8B1A1A | Health, danger, critical |

### Faction Colors
| Faction | Primary | Secondary |
|---------|---------|-----------|
| Iron Legion | #7A8B9E | #3D4F63 |
| Ash Cult | #C44B1B | #8B2E10 |
| Stone Sanctum | #8B7355 | #5C4A32 |
| Wild Hunters | #3A7D44 | #1F4D2E |
| Void Reapers | #6B2FA0 | #3D1A60 |

### Element Colors
| Element | Color | Hex |
|---------|-------|-----|
| Fire | Bright Orange-Red | #FF4500 |
| Ice | Crystal Blue | #4FC3F7 |
| Lightning | Electric Yellow | #FFD700 |
| Shadow | Deep Purple | #4A0E78 |
| Holy | Radiant Gold | #FFD54F |
| Nature | Forest Green | #2E7D32 |
| Physical | Steel Gray | #90A4AE |
| Arcane | Mystic Teal | #00BFA5 |

## Character Art Standards

### Portraits (512×512)
- Bust shot: shoulders and above
- 3/4 angle preferred
- Dark, moody background (not transparent)
- Dramatic rim lighting from behind
- Eyes should be the focal point
- Faction colors visible in costume

### Full Body (512×1024)
- Full figure, head to feet visible
- Action-ready pose (not T-pose)
- Weapon visible and prominent
- Magical effects if applicable (auras, particles)
- Dark environment background

### Animation Sprites (Spine 2D)
- Idle: subtle breathing, cape/hair sway
- Attack: weapon swing with anticipation frame
- Hit: knockback with squash
- Death: dramatic collapse with particle dissolve
- Victory: triumphant pose with effect burst

## Building Art Standards

### Tier Progression (3 visual tiers)
- **Tier 1 (Basic):** Simple wooden/stone construction, small, humble
- **Tier 2 (Upgraded):** Reinforced with better materials, medium size, decorations
- **Tier 3 (Masterwork):** Grand, ornate, fully realized, large with magical effects

### Isometric Perspective
- 30-degree isometric angle
- Buildings should fit within 512×512 canvas
- Consistent light source: upper-left at 45 degrees
- Ground shadow visible

## Card Frame Art

### Dimensions: 384×576 (2:3 ratio)
- Ornate border matching element type
- Center area clear for card illustration
- Bottom area for text/stats
- Element icon position: top-left corner
- Cost indicator: top-right corner

## Environment Tiles

### Combat Tiles (512×512)
- Top-down view with slight perspective
- Seamless edges for grid tiling
- Visual variety per terrain type
- Subtle damage/wear details

### World Map Regions (1024×1024)
- Bird's eye view
- Painterly style (less detailed than character art)
- Atmospheric perspective (distant areas hazier)
- Key landmarks visible

## UI Design Rules

1. **No sharp corners** — all UI panels have rounded edges or ornate frames
2. **Dark backgrounds** — panels use Ashen Gray (#2D2D3D) at 90% opacity
3. **Gold accents** — interactive elements use Dawn Gold for highlights
4. **Icon consistency** — all icons use same stroke weight and padding
5. **Readable text** — minimum 18pt for body text, high contrast on dark backgrounds
6. **Bar fills** — left-to-right fill, rounded ends, glow effect at fill edge

## Technical Requirements

- All images: PNG format, sRGB color space
- Hero portraits: 512×512, 24-bit color
- Hero full body: 512×1024, 24-bit color
- Building sprites: 512×512 per tier
- Card frames: 384×576
- UI elements: power-of-2 dimensions where possible
- Sprite atlases: max 4096×4096
- No alpha premultiplication (straight alpha)
