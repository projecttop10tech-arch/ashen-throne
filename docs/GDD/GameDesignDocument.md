# Ashen Throne — Game Design Document

**Version:** 0.1.0 (Phase 0)
**Last Updated:** 2026-03-07

---

## 1. Game Overview

**Title:** Ashen Throne
**Platforms:** iOS 15+, Android 11+
**Genre:** True Hybrid — 4X Strategy + Tactical Card Combat RPG
**Target Audience:** Ages 18–45, strategy and RPG fans, lapsed players of Evony/P&C/GoTC

### Elevator Pitch
Build your dark fantasy empire by day. Master tactical card combat by night. Unite your alliance to conquer a living world — without spending a fortune to win.

### Core Design Promises to Players
1. Skill beats wallet — a F2P player who masters tactics can defeat a paying player.
2. Your time is respected — no build timer exceeds 8 hours. No energy blocks play.
3. Your progress is permanent — no server merges. Your city exists until you choose to leave.
4. Honest monetization — every item in the cash shop is cosmetic or quality-of-life only.

---

## 2. Core Pillars

### Pillar 1: Tactical Mastery
Turn-based grid combat (5x7) with hero squads and ability card decks. Inspired by Into The Breach (positioning depth) and Slay the Spire (card synergy). PvP is async to eliminate lag/timing abuse.

### Pillar 2: Empire Ascendancy
Build a dark fantasy stronghold with 4 specialized districts. Meaningful architectural choices, visual progression, and cosmetic customization. Timers are generous; speedups are plentiful.

### Pillar 3: Alliance Conquest
30–50 player guilds compete for territory on a server world map. Weekly war windows (2 hours). Strategic depth: supply lines, fortifications, rally attacks. Cross-server tournaments monthly.

### Pillar 4: Hero Legacy
60 heroes at launch, 4–6 per season. All earnable through skill-based play. Each hero has a unique passive + ability card pool. Rarity affects base stats only; a Common hero built correctly beats a Legendary hero used poorly.

---

## 3. Combat System

### Grid Layout
- 5 columns × 7 rows
- Left 3 rows: player team zone (front/mid/back)
- Right 3 rows: enemy zone (front/mid/back)
- Center row: neutral terrain (contested tiles grant bonuses)

### Turn Structure
1. **Initiative Phase:** Speed stats determine turn order. Displayed on turn-order HUD.
2. **Draw Phase:** Active hero draws 3 cards from their shuffled 15-card deck. Hand max = 5.
3. **Action Phase:** Player plays 1–2 cards (limited by energy bar, max 4 energy per turn, +2 regenerated per turn).
4. **Resolve Phase:** All effects resolve (damage, status, terrain changes).
5. **End Phase:** Unplayed cards remain in hand (carried to next turn). Turn passes to next in initiative order.

### Terrain Tiles
| Tile | Effect |
|---|---|
| High Ground | +25% ranged damage for unit on this tile |
| Water | Movement costs +1 action point. Units take no water damage |
| Fire | 50 fire damage per turn to any unit standing on it |
| Fortress Wall | Blocks movement and ranged attacks. Destroyable with siege cards |
| Arcane Ley Line | Reduces energy costs by 1 for all cards played this turn |
| Shadow Veil | Attacker misses 30% of attacks against unit in shadow |

### Combo System
Cards output and consume ComboTags. When a card consumes the required tag (set by a previous card this or last turn), its combo bonus activates.

Example: Ember Strike (outputs Ignite) → Inferno Wave (consumes Ignite: +50% bonus damage) = combo.

### PvP (Async)
- Player sets hero roster + card loadouts as their "defense team."
- Attacker battles against the defense team controlled by AI that mirrors the loadout.
- After battle resolves, both players receive a full replay video.
- Outcome submitted to server with cryptographic hash for validation.

### Balance Targets
- Average battle length: 4–6 minutes
- Skill expression: Top player win rate vs equal-power opponent = 65–70%
- New player can understand all cards and win their first battle: target 100%

---

## 4. Empire System

### District Structure
```
                    [STRONGHOLD]
                         |
    ┌────────────┬────────────┬────────────┐
  Military    Resource    Research     Hero
  District    District    District   District
```

### Resource Types
| Resource | Primary Source | Used For |
|---|---|---|
| Stone | Stone Mines | Building construction, walls |
| Iron | Iron Mines | Military upgrades, siege weapons |
| Grain | Farms | Troop upkeep, expansion |
| Arcane Essence | Arcane Towers | Research, hero enchanting |

### Timer Rules (Hard Limits — Enforced in Code)
```csharp
// In BuildingManager.cs
public const int MaxBuildTimeSecondsStronghold = 28800; // 8 hours
public const int MaxBuildTimeSecondsOther = 14400;      // 4 hours
```
Timers above these values are a bug, not a feature. No exceptions.

### Speedup System
- 5-minute, 15-minute, 1-hour, 4-hour, 8-hour speedup items
- Earned from: daily quests (3/day), events (variable), alliance help (2 min per player, max 20 players = 40 min)
- Never sold for premium currency (no urgency creation)

---

## 5. Hero System

### Progression Path
1. **Unlock:** Collect enough shards (60 for Common, up to 150 for Legendary)
2. **Level Up:** XP from combat, quests, dungeons. Max level = 80 (at Stronghold 20)
3. **Star Promotion:** Spend additional shards at level caps to promote (1★ → 6★)
4. **Ability Unlock:** Each star tier unlocks additional cards for the ability pool
5. **Equipment:** 5 equipment slots (Weapon, Armor, Ring, Amulet, Relic). Crafted at Forge.
6. **Awakening:** At 6★, complete Awakening Quest to unlock enhanced passive and 1 special card

### Team Synergies
Alliance bonuses activate when 2+ heroes from the same faction are in a squad:
- 2 heroes same faction: +10% damage
- 3 heroes same faction: +10% damage + faction-specific bonus
- Full squad (3) same faction: +15% damage + faction ultimate passive

### Hero Acquisition (No P2W Gacha)
| Method | Heroes Available |
|---|---|
| Story Campaign | 10 starter heroes unlockable via chapter clears |
| Daily Dungeon | Random shard rewards (all heroes eligible) |
| Events | Featured hero shard bundles |
| Alliance Store | Rotate of 5 heroes per week |
| Achievement Milestones | Specific heroes as rewards |
| Cosmetic Gacha | ONLY cosmetic items — never heroes |

---

## 6. Alliance & Warfare

### Alliance Tiers (by member count)
- Recruit: 1–10 members
- Established: 11–25 members
- Elite: 26–50 members (unlocks Cross-Server Tournament eligibility)

### Territory War Schedule
- **Declaration Window:** Monday 00:00–Friday 23:59 (plan and declare war targets)
- **War Window:** Saturday 18:00–20:00 server time (active battles)
- **Occupation Period:** Sunday 00:00–Friday 23:59 (territory held, bonuses active)

### Rally System
- Any alliance member can start a rally on a target (building or enemy tile)
- Rally leader sets deployment time (5–30 minutes)
- Up to 20 members can join with specified troop counts
- Rally launches simultaneously — coordinated assault

### Territory Bonuses (held per week)
| Territory Type | Holder Bonus |
|---|---|
| Resource Node | +20% production of that resource type |
| Ancient Ruins | +5% hero XP earned |
| Dragonflight | +10% combat power in all battles |
| Shadow Nexus | -15% research time |
| Iron Citadel | +25% defensive power |

---

## 7. Monetization Design

### Principles
1. Never sell power. Cosmetics only.
2. Never create artificial urgency. No "this offer expires in 00:59:59" popups.
3. Always show odds. Gacha pity and rates visible on every pull screen.
4. Free tier must be genuinely fun. Battle Pass Free track stands alone.

### Products
| SKU | Price | Description |
|---|---|---|
| BATTLE_PASS_PREMIUM | $9.99/month | 50-tier pass, cosmetics, city theme, extra speedups |
| SEASON_PASS | $29.99 | Exclusive story chapter + seasonal legendary skin |
| PLAY_PLUS | $4.99/month | Remove ads, +10% idle income (not PvP relevant) |
| COSMETIC_CHEST_STANDARD | $1.99 | 5 cosmetic pulls |
| COSMETIC_CHEST_PREMIUM | $9.99 | 30 cosmetic pulls |
| SKIN_DIRECT | $7.99–$14.99 | Direct skin purchase (no gacha) |

### Anti-P2W Verification Checklist (per sprint)
- [ ] No new item grants combat power advantage
- [ ] No new item reduces build timers beyond the soft cap
- [ ] No hero is exclusively obtainable through cash spending
- [ ] F2P player at 90 days can reach 70% of maximum power

---

## 8. Events

### Launch Events (5)
1. **Dragon Siege** — Server-wide PvE boss. All alliances contribute damage. Rewards scale with total server participation. No individual whale-carry.
2. **Alliance Tournament** — 8-alliance bracket, 3 rounds over 3 weeks. Cross-server.
3. **Harvest Crisis** — Resource management PvE event. Defend resource convoys from waves.
4. **Shard Hunt** — Collection race. Earn shard points from dungeons and quests. Leaderboard prizes.
5. **Void Rift** — Roguelite dungeon variant. Random modifiers each day. Solo or duo.

### Event Engine Design
Events defined as EventDefinition ScriptableObjects with:
- Start/end timestamps
- Objective type enum (Damage, Collection, Defense, Race)
- Reward table
- Server-wide vs alliance vs solo scope
- UI banner asset reference

---

## 9. Progression Milestones

### Day 1
- Complete tutorial (8 steps)
- Complete Act 1 of story campaign (5 battles)
- Build core district (Military or Resource)
- Unlock first non-starter hero via shard drop

### Week 1
- Stronghold level 5
- 3 heroes unlocked
- Join or create an alliance
- Win first PvP defense

### Month 1
- Stronghold level 12
- 10+ heroes unlocked
- Participate in first Territory War
- Complete Battle Pass free track

### Month 3 (F2P ceiling)
- Stronghold level 20
- 25+ heroes unlocked
- 70% of maximum combat power achievable
- Multiple 6-star heroes viable

---

## 10. FTUE (First-Time User Experience)

8-step interactive tutorial, non-skippable but fast (target: 4 minutes):
1. Welcome cinematic — world lore in 60 seconds
2. Place your first building (Barracks)
3. Play your first battle (tutorial opponent, guided)
4. Meet your first hero (Kaelen the Iron Warden, free)
5. Build a Resource district building
6. Complete your first quest
7. Join the Starter Alliance (curated for new players)
8. Complete Act 1, Chapter 1

---

## Changelog

| Version | Date | Changes |
|---|---|---|
| 0.1.0 | 2026-03-07 | Initial GDD — Phase 0 |
