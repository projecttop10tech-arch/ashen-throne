# Ashen Throne — Art Style Guide

**Version:** 0.1.0
**Last Updated:** 2026-03-07

---

## 1. Visual Identity

**Style:** Dark Fantasy with High Readability
Not grimdark — rich, atmospheric, readable at 375px wide. Think Diablo 3 color clarity meets Darkest Dungeon atmosphere, minus the desaturation.

**Three Words:** Atmospheric. Powerful. Legible.

---

## 2. Color Palette

### Primary Brand Colors
| Name | Hex | Usage |
|---|---|---|
| Ashen Gold | `#C9922A` | Primary CTAs, headers, hero name text |
| Ember Red | `#B03A2E` | Combat damage, danger indicators, enemy UI |
| Deep Slate | `#1A1D2E` | Primary background, dark panels |
| Storm Grey | `#4A5568` | Secondary backgrounds, inactive elements |
| Bone White | `#E8E0D0` | Body text, subtitles |

### Faction Accent Colors
| Faction | Primary | Secondary |
|---|---|---|
| Iron Legion | `#7F8C8D` (Iron Grey) | `#D4AC0D` (Burnished Gold) |
| Ash Cult | `#6C3483` (Deep Purple) | `#E74C3C` (Ember Red) |
| Wild Hunters | `#27AE60` (Forest Green) | `#E67E22` (Amber) |
| Stone Sanctum | `#2980B9` (Sapphire) | `#F0E6D2` (Ancient Parchment) |
| Void Reapers | `#1A1A2E` (Void Black) | `#9B59B6` (Arcane Purple) |

### Status Effect Colors
| Status | Color | Hex |
|---|---|---|
| Fire / Burn | Flame Orange | `#E67E22` |
| Ice / Freeze | Crystal Blue | `#85C1E9` |
| Lightning / Stun | Electric Yellow | `#F7DC6F` |
| Shadow / Poison | Toxic Green | `#58D68D` |
| Holy / Heal | Radiant White | `#FDFEFE` |
| Bleed | Deep Crimson | `#922B21` |
| Shield | Shield Blue | `#5DADE2` |

### Accessibility — Color Blind Support
All status effects must use BOTH color AND iconography. Never communicate game state through color alone.
Three colorblind modes implemented: Protanopia, Deuteranopia, Tritanopia. Full palette swaps provided.

---

## 3. Typography

### Type Scale (pixel values at 1x — 375px base width)
| Use | Size | Weight | Font |
|---|---|---|---|
| Hero Name (large) | 32px | Bold | Cinzel |
| Section Header | 24px | SemiBold | Cinzel |
| Card Title | 20px | Medium | Lora |
| Body / Descriptions | 16px | Regular | Lora |
| Labels / Tags | 14px | Medium | Inter |
| Fine Print / Tooltips | 12px | Regular | Inter |

**Font Files Required:**
- Cinzel Regular, Bold (Google Fonts — free commercial license)
- Lora Regular, Medium, SemiBold (Google Fonts)
- Inter Regular, Medium, SemiBold (Google Fonts)

### Text Rules
- Minimum readable size: 12px (never go below)
- Text scaling option: 100%, 125%, 150% (accessibility)
- No text embedded in art assets — all text uses TextMeshPro components
- WCAG AA compliance required: 4.5:1 contrast ratio for all body text

---

## 4. UI Components

### Panels & Cards
- Background: Deep Slate (`#1A1D2E`) with 8% opacity inner shadow
- Border: 1px Ashen Gold (`#C9922A`) with 60% opacity
- Corner radius: 8px (panels), 4px (tags/chips)
- Padding: 16px (panels), 8px (compact elements)

### Buttons
| Type | Background | Text | Border |
|---|---|---|---|
| Primary | Ashen Gold `#C9922A` | Deep Slate | None |
| Secondary | Transparent | Ashen Gold | 1px Ashen Gold |
| Danger | Ember Red `#B03A2E` | Bone White | None |
| Disabled | Storm Grey `#4A5568` | `#6B7280` | None |

### Touch Targets
Minimum 44×44 points on all interactive elements. No exceptions.

### Spacing System
8px base unit. All spacing values must be multiples of 8px.
`8, 16, 24, 32, 40, 48, 64, 80, 96`

---

## 5. Hero Art Direction

### Portrait Style
- Bust shot, 3/4 angle
- Strong rim lighting (faction color)
- Dark, atmospheric background with faction motifs
- Expression: powerful/determined, not friendly/cute
- Canvas: 512×512px source, displayed at 128×128 (list) or 256×256 (detail)

### Full Body Style
- Upright stance, combat-ready
- Detailed armor/outfit clearly communicating faction and role
- Silhouette must be identifiable at 64px height
- Exported as PNG with transparent background

### Spine2D Animation Requirements
| Animation | Duration | Loop |
|---|---|---|
| Idle | 2–3s | Yes |
| Walk | 0.6s | Yes |
| Attack (normal) | 0.5s | No |
| Cast Ability | 0.8s | No |
| Hit Reaction | 0.3s | No |
| Death | 1.2s | No |
| Victory | 2s | Yes |

All animations must have ease-in (0.1s) and ease-out (0.1s). No linear keyframes on visible bones.

---

## 6. Empire Visual Direction

### Camera
- Isometric 45° angle, fixed perspective
- Slight atmospheric perspective haze at far edge (fog)
- Dynamic weather layer: rain, ash fall, aurora (particle systems)

### Building Visual Progression
- Each tier upgrade must be VISUALLY distinct — not just a size scale
- Color, complexity, and material quality increase with tier
- Tier 1: rough stone. Tier 5: ornate carved stone. Tier 10: glowing magical enhancements

### Terrain Tiles
- Base tile: 128×128px isometric diamond
- Variants per biome: grassland (default), scorched, arctic, enchanted forest
- Biome is cosmetic — no gameplay effect

---

## 7. Combat Arena Direction

### Grid Tiles
- 96×96px diamond tiles
- Subtle idle animation (shimmer, floating particles) for special tiles
- Clear visual distinction between terrain types — no reliance on color alone

### Combat Camera
- Fixed isometric view (same perspective as empire)
- Slight camera shake on heavy impacts
- Camera zoom out for AoE abilities, zoom in for death animations

### Particle Effect Guidelines
- Max 200 particles per effect
- LOD fallback: reduce to 50 particles on low-end devices
- Effect must complete within 1.5 seconds (no lingering overload)
- Must not obscure health bars, turn order, or card hand UI
- Test on dark AND bright tile backgrounds

---

## 8. World Map Direction

### Style
- Stylized satellite view (not realistic)
- Hexagonal tile grid, each hex 64×64px on screen at default zoom
- Territory ownership shown via border glow (faction color)
- Alliance headquarters show custom emblem

### Zoom Levels
- Level 1 (zoomed out): Overview, all territories visible, emblems shown
- Level 2 (default): Individual building icons on territories
- Level 3 (zoomed in): Full building detail, unit counts visible

---

## 9. Asset Specifications

### Texture Compression
| Platform | Format | Quality |
|---|---|---|
| iOS | ASTC 6×6 | High Quality |
| Android | ASTC 6×6 | High Quality |
| Android (Low-end) | ETC2 | Standard |

### Max Uncompressed Source Sizes
| Asset Type | Max Size |
|---|---|
| Hero Portrait | 512×512px |
| Building Sprite | 1024×1024px |
| UI Background | 2048×2048px |
| Icon | 256×256px |
| World Map Tile | 256×256px |

### Sprite Atlases
All UI sprites must be packed into atlases per screen/panel:
- `UI_Combat_Atlas` — combat HUD elements
- `UI_Empire_Atlas` — empire view UI
- `UI_Lobby_Atlas` — main menu UI
- `Hero_Portraits_Atlas` — all hero portrait thumbnails
- `Icons_Resources_Atlas` — resource and currency icons

---

## 10. Audio Direction

### Music
- 16 tracks minimum at launch
- Style: Dark orchestral, adaptive layers (tension → resolution)
- Instrumentation: strings, brass, choir, dark synth undertones
- Combat: high-energy, percussive, changes intensity based on HP state
- Empire: ambient, atmospheric, slower tempo
- Alliance War: epic, full orchestra, choir

### Sound Effects
- Ability impacts: each element has distinct sound profile
- Building construction: satisfying completion chime
- Resource collection: distinct coin-type sound per resource
- UI interactions: subtle, non-intrusive click/whoosh (never jarring)

### Haptic Feedback
- Heavy hit: impact pattern (Android: vibrate 80ms; iOS: UIImpactFeedbackGenerator.heavy)
- Build complete: success pattern (two short pulses)
- Level up: celebration pattern (ascending triple pulse)
- All haptics respect system vibration settings
- Haptic toggle in settings (default: on)
