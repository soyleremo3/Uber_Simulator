# Development Checklist — Delivery Driver Simulator

This list is designed to be followed in order. Each main heading builds on the previous one. Try not to move to the next section before fully finishing one — especially the "Core Loop" sections (3-6) depend on each other.

## 0. Preparation

- [x] Create the Unity 6.4 project (3D Core template)
- [x] Set up version control (Git + .gitignore)
- [x] Create the folder structure (`Assets/_Uber Simulator/_Scripts/...` — the existing structure is preserved)
- [x] Decide on a namespace (`DeliverySim`) and use it in all scripts
- [ ] Lock in the Input System decision (package installed, code currently uses Legacy Input — decision pending)
- [x] Git initial commit

## 1. Architectural Foundations (Code Skeleton)

- [x] `GameManager` singleton (scene transitions, GameState enum)
- [x] `EconomyManager` (balance, AddMoney/SpendMoney, OnMoneyChanged, OnTransactionFailed)
- [x] `OrderData : ScriptableObject` (pickupPointId, deliveryPointId, payment, timeLimit, cargoType)
- [x] `IInteractable` interface
- [x] `IUsable` interface
- [x] `SaveSystem` skeleton (JSON, versioning + includes owned vehicles)

## 2. Vehicle Controller (Driving)

- [x] Placeholder vehicle + physics setup (raycast suspension — WheelCollider NOT USED, a deliberate decision)
- [x] `VehicleController.cs` (throttle/brake, steering, gear/RPM simulation, handbrake, driving assists)
- [x] Camera system (Cinemachine 3 + `VehicleCameraRig` intermediate target + `SmoothMouseLook` + `CameraModeController`)
- [x] Driving feel tested (previous commits: "Vehicle Controller Fixed", "VehicleFollowCamerea Fixed")
- [ ] Basic damage/balance tracking (optional — deferred)
- [ ] `VehicleData : ScriptableObject` — the current VehicleController deliberately does not use a SO; will be reconsidered in the shop phase (7)

## 3. World / Map (Blockout)

- [ ] Small test map blockout (gray boxes, road network)
- [ ] Waypoint/route system (code: `Waypoint` + `RouteManager`)
- [ ] 5-8 pickup/delivery point locations (code: `PickupPoint` + `DeliveryPoint` components + gizmo)
- [ ] Spawn point (garage)
- [ ] Environment collisions
- [ ] Basic lighting

## 4. Order System (CORE LOOP)

- [ ] `OrderManager.cs` (order pool, random generation, accept/reject, time counter)
- [ ] Accept → mark pickup point → pick up cargo → mark delivery point → deliver
- [ ] Score based on time + `EconomyManager.AddMoney()` + new order generation
- [ ] First playable prototype test (test scenario in MANUAL_STEPS)

## 5. Scoring & Reputation

- [ ] `ReputationManager.cs` (average score, Bronze/Silver/Gold/Diamond thresholds, OnReputationLevelChanged)
- [ ] Order pool filtering based on reputation
- [ ] Low-score penalty (fewer orders / lower payment multiplier)

## 6. Economy & Expenses

- [ ] Fuel system (depletion while driving + station interaction)
- [ ] Maintenance/repair cost
- [ ] Income/expense balance test (table)

## 7. Shop & Upgrades

- [ ] `ShopManager.cs`
- [ ] `VehicleUpgradeData : ScriptableObject`
- [ ] 3 upgrade categories: Engine, Fuel Tank, Durability
- [ ] New vehicle purchase flow
- [ ] Cosmetics (optional, for later)

## 8. UI

- [ ] Canvas structure
- [ ] Phone/Order screen (cards, accept/reject, earnings history)
- [ ] HUD (speed, route/distance, time, cargo icon)
- [ ] Shop screen
- [ ] Reputation panel
- [ ] Main menu / Settings / Pause
- [ ] All UI wired event-driven

## 9. Asset Production / Sourcing

- [ ] Vehicle models (3-4 types), environment/city kits, UI icons, sound/music → ASSET_NEEDS.md

## 10. Animations

- [ ] Wheel rotation (already in code), door, suspension effect, UI tweens

## 11. Sound & Music

- [ ] Engine sound (RPM pitch), ambience, UI SFX, music, `AudioManager.cs`

## 12. Visual Effects & Polish

- [ ] Particles, camera shake, UI transitions, post-processing, final lighting

## 13. Testing & Balance

- [ ] 20-30 full-loop playtests, economy balance, time limits, performance, external testing

## 14. Optimization

- [ ] Batching, LOD, Occlusion Culling, object pooling, Profiler

## 15. Build & Release

- [ ] Player Settings, Windows build, Steamworks, store materials, localization (TR+EN), final bug sweep

---

**Golden rule:** Don't move on to asset/animation/visual polish work before Section 4 is complete (before accept → go → deliver → earn money works).
