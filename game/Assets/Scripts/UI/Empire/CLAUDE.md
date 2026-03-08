# Scripts/UI/Empire/ — Empire HUD & Panels

> See root `/CLAUDE.md` and `Scripts/UI/CLAUDE.md` for architecture rules.

Empire UI provides the city-builder interface: resource HUD (always visible), building placement panel, research tree browser, and active build queue overlay.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `EmpireUIController.cs` | `EmpireUIController` | Root coordinator; show/hide panels |
| `ResourceHUD.cs` | `ResourceHUD` | 4 resource stockpiles with vault capacity bars |
| `BuildingPanel.cs` | `BuildingPanel` | Building catalog, placement, tier progression |
| `BuildQueueOverlay.cs` | `BuildQueueOverlay` | Active builds with countdown timers |
| `ResearchTreePanel.cs` | `ResearchTreePanel` | Research node graph browser |
| `ResearchNodeWidget.cs` | `ResearchNodeWidget` | Single node: icon, state, timer, prerequisites |

## Panel Visibility Rules

| Panel | Visibility |
|-------|-----------|
| `ResourceHUD` | Always visible in Empire scene |
| `BuildingPanel` | Modal; toggled by toolbar button |
| `ResearchTreePanel` | Modal; toggled by toolbar button |
| `BuildQueueOverlay` | Slides in when a build starts; dismissible |

Modals are exclusive: opening one closes the other. Managed by `EmpireUIController`.

## ResourceHUD

- Subscribes to EventBus `OnResourceChanged` event
- Displays Stone, Iron, Grain, Arcane Essence with current/max values
- Vault capacity bar turns red at 90% full (warning to spend resources)

## BuildingPanel

- Reads `BuildingData` assets from `BuildingManager` via ServiceLocator
- Validates cost via `ResourceManager.CanAfford()` before enabling "Build" button
- On confirm: calls `BuildingManager.PlaceBuilding()`
- Greyed-out buildings show prerequisite tooltip

## ResearchTreePanel / ResearchNodeWidget

- Tree layout computed from `ResearchNodeData.Prerequisites` graph
- Node states: `Locked` (grey), `Available` (white), `InProgress` (pulsing), `Complete` (gold)
- Pooled `ResearchNodeWidget` instances; re-bound when panel opens
- Click on Available node: calls `ResearchManager.StartResearch(nodeId)`

## BuildQueueOverlay

- Subscribes to `OnBuildingUpgradeStarted` and `OnBuildingUpgradeCompleted`
- `TickBuildQueue(deltaTime)` — updates countdown displays; call from Update
- Cancel button: shows confirmation dialog (cancellation costs premium currency)
