# Scripts/Economy/ — Monetization & Economy Systems

> See root `/CLAUDE.md` for project-wide rules.

Economy systems handle all player progression rewards, purchases, and engagement loops. This is the most ethically sensitive system — all code here must pass the P2W gate.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `GachaSystem.cs` | `GachaSystem` | Cosmetic-only randomized pulls with pity counter |
| `BattlePassManager.cs` | `BattlePassManager` | 50-tier seasonal pass (free + premium tracks) |
| `IAPManager.cs` | `IAPManager` | Unity IAP integration and server-side validation |
| `HeroShardSystem.cs` | `HeroShardSystem` | Hero shard tracking and unlock summoning |
| `QuestEngine.cs` | `QuestEngine` | Daily/weekly quests, objective tracking, rotation |

## HARD CONSTRAINTS — NEVER VIOLATE

```
1. Gacha ONLY contains cosmetics. Never add HeroData to gacha pools.
   Enforced: GachaSystem filters by CosmeticType enum.

2. BattlePassReward.IsCombatPowerReward must return false for ALL premium rewards.
   This gate is checked in BattlePassManager.ClaimReward() and by IAPManager.
   Adding a premium reward that boosts combat power WILL be blocked at runtime.

3. Heroes are ONLY unlocked via HeroShardSystem. Never via GachaSystem or direct IAP.
   Server validates shard threshold before summoning.

4. All IAP goes through server-side receipt validation before granting anything.
   Never grant rewards client-side only.
```

## GachaSystem

- Pool: cosmetics only (skins, frames, emotes) — `CosmeticType` enum
- Pity: guaranteed rare at 90 pulls, epic at 50 pulls (from config)
- Duplicate handling: converts to premium currency
- `RecalculateWeights()` must be called if pool contents change

## BattlePassManager

- 50 tiers per season
- Free track: in-game currency, consumables, XP
- Premium track: cosmetics, QoL (e.g., extra build queue slot) — NO combat power
- `AddPoints(amount)` — call from QuestEngine, battle victories, daily login
- `ClaimReward(tier, track)` — validates `IsCombatPowerReward` before granting

## HeroShardSystem

```
AddShards(heroId, amount, source)  <- from quests, PvE drops, events
RequestSummon(heroId)              <- checks shard threshold, calls PlayFab to validate
```

Shard threshold per hero rarity is in `ProgressionConfig`. Server validates before HeroRoster unlocks the hero.

## QuestEngine

- Loads `QuestConfig` ScriptableObject at init
- Daily quests reset at 00:00 UTC; weekly at Monday 00:00 UTC
- `UpdateProgress(questType, amount)` — call from any system that generates quest-relevant events
- Prefer publishing an EventBus event and having QuestEngine subscribe, rather than calling directly

## Integration Points

| System | Connection |
|--------|-----------|
| HeroRoster | HeroShardSystem calls `HeroRoster.UnlockHero()` after server validation |
| EventBus | QuestEngine subscribes to combat/empire/alliance events for objective tracking |
| PlayFabService | All IAP validation and shard summons are server-authoritative |
| BattlePassManager | Receives points from QuestEngine on quest completion |

## Tests

`Tests/Economy/` — 4 test files:
- `GachaSystemTests.cs` — pity counter, odds, no hero items in pool
- `BattlePassManagerTests.cs` — tier progression, P2W gate enforcement
- `IAPManagerTests.cs` — receipt validation, P2W gate (throws on combat-power products)
- `QuestEngineTests.cs` — progress tracking, daily/weekly resets, reward distribution
