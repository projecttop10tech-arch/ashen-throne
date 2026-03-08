# Scripts/Alliance/ — Alliance & Social Systems

> See root `/CLAUDE.md` for project-wide rules.

Alliance systems provide the social layer: guild management, war, territory control, async PvP, chat, and leaderboards. All mutations to alliance state are server-authoritative via PlayFab.

## Files

| File | Class | Purpose |
|------|-------|---------|
| `AllianceManager.cs` | `AllianceManager`, `AllianceData`, `AllianceMember` | Guild CRUD, membership, roles |
| `WarEngine.cs` | `WarEngine`, `AllianceWar`, `WarPhase` | Alliance vs Alliance war lifecycle |
| `TerritorySystem.cs` | `TerritorySystem`, `TerritoryTile` | Territory grid control and perks |
| `AsyncPvpManager.cs` | `AsyncPvpManager`, `PvpLoadoutRecord` | Async attack/defense with replay validation |
| `LeaderboardManager.cs` | `LeaderboardManager` | Score tracking and leaderboard fetches |
| `AllianceChatManager.cs` | `AllianceChatManager`, `ChatMessage` | Guild chat with profanity filter |

## AllianceManager

- Max 50 members per alliance
- Roles: `Leader`, `Officer`, `Member`
- Name/tag validated server-side for uniqueness and profanity
- All mutations (`CreateAlliance`, `JoinAlliance`, `SetMemberRole`) call PlayFab Cloud Script

## WarEngine Phases

```
Declare -> (24h cooldown) -> Battle Phase (48h) -> Reward Distribution
```
- Territory control during Battle Phase determines victory
- `RecordAttack(attackerId, targetTileId, result)` — called per combat outcome
- Rewards distributed by contribution score, not just winning side

## AsyncPvpManager

- Stores player's hero loadout as `PvpLoadoutRecord` to PlayFab
- Attacks simulate the defender's squad with the current fight engine (ghost copy)
- Replay validation uses SHA-256 of seed + loadout hash to prevent result spoofing
- Defense record is read-only to the defender until war phase ends

## AllianceChatManager

- Profanity filter runs client-side (fast) + server-side (authoritative)
- Rate limit: server enforces 60 messages/minute
- History: last 100 messages stored in PlayFab Group Data
- `MutePlayer(playerId, duration)` — officer/leader only

## No Server Merges Policy

Per the root CLAUDE.md: **no server merges**. Cross-server events (handled by EventEngine) allow players on different servers to compete without merging databases. Do NOT design any alliance feature that requires moving players between servers.

## Integration Points

| System | Connection |
|--------|-----------|
| PlayFabService | All alliance mutations and leaderboard fetches |
| TerritorySystem | WarEngine reads territory control state to calculate winner |
| EventEngine | World Boss and Void Rift events involve alliance-wide damage tracking |
| HeroRoster | AsyncPvpManager reads active loadout from HeroRoster |

## Tests

`Tests/Alliance/` — 4 test files:
- `AllianceChatManagerTests.cs` — sanitization, rate limiting, message history
- `AsyncPvpManagerTests.cs` — loadout hashing, replay validation (SHA-256)
- `TerritorySystemTests.cs` — hex coordinates, connectivity, fortification logic
- `WarEngineTests.cs` — rally lifecycle, power calculation, result validation
