# PROGRESS — Development Status

Last updated: 2026-07-19 (uninterrupted full coding session)

## Phase Status

| Phase | Status | Note |
|---|---|---|
| 0. Preparation | ✅ | Folders, git, docs, .gitkeep |
| 1. Architectural Foundations | ✅ | GameManager (Shop state added), EconomyManager (TrySpendMoney alias), OrderData (reputation lock + English CargoType), IInteractable/IUsable, SaveSystem v2 (versioning+migration, reputation, vehicles, upgrades) |
| 2. Vehicle + Camera | ✅ | Existing VehicleController preserved + upgrade/fuel multipliers added. NEW: VehicleCameraController (6 classic camera bugs fixed) + CameraSettings SO + Cinemachine setup MenuItem. VehicleData SO (economy metadata) |
| 3. World Skeleton (code) | ✅ | Waypoint + RouteManager (BFS + LineRenderer GPS), InteractionPoint (ID registry + gizmo) → PickupPoint/DeliveryPoint |
| 4. Order System | ✅ CODE DONE | OrderManager: offer pool, accept/reject, pickup→delivery, time counter, late-delivery score/payment reduction, failure. One-click sample content in editor (Setup menu 4). SCENE SETUP IS ON THE USER — MANUAL_STEPS Sections B+C |
| 5. Reputation | ✅ | ReputationManager: average of last N, Bronze/Silver/Gold/Diamond, payment multiplier, order pool filter |
| 6. Economy & Expenses | ✅ | VehicleFuel (consumption while driving, engine cutoff), FuelStation (partial refill), VehicleCondition (crash damage), RepairStation |
| 7. Shop | ✅ | ShopManager + VehicleUpgradeData (3 categories), vehicle purchase, VehicleUpgradeApplier (applies effects to the vehicle). Upgrade ASSETS to be produced by the user (MANUAL_STEPS D2) |
| 8. UI | ✅ | Event-driven: HUD, order panel (Tab), shop (B), pause (Esc), main menu, interaction prompt, notifications. UIBootstrap builds zero-setup UI at runtime |
| 9. Assets | 🟡 Human work | ASSET_NEEDS.md is up to date; code works fully with placeholders |
| 10. Animation | 🟡 | Wheel rotation is in code; the rest is asset phase |
| 11. Sound | ✅ hooks | AudioManager: music+SFX, PlayerPrefs volume, auto-wires to order events. Clips are human work |
| 12. Effects/Polish | 🟡 | Skid mark exists in code; particles/post-processing are human work (MANUAL_STEPS F) |
| 13. Testing & Balance | ⬜ | User playtest (MANUAL_STEPS Section C scenario) |
| 14. Optimization | ✅ hooks | ObjectPool ready; static/LOD/occlusion steps in MANUAL_STEPS F4 |
| 15. Build & Release | ⬜ | Steps in MANUAL_STEPS F5-F6 |

## Decisions / Assumptions Made (noted to avoid stalling)

1. **No GDD.md** — the GDD summary in the root CLAUDE.md was used as the basis. If the full GDD arrives, put it in `docs/GDD.md`, I'll fix any inconsistencies.
2. **Legacy Input** — the existing (tested) VehicleController uses Legacy; all new code follows suit. Player Settings should be "Both" (MANUAL_STEPS A2).
3. **Flat `DeliverySim` namespace** — a sub-namespace was requested but all existing code is flat; the project's CLAUDE.md requires consistency.
4. **Did not revert to WheelCollider** — the existing raycast-suspension controller is tested and working; rewriting it was risky. Kept the existing RPM/gear simulation instead of a torque curve.
5. **VehicleData SO is economy metadata, not physics** — the controller deliberately doesn't read the SO (noted in code).
6. **Order time starts AT PICKUP** (the delivery leg is timed — matches the GDD's "deliver on time" phrasing).
7. **Interact key is F** — E is used for gear shifting (VehicleController).
8. **UI uses legacy Text** — doesn't require a TMP import, works out of the box. Switch to TMP in the polish phase.
9. **EconomyManager kept its float balance + SpendMoney**; a `TrySpendMoney` alias was added.
10. **Vehicle switching (garage) is out of MVP scope** — purchase + ownership record exist, spawn/switch doesn't.

## System Map (file → responsibility)

- `_Managers/`: GameManager, EconomyManager, ReputationManager, ShopManager, AudioManager
- `_Orders/`: OrderManager (+DeliveryResult), InteractionPoint, PickupPoint, DeliveryPoint
- `_Core/`: Waypoint, RouteManager, FuelStation, RepairStation, NotificationService, ObjectPool
- `_Vehicles/`: VehicleController (+multipliers), VehicleCameraController, VehicleFuel, VehicleCondition, VehicleInteractor, VehicleUpgradeApplier, VehicleCameraRig, SmoothMouseLook, CameraModeController
- `_Data/`: OrderData, VehicleData, VehicleUpgradeData, CameraSettings, ReputationTier
- `_Save/`: SaveSystem (v2, with migration)
- `_UI/`: UIFactory, UIBootstrap, HUDController, OrderPanelController, ShopPanelController, PauseMenuController, MainMenuController, InteractionPromptUI, NotificationUI
- `Editor/`: DeliverySimSetup (4 setup MenuItems)

## Fix Log (2026-07-20 — "vehicle control broke" report)

Diagnosis: vehicle settings (grip/torque/suspension) hadn't been touched; there were two new interaction problems:
1. **Tab conflict:** OrderPanelController and CameraModeController both used Tab — opening the panel switched the camera to first-person. Fix: the order panel was moved to the **O** key.
2. **Rigidbody Interpolation:** the Setup 2 command had changed None→Interpolate; the mass=1 sensitive custom physics became unstable with this. Fix: Setup no longer touches interpolation + the new **Setup 5** menu command reverts to the old setting (None).
3. **New:** `VehicleReset` (R key) — resets the vehicle in place after a rollover. Setup 2 and 5 add it automatically.
4. **ROOT CAUSE OF THE ROLLOVER (user's diagnosis confirmed):** Point triggers are 5m-radius spheres; with default settings the suspension raycast was also hitting triggers → entering the area, the wheel ray mistook the invisible sphere shell for ground and launched the vehicle. Fix: added `QueryTriggerInteraction.Ignore` to the suspension raycast (VehicleController.FixedUpdate).

Old scripts (`_CarScripts/Car.cs`, `Camera.cs`, `ui.cs`) were checked: they're only on passive objects, no effect on the active vehicle.

## Fix Log 2 (2026-07-20 — rollover root-cause fix + realistic driving)

Deep diagnosis (git history + scene YAML + code analysis):
- Values hadn't changed during the session (`git diff` clean). The actual problem: the active vehicle "PlayerVeichle Car" was running with **1 kg mass**, assists off, reduced grip/suspension values — rollover inertia ~0.4 kg·m², every corner force was enough to flip it. (The passive old "PlayerVehicle": 1500 kg + assists on.)
- Code gaps: lateral grip force was applied at the ground contact point (maximum tipping moment), there was no anti-roll bar, the physics was reading `transform` (with Interpolation on, an intermediate pose was being read in FixedUpdate, corrupting the suspension).

Solution applied:
1. `VehicleController.FixedUpdate` is now entirely `rb.position/rb.rotation`-based — **Interpolation can now stay ON** (smooth camera, correct physics). The Setup 5 command was reversed: it now guarantees Interpolation is ON.
2. New mechanic: **anti-roll bar** (per axle, `antiRollStiffness`, default 0=off) + **roll-center** (`lateralForceHeight`, lateral force is applied toward the CoM height, default 0.6).
3. **Setup 6 - Apply Realistic Vehicle Tuning**: 1200 kg + a derived consistent set (suspensionForce 40000, clamp 15000, damp 4, gripX 8 / gripZ 42, engineTorque 1200, wheel 20 kg, turnAngle 30, CoM -0.3, antiRoll 12000, assists on). Undo-supported; old values are logged to Console before being applied.
4. Side benefit: with realistic mass, `VehicleCondition`'s damage threshold (impulse 300) actually works now (it never triggered at 1 kg).

## Fix Log 3 (2026-07-20 — pro controls, camera feel, point physics, distance-based time)

1. **Key layout moved to the driving-game standard:** E=interact (instead of F; the scene value is updated via Setup 7), Tab=orders, C=camera mode, LShift/LCtrl=gear (instead of E/Q), Space/R/B/Esc unchanged. All keys are now serialized fields (changeable from the Inspector).
2. **Camera feel:** Main finding — OrbitalFollow was in "World Space" binding, the camera wasn't following the vehicle's turning. Setup 7: binding → LockToTargetWithWorldUp (rotates with a yaw-smoothed rig), PositionDamping (0.3, 0.8, 0.3), RotationComposer (0.4), rig yaw 6. Auto-recentering added to `SmoothMouseLook` (1.2 sec idle → glides back behind the vehicle, can be disabled).
3. **Camera transitions:** Brain DefaultBlend = EaseInOut 0.8 sec (instead of the loose 2 sec default). A micro-jitter filter for first-person (HardLock 0.08 / RotateWith 0.15 damping).
4. **Point physics + visibility (Setup 8):** A solid Kiosk cube on every point (can't drive through — a visible real obstacle, unrelated to the trigger-launch bug), an always-visible colored marker (green=pickup, orange=delivery; `permanentBeacon` field, never hidden), a glowing ground ring on the active target (`markerVisual`). Station cubes became solid too. URP Lit materials are generated as assets under `Art/Materials/`.
5. **Distance-based time:** OrderManager's `GetEstimatedTimeLimit` — pickup→delivery distance × routeFactor(1.4) / average speed(40 km/h) + buffer(20 sec), min 45 sec. Calculated on accept (`activeTimeLimit`), the counter/failure/score use this value; the offer card shows the estimate. OrderData.timeLimitSeconds remains as a fallback.

## For the Next Session

- The user will apply MANUAL_STEPS B+C and report the test result.
- If a compile error comes up, the first error line in Console is enough.
- TMP migration, garage/vehicle switching, engine sound pitch binding, localization — on request.
