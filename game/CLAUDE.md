# game/ — Unity Project Root

> See root `/CLAUDE.md` for project-wide architecture rules and design constraints.

This directory is the Unity 6 LTS project. All game code, assets, and configuration live here.

## Directory Layout

| Directory | Purpose |
|-----------|---------|
| `Assets/Art/` | Sprites, models, animations (do not modify without art review) |
| `Assets/Audio/` | SFX and music clips |
| `Assets/Data/` | Generated ScriptableObject assets (heroes, cards, levels) |
| `Assets/Editor/` | Unity Editor tools (asset generators — see Editor/CLAUDE.md) |
| `Assets/Prefabs/` | Reusable GameObject prefabs |
| `Assets/Resources/` | Runtime-loaded assets (loaded via Resources.Load) |
| `Assets/Scenes/` | Unity scenes |
| `Assets/Scripts/` | All C# game logic (see Scripts/*/CLAUDE.md per system) |
| `Assets/StreamingAssets/` | Config overrides and localization bundles |
| `Packages/` | Unity Package Manager manifest |
| `ProjectSettings/` | Unity project settings (do not edit by hand) |

## Assembly Definitions

| Assembly | File | Purpose |
|----------|------|---------|
| `AshenThrone` | `Assets/Scripts/AshenThrone.asmdef` | Main game code |
| `AshenThrone.Tests` | `Assets/Scripts/Tests/AshenThrone.Tests.asmdef` | NUnit test suite |
| `AshenThrone.Editor` | `Assets/Editor/AshenThrone.Editor.asmdef` | Unity Editor tools |

`AssemblyInfo.cs` grants `InternalsVisibleTo("AshenThrone.Tests")` so tests can access internal members (e.g., `CombatHero.ResetInstanceIdForTesting()`).

## Key Packages (manifest.json)

- **Universal Render Pipeline 17.3.0** — All shaders/materials must use URP
- **Input System 1.18.0** — New input system; do NOT use legacy Input.GetKey
- **Unity Test Framework 1.6.0** — NUnit runner for Play Mode and Edit Mode tests
- **Addressables 2.9.0** — For large assets loaded on demand (not yet wired to scripts)
- **Mobile Notifications 2.4.3** — Used by `NotificationScheduler.cs`
- **Unity Purchasing** — Used by `IAPManager.cs`
- **Localization 1.5.8** — UI text uses localization tables; no hardcoded strings in UI

## Running Tests

Tests run via Unity Test Runner (Window → General → Test Runner) or via command line:

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -testPlatform EditMode \
  -projectPath . -testResults test-results.xml
```

## Adding a New Scene

1. Create scene under `Assets/Scenes/`
2. Add to Build Settings (File → Build Settings → Add Open Scenes)
3. Add a `SceneName` enum value to `Core/GameManager.cs`
4. Use `GameManager.LoadSceneAsync(SceneName.X)` to transition — never use `SceneManager.LoadScene` directly
