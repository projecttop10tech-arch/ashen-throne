# Scripts/Empire/ — 4X Empire Building System

> See root `/CLAUDE.md` for project-wide rules.

The Empire system handles the 4X city-builder layer: players place and upgrade buildings, manage 4 resource types, and research tech tree nodes to unlock bonuses.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `BuildingManager.cs` | `BuildingManager`, `PlacedBuilding`, `BuildQueueEntry` | Placement, upgrade queue, HARD timer caps |
| `ResourceManager.cs` | `ResourceManager` | 4 resource types, idle production, vault caps |
| `ResearchManager.cs` | `ResearchManager`, `ResearchQueueEntry`, `ResearchBonusState` | Tech tree progression, bonus application |

## HARD CONSTRAINTS — NEVER VIOLATE

```
BuildingManager.cs contains a HARD CODED CAP:
  Normal buildings: MAX 4 hours build/upgrade time
  Stronghold:       MAX 8 hours build/upgrade time

These caps are enforced in BuildingManager regardless of what BuildingData says.
DO NOT raise these caps. DO NOT add a premium item that bypasses them.
This is a core anti-P2W design rule (see root CLAUDE.md check #40).
```

## Resource Types (`ResourceType` enum)

| Resource | Produced By | Primary Use |
|----------|------------|-------------|
| Stone | Quarry | Basic buildings |
| Iron | Mine | Military, research |
| Grain | Farm | Population, sustain |
| Arcane Essence | Arcane Tower | Research, special items |

Production rates scale with building tier. Offline earnings cap: **8 hours** (from `EmpireConfig.OfflineEarningsCap`).

## BuildingManager

```csharp
// Place a building
buildingManager.PlaceBuilding(buildingData, gridCoord);

// Start upgrade (goes into queue)
buildingManager.StartUpgrade(buildingInstanceId);

// Queue has 2 free slots; 3rd slot requires premium (IAPManager)
buildingManager.TickBuildQueue(deltaTime); // Call from Update
```

Key events: `OnBuildingPlaced`, `OnBuildingUpgradeStarted`, `OnBuildingUpgradeCompleted`

## ResearchManager

- Loads all `ResearchNodeData` assets from `Resources/Research/` at init
- Tracks `HashSet<string>` of completed node IDs
- `ResearchBonusState` is read by `CombatHeroFactory` to apply stat modifiers
- One research can be active at a time

```csharp
researchManager.StartResearch(nodeId);
researchManager.CompleteResearch(nodeId); // Called internally when timer expires
var bonus = researchManager.GetCurrentBonuses(); // Read by CombatHeroFactory
```

## Integration Points

| System | Connection |
|--------|-----------|
| ResourceManager → BuildingManager | BuildingManager checks `CanAfford()` before placement |
| ResearchManager → CombatHeroFactory | `ResearchBonusState` stat modifiers applied to combat heroes |
| EventBus | ResourceManager: `OnResourceChanged`; BuildingManager: build events; ResearchManager: research events |
| PlayFabService | BuildingManager and ResearchManager persist state to PlayFab on completion |
| EmpireConfig SO | All caps and starting values (vault size, queue slots) |

## Tests

`Tests/Empire/` — 3 test files:
- `BuildingManagerTests.cs` — placement, timers, queue, 4h/8h cap enforcement
- `ResourceManagerTests.cs` — spending, production, vault cap, offline earnings
- `ResearchManagerTests.cs` — tree traversal, prerequisites, bonus application
