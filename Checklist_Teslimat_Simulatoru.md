# Development Checklist — Delivery Driver Simulator
### Step by Step, From Scratch to Finished Game

> This list is designed to be followed in order. Each main heading builds on the previous one. Try not to move to the next section before fully finishing one — especially the "Core Loop" sections (3-6) depend on each other.

---

## 0. Preparation

- [ ] Create the Unity 6.4 project (3D Core template)
- [ ] Set up version control (Git + .gitignore — use a ready-made Unity `.gitignore` template)
- [ ] Create the folder structure:
  - `_Project/Scripts`
  - `_Project/Scripts/Data` (ScriptableObjects)
  - `_Project/Scripts/Managers`
  - `_Project/Scripts/Vehicles`
  - `_Project/Scripts/Orders`
  - `_Project/Scripts/UI`
  - `_Project/Prefabs`
  - `_Project/Art/Models`, `Art/Materials`, `Art/Animations`
  - `_Project/Audio`
- [ ] Decide on a namespace (e.g. `DeliverySim`) and use it in all scripts
- [ ] Install the Input System package (new Input System or Legacy — decide and lock it in)
- [ ] Git initial commit ("Initial project setup")

---

## 1. Architectural Foundations (Code Skeleton)

- [ ] Create the `GameManager` singleton (scene transitions, overall game state)
- [ ] Create `EconomyManager`:
  - [ ] Balance (money) field + `AddMoney()` / `SpendMoney()` methods
  - [ ] Money change event (`OnMoneyChanged`) — for the UI to listen to
- [ ] Define the `OrderData` ScriptableObject:
  - [ ] Pickup point (Transform/Vector3 reference or ID)
  - [ ] Delivery point
  - [ ] Payment amount
  - [ ] Time limit
  - [ ] Cargo type (enum: Food, Package, Fragile, etc.)
- [ ] Define the `IInteractable` interface (can be adapted from your existing Interaction System)
- [ ] An `IUsable` or similar interface (for cargo pickup/drop-off actions)
- [ ] Set up a simple `SaveSystem` skeleton (JSON-based — at least for balance and reputation)

---

## 2. Vehicle Controller (Driving)

- [ ] Import a placeholder vehicle model (cube/simple low-poly car — don't wait for assets)
- [ ] Set up `Rigidbody` + `WheelCollider` (4 wheels)
- [ ] Write the `VehicleController.cs` script:
  - [ ] Throttle/brake input
  - [ ] Steering input
  - [ ] Engine torque curve (adjustable via AnimationCurve)
  - [ ] Handbrake (optional, for drifting)
- [ ] Set up the camera system (3rd person follow with Cinemachine)
- [ ] Test and tune the driving feel on a straight road in the test scene (speed, grip, turning)
- [ ] Add basic damage/balance tracking (optional — if it will affect cargo quality, lay the groundwork now)
- [ ] Create the `VehicleData` ScriptableObject (fields like speed, acceleration, fuel capacity, price) — you can adapt the vehicle profile structure from PRO RACER

---

## 3. World / Map (Blockout Phase)

- [ ] Blockout a small test map (gray boxes for the road network, building volumes)
- [ ] Mark the road network with Unity's NavMesh or a simple waypoint system (needed for the GPS/route line)
- [ ] Decide and mark 5-8 "pickup point" and "delivery point" locations in the scene (empty GameObject + tag/label)
- [ ] Determine the spawn point (the player's starting garage)
- [ ] Add basic environment collisions (road edges, building walls)
- [ ] Set up post-processing / basic lighting (minimal at this stage, just needs to be readable)

---

## 4. Order System (Core Loop — Part 1)

- [ ] Write `OrderManager.cs`:
  - [ ] Active order pool (List/Queue)
  - [ ] Random order generation logic (selecting from the `OrderData` list you have)
  - [ ] Order accept/reject methods
  - [ ] Order time countdown (Coroutine or `Update`-based timer)
- [ ] Mark the pickup point in the scene when an order is accepted (route/icon)
- [ ] Trigger the "pick up cargo" interaction on reaching the pickup point (via `IInteractable`)
- [ ] Mark the delivery point after the cargo is picked up
- [ ] Trigger the "deliver" interaction on reaching the delivery point
- [ ] When a delivery is completed:
  - [ ] Calculate score based on time (on-time / late / very late)
  - [ ] Call `EconomyManager.AddMoney()`
  - [ ] Remove from the order list, generate a new order

> By the end of this section, a minimal "accept one order → go → deliver → earn money" loop should be working. This is your **first playable prototype**.

---

## 5. Scoring & Reputation System

- [ ] Write `ReputationManager.cs`:
  - [ ] Average star/score calculation (average of the last N deliveries or cumulative)
  - [ ] Reputation level thresholds (e.g. Bronze/Silver/Gold/Diamond)
  - [ ] Fire an event when the level changes (`OnReputationLevelChanged`)
- [ ] Filter the order pool based on reputation level (higher levels unlock better-paying orders)
- [ ] Define the negative consequence of a low score (fewer orders, lower payment multiplier, etc.)

---

## 6. Economy & Expense System

- [ ] Add the fuel system:
  - [ ] Fuel level field + depletion logic while driving
  - [ ] Fuel station interaction (refill for money)
- [ ] Maintenance/repair cost system (the vehicle should lose value as it takes damage, a fee is paid at the repair point)
- [ ] Do a simple test of the income/expense balance (produce an estimate table on paper or in Excel — average earnings per delivery vs. fuel/repair cost)

---

## 7. Shop & Upgrade System

- [ ] Write `ShopManager.cs` (purchase/upgrade logic, balance check)
- [ ] Define the `VehicleUpgradeData` ScriptableObject (upgrade type, cost, effect amount)
- [ ] Code at least 3 basic upgrade categories: Engine, Fuel Tank, Durability
- [ ] Set up the new vehicle purchase flow (vehicle list → buy → becomes selectable in the garage)
- [ ] Cosmetic system (optional in the first phase, can be left for later): color/livery change

---

## 8. User Interface (UI)

- [ ] Set up the UI Canvas structure (Screen Space - Overlay or Camera, based on the project's needs)
- [ ] **Phone/Order Screen:**
  - [ ] Active order cards (in a list)
  - [ ] Accept/Reject buttons
  - [ ] Earnings history panel
- [ ] **HUD (while driving):**
  - [ ] Speedometer
  - [ ] Route/distance indicator
  - [ ] Remaining time (order timer)
  - [ ] Cargo status icon
- [ ] **Shop Screen:**
  - [ ] Vehicle/upgrade/cosmetic tabs
  - [ ] Balance indicator (bound to EconomyManager)
- [ ] **Reputation Panel:**
  - [ ] Star/score display
  - [ ] Level progress bar
- [ ] Main Menu / Settings / Pause menu
- [ ] Wire the UI to all manager events (event-driven updates, not checked every frame)

---

## 9. Asset Production / Sourcing

> Doing this stage after the blockout is complete and the systems are working (moving from placeholders to real assets) avoids wasted time.

- [ ] Vehicle models:
  - [ ] Decide on a source (Asset Store / your own modeling / AI-assisted 3D generation)
  - [ ] Source models for at least 3-4 vehicle types (bike, motorcycle, car, truck)
  - [ ] Set up correct collider/wheel pivot points on the vehicles
- [ ] Environment/city assets:
  - [ ] Building modules (prefer a modular kit — for reusability)
  - [ ] Modular pieces like road/sidewalk/street lamp
  - [ ] Nature/decor elements (tree, bench, trash can, etc.)
- [ ] Character model (skip this step if the player isn't visible, needed if there's a 3rd-person view)
- [ ] UI icons (order types, money icon, star icon, etc.)
- [ ] Sound/music assets (see Section 11)

---

## 10. Animations

- [ ] Vehicle animations:
  - [ ] Wheel rotation (code-based, automatic from WheelCollider rotation)
  - [ ] Door open/close (at the moment of picking up a delivery, if applicable)
  - [ ] Suspension/bounce effect (optional, cosmetic)
- [ ] Character animations (if there's a 3rd-person view):
  - [ ] Walk/run
  - [ ] Carrying-cargo pose
  - [ ] Delivery/interaction animation (knocking on the door, dropping off the package, etc.)
- [ ] Animator Controller setup and state machine design
- [ ] UI animations (button transitions, panel open/close — simple tweens are enough)

---

## 11. Sound & Music

- [ ] Engine sound (pitch change tied to speed/RPM)
- [ ] Ambient sounds (city ambience, traffic)
- [ ] UI sound effects (order accepted, delivery completed, earning money, error sound)
- [ ] Background music (calm/casual tone, matching the game's overall feel)
- [ ] Write `AudioManager.cs` (centralized sound triggering, volume settings)

---

## 12. Visual Effects & Polish

- [ ] Particle effects: tire tracks/smoke, delivery-completed effect, money-earned effect
- [ ] Camera shake (on collision, optional)
- [ ] UI transition effects (fade in/out, panel animations)
- [ ] Final post-processing tuning (color grading, bloom, ambient occlusion — mindful of performance)
- [ ] Final lighting pass (add day/night transition here, if applicable)

---

## 13. Testing, Balance & Debugging

- [ ] Play through the core loop 20-30 times end-to-end (your own playtesting)
- [ ] Review the economy balance: is the average hourly income reasonable?
- [ ] Test order time limits for difficulty (too easy/too hard?)
- [ ] Performance test on different computers/settings (FPS, load times)
- [ ] Keep a bug tracking list (a simple Trello/Notion table is enough)
- [ ] If possible, have 2-3 outside people test it and collect feedback

---

## 14. Optimization

- [ ] Draw call / batching check (mark static objects as static)
- [ ] Add LOD (Level of Detail) systems (especially for city/building models)
- [ ] Set up Occlusion Culling (especially for a large map)
- [ ] Apply object pooling (for frequently spawned/destroyed objects — particles, orders, etc.)
- [ ] Identify and fix performance bottlenecks with the Profiler

---

## 15. Build & Release Preparation

- [ ] Configure build settings (Player Settings, icon, name, version number)
- [ ] Make a Windows build and test it on a clean machine
- [ ] Create a Steamworks account (start this stage early if you're considering releasing, the process takes time)
- [ ] Steam store page materials: cover art, screenshots, short/long description, trailer (optional but recommended)
- [ ] Achievements / Steam integration (optional)
- [ ] Localization (at least Turkish + English text support should be considered)
- [ ] Final bug sweep and closing critical bugs
- [ ] Pre-release checklist: does the save system work, are settings saved, no crashes

---

## Priority Order Summary (Quick Reminder)

1. **Sections 0-2** → Basic skeleton + driving feel
2. **Sections 3-4** → Blockout map + working order loop (at this point you have something you could call a "game" prototype)
3. **Sections 5-7** → Reputation + economy + shop (the layer that makes the loop meaningful)
4. **Section 8** → Make the UI real
5. **Sections 9-12** → Moving from placeholders to real assets/animations/effects
6. **Sections 13-15** → Testing, optimization, release

> Golden rule: **never** move on to assets/animation/visual polish work **before Section 4 is complete** (i.e. before "accept → go → deliver → earn money" works). Mechanical skeleton first, then flesh it out visually.
