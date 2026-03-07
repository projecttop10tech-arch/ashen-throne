# Scripts/Events/ — Time-Limited Events Framework

> See root `/CLAUDE.md` for project-wide rules.

Events are the live-ops engine: World Boss raids, Void Rift dungeons, and custom launch events that activate on a schedule and grant limited rewards.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `EventEngine.cs` | `EventEngine`, `ActiveGameEvent`, `EventDefinition` | Pluggable event framework, schedule management |
| `WorldBossManager.cs` | `WorldBossManager`, `WorldBossDefinition` | Alliance-wide world boss raids |
| `VoidRiftManager.cs` | `VoidRiftManager`, `VoidRift`, `RiftTier` | Solo/alliance rift dungeons |
| `NotificationScheduler.cs` | `NotificationScheduler` | Local and remote push notification scheduling |

## EventEngine

- Loads `EventDefinition` ScriptableObjects from `Resources/Events/` at init
- Schedule check runs every **10 seconds** (throttled — do NOT poll every frame, see QA check #19)
- Events have start/end timestamps; EventEngine activates/deactivates accordingly
- `TrackObjectiveProgress(eventId, objectiveType, amount)` — call when player completes relevant actions
- Rewards distributed via `QuestEngine.ClaimReward()` pattern

```csharp
eventEngine.LoadActiveEvents();                               // Call at boot
eventEngine.CheckEventSchedules();                            // Internal, called on 10s interval
eventEngine.TrackObjectiveProgress(id, type, amount);        // From combat/empire/etc
```

Key events: `OnEventStarted`, `OnEventEnded`, `OnObjectiveProgress`

## WorldBossManager

- Boss spawns server-wide; all alliance members contribute damage
- `RequestAttack(attackPower)` — submits attack, tracks local attacks used
- `ReceiveServerHpUpdate(newHp)` — applies server-authoritative HP, checks milestones
- Max attacks per player enforced via `WorldBossDefinition.MaxAttacksPerPlayer`
- Rewards scale by damage contribution, not just participation
- Boss HP is stored server-side (PlayFab) to prevent spoofing

## VoidRiftManager

- Tiers: Tier 1–5, each with escalating difficulty and rewards
- Player can attempt solo or with alliance members
- `OpenRift()` / `CloseRift()` — time-gated; limited attempts per week
- Rewards: hero shards, premium currency, cosmetic fragments

## NotificationScheduler

- Schedules local notifications (build complete, event starting, energy cap)
- Uses Unity Mobile Notifications package
- Publishes `OnNotificationDue` to EventBus so UI can surface in-game banners
- Never spam notifications: max 3 pending local notifications at a time

## Adding a New Event Type

1. Create a new `EventDefinition` ScriptableObject via `Ashen Throne → Generate Launch Events` or manually
2. If the event needs custom logic beyond objective tracking, create a new Manager class here
3. Register the manager with ServiceLocator in `GameManager`
4. Add objective-type routing to `EventEngine.TrackObjectiveProgress()`
5. Write tests in `Tests/Events/`

## Integration Points

| System | Connection |
|--------|-----------|
| AllianceManager | WorldBoss reads alliance member list for damage aggregation |
| QuestEngine | Events reuse quest reward distribution infrastructure |
| NotificationScheduler | Schedules notifications when events start/end |
| PlayFabService | Boss HP and rift attempts stored server-side |

## Tests

`Tests/Events/` — 5 test files:
- `EventEngineTests.cs` — schedule checking, event lifecycle (12 tests)
- `ActiveGameEventTests.cs` — event activation, objective tracking, expiry (17 tests)
- `VoidRiftRunStateTests.cs` — rift phase progression, tier rewards (26 tests)
- `WorldBossManagerTests.cs` — spawn, attacks, HP updates, milestones (19 tests)
- `NotificationSchedulerTests.cs` — scheduling, cancellation, idempotency (15 tests)
