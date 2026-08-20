# Game Design Document
## [Working Title] — Delivery Driver Simulator

**Version:** 0.1 (First Draft)
**Engine:** Unity 6.4
**Platform:** PC (Steam) — a mobile port possibility can be evaluated in a later phase
**Genre:** Casual Driving / Job Simulator / Economy

---

## 1. Concept Summary

The player embodies an independent driver registered with a delivery/courier platform. They take orders from the app, pick up the cargo/package/food from the specified point, and deliver it to the customer's address on time and undamaged. Delivery quality is directly reflected in the customer rating, and the rating directly translates into money earned and new opportunities unlocked. The money earned is reinvested into the game through vehicle upgrades, new vehicles, licenses, and cosmetic customization.

**Core fantasy:** "Be your own boss, learn the streets, build your reputation, grow your fleet."

**One-sentence goal:** A simple but satisfying delivery loop + a meaningful economy loop = a replayable, relaxing job simulator.

---

## 2. Core Game Loop

```
Take Order → Drive to Pickup Point → Pick Up Cargo → Drive to Delivery Point (time pressure)
   → Deliver → Get Score/Rating → Get Paid → Spend/Upgrade in Shop → Take Order Again
```

Loop length: A single delivery should take **2-5 minutes** (so a session is playable for 20-40 minutes).

---

## 3. Core Mechanics

### 3.1 Order System
- Order list coming through the phone/app interface (1-3 active offers at a time)
- Each order: pickup point, delivery point, estimated time, payment amount, cargo type (food/package/fragile item, etc.)
- The player can accept/reject an order — this adds a route-planning and risk/reward decision

### 3.2 Delivery & Scoring
- On-time delivery = full score (5 stars)
- Late delivery = gradual score reduction (linear based on time, or threshold-based)
- Delivering to the wrong address / vehicle taking damage (if the cargo has a "health" value) = low score
- The average score determines the player's **reputation level**

### 3.3 Reputation & Level System
- Reputation tiers tied to average score (e.g. Bronze → Silver → Gold → Diamond)
- Higher reputation → access to higher-paying orders + fewer "ordinary" jobs
- Lower reputation → order pool narrows, penalty risk increases

### 3.4 Economy & Spending (Critical System)
The money earned must flow into these channels — **this is the game's most important design axis:**

| Category | Example | Purpose |
|---|---|---|
| Vehicle purchase | New bike/motorcycle/car/truck | Sense of progression, access to new order types |
| Vehicle upgrade | Engine, tires, fuel tank, damage resistance | Performance boost |
| Cosmetic | Livery, wheels, decals | Personalization (low cost, high feel) |
| Expense | Fuel, maintenance, repair | "Friction" that eats money back — makes the economy meaningful |
| License | Motorcycle license, heavy vehicle license | Unlocks new vehicle categories |
| Base/Garage | Storage space, housing multiple vehicles | Sense of long-term investment |

> **Design note:** Without expense items (fuel/repair), the economy inflates one-directionally and loses its meaning. Every delivery must have a small cost so the concept of "profit margin" feels real to the player.

### 3.5 Driving System
- Simple but satisfying vehicle physics (WheelCollider-based — the existing PRO RACER foundation can be used as a base)
- Optional damage/balance system: hard maneuvers can affect cargo quality (especially when carrying food/fragile items)
- Mini-map + route line (GPS simulation)

---

## 4. Progression Structure

**Early game:** A single vehicle (bike/scooter), a small map area, low-paying orders
**Mid game:** Car unlocked, map expands, reputation system kicks in, upgrade shop becomes active
**Late game:** Truck/heavy vehicle, multi-vehicle fleet (optional idle/passive income mechanic), high-tier reputation rewards

---

## 5. User Interface (UI)

- **Phone Screen:** Active order list, accept/reject buttons, earnings history
- **HUD (while driving):** Route marker, remaining time, cargo status icon, speedometer
- **Shop Screen:** Vehicle/upgrade/cosmetic categories, owned balance
- **Score/Reputation Panel:** Average stars, reputation level, progress remaining to next level

---

## 6. Technical Architecture Notes (Unity)

Systems that directly overlap with your existing project experience:

- **Order data** → `ScriptableObject`-based `OrderData` (pickup/delivery coordinates, payment, time, cargo type) — exactly the same logic as the `ItemData` approach in the Inventory system
- **Cargo interaction** → `IInteractable` / `IUsable` interfaces — can be built on top of the existing Interaction System
- **Vehicle physics** → WheelCollider + ScriptableObject vehicle profiles (the structure from PRO RACER)
- **Inventory/Upgrade** → A `VehicleUpgradeSlot` system similar to `InventorySlot`
- **Economy Manager** → A single `EconomyManager` (MonoBehaviour or Singleton) — balance, expense/income events
- **Order Manager** → `OrderManager` — active order pool, timer, score calculation logic

> Namespace suggestion: an independent namespace like `DeliverySim` can be used to avoid conflicting with your existing projects.

---

## 7. MVP Scope (First Prototype Goal)

To avoid excessive scope creep, the first prototype should have **only** the following:

1. A single vehicle (car)
2. A small, hand-built map (5-8 delivery points)
3. A simple order loop (accept → go → deliver → get score)
4. Basic economy: earn money, buy 2-3 upgrades
5. Minimal UI (text-based order list + HUD)

Not touching things like map expansion, multiple vehicles, or story before this scope is nailed down directly improves the project's chances of being finished.

---

## 8. Differentiation / Standout Ideas (optional, later phase)

- A distinctive visual identity (e.g. a retro/low-poly style, or a local/Turkey-themed city setting)
- Light humor or short NPC dialogues (your existing NPC dialogue system could be adapted)
- Dynamic difficulty layers like weather / traffic density

---

## 9. Risks & Things to Watch Out For

- **Economy balance:** Proceeding without testing the income/expense ratio is the biggest risk — even in the early prototype, a basic income/expense table should be kept
- **Content volume:** Map and order variety are produced by hand, which eats time — keeping the scope small in the MVP is essential
- **Repetitiveness:** Since the loop is simple, there's a monotony risk in play sessions longer than 5-10 hours; progression/cosmetic systems should compensate for this
