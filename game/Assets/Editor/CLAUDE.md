# Assets/Editor/ — Unity Editor Tools

> See root `/CLAUDE.md` for project-wide rules.

Editor scripts run only in the Unity Editor (never in builds). They generate ScriptableObject assets that populate `Assets/Data/` and `Assets/Resources/`.

## Files

| File | Menu Path | What It Generates |
|------|-----------|-------------------|
| `StarterAssetGenerator.cs` | Ashen Throne → Generate Starter Assets | 10 HeroData, 50 AbilityCardData, 20 PveLevelData assets |
| `ResearchTreeGenerator.cs` | Ashen Throne → Generate Research Tree | ResearchNodeData assets for the full tech tree |
| `LaunchEventGenerator.cs` | Ashen Throne → Generate Launch Events | EventDefinition ScriptableObjects for tutorial + launch events |
| `BuildingDataGenerator.cs` | Ashen Throne → Generate Building Data | 21 BuildingData assets (Stronghold + 5 per district × 4) |
| `QuestDefinitionGenerator.cs` | Ashen Throne → Generate Quest Definitions | 30 QuestDefinition assets (10 daily, 10 weekly, 10 one-time) |
| `TutorialStepGenerator.cs` | Ashen Throne → Generate Tutorial Steps | 8 TutorialStep assets for the FTUE tutorial |
| `SceneGenerator.cs` | AshenThrone → Generate Scenes | Populates all 6 scenes with Camera, Canvas, UI hierarchies |
| `ConfigGenerator.cs` | AshenThrone → Generate Configs | CombatConfig, EmpireConfig, ProgressionConfig, TerritoryConfig, AccessibilityConfig, QuestDefinitions |
| `Phase8Generator.cs` | AshenThrone → Phase 8/* | 132 placeholder art PNGs, 21 UI prefabs, 3 particle prefabs, 18 audio WAVs, colorblind shader + 3 materials |

**Assembly:** `AshenThrone.Editor.asmdef` — references `AshenThrone` + `UnityEditor`

## Rules

- All generators are **idempotent**: safe to re-run, they overwrite existing assets.
- Generated assets go to `Assets/Data/<Type>/` or `Assets/Resources/<Type>/`.
- Never reference editor namespaces (`UnityEditor.*`) from game runtime code.
- Never put game logic in editor scripts. They set data values only.

## Adding a New Generator

1. Create `Assets/Editor/YourGenerator.cs`
2. Class should be `static` with a `[MenuItem("Ashen Throne/Generate X")]` method
3. Use `AssetDatabase.CreateAsset()` + `AssetDatabase.SaveAssets()` pattern
4. Output to `Assets/Data/<Category>/`
5. Add the new `.cs` file — it is automatically included in `AshenThrone.Editor.asmdef`

## When to Re-Run Generators

- After changing a ScriptableObject schema (adding/removing fields)
- After rebalancing heroes/cards in a design sprint
- After adding new research nodes to the tech tree
- Never run generators in CI — assets are committed to source control
