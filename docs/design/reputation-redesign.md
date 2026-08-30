# Reputation + Star-Rating Redesign — Design Spec

Status: **APPROVED for implementation (2026-08-30)**. Note from user: **TEKRAR GÖZDEN GEÇİRİLECEK** (revisit / review again after first pass). TODO #25.

Source: research agent proposal. This file is the implementation spec; sibling: `order-board-redesign.md` (they share Save v2→v3, `OrderManager.TryDeliver`, `DeliveryResult` — implement in a coordinated way).

## Problem (confirmed against code)

- `OrderManager.TryDeliver`: `if (onTime) { stars = 5; payFactor = 1; }` — any on-time arrival is a flat 5★ regardless of crashes/damage/cargo.
- `ReputationManager.RecalculateAverage`: `AverageScore = sum / recentScores.Count` — divides by jobs done, not the window, so `startingAverage = 3.0` starts you at the Silver threshold and the first 5★ delivery → Diamond.

## A. Star-rating formula

Computed per delivery in `OrderManager` (it owns `remainingTime`, `activeTimeLimit`, `activeOrder.CargoType`). Start at 5.0, subtract penalties, clamp [1,5]. Round to 0.5 for UI, keep raw for reputation math.

```
L        = clamp01(-remainingTime / lateWindow)      // 0 = on-time, 1 = at fail edge (already computed)
margin   = remainingTime / activeTimeLimit           // fraction of limit left (on-time only)
crashes  = collisions during this delivery           // NEW hook
condLost = conditionAtPickup - conditionAtDelivery   // NEW hook (0..100)
spdFrac  = speedingSeconds / deliveryDuration        // NEW hook, time above carefulSpeedThresholdKph (90)

sens     = { Package: 0.6, Food: 1.0, Fragile: 1.6 }[cargoType]

latePenalty = L * 4.0
closeCall   = (L == 0 && margin < 0.08) ? 0.3 : 0.0
crashDed    = min(2.0, crashes * 0.8)
damageDed   = min(2.0, condLost * 0.05)
speedDed    = min(0.6, spdFrac * 1.2)

stars = 5.0 - latePenalty - closeCall - sens * (crashDed + damageDed + speedDed)
stars = clamp(stars, 1.0, 5.0)
stars += Random.Range(-0.15f, 0.10f); stars = clamp(stars, 1.0, 5.0)   // picky-customer jitter (E3)
```

Expected: clean on-time ≈ 4.9; 1 bump + 8 cond lost Fragile ≈ 3.1, Package ≈ 4.3; sloppy on-time (2 crashes, 25 cond, Food) ≈ 2.2; late-fail edge ≈ 1.0.

### New tracking hooks (exactly 3)

1. **Collision count during active delivery** — `VehicleCondition.cs`: add `public int CollisionCount { get; private set; }` + `public event Action<float> OnCollision;`, increment/raise inside existing `OnCollisionEnter` after `ApplyDamage(damage)`. `OrderManager` snapshots in `TryPickup`, diffs in `TryDeliver`.
2. **Condition at pickup vs delivery** — `OrderManager` caches `vehicleCondition.CurrentCondition` in `TryPickup`, diffs in `TryDeliver`.
3. **Sustained speed** — in `OrderManager.Update()` `Delivering` branch: `speedingSeconds += Time.deltaTime` while `vehicle.CurrentSpeedKph > carefulSpeedThresholdKph`; also record `peakSpeedKph`.

`OrderManager` resolves the vehicle once in `Start()` via `FindFirstObjectByType<VehicleController>()`. If absent, formula degrades to lateness + close-call (still not automatic 5).

## B. Reputation progression — XP-style "Reputation Points" (RP)

Cumulative points, rising level curve, **no decay**. Pattern from ETS2/SnowRunner XP-per-job and Death Stranding connection levels. `RegisterDelivery` stays the single entry point — converts stars→RP and accumulates instead of averaging.

Sources studied: Uber (mean of last ~500 trips, outliers excluded — immovable by one trip), DoorDash (~100 recent, lowest dropped, rating separate from standing), Death Stranding (per-delivery grade → accumulating Likes → star Connection Levels, each needing progressively more, no decay), ETS2/ATS (XP≈distance+bonuses → level → skill point → better cargo), SnowRunner (XP→Rank 1-30), Crazy Taxi (session letter grade — only good as an end-of-session flourish).

### Numbers

```
baseRP(stars):  5.0★→100  4.5★→80  4.0★→60  3.0★→30  2.0★→10  1.0★→2   (linear between)
distanceFactor = clamp(jobDistanceMeters / 250, 0.5, 2.0)
RP earned      = round(baseRP(stars) * distanceFactor * routeRepeatFactor)   // repeat factor: E2
```

Great long delivery ≈ 200 RP; mediocre short ≈ 15 RP; timed-out order ≈ 2-4 RP (never 0, never negative — abandoning is never better than finishing badly).

Level curve (2 inspector floats):

```
RPtoReach(level) = round(rpCurveBase * level^rpCurveExp)      // rpCurveBase = 500, rpCurveExp = 1.6
```

| Level | Cumulative RP | ≈ deliveries (~90 RP avg) |
|---|---|---|
| 2 | 500 | ~6 |
| 3 | 1 227 | ~14 |
| 5 | 3 264 | ~36 |
| 9 | 9 183 | ~100 |
| 16 | 24 000 | ~265 |
| 20 | 35 218 | ~390 |

On crossing a threshold: `OnLevelUp(int newLevel)` → `NotificationService.Raise("Seviye {n}!")` + HUD progress bar (`RP into level / RP for next`).

**Decay: none on the RP total.** Recent-quality pressure comes from a separate **"Recent Form"** = the existing rolling mean of the last 10 star scores (keep `recentScores` / `AverageScore`; just stop using it to pick the tier). Display it; it does not lower level; it gates the top multiplier (C).

## C. Tier mapping

Keep Bronze/Silver/Gold/Diamond as bands over the level. `ReputationTier` enum, `OrderData.MinReputationTier`, `IsOrderUnlocked`, `CurrentPaymentMultiplier`, `{1.0, 1.05, 1.12, 1.2}` all stay. Only `TierForAverage(float)` → `TierForLevel(int)`:

```
Level  1–3   → Bronze   ×1.00
Level  4–8   → Silver   ×1.05
Level  9–15  → Gold     ×1.12
Level 16+    → Diamond  ×1.20
```

Serialize `int[] tierMinLevel = { 1, 4, 9, 16 }`. Start at Bronze / Level 1. No single delivery can skip a tier.

**Diamond multiplier gate:** while `RecentForm < 4.2`, a Diamond player keeps the tier + order access but the payout multiplier temporarily drops to Gold's 1.12 (DoorDash "rating separate from standing").

## D. Integration points

**`ReputationManager.cs`** (bulk of the work; stays `DontDestroyOnLoad` singleton, event-driven):

- New serialized: `rpCurveBase = 500f`, `rpCurveExp = 1.6f`, `int[] tierMinLevel = {1,4,9,16}`, `float[] rpByStar = {2,10,30,60,100}` (1★–5★, interpolate). Keep `recentDeliveryWindow`, `paymentMultipliers`. `silver/gold/diamondThreshold` + `startingAverage` become unused.
- New state/API: `int TotalReputationPoints`, `int CurrentLevel`, `int RPIntoCurrentLevel`, `int RPForNextLevel`, `float RecentForm => AverageScore`.
- New events: `event Action<int> OnLevelUp`, `event Action<int,int,int> OnReputationProgress`. **Keep** `OnReputationChanged(float, ReputationTier)` + `OnReputationLevelChanged(ReputationTier)` firing (HUDController subscribes).
- Changed: add overload `RegisterDelivery(DeliveryResult result)` (stars + distance + repeat); keep `RegisterDelivery(float stars)` (distanceFactor = 1). Body: enqueue star into `recentScores` (Recent Form); `TotalReputationPoints += round(baseRP(stars) * distanceFactor * routeRepeatFactor)`; recompute `CurrentLevel`; on increase → `OnLevelUp`; `CurrentTier = TierForLevel(CurrentLevel)`; fire events.
- `TierForAverage` → `TierForLevel`. `CurrentPaymentMultiplier` → tier index + RecentForm gate on Diamond. `Awake()`: `TotalReputationPoints = 0; CurrentLevel = 1; CurrentTier = Bronze`.
- Save API: keep `GetScoresSnapshot()` / `RestoreScores(...)`; add `int GetReputationPoints()` + `RestoreReputation(int rp, List<float> recentScores)`.

**`OrderManager.cs`** (surgical):

- New serialized: `carefulSpeedThresholdKph = 90f`, `crashStarPenalty = 0.8f`, `damageStarPenaltyPerPoint = 0.05f`, `speedStarPenaltyMax = 0.6f`, `closeCallMarginFraction = 0.08f`, `closeCallPenalty = 0.3f`, `float[] cargoSensitivity = {0.6f, 1.0f, 1.6f}` (by `CargoType`).
- `Start()`: resolve `VehicleController` + `VehicleCondition` (null-safe).
- `TryPickup`: snapshot `conditionAtPickup`, `collisionsAtPickup`, `deliveryStartTime`, reset `speedingSeconds`/`peakSpeedKph`.
- `Update()` `Delivering` branch: sample speed.
- `TryDeliver`: replace `if (onTime) { stars = 5; }` with `ScoreDelivery(...)` per A. **Leave `payFactor` as-is** (time-based only — money balance is an open TODO).
- `DeliveryResult` gains: `float RawStars`, `int Collisions`, `float ConditionLost`, `float PeakSpeedKph`, `float JobDistanceMeters`, optional `string[] StarPenalties`. Pass whole `DeliveryResult` to `RegisterDelivery(result)`.
- `FailActiveOrder`: `RegisterDelivery(1f)` now yields ~2 RP, no level movement.

**`VehicleCondition.cs`** — additive only: `CollisionCount` + `OnCollision` in `OnCollisionEnter`.

**`SaveSystem.cs` / `SaveData`**:

- `SaveData`: add `int reputationPoints;` + `int reputationLevel;`. Keep `reputationScore` (now = Recent Form) + `recentDeliveryScores`.
- `CurrentSaveVersion` 2 → 3.
- `Migrate()`: `if (saveVersion < 3) { reputationPoints = 0; reputationLevel = 1; }` — optionally seed from old `reputationScore` (`>= 4f → 3000 RP`, `>= 3f → 800 RP`, else 0).
- `SaveGame` writes RP/level; `LoadGame` → `RestoreReputation(...)`.

**`HUDController.cs`** — display only: `HandleReputationChanged` text includes level (`Sv {level} · {tier} · ★{recentForm:F1}`); subscribe `OnReputationProgress` for a fill bar. Post-delivery results card = follow-up.

## E. Anti-farm / realism

1. **Distance floor on RP** — `distanceFactor` floors at ×0.5, reaches ×1.0 at 250 m.
2. **Same-route diminishing returns** — `OrderManager` keeps a 5-entry ring buffer of recent completed `(pickupId → deliveryId)` pairs; if the finished pair appears `k` times, `routeRepeatFactor = 1 / (1 + 0.6k)` (2nd ≈ 62%, 3rd ≈ 45%, 4th ≈ 36%).
3. **Picky-customer jitter** — `stars += Random.Range(-0.15f, +0.10f)` before final clamp.
4. **Recent Form gates the Diamond multiplier** (C).
5. **Failures never negative, never profitable** — timed-out = +2 RP, 1★ into Recent Form.
6. *(Later)* min-distance job filtering, cooldown on re-accepting the exact order.

## Smallest-first implementation

1. `VehicleCondition`: `CollisionCount` + `OnCollision` (~4 lines). Commit.
2. `ReputationManager`: RP total + level curve + `TierForLevel`; keep averaging as Recent Form; start Bronze/L1; `RegisterDelivery(float)` accumulates RP; fire `OnLevelUp` / `OnReputationProgress`. Commit.
3. `SaveData` v3 + `Migrate` + read/write RP + `RestoreReputation`. Commit.
4. `OrderManager`: resolve vehicle in `Start`; snapshot in `TryPickup`; sample speeding in `Update`; replace flat-5 with `ScoreDelivery(...)`; extend `DeliveryResult`; pass to `ReputationManager`. Commit.
5. Tier→level bands + Diamond / Recent-Form multiplier gate. Commit.
6. Anti-farm: `distanceFactor`, same-route ring buffer, rating jitter. Commit.
7. `HUDController`: level text + progress bar. Commit.
8. *(Optional follow-up)* post-delivery results card.

Steps 1–4 alone fix both reported problems; every step compiles.
