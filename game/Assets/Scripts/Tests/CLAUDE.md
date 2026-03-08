# Scripts/Tests/ — Test Suite

> See root `/CLAUDE.md` for project-wide rules.

All game logic is covered by NUnit tests running in Unity Test Framework (UTF) Edit Mode. Tests live in a mirror structure of the source tree.

## Structure

```
Tests/
├── Core/         -> mirrors Scripts/Core/ (EventBus, ServiceLocator, StateMachine, ObjectPool)
├── Combat/       -> mirrors Scripts/Combat/
├── Empire/       -> mirrors Scripts/Empire/
├── Economy/      -> mirrors Scripts/Economy/
├── Alliance/     -> mirrors Scripts/Alliance/
├── Heroes/       -> mirrors Scripts/Heroes/
├── Events/       -> mirrors Scripts/Events/
└── UI/           -> mirrors Scripts/UI/ (Tutorial, Accessibility, Localization)
```

## Test Files

| System | File | Tests |
|--------|------|-------|
| Combat | `CombatHeroTests.cs` | 35 |
| Combat | `CombatGridTests.cs` | 30 |
| Combat | `CardHandManagerTests.cs` | 25 |
| Combat | `AbilityResolverTests.cs` | 8 |
| Combat | `CombatHeroFactoryTests.cs` | 11 |
| Combat | `TurnManagerTests.cs` | 8 |
| Empire | `BuildingManagerTests.cs` | 16 |
| Empire | `ResourceManagerTests.cs` | 16 |
| Empire | `ResearchManagerTests.cs` | 19 |
| Alliance | `TerritorySystemTests.cs` | 20 |
| Alliance | `WarEngineTests.cs` | 18 |
| Alliance | `AsyncPvpManagerTests.cs` | 18 |
| Alliance | `AllianceChatManagerTests.cs` | 14 |
| Economy | `BattlePassManagerTests.cs` | 14 |
| Economy | `QuestEngineTests.cs` | 22 |
| Economy | `GachaSystemTests.cs` | 16 |
| Economy | `IAPManagerTests.cs` | 14 |
| Events | `ActiveGameEventTests.cs` | 17 |
| Events | `VoidRiftRunStateTests.cs` | 26 |
| Events | `WorldBossManagerTests.cs` | 19 |
| Events | `NotificationSchedulerTests.cs` | 15 |
| Events | `EventEngineTests.cs` | 12 |
| UI | `TutorialManagerTests.cs` | 22 |
| UI | `AccessibilityManagerTests.cs` | 20 |
| UI | `LocalizationBootstrapTests.cs` | 14 |
| Core | `EventBusTests.cs` | 15 |
| Core | `ServiceLocatorTests.cs` | 16 |
| Core | `StateMachineTests.cs` | 20 |
| Core | `ObjectPoolTests.cs` | 14 |
| Heroes | `HeroRosterTests.cs` | 35 |

**Total: 579 tests**

## Test Patterns

### Setup Pattern

```csharp
[SetUp]
public void SetUp()
{
    CombatHero.ResetInstanceIdForTesting(); // Always reset static ID counter
    _go = new GameObject();
    _system = _go.AddComponent<CardHandManager>();
    _config = ScriptableObject.CreateInstance<CombatConfig>();
    // set config values...
}

[TearDown]
public void TearDown()
{
    Object.DestroyImmediate(_go);
    Object.DestroyImmediate(_config);
}
```

### ScriptableObject Factory

```csharp
private AbilityCardData MakeCard(int cost, CardType type = CardType.Attack)
{
    var card = ScriptableObject.CreateInstance<AbilityCardData>();
    card.Cost = cost;
    card.Type = type;
    card.Id = Guid.NewGuid().ToString(); // Unique IDs prevent test collisions
    return card;
}
```

### Event Verification

```csharp
bool eventFired = false;
EventBus.Subscribe<CardPlayedEvent>(_ => eventFired = true);
system.TryPlayCard(0, targetPos);
Assert.IsTrue(eventFired, "CardPlayedEvent should fire on successful play");
```

## Rules

- **No scene loading in tests.** Use `AddComponent` on a `new GameObject()`.
- **No real PlayFab calls.** Stub/mock PlayFabService in tests.
- **Always `DestroyImmediate` in TearDown.** Prevent memory leaks across test runs.
- **Always `ResetInstanceIdForTesting()` if using CombatHero.** Static counter causes test pollution.
- **Use GUID-based IDs** for test data to avoid collisions.
- **Test one behavior per test.** Descriptive names: `PlayCard_WithInsufficientEnergy_ReturnsFalse`.

## Running Tests

**Unity Editor:** Window → General → Test Runner → Edit Mode → Run All

**Command Line:**
```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -testPlatform EditMode \
  -projectPath game/ -testResults test-results.xml -logFile test-run.log
```

## Coverage Status (as of Phase 6)

All systems have dedicated unit test coverage. No remaining gaps.
