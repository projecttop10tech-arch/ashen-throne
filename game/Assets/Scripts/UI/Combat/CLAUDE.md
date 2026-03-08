# Scripts/UI/Combat/ — Combat HUD & Input

> See root `/CLAUDE.md` and `Scripts/UI/CLAUDE.md` for architecture rules.

Combat UI displays the card hand, hero status, energy, turn order, and damage popups. Input from the player (card selection, target tap) is captured and forwarded to game systems.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `CombatUIController.cs` | `CombatUIController` | Root HUD coordinator; manages all sub-panels |
| `CardHandView.cs` | `CardHandView` | Displays 5-card hand; pools CardWidgets |
| `CardWidget.cs` | `CardWidget` | Single card tile: name, cost, element, combo badge |
| `HeroStatusDisplay.cs` | `HeroStatusDisplay` | Portrait, HP bar, status effect icons |
| `StatusIconWidget.cs` | `StatusIconWidget` | Single status icon with remaining-turn counter |
| `EnergyDisplay.cs` | `EnergyDisplay` | Current/max energy with regen animation |
| `TurnOrderDisplay.cs` | `TurnOrderDisplay` | Queue of next N heroes as TurnTokenWidgets |
| `TurnTokenWidget.cs` | `TurnTokenWidget` | Single hero token: portrait + faction color |
| `CombatInputHandler.cs` | `CombatInputHandler` | Raycast -> GridPosition/Hero, routes to CardHandManager |
| `DamagePopupManager.cs` | `DamagePopupManager` | Floating damage numbers, color by damage type, pooled |

## CombatUIController Event Subscriptions

| EventBus Event | Handler | Action |
|----------------|---------|--------|
| `PhaseChangedEvent` | `OnPhaseChanged()` | Show/hide relevant panels per phase |
| `BattleEndedEvent` | `OnBattleEnded()` | Show victory/defeat panel |
| `HeroDamagedEvent` | `OnHeroDamaged()` | Trigger HeroStatusDisplay update, spawn popup |
| `CardDrawnEvent` | `OnCardDrawn()` | Refresh CardHandView |
| `EnergyChangedEvent` | `OnEnergyChanged()` | Refresh EnergyDisplay |
| `TurnChangedEvent` | `OnTurnChanged()` | Refresh TurnOrderDisplay |

## Input Flow

```
CombatInputHandler.Update()
  |- Raycast hit GridTileIdentifier -> store targetGridPos
  |- Raycast hit hero collider -> store targetHeroId
  `- On card button click -> CardHandManager.TryPlayCard(selectedIndex, targetGridPos)
```

`CombatInputHandler` disables itself during non-Action phases (checked via `TurnManager.CurrentPhase`).

## Pooled Widgets

| Widget | Pool Owner | Return Trigger |
|--------|-----------|----------------|
| `CardWidget` | `CardHandView` | Card discarded or hand cleared |
| `StatusIconWidget` | `HeroStatusDisplay` | Status removed or hero dies |
| `TurnTokenWidget` | `TurnOrderDisplay` | Turn order changes |
| Damage popup GO | `DamagePopupManager` | Animation completes |

## Damage Popup Colors (DamagePopupManager)

| DamageType | Color |
|-----------|-------|
| Fire | Orange |
| Ice | Cyan |
| Shadow | Purple |
| Physical | White |
| Healing | Green |
| ArcaneLeylLine | Gold |
