# Scripts/UI/ — User Interface Systems

> See root `/CLAUDE.md` for project-wide rules.

UI is split into two sub-systems, each with its own CLAUDE.md:
- `UI/Combat/` — Combat HUD, card hand, hero status, damage popups, input
- `UI/Empire/` — Empire HUD, building panel, research tree, resource display

## Architecture Rules

1. **UI never calls game system methods directly.** UI subscribes to EventBus events and reads state from ServiceLocator-registered systems. UI publishes input events; game systems handle them.
2. **All widget GameObjects are pooled** via `ObjectPool<T>` if they are spawned repeatedly (CardWidget, StatusIconWidget, TurnTokenWidget, DamagePopup).
3. **No hardcoded strings.** Use Unity Localization tables for all visible text.
4. **No scene dependencies in UI scripts.** UI components find their data via ServiceLocator or SerializedField references set in the Inspector.
5. **URP only.** All shaders and materials must use Universal Render Pipeline.

## UI -> Game Communication Pattern

```
Player taps card -> CombatInputHandler -> CardHandManager.TryPlayCard()
                                              |
                                       EventBus.Publish(CardPlayedEvent)
                                              |
                                       CombatUIController.OnCardPlayed()
```

UI listens; game systems act. Never the reverse (game systems should not hold UI references).

## Adding a New UI Panel

1. Create the panel MonoBehaviour in the appropriate sub-folder (`UI/Combat/` or `UI/Empire/`)
2. Subscribe to relevant EventBus events in `OnEnable`, unsubscribe in `OnDisable`
3. Read initial state from ServiceLocator-registered system in `Start()`
4. If the panel contains repeated widgets, use `ObjectPool<YourWidget>`
5. Add the panel to the relevant root controller (`CombatUIController` or `EmpireUIController`)
