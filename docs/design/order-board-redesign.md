# Order-Offer / Job-Board Redesign — Design Spec

Status: **APPROVED for implementation (2026-08-30)**. Note from user: **TEKRAR GÖZDEN GEÇİRİLECEK** (revisit / review again after first pass). TODO #27 + #28 (and touches #41/#44/#42, aligns with #25 and lays groundwork for #36).

Source: research agent proposal. Sibling: `reputation-redesign.md` — shared: Save v2→v3, `OrderManager.TryDeliver`, `DeliveryResult`. Coordinate.

**Map-scale note:** current blockout points are ~35–150 m apart, so almost every order hits the 45 s `minTimeLimitSeconds` floor today. Formulas below are scale-independent + use a distance floor, so they work now and after the map grows (TODO #11/#12).

## A. Offer generation & pacing

### Hybrid: authored templates + runtime composition

`OrderData` becomes an **archetype template** (flavor + rules). Each arrival builds a concrete **`OrderOffer`** (plain runtime class, not an asset) = template + runtime-chosen pickup point + delivery point + customer + difficulty rolls. Pay and time **derived from real pickup→delivery distance**, not stored. ~12–18 templates → effectively unlimited offers (how ETS2's freight market works).

### Arrival process — time-varying Poisson

- **In-game day = 20 real minutes.** 24 h in 20 min → 1 in-game hour ≈ 50 real s. Fresh save starts **06:00**.
- **Demand curve λ(t)** (offers per real-minute, before reputation scaling):

| Period | Hours | λ | Feel |
|---|---|---|---|
| Early morning | 06–08 | 0.5 | slow start |
| Morning rush | 08–11 | 1.6 | busy |
| Midday lull | 11–12 | 0.4 | quiet |
| Lunch rush | 12–14 | 2.2 | peak, food-heavy |
| Afternoon | 14–17 | 0.9 | steady |
| Evening rush | 17–20 | 2.4 | peak |
| Night | 20–00 | 0.7 | tapering, high-value niche |
| Late night | 00–06 | 0.15 | near-dead |

Per frame `p = λ(t) * Time.deltaTime / 60`; Bernoulli roll; on success spawn one offer if `currentOffers.Count < maxOffers`. Add ±15 % per-day noise to each λ.

- **Burst mechanic:** every arrival has **35 % chance to trigger a cluster** — schedule 1–2 extra arrivals 3–8 s later (restaurant dumping a batch; Uber Eats / DoorDash "stacked offers"). This is what makes the board feel alive.
- **Max concurrent offers: 10** (hard cap). Arrivals while full are **silently dropped** ("another driver took it").
- **Offer expiry:** replace fixed 15 s `RotateOffers()` with **per-offer TTL = Random(75 s, 150 s)** real time; on expiry the row fades. Accepted/rejected offers leave immediately; their slot is not instantly refilled — arrivals do that.
- **Minimum floor:**
  - Daytime 06:00–22:00: soft floor **1**. Board empty > 45 s → force-spawn one modest offer. 20–40 s gaps allowed.
  - Night 22:00–06:00: floor **0**. Board can be genuinely empty up to ~3 real minutes — the "quiet stretch"; makes rushes feel earned.
  - Floor is a floor, never a target — during a lull the board is not pushed above ~3.
- **Reputation / level scaling** — 3 knobs:
  1. Arrival rate ×tier: Bronze 0.8 / Silver 1.0 / Gold 1.25 / Diamond 1.5. Daytime floor: Bronze 1 / Silver 1 / Gold 2 / Diamond 2.
  2. Quality mix via per-template `weightByTier[4]`: Bronze ≈ 60 % short cheap hops; Diamond ≈ 25 % short, more mid/long premium.
  3. Priority/VIP offers (B3) only at Silver+.

### Worked example (Gold, start 06:00)
0:40 first offer → 1. 1:10 second → 2. Accept closer → 1. 1:50 (~08:00 rush) cluster: 3 arrivals in 6 s → 4 mid-delivery. 3:30 board 5. 4:30 (~11:00 lull) arrivals dry up, 2 hit TTL → 5→3→2. 5:00 (~12:00 lunch) floods to cap 10 in ~90 s, extras dropped, surge banner. 7:00 steady 3–5. 9:30 (~17:30) second flood + surge. 12:00 (~20:30) drifts to 1–2; fuel stop + upgrade. 13:00 (~22:30) night floor 0 → empty ~2:30. 15:00 (~00:00) one "gece nöbeti" offer: ₺180 fragile pharmacy, long TTL. Day rolls over 15:20.

## B. Engagement hooks (4, all simple)

**B1. Surge pricing** — rolling demand ratio `D = arrivals_last_120s / expected_last_120s`; plus scarcity trigger (board ≤ 1 for > 40 s). Surge when `D > 1.6` OR scarcity. `S = clamp(1 + 0.5·(D−1), 1.15, 2.0)` for demand; flat **1.35** for scarcity. New offers during surge get `payment *= S` + red `SURGE` tag. Recompute every 5 s; ends when `D < 1.2` and board > 2. *Lull now pays you to patrol.*

**B2. Delivery streak** — `streak` = consecutive on-time (≥ 4★) deliveries; resets on late/fail. Bonus per delivery `min(streak · 0.04, 0.40)` × base pay. HUD `🔥 ×{streak}`; toasts at 3/5/10; at 5 and 10 also +0.1 to last reputation score. **Session-scoped, not persisted** — "don't break the chain, one more run".

**B3. Priority / VIP offer** — each arrival: **4 % chance** (Silver+ only). `payment × Random(2.2, 3.5)`, time limit **×0.8** (harder), gold border + "ÖNCELİKLİ" tag, **TTL only 40 s**. On-time delivery makes that customer a guaranteed Regular. Max 1 on board; ≥ 90 s between spawns. *Variable-ratio jackpot.*

**B4. Daily target** — per in-game day: **6 deliveries** OR `₺ earned ≥ tier baseline` (Bronze ₺400 → Diamond ₺1200). Hit → bonus **₺150–₺400** (tier-scaled) + small reputation bump + toast. Shown in panel header ("4/6 teslimat"). One target only — never a chore list.

## C. Order variety model

### `OrderOffer` (runtime class, per arrival)

```
OrderOffer {
  OrderData template;
  string pickupPointId, deliveryPointId;    // chosen at runtime
  CustomerInstance customer;
  float distanceKm, payment, timeLimit;     // derived
  float surgeMultiplier;                    // 1.0 unless surge
  OfferFlags flags;                         // Priority | Rush | LongHaul | AwkwardDrop | RegularCustomer
  float ttl;
  string displayName;                       // composed
}
```

### Authored vs generated

**Authored — `OrderData` template (~12–18 assets):** `cargoType` (existing), `minReputationTier` (existing), `namePatterns[]` (e.g. `"{customer} için yemek"`, `"{pickupPlace}'ten paket"`), `cargoNouns[]` ("sıcak çorba", "seramik vazo", "evrak"), `allowedPickupCategories[]` / `allowedDeliveryCategories[]` (tags: `restaurant`/`depot`/`shop`/`house`/`office`/`pharmacy`), `baseDifficulty` (Easy/Normal/Hard), `weightByTier[4]`, optional `payPerKmOverride`, `handlingFee`.

**Generated at runtime:** pickup & delivery point (random valid pair from `InteractionPoint` registry filtered by allowed categories + min/max separation), customer, distance, payment, time limit, surge, flags, display name.

### Distance → time (extends `GetEstimatedTimeLimit`)

```
d          = Vector3.Distance(pickup, delivery)
routeDist  = d * routeFactor                          // existing 1.4
travelTime = routeDist / (averageSpeedKmh / 3.6)
timeLimit  = max(minTimeLimitSeconds, (travelTime + timeBufferSeconds) * diffTimeFactor)
diffTimeFactor = Easy 1.15 / Normal 1.0 / Hard 0.85
```

**Pickup timer (#42):** separate, generous, **no rating penalty** — `pickupLimit = travelToPickupEstimate * 1.8`, only cancels if wildly exceeded. *(Already implemented in a compatible way — tune to ×1.8.)*

### Distance → pay (#44 — replaces flat `paymentAmount`)

```
effKm     = max(distanceKm, 0.3)
base      = 20 + 14 * effKm
cargoMult = Food 1.0 / Package 1.05 / Fragile 1.25
diffMult  = Easy 0.95 / Normal 1.0 / Hard 1.20
rushMult  = 1.30 if flags.Rush else 1.0
payment   = round( (base*cargoMult*diffMult*rushMult + handlingFee)   // Fragile handlingFee +15
                   * surgeMultiplier * reputationPayMultiplier )
```

Worked: 2.0 km Fragile/Hard, no surge, Gold ×1.12 → `(20+28)=48 → ×1.25×1.20=72 → +15=87 → ×1.12 ≈ ₺97`, ~3:30. 0.8 km Food/Easy/Bronze → ≈ ₺30, ~2:10.

Keep `OrderData.paymentAmount` / `timeLimitSeconds` as **fallback** when scene points don't resolve (same guard `GetEstimatedTimeLimit` already has) — old assets + `_Test` runners keep working.

### Difficulty modifiers (0–2 per offer, flags + multipliers)
- **Rush** — timer ×0.8, pay ×1.3, "ACİL" tag
- **LongHaul** — forces a far delivery point (top 25 % distance), `payPerKm ×1.15`
- **AwkwardDrop** — delivery point flagged `tightAccess`, +₺20 handling
- **Fragile** — via `cargoType`; "hasar = düşük puan" (hooks `VehicleCondition`)

## D. Customer / sender variety (#27)

### `CustomerPoolData : ScriptableObject` (single asset, or one per district)

```
string[] individualFirstNames;    // ~60
string[] individualLastNames;     // ~40
BusinessEntry[] businesses;       // ~30: { name, CustomerType, pickupCategories[] }
CustomerTypeWeight[] typeWeights; // Individual / Restaurant / Shop / Corporate / Clinic
```

60 × 40 = 2400 individual names from ~100 authored strings + ~30 businesses.

**`CustomerInstance`** (runtime): `displayName` ("Ayşe Y." / "Kardelen Çiçekçilik"), `type`, `customerId` (stable hash of displayName), `completedForThisCustomer` (from save).

### Attach & display
- On offer creation: pick `CustomerType` weighted by template (Food → Restaurant sender + Individual recipient; Corporate parcel → Office). Sender bound to pickup category, recipient to delivery category.
- Recipient = "the customer" who rates you; sender is flavor.
- Row line: `Kimden: Kardelen Çiçekçilik → Kime: Ayşe Y.` Icons: ⭐ Regular, 👑 VIP. If `completedForThisCustomer > 0`, show their last star rating.

### Regular customer
- After **≥ 2 on-time** deliveries for the same `customerId` → **Regular** (persisted).
- Regular offers: **+15 % pay**, **+0.3 star lateness forgiveness**, TTL ×1.5, "Sadık Müşteri" tag, ~1.3× more often.
- Fail a Regular twice → reverts ("müşteriyi kaybettin" toast).
- Cap tracked Regulars at **12** (drop least-recently-served).

## E. Integration points

### `OrderManager.cs`
- Fields: `maxOffers 3 → 10`; drop fixed `offerRefreshInterval` rotation; add `float[24] lambdaByHour` (or `DemandCurveData` SO), `float dayLengthRealMinutes = 20`, surge/priority/streak params, `CustomerPoolData customerPool`. `orderPool` becomes the template list.
- State: `List<OrderOffer> currentOffers` (was `List<OrderData>`), `float clockHours`, `float surgeMultiplier`, `int streak`, `Queue<float> arrivalTimestamps`, `Dictionary<string,int> regularCustomers` (from save), pending-cluster queue, per-day daily-target counter.
- Replace `RefreshOffers()` + timer `RotateOffers()` with `TickArrivals(dt)` (advance clock, λ(t)×tierFactor Bernoulli, clusters, cap, floors), `TickOfferTTL(dt)` (expire + `OnOffersChanged`), `TickSurge(dt)`.
- `SpawnOffer()` — weighted template pick → valid pickup/delivery pair → `CustomerInstance` → distance/pay/time → modifier & priority rolls → TTL → add → event.
- `AcceptOffer` / `RejectOffer` — take `OrderOffer`; resolve points from the offer's chosen IDs; time limit & payout from the instance.
- `TryDeliver` — streak update + bonus, `regularCustomers[id]++`, daily-target increment, milestone toasts. Apply `reputationPayMultiplier` here; bake only surge/streak at accept.
- New events: `OnSurgeChanged(float)`, `OnStreakChanged(int)`, `OnDailyProgress(int,int)`, `OnOfferExpired(OrderOffer)`. `OnOffersChanged` now carries `IReadOnlyList<OrderOffer>`.

### `OrderData.cs`
- Add: `namePatterns[]`, `cargoNouns[]`, `allowedPickupCategories[]`, `allowedDeliveryCategories[]`, `enum Difficulty baseDifficulty`, `float[] weightByTier` (len 4), optional `payPerKmOverride`, `handlingFee`.
- Keep all existing fields; `paymentAmount` / `timeLimitSeconds` = fallback. `OnValidate`: warn if `namePatterns` empty / `weightByTier.Length != 4`, clamp weights ≥ 0.

### `OrderPanelController.cs` + `UIFactory` + `UIBootstrap`
- **ScrollRect required** (10 rows × ~90 px > panel height; `UIFactory` has no ScrollRect helper). Add `UIFactory.CreateScrollView(parent, name) → (ScrollRect, RectTransform content)` — viewport `RectMask2D`, content `VerticalLayoutGroup` + `ContentSizeFitter`, vertical only. `UIBootstrap.BuildOrderPanel` feeds that content as `listContainer` (panel ~470×560).
- `BuildRow(OrderOffer)`: line 1 `displayName` + tags (SURGE / ÖNCELİKLİ / ACİL / ⭐Sadık); line 2 `Kimden → Kime`; line 3 `{cargoLabel} • {distanceKm:0.0} km • ₺{payment:0} • {mm:ss}`; colored left border by tag. Rows via layout group, not manual index offsets.
- Header: in-game clock + daily-target progress + surge banner. Empty state: "Şu an teklif yok…", + "(gece — sipariş azalır)" at night. Countdown refresh once/sec (`InvokeRepeating`), not per-frame.

### `GameClock.cs` (new, `_Core/`, tiny)
Scene-local like `RouteManager`. Exposes `Hour` (0–24), `Hours01`, `DayIndex`, `IsNight`, `OnHourChanged`. Drives demand curve + HUD clock; seed for TODO #36 day/night visuals later (visuals out of scope here).

### Save / load (`SaveSystem.cs` / `SaveData`)
- `CurrentSaveVersion 2 → 3`.
- Add: `List<CustomerRegularEntry> regularCustomers` (`{customerId, displayName, completed}`), `int currentDayIndex`, `float clockHours`, `int lifetimeDeliveries`.
- **Do NOT persist `streak`** (session-scoped) and **do NOT persist the live board** (regenerate from clock on load).
- `Migrate()` v2→v3: null-init the new collections (same pattern as existing v0/1→2 block).

### Editor tooling (`DeliverySimSetup.cs`)
- New idempotent `[MenuItem("DeliverySim/Setup/5 - Create Order Templates + Customer Pool")]` — ~12 template assets + one `CustomerPoolData` (pre-filled), wired into `OrderManager`. Keep the "9 sample orders" generator as fallback/tests.
- Points need **category tags**: add `string[] categories` to `InteractionPoint`, default-populated in an editor pass from existing `InferTheme(pointId)` logic.

## F. Comparable games — what was taken

- **ETS2/ATS freight & cargo market:** templates-not-instances, distance-derived pay/time, reputation-gated quality tiers, board refresh as a first-class rhythm; special/high-value transport → Priority offer.
- **Snowrunner/Expeditions:** `weightByTier` gating; a few always-available low-value jobs beneath a rotating better set.
- **Death Stranding:** Priority/VIP as a distinct rare offer with tighter constraints + big reward; "regular customer" = per-location affiliation with rising lateness forgiveness.
- **Hard Truck / Truck Tycoon:** keep accept/reject meaningful with 1–2 **blunt** modifiers (Rush, AwkwardDrop, LongHaul), not many subtle stats.
- **PowerWash Simulator:** the daily target as a soft completion goal; penalty-free generous pickup timer; low-friction panel.
- **Slay the Spire / roguelite shop psychology:** per-offer TTL so the board visibly churns; stochastic arrivals as the "reroll"; 4 % Priority as the rare drop; surge as a reason to watch during a lull.
- **Mobile courier / idle:** capped session-scoped streak bonus; daily target payout; milestone toasts.
- **Jalopy / Lonely Mountains:** night 0-offer floor (downtime is a feature); one target per day, not a checklist.
- **Real gig apps (Uber/DoorDash):** meal-time λ(t) curve; surge `1 + 0.5·(D−1)` capped 2.0; cluster arrivals (35 % → 1–2 extra in 3–8 s); short TTL / 40 s Priority window; unaccepted arrivals silently "taken by someone else".

## Smallest-first implementation

Steps 1–5 = mechanical backbone (touch `OrderManager`, `OrderPanelController`, `UIFactory`, `UIBootstrap`, + new `GameClock`). Steps 6+ layer variety/hooks on top. Save v2→v3 only at step 10.

1. **`maxOffers` 3 → 10 + scroll view.** Add `UIFactory.CreateScrollView`; `OrderPanelController` uses a `VerticalLayoutGroup` content instead of index math. Add distance to the row. Prove 10 rows render.
2. **Per-offer TTL** = `Random(75,150)s`; delete the fixed 15 s `RotateOffers()` timer. Board churns.
3. **Time-varying arrivals.** Add `GameClock` + `float[24] lambdaByHour`; replace "fill every empty slot" with per-frame Bernoulli arrivals; daytime soft floor (1 after 45 s empty), night floor 0.
4. **Cluster arrivals** — 35 % chance an arrival schedules 1–2 more in 3–8 s.
5. **Distance-derived pay & time** (#44/#41): `payment = 20 + 14·effKm` with cargo/difficulty mults; keep `OrderData.paymentAmount` fallback.
6. **Runtime composition + customer names:** `OrderOffer` class + `CustomerPoolData` (one asset) + 12 `OrderData` templates via a new idempotent setup MenuItem; random valid pickup/delivery pair by category. Solves #27.
7. **Surge** (demand ratio + scarcity, `S` capped 2.0, row tag).
8. **Delivery streak** (`min(streak·0.04, 0.40)`, HUD `🔥×n`, session-scoped).
9. **Priority/VIP offer** (4 % at Silver+, pay ×2.2–3.5, 40 s TTL, gold border).
10. **Regular customers** (`Dictionary<customerId,int>` in save, v3 migration, +15 % pay tag) + **daily target** payout.
