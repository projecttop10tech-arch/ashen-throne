# backend/ — PlayFab Cloud Scripts (Node.js)

> See root `/CLAUDE.md` for project-wide architecture rules.

The backend is a minimal Node.js project containing PlayFab Cloud Scripts and their Jest test suite. Cloud Scripts run server-side in the PlayFab runtime and are the authoritative source for all economy mutations.

## Directory Layout

```
backend/
├── CloudScript/
│   └── economy.js         # All PlayFab Cloud Script functions
├── Tests/
│   └── economy.test.js    # Jest test suite
└── package.json           # Jest, ESLint dependencies
```

## Running Tests

```bash
cd backend
npm install
npm test              # Single-threaded Jest (prevents race conditions)
npm run test:watch    # Watch mode
npm run lint          # ESLint validation
```

## economy.js — Cloud Script Functions

| Function | Called By | Purpose |
|----------|-----------|---------|
| `GrantCombatShards` | `HeroShardSystem.cs` | Validate and grant shards server-side |
| `ValidateIAP` | `IAPManager.cs` | Receipt validation before granting IAP rewards |
| `ClaimQuestReward` | `QuestEngine.cs` | Server-side quest reward grant with duplicate protection |
| `SaveAllianceData` | `AllianceManager.cs` | Atomic alliance state mutations |
| `RecordPvpResult` | `AsyncPvpManager.cs` | Store validated async PvP outcome |

## Security Rules

1. **Zero client trust.** Every Cloud Script re-validates all inputs. Never trust values sent from the client.
2. **Rate limiting.** All functions check 60 calls/minute/player. Exceeded = error returned, no action taken.
3. **Battle token validation.** PvP result tokens are one-time-use (stored in PlayFab, deleted on claim).
4. **Shard cap enforcement.** `GrantCombatShards` enforces maximum shards per hero server-side.
5. **P2W gate.** `ValidateIAP` checks product IDs against an allowlist of non-combat-power items.

## Adding a New Cloud Script Function

1. Add the function to `CloudScript/economy.js`
2. Write a Jest test in `Tests/economy.test.js` before implementing (TDD)
3. Deploy to PlayFab via Dashboard: Build & Deploy → Cloud Script
4. Add the corresponding `CallCloudScript("YourFunctionName", args)` call in `Network/PlayFabService.cs`
5. Update the function table above in this CLAUDE.md

## Deployment

Cloud Scripts are deployed manually via the PlayFab Game Manager dashboard:
- Navigate to: Build & Deploy → Cloud Script → Upload `economy.js`
- Test in PlayFab's built-in script console before deploying to production

## Local vs PlayFab Runtime

The Jest tests run locally with mocked PlayFab context objects. Ensure any `currentPlayerId`, `args`, and `server` references use the PlayFab Cloud Script runtime API — not Node.js builtins that aren't available in PlayFab's sandboxed environment.
