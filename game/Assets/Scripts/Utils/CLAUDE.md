# Scripts/Utils/ — Utilities

> See root `/CLAUDE.md` for project-wide rules.

General-purpose utilities that don't belong to any single game system.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `MainThreadDispatcher.cs` | `MainThreadDispatcher` | Thread-safe Unity main-thread dispatch queue |

## MainThreadDispatcher

PlayFab SDK callbacks and any `async/await` continuations may arrive on background threads. Unity APIs (GameObjects, MonoBehaviours, ScriptableObjects) are NOT thread-safe and must only be called from the main thread.

```csharp
// From any thread:
MainThreadDispatcher.Enqueue(() => {
    heroRoster.LoadFromSaveData(result);
    EventBus.Publish(new HeroDataLoadedEvent());
});
```

`MainThreadDispatcher` is a MonoBehaviour singleton. It dequeues actions in `Update()` (main thread). It is initialized by `GameManager` at boot.

## Rules

- `Enqueue()` is thread-safe (uses `ConcurrentQueue<Action>` internally)
- Never call `Enqueue()` from the main thread for performance-critical paths — just call directly
- Do NOT use for Unity Coroutines — use `StartCoroutine()` from a MonoBehaviour instead
- This is the ONLY approved mechanism for dispatching PlayFab callback results to Unity APIs

## Adding New Utilities

Before adding a new file to Utils/:
1. Is it used by only one system? Put it in that system's folder instead
2. Is it Unity-specific infrastructure? It belongs here
3. Is it pure C# with no Unity dependency? Consider making it a static helper class in the relevant system
