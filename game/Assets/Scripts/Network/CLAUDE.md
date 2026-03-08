# Scripts/Network/ — Network & SDK Integration Layer

> See root `/CLAUDE.md` for project-wide rules.

The Network layer wraps all external SDK integrations. All services register with ServiceLocator and use conditional compilation (`#if FIREBASE_SDK`, `#if PHOTON_SDK`) to switch between stub and real SDK modes.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `PlayFabService.cs` | `PlayFabService` | Auth, CloudScript, UserData CRUD |
| `AnalyticsService.cs` | `AnalyticsService` | Firebase Analytics wrapper (events, user properties) |
| `CrashReporter.cs` | `CrashReporter` | Firebase Crashlytics wrapper (crash reporting, breadcrumbs) |
| `PhotonManager.cs` | `PhotonManager` | Photon Fusion 2 wrapper (rooms, chat, data broadcast) |
| `ATTManager.cs` | `ATTManager` | iOS App Tracking Transparency prompt |

## Current Status: STUB MODE

```
PlayFabService is currently running in stub mode.
Real PlayFab SDK calls are commented out pending SDK installation.
Stub methods return success callbacks immediately.

To activate real SDK:
1. Install PlayFab Unity SDK via Package Manager
2. Uncomment the SDK calls in PlayFabService.cs
3. Set PlayFab Title ID in ProjectSettings -> Player -> Other Settings
```

## PlayFabService API

```csharp
// Authentication (call at boot)
await playFabService.AuthenticateAsync();

// Cloud Script
await playFabService.CallCloudScript("FunctionName", args);

// User Data
await playFabService.GetUserData(keys);
await playFabService.UpdateUserData(data);
```

Events: `OnAuthenticationSuccess`, `OnAuthenticationFailed`

## Threading Rule

PlayFab callbacks arrive on a background thread. NEVER touch Unity APIs (GameObjects, UI, ScriptableObjects) inside a PlayFab callback. Always use `MainThreadDispatcher.Enqueue()`:

```csharp
playFabService.GetUserData(keys, result => {
    MainThreadDispatcher.Enqueue(() => {
        // Safe to touch Unity APIs here
        heroRoster.LoadFromSaveData(result.Data);
    });
});
```

## Retry Logic

`PlayFabService` automatically retries failed requests 3 times with 2-second delays before raising `OnAuthenticationFailed` or the error callback. Do not implement retry logic in callers.

## Security Rules

- All economy mutations (IAP, shard grants, quest rewards) go through PlayFab Cloud Script — never client-side only
- `PlayFabService` is registered with `ServiceLocator` — retrieve via `ServiceLocator.Get<PlayFabService>()`
- Never expose PlayFab Title ID or Developer Secret Key in client code

## Integration Points

All systems that need persistence call PlayFabService:
- BuildingManager, ResearchManager — empire state
- HeroRoster — hero collection
- AllianceManager — guild data
- IAPManager — receipt validation
- HeroShardSystem — summon validation
