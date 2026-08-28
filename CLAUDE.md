# CLAUDE.md — Uber Simulator (Delivery Driver Simulator)

This file is the project context for Claude Code. Follow these rules when writing/reviewing code.

## ⚠️ CRITICAL — Read RULES.md First, Every Session

[RULES.md](RULES.md) holds the user's non-negotiable working rules (Input System policy, commit/push discipline, communication style, verify-before-acting, permission to push back, hands-off backup scenes folder). These are not optional preferences — read them at the start of every session and follow them in every response, unless the user explicitly says otherwise in that session.

## Game Summary

Casual driving / job simulator / economy. The player is an independent courier driver: accept an order → pick up the load from the pickup point → deliver it to the delivery point on time → earn a rating and payment → invest earnings into vehicle/upgrades/cosmetics.

**Core loop (working in code):**
```
Offer list (max 3, auto-fills from pool) → Accept → Drive to Pickup Point → Pick Up Load
   → Drive to Delivery Point (distance-based time limit, gradual score/payment drop when late)
   → Deliver → Star Rating + Payment (reputation multiplier) → Reputation Updated → Spend/Upgrade in Shop
```
Single delivery: 2-5 min. Session: 20-40 min.

Engine: Unity 6000.4.6f1 (Unity 6.4). URP + Cinemachine 3.1.7 + new Input System. Platform: PC (Steam) priority, mobile port in a later phase.

## Actual Project Status (important — this section should be updated periodically)

The code is well beyond the "MVP" definition in the GDD: order loop, economy, reputation/tier system, vehicle upgrades, fuel/damage/repair costs, JSON save (with versioned migration), fully runtime-built UI, and one-click editor setup tools are all working and interconnected. Before starting new work, check under `_Scripts/` for "does this already exist" — items on the MVP list can be considered complete, and some systems the GDD calls "later phase" (reputation tiers, vehicle upgrades) are already in production.

The most active/fragile area right now is the **map/scene** side: a single scene `MainScene.unity` (45 MB) combines two different visual styles — Tirgames "Stylized Street" (downtown) + Kenney "City Kit Commercial/Suburban/Roads" (`DowntownMapSetup.cs`, `KennyDistrictSetup.cs`). Git history still shows trial-and-error here — before starting new map work, read these two editor scripts and the notes inside them.

**Asset licensing — must be verified before release:**
- `Assets/_Uber Simulator/Art/Assets/Kenny/CityKit*` → Kenney.nl City Kit packages, likely CC0 but there's no license file in the project — should be confirmed via kenney.nl.
- `Assets/TirgamesAssets/StylizedWorld` → Unity Asset Store package ("Stylized Street"), subject to the Asset Store EULA; commercial distribution rights should be verified against the purchase record (Asset Store license type) — no written proof exists in the project.

## Namespace and Architecture

All project code lives under the `DeliverySim` namespace (to avoid conflicts with other existing projects). Editor-only tools are in the `DeliverySim.EditorTools` sub-namespace.

**Established architectural patterns — new code should be consistent with these:**

| System | Pattern | Location |
|---|---|---|
| Order data | `ScriptableObject` (`OrderData`) — pickup/delivery ID, payment, duration, cargo type, min reputation tier | `_Scripts/_Data/OrderData.cs` |
| Order loop | `OrderManager` singleton (scene-local, not DontDestroyOnLoad) — offer pool/rotation, distance-based time limit, lateness penalty, events (`OnOffersChanged`, `OnOrderAccepted`, `OnOrderCompleted`...) | `_Scripts/_Orders/OrderManager.cs` |
| Scene points | `InteractionPoint` abstract base + static ID registry (`TryGetPoint`) — `PickupPoint`/`DeliveryPoint` subclasses | `_Scripts/_Orders/` |
| Cargo/point interaction | `IInteractable` (Interact/GetInteractionPrompt), `IUsable` (CanUse/Use) | `_Scripts/_Interfaces/` |
| Economy | `EconomyManager` singleton, `DontDestroyOnLoad`, `OnMoneyChanged`/`OnTransactionFailed` events | `_Scripts/_Managers/EconomyManager.cs` |
| Reputation | `ReputationManager` singleton — average of last N deliveries → `ReputationTier` (Bronze/Silver/Gold/Diamond), payment multiplier + order lock | `_Scripts/_Managers/ReputationManager.cs`, `_Scripts/_Data/ReputationTier.cs` |
| Shop/upgrades | `ShopManager` singleton — vehicle upgrade catalog (`VehicleUpgradeData`, an asset per category+level) and vehicle catalog (`VehicleData`); `VehicleUpgradeApplier` component applies the purchased level to the vehicle | `_Scripts/_Managers/ShopManager.cs`, `_Scripts/_Vehicles/VehicleUpgradeApplier.cs` |
| Vehicle costs | `VehicleFuel` (fuel consumption/refill), `VehicleCondition` (crash damage/repair) — paid off via `FuelStation`/`RepairStation` (`IInteractable`) | `_Scripts/_Vehicles/`, `_Scripts/_Core/FuelStation.cs`, `RepairStation.cs` |
| Game state | `GameManager` singleton — `GameState` enum (MainMenu/Playing/Paused/OrderActive/GameOver/Shop), scene transitions, `Time.timeScale` on pause | `_Scripts/_Managers/GameManager.cs` |
| Save | `SaveSystem` singleton, JSON-based (`SaveData`), versioned field (`CurrentSaveVersion`) + `Migrate()`, `Application.persistentDataPath` | `_Scripts/_Save/SaveSystem.cs` |
| Route/GPS line | `RouteManager` — Dijkstra (real distance-weighted) over the scene's `Waypoint` graph, a real ground-mesh ribbon (NOT a LineRenderer), `NextTurn` for the HUD turn indicator. The ground-snap raycast is restricted to a dedicated "Road" physics layer (so it doesn't hit building/prop colliders) | `_Scripts/_Core/RouteManager.cs`, `Waypoint.cs` |
| Notification (toast) | Static event hub `NotificationService.Raise(string)` — called from gameplay code, UI listens | `_Scripts/_Core/NotificationService.cs` |
| Vehicle physics | `VehicleController` — **does NOT use WheelCollider**, has its own raycast-suspension system (`VehicleEngine`/`VehicleWheel` serializable subclasses). No longer reads the `VehicleData` ScriptableObject — all tuning lives in Inspector fields | `_Scripts/_Vehicles/VehicleController.cs` |
| Camera | `VehicleCameraRig`, `SmoothMouseLook`, `CameraModeController` (3rd/1st person switching), Cinemachine 3-based | `_Scripts/_Vehicles/` |
| UI | Entirely built in code at runtime — `UIBootstrap` + `UIFactory` (no hand-built Canvas); `HUDController`, `OrderPanelController`, `ShopPanelController`, `PauseMenuController`, `NotificationUI`, `InteractionPromptUI` are event-driven | `_Scripts/_UI/` |
| Editor tooling | `DeliverySimSetup` — one-click scene setup (manager objects, vehicle components, camera, sample order content), idempotent | `_Scripts/Editor/` |

**Folder structure** (`Assets/_Uber Simulator/_Scripts/`):
- `_CarScripts/` — **old/experimental** vehicle code (namespace-less `Car.cs`/`Camera.cs`/`ui.cs`, the ancestor of `VehicleController`) — unused, new work should go into `_Vehicles/`. May be safe to delete but do not delete without approval.
- `_Core/` — scene-wide helpers: `RouteManager`, `Waypoint`, `NotificationService`, `ObjectPool`, `FuelStation`, `RepairStation`
- `_Orders/` — order loop: `OrderManager`, `InteractionPoint`, `PickupPoint`, `DeliveryPoint`
- `_Data/` — ScriptableObject data classes: `OrderData`, `VehicleData`, `VehicleUpgradeData`, `ReputationTier` (enum), `CameraSettings`
- `_Interfaces/` — `IInteractable`, `IUsable`
- `_Managers/` — singleton managers: `GameManager`, `EconomyManager`, `ReputationManager`, `ShopManager`, `AudioManager`
- `_Save/` — `SaveSystem`
- `_Test/` — test runners (`EconomyTestRunner` — temporary, keys 1-4 for economy/save verification)
- `_UI/` — runtime UI (`UIBootstrap`, `UIFactory`, controllers)
- `_Vehicles/` — current vehicle physics, camera, fuel/damage/reset/upgrade components
- `Editor/` — `DeliverySimSetup`, `DowntownMapSetup`, `KennyDistrictSetup` (scene setup tools, compiled Editor-only)

## Controls (current input scheme)

WASD to drive, Space handbrake, E interact, Tab order panel, B shop, C camera mode (3rd/1st person), R reset vehicle (after a rollover), LShift/LCtrl manual gear shift (automatic is already active), Esc pause.

## Code Conventions

- Turkish comment/log message style is already established in the project (`Debug.LogWarning("[EconomyManager] ...")`) — keep following this style. English comments also exist (especially in newer/complex systems, e.g. `VehicleController`, `RouteManager`) — preserve the existing file's language.
- Singleton managers: `Instance` static property + collision check in `Awake()` + `DontDestroyOnLoad` — the existing `EconomyManager`/`GameManager`/`ReputationManager`/`ShopManager`/`AudioManager`/`SaveSystem` pattern. `OrderManager` and `RouteManager` are exceptions: scene-local singletons, NO `DontDestroyOnLoad` (rebuilt in every scene).
- Money/economy transactions must go ONLY through `EconomyManager` — don't keep balance state anywhere else.
- In-scene references are resolved via ID strings (`pickupPointId`/`deliveryPointId` → `InteractionPoint.PointId` in the scene, static registry), not direct object references.
- When adding a new ScriptableObject data class, add `[CreateAssetMenu(menuName = "DeliverySim/...")]` + field validation in `OnValidate()` (the `OrderData`/`VehicleData`/`VehicleUpgradeData` pattern).
- If a new scene setup step is needed, add an idempotent `[MenuItem]` to `DeliverySimSetup` (or the relevant map setup script) instead of dragging objects by hand — this is the existing pattern.
- Use `NotificationService.Raise(...)` for temporary messages shown to the player, don't write to the UI directly.

## Economy Design Note (critical, GDD item 3.4)

Without expense items (fuel, maintenance, repair) the economy inflates one-directionally. This expense layer already exists in code (`VehicleFuel` + `FuelStation`, `VehicleCondition` + `RepairStation`). When adding a new system, make sure the concept of profit margin stays meaningful — every delivery should carry a small cost. The income/expense ratio has not yet been numerically playtested and balanced (see Risks).

Money flow categories: vehicle purchase, vehicle upgrade (engine/fuel tank/durability — implemented), cosmetic (not yet), expense (fuel/maintenance/repair — implemented), license (not yet), base/garage (not yet).

## Scoring / Reputation

On-time delivery = full score (5 stars, full payment). Late delivery = linear decline within the late-grace window (star rating and payment ratio decrease together); if the window is exceeded, the order is cancelled entirely. Average of the last N deliveries → reputation tier (Bronze/Silver/Gold/Diamond) → payment multiplier + order pool access lock. This system was marked as "later phase" in the GDD but is already in production and working — don't treat it as post-MVP "future work" when making changes.

## Risks (GDD item 9)

- Proceeding without testing the economy balance is the biggest risk — validate the income/expense ratio early (systems are in place, numeric balance testing hasn't happened yet).
- Map/order content is produced by hand, which eats time — currently the hottest area; consistently merging two different asset styles (Tirgames + Kenney) carries additional risk.
- The simple loop carries a monotony risk over long play sessions — progression/cosmetics should compensate for this (cosmetic system doesn't exist yet).
- Should not ship to Steam before pre-release asset licenses (section above) are verified.
