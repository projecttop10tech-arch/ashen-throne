# Ashen Throne — PlayFab Cloud Script API: Economy

**File:** `backend/CloudScript/economy.js`
**Rate Limit:** 60 calls/minute/player on all endpoints.

---

## Endpoints

### `GrantCombatShards`

Grant hero shards following a validated combat victory.

**Request:**
```json
{
  "heroId": "string",          // e.g. "hero_kaelen" — must match a valid heroId in catalog
  "shardAmount": "number",     // 1–10 (server caps at 10 regardless of client value)
  "battleToken": "string"      // one-time token issued by server, consumed on use
}
```

**Response (success):**
```json
{
  "success": true,
  "totalShards": 42            // player's new total shard count for this hero
}
```

**Response (failure):**
```json
{
  "success": false,
  "error": "INVALID_ARGS | RATE_LIMITED | INVALID_BATTLE_TOKEN"
}
```

**Notes:**
- `battleToken` is a one-time server-issued token. Prevents duplicate shard grants for the same battle.
- Server caps `shardAmount` at 10 to prevent exploit if client sends inflated value.
- Hero shard count stored in PlayFab User Data as `shards_{heroId}`.

---

### `ValidateIAP`

Validate an in-app purchase receipt and grant product rewards. Never grant premium items without calling this.

**Request:**
```json
{
  "receiptData": "string",     // raw receipt data from Apple/Google
  "platform": "ios | android",
  "productId": "string",       // must match a key in rewardMap
  "signature": "string"        // Android only: Google Play signature
}
```

**Response (success):**
```json
{
  "success": true,
  "granted": {
    "type": "battlepass_premium | season_pass | play_plus | cosmetic_chest",
    "duration": 30,            // days (subscriptions only)
    "quantity": 5              // (cosmetic chest pulls only)
  }
}
```

**Response (failure):**
```json
{
  "success": false,
  "error": "INVALID_ARGS | RATE_LIMITED | UNSUPPORTED_PLATFORM | VALIDATION_FAILED | INVALID_RECEIPT | DUPLICATE_RECEIPT"
}
```

**Valid productIds:**
| productId | Type | Price |
|---|---|---|
| `BATTLE_PASS_PREMIUM` | Subscription | $9.99/month |
| `SEASON_PASS` | One-time | $29.99 |
| `PLAY_PLUS` | Subscription | $4.99/month |
| `COSMETIC_CHEST_STANDARD` | Consumable | $1.99 |
| `COSMETIC_CHEST_PREMIUM` | Consumable | $9.99 |

**Notes:**
- Duplicate receipt detection uses a hash of `receiptData`. Last 100 hashes stored per player.
- Grants are stored in PlayFab User Internal Data as `{type}: { active: true, grantedAt: timestamp }`.
- **Zero combat power is granted by any IAP.** All grants are cosmetic or quality-of-life.

---

### `SubmitPvPOutcome`

Submit a PvP battle result with a replay hash for server-side anti-cheat validation.

**Request:**
```json
{
  "attackerHeroIds": ["string"],  // array of heroId strings in attacker's squad
  "defenderPlayFabId": "string",  // PlayFab ID of the defending player
  "outcome": "win | loss | draw",
  "replayHash": "string",         // cryptographic hash of battle replay data
  "heroStatSnapshot": [           // client-reported stats for validation
    { "heroId": "string", "attack": 120, "defense": 80, "health": 5000 }
  ]
}
```

**Response (success):**
```json
{
  "success": true,
  "rewards": {
    "xp": 100,
    "shardTokens": 2
  }
}
```

**Response (failure):**
```json
{
  "success": false,
  "error": "INVALID_ARGS | RATE_LIMITED | INVALID_OUTCOME | STAT_MISMATCH"
}
```

**Notes:**
- `heroStatSnapshot` is validated against server-side hero progression records. Mismatch = rejected + logged.
- Battle history stored (last 50 battles) for support and anti-cheat review.
- Rewards are XP and shard tokens only. No premium currency from PvP.

---

### `ApplyBuildSpeedup`

Consume speedup items from player inventory to reduce a building's construction time.

**Request:**
```json
{
  "placedBuildingId": "string",   // unique ID of the building instance
  "speedupItemId": "string",      // must match a valid speedup item ID
  "quantity": 1                   // number of speedup items to consume
}
```

**Response (success):**
```json
{
  "success": true,
  "secondsReduced": 3600
}
```

**Response (failure):**
```json
{
  "success": false,
  "error": "INVALID_ARGS | RATE_LIMITED | INSUFFICIENT_ITEMS"
}
```

**Valid speedupItemIds:**
| itemId | Seconds |
|---|---|
| `speedup_5min` | 300 |
| `speedup_15min` | 900 |
| `speedup_1hr` | 3600 |
| `speedup_4hr` | 14400 |
| `speedup_8hr` | 28800 |

**Notes:**
- Server validates item ownership via PlayFab inventory before consuming.
- Items are consumed one by one via `ConsumeItem` to ensure atomicity per item.
- Client applies the `secondsReduced` to the local timer after server confirmation.
