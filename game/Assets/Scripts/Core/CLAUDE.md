# Scripts/Core/ — Foundation Architecture

> See root `/CLAUDE.md` for project-wide rules.

Core provides the cross-cutting infrastructure every other system depends on. These five components are initialized at boot by `GameManager` before any other system starts.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `GameManager.cs` | `GameManager` | App lifecycle, scene transitions, boot sequence |
| `ServiceLocator.cs` | `ServiceLocator` (static) | Cross-system dependency container |
| `EventBus.cs` | `EventBus` (static) | Typesafe publish/subscribe event dispatch |
| `StateMachine.cs` | `StateMachine<TState>`, `IState<TState>` | Generic finite state machine |
| `ObjectPool.cs` | `ObjectPool<T>` | Generic GameObject pooling |

## ServiceLocator

Use this instead of `FindObjectOfType` or singletons.

```csharp
// Registration (at boot, before scene loads)
ServiceLocator.Register<HeroRoster>(heroRoster);

// Retrieval
var roster = ServiceLocator.Get<HeroRoster>();

// Safe retrieval
if (ServiceLocator.TryGet<HeroRoster>(out var roster)) { ... }
```

- Register services in `GameManager.Awake()` or scene initializers
- Unregister in `OnDestroy` to prevent stale references
- Never call `Get<T>()` from `Awake()` — services may not be registered yet; use `Start()`

## EventBus

All inter-system communication goes through EventBus. Events MUST be `readonly struct`.

```csharp
// Define an event (in the relevant system's file, not a separate file)
public readonly struct HeroDiedEvent { public CombatHero Hero; }

// Subscribe
var sub = EventBus.Subscribe<HeroDiedEvent>(OnHeroDied);

// Publish
EventBus.Publish(new HeroDiedEvent { Hero = hero });

// Unsubscribe (store subscription, call in OnDestroy)
sub.Dispose();
```

- Events are structs — zero heap allocation
- Snapshot-based: subscribers added during broadcast are not called until next publish
- Thread-safe publish is supported but callbacks execute on the publishing thread

## StateMachine

Used by TurnManager (combat phases), BuildingManager (queue states), GameManager (app states).

```csharp
var sm = new StateMachine<CombatPhase>();
sm.AddState(CombatPhase.Draw, new DrawPhaseState(this));
sm.Initialize(CombatPhase.Draw);
sm.TransitionTo(CombatPhase.Action);
sm.Tick(deltaTime);
```

- Each state implements `IState<TState>` with `Enter()`, `Tick(float dt)`, `Exit()`
- `OnStateEntered` and `OnStateExited` events fire on transitions

## ObjectPool

Use for any GameObject spawned more than 5× per session (particles, damage popups, UI widgets).

```csharp
var pool = new ObjectPool<DamagePopup>(prefab, initialSize: 20, parent: transform);
var popup = pool.Get(position, rotation);
// ... use popup ...
pool.Return(popup);
```

- `ReturnAll()` returns all checked-out objects at once (useful on scene unload)
- Pre-warms on initialization to avoid runtime spikes

## Boot Order (GameManager)

1. `EventBus.Initialize()`
2. `ServiceLocator.Initialize()`
3. Register all system services
4. Load initial scene via `TransitionTo(AppState.MainMenu)`

## Adding a New Core Utility

Only add to Core/ if the utility is used by 3+ independent systems. Otherwise put it next to its primary user. New utilities must have no dependencies on game systems (Combat, Empire, etc.).
