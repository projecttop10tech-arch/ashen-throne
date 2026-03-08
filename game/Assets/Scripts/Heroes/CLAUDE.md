# Scripts/Heroes/ — Hero Collection & Progression

> See root `/CLAUDE.md` for project-wide rules.

Heroes is the persistent player collection layer. It tracks which heroes a player owns, their levels and star tiers, and their shard counts. This is distinct from `Combat/CombatHero` which is the *ephemeral runtime state* during a battle.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `HeroRoster.cs` | `HeroRoster`, `OwnedHero` | Player's hero collection, persistence, leveling |
| `ProgressionConfig.cs` | `ProgressionConfig` (ScriptableObject) | XP curves, stat growth, max level |

## HeroRoster vs CombatHero

| | HeroRoster / OwnedHero | CombatHero |
|---|---|---|
| Lifetime | Persistent (save game) | Ephemeral (one battle) |
| Data | Level, stars, shards, loadout | Current HP, active status effects |
| Source | Loaded from PlayFab | Created by CombatHeroFactory |
| Location | `Heroes/HeroRoster.cs` | `Combat/CombatHero.cs` |

## HeroRoster API

```csharp
roster.LoadFromSaveData(saveData);      // Called at boot from PlayFab data
roster.AddShards(heroId, amount);       // From HeroShardSystem
roster.UnlockHero(heroId);             // From HeroShardSystem after server validation
roster.LevelUp(heroId);                // Costs XP from EmpireConfig
roster.PromoteStar(heroId);            // Costs shards + resources
```

Key events: `OnHeroUnlocked`, `OnHeroLeveledUp`, `OnHeroStarPromoted`, `OnShardsAdded`

## ProgressionConfig

- XP curve: `GetXpForLevel(level)` — exponential formula with `BaseXp` and `GrowthFactor`
- Max hero level gated by Stronghold level (read from BuildingManager)
- Stat growth per level: `StatGrowthPerLevel` float — applied by CombatHeroFactory
- `RegenerateXpCurve()` — editor-only utility to preview the curve

## HARD CONSTRAINT

Heroes must only be unlocked through `HeroShardSystem.RequestSummon()` which calls PlayFab for server validation. Never call `HeroRoster.UnlockHero()` directly from UI or other systems — always go through HeroShardSystem.

## Integration Points

| System | Connection |
|--------|-----------|
| HeroShardSystem | Calls `AddShards()` and `UnlockHero()` |
| CombatHeroFactory | Reads `OwnedHero` data to create runtime `CombatHero` |
| ResearchManager | Research bonuses applied during `CombatHeroFactory.Create()`, not stored in OwnedHero |
| PlayFabService | HeroRoster serializes to/from PlayFab User Data |

## Adding a New Hero

1. Create a `HeroData` ScriptableObject in `Assets/Resources/Heroes/`
2. Set base stats, rarity, faction, ability card pool, passive, portrait
3. Set shard unlock cost in `HeroData` (must match `ProgressionConfig` thresholds by rarity)
4. Update `StarterAssetGenerator.cs` if this hero should be generated for all players
5. No code changes needed — HeroRoster loads all HeroData from Resources at runtime
