# Ashen Throne — Monetization Design Document

**Version:** 0.1.1
**Last Updated:** 2026-03-07

---

## Core Philosophy

**Earn trust before asking for money.**
Players who feel respected spend more and stay longer than players who feel exploited.

### The Four Rules (Non-Negotiable)
1. **Never sell power.** No item in any store improves combat stats, reduces build timers beyond the soft cap, or provides exclusive heroes.
2. **Never create artificial urgency.** No "offer expires in 00:59:59" popups. No FOMO-driven limited IAP.
3. **Always show odds.** Gacha pity counter and pull rates visible on every cosmetic pull screen.
4. **Free tier must be genuinely fun.** The game is fully playable and progressable at $0 spend.

---

## Product Catalog & Pricing Matrix

| SKU | Display Name | Price | Type | What it Contains |
|---|---|---|---|---|
| `BATTLE_PASS_PREMIUM` | Battle Pass Premium | $9.99/month | Subscription | 50-tier pass premium track: hero skins, city theme, emotes, extra speedup bundles. Zero combat power. |
| `SEASON_PASS` | Season Pass | $29.99 | One-time per season | Exclusive story chapter unlock, one legendary hero skin, seasonal cosmetic bundle. |
| `PLAY_PLUS` | Play+ | $4.99/month | Subscription | Ad removal, +10% idle resource income (NOT PvP relevant — only affects offline accumulation). |
| `COSMETIC_CHEST_STANDARD` | Minor Chest | $1.99 | Consumable | 5 cosmetic gacha pulls. |
| `COSMETIC_CHEST_PREMIUM` | Major Chest | $9.99 | Consumable | 30 cosmetic gacha pulls (better value rate). |
| `skin_direct_*` | Direct Skin Purchase | $7.99–$14.99 | Permanent | Direct purchase of a specific cosmetic skin. Bypasses gacha entirely. |

---

## Battle Pass Design

### Free Track (available to all players)
Tier rewards must stand alone as a compelling progression. Free track is NOT a crippled version.

| Tier Range | Example Rewards |
|---|---|
| 1–10 | Resource bundles, 5-min/15-min speedups, hero shards (various) |
| 11–20 | 1hr speedups, hero shard bundles, XP potions |
| 21–30 | 4hr speedups, featured hero shards, alliance contribution tokens |
| 31–40 | Cosmetic frame (free variant), resource bundles, 4hr speedups |
| 41–50 | Season-title cosmetic (free variant), hero shard cache, 4hr speedup bundle |

### Premium Track (Battle Pass Premium subscribers only)
ALL premium rewards must be cosmetic or QoL only. This is enforced in code via `BattlePassReward.IsCombatPowerReward`.

| Tier Range | Example Rewards |
|---|---|
| 1–10 | Profile icons, emote, resource bundles |
| 11–20 | City decoration, hero skin fragment, cosmetic chest pull |
| 21–30 | Hero skin (featured), emote bundle, city theme component |
| 31–40 | Alliance emblem set, title, cosmetic chest pulls |
| 41–50 | Legendary hero skin (premium exclusive), full city theme, season title |

---

## Cosmetic Gacha

### Pull Rates
| Rarity | Base Rate | Pity Rate (after 40 pulls) |
|---|---|---|
| Standard Cosmetic | 75% | — |
| Rare Cosmetic | 20% | — |
| Epic Cosmetic | 4% | 20% (guaranteed within 50) |
| Legendary Cosmetic | 1% | 100% (guaranteed at pull 50) |

### Pity System
- Counter resets on Legendary pull.
- Counter persists across sessions (server-stored).
- Counter visible to player at all times on the pull screen.
- All cosmetics available for direct purchase via `skin_direct_*` SKU after 90 days.

### What Cosmetic Gacha NEVER Contains
- Hero characters (any rarity)
- Hero ability cards
- Speedup items
- Resources
- Any item that affects combat power, build speed, or progression speed

---

## Anti-P2W Verification Checklist

Run this check every sprint before shipping any new feature, item, or system.

- [ ] Does any new item improve combat attack, defense, health, or speed?
- [ ] Does any new item reduce build timer beyond the speedup item soft cap?
- [ ] Is any hero exclusively obtainable through cash spending?
- [ ] Does any new IAP product give a spending player an advantage over a 30-day F2P player?
- [ ] Does any new UI element use countdown urgency on IAP purchases?
- [ ] Are gacha odds displayed clearly and accurately?
- [ ] Can a F2P player at 90 days of active play reach ≥70% of maximum combat power?

All answers must be NO (or YES for the last item) before shipping.

---

## Revenue Projections (Reference Only)

Assumptions: 100,000 DAU at launch, industry benchmarks.

| Metric | Target |
|---|---|
| ARPU (Average Revenue Per User) | $0.80/month |
| Conversion rate (free → any paying) | 3–5% |
| Battle Pass Premium conversion | 2% of DAU |
| Play+ conversion | 1.5% of DAU |
| ARPPU (Average Revenue Per Paying User) | $18–$25/month |

These are reference targets, not promises. Fair monetization is the goal; revenue follows naturally.

---

## Dark Pattern Audit (Banned Practices)

The following tactics are explicitly prohibited in Ashen Throne:

| Practice | Why Banned |
|---|---|
| Countdown timers on IAP offers | Creates false urgency |
| Dual currency (premium → regular) | Obscures real price |
| Auto-renewal without clear disclosure | Deceptive |
| Loot box odds hidden | Illegal in several markets, always unethical |
| Spending required to view content | Pay-to-progress is pay-to-win |
| Push notification pressure to spend | Harassment |
| Social comparison pressure ("Your friend spent...") | Exploitation |
| Forced IAP screen after tutorial | Creates negative first impression |

Any feature that resembles these patterns must be redesigned before shipping.
