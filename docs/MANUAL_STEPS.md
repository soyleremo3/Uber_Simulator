# MANUAL_STEPS — Unity Editor Setup Guide (VERY DETAILED, CLICK BY CLICK)

This guide explains everything step by step: what to click, what you'll see on screen,
and how to know you did it right. Go through it top to bottom in order, don't skip any step.
The **✓ CHECK** line at the end of each step tells you how to know that step succeeded.

**Panel locations (reminder):**
- **Hierarchy** = usually on the LEFT, the list of objects in the scene.
- **Scene** = the 3D view in the middle.
- **Inspector** = on the RIGHT, properties of the selected object.
- **Project** = at the BOTTOM, the project's files.
- **Console** = at the BOTTOM (tab next to Project). If not visible: top menu **Window → General → Console**.

---

# SECTION A — PRE-CHECK (done once, ~5 minutes)

## STEP A1 — Open the project and check for errors

1. Open Unity Hub.
2. Click **Uber_Simulator** from the projects list, wait for the project to open.
3. Once open, you may see a spinning wheel at the bottom right — this means scripts are compiling. **Don't touch anything until the wheel disappears.**
4. From the top menu, click **Window → General → Console** (the Console panel opens).
5. Look at the three icons at the top right of the Console panel: white speech bubble, yellow triangle, red exclamation mark.
6. Look at the number next to the **red exclamation mark**.

**✓ CHECK:** If the red number is **0**, there's no problem, continue.
**✗ PROBLEM:** If the red number is not 0 → click the red line once → copy the whole line → paste it to me in chat. Don't continue until I fix it.

## STEP A2 — Check the Input setting (VERY IMPORTANT)

All of the game's key controls were written with the "old input system". If Unity is in the wrong mode, the game will throw an error the moment it opens. Let's check:

1. From the top menu, click **Edit → Project Settings...**. A new window opens.
2. From this window's LEFT list, click **Player**.
3. On the right side, find the section labeled **Other Settings**, click it to expand it (if collapsed).
4. Scroll down, find the **Configuration** heading.
5. Inside it there's a line called **Active Input Handling**. Look at the value next to it.

There are three possibilities:
- If it says **"Input Manager (Old)"** → leave it, this is fine. Close the window.
- If it says **"Both"** → leave it, this is also fine. Close the window.
- If it says **"Input System Package (New)"** → click it, select **Both** from the dropdown that opens. Unity will ask "the editor will restart" → click **Apply/Yes**. Unity restarts itself (1-2 min). Once it reopens, come back to this section.

**✓ CHECK:** Active Input Handling = "Input Manager (Old)" or "Both".

## STEP A3 — Check if the Cinemachine package is installed

1. From the top menu, click **Window → Package Management → Package Manager** (in older versions, directly Window → Package Manager).
2. There's a dropdown at the top left of the window that opens; make sure **In Project** is selected.
3. Look for **Cinemachine** in the list on the left. The version should be **3.x** (e.g. 3.1.7).

**✓ CHECK:** Cinemachine 3.x appears in the list. (It should already be installed in your project — I saw it in the manifest. If not: select **Unity Registry** from the top left of the window, type "Cinemachine" in the search box, select it, click **Install** at the bottom right.)

4. Close the window.

---

# SECTION B — SCENE SETUP (~10 minutes, order matters)

## STEP B1 — Open the correct scene

1. In the **Project** panel at the bottom, go to: **Assets → _Uber Simulator → Scenes**.
   (Navigate by double-clicking folders, or use the folder tree on the left.)
2. DOUBLE-click the **MainScene** file.
3. If it asks "Save current scene?" click **Save**.

**✓ CHECK:** The very top title bar of the Unity window says "MainScene" and the Hierarchy panel shows **MainScene** at the top.

## STEP B2 — Check whether there's ground

The sample delivery points will be placed in the scene between -60 and +90 meters. There must be ground under the vehicle and across that area.

1. In the Scene view, look at where the vehicle is sitting. Is the vehicle on a ground/road surface?
2. If the ground is large (at least 100 meters in every direction around the car) → skip this step, go to B3.
3. If there's no ground or it's small:
   a. Right-click an EMPTY spot in the Hierarchy panel.
   b. Click **3D Object → Plane** from the menu. A flat "Plane" ground object is added to the scene.
   c. With the Plane selected (blue in Hierarchy), look at the Inspector on the right.
   d. At the top there's a **Transform** section. Type the following by hand:
      - **Position**: X = `0`, Y = `0`, Z = `0`
      - **Scale**: X = `30`, Y = `1`, Z = `30`
   (Click the box, delete the value, type the new one, press Enter.)

**✓ CHECK:** In the Scene, there's the vehicle and a large ground area around it.

## STEP B3 — Create the managers (ONE CLICK) ⚠️ THE MOST CRITICAL STEP

1. Look at Unity's TOP menu bar (File, Edit, Assets, GameObject...). You'll see a NEW menu there called **DeliverySim**.
   - **Can't see it?** Scripts haven't compiled yet. Wait 10 seconds; if it's still not there, there's a red error in Console → go back to A1.
2. Click **DeliverySim → Setup → 1 - Create Managers**.
3. Look at the Hierarchy panel.

**✓ CHECK:** The following THREE new objects appeared in the Hierarchy:
- `_Managers`
- `_Gameplay`
- `_UI`

For extra verification: click `_Managers` → in the Inspector you should see these 6 components: **Game Manager, Economy Manager, Save System, Reputation Manager, Shop Manager, Audio Manager**. Under `_Gameplay`: **Order Manager, Route Manager**. Under `_UI`: **UI Bootstrap**.

## STEP B4 — Add components to the vehicle (ONE CLICK)

1. From the top menu, click **DeliverySim → Setup → 2 - Setup Player Vehicle Components**.
2. Look at Console (the panel at the bottom).

**✓ CHECK:** You'll see a line in Console like this:
`[Setup] '...' vehicle components complete (fuel, damage, interaction, upgrade).`
Also, in the Hierarchy the vehicle will be automatically selected and you'll see these components added in the Inspector: **Vehicle Fuel, Vehicle Condition, Vehicle Interactor, Vehicle Upgrade Applier**.

**✗ PROBLEM:** If Console says `VehicleController not found in scene` → there's no car in the scene or the car doesn't have a VehicleController component. Put your car in the scene, then redo this step.

## STEP B5 — Set up the follow camera (ONE CLICK)

**IMPORTANT PRE-NOTE:** If your scene ALREADY has a working Cinemachine follow camera (from a previous session), SKIP this step and go to B6. Two follow cameras will fight each other.

If you're not sure, check: type `CM` in the search box above the Hierarchy. If an object starting with "CM" or containing "CinemachineCamera" shows up, you already have a camera → skip. If not, continue:

1. From the top menu, click **DeliverySim → Setup → 3 - Create Follow Camera (Cinemachine)**.

**✓ CHECK:** `CameraRig` and `CM_FollowCamera` objects appeared in the Hierarchy. Console shows `[Setup] Cinemachine follow camera ready`.

## STEP B6 — Generate sample orders and points (ONE CLICK)

1. From the top menu, click **DeliverySim → Setup → 4 - Create Sample Orders + Points**.

**✓ CHECK (three things):**
1. In the Hierarchy there are these 7 new objects: `Pickup_Restaurant`, `Pickup_Depot`, `Delivery_HouseA`, `Delivery_HouseB`, `Delivery_Office`, `FuelStation_Main`, `RepairStation_Main`.
2. In the Project panel, an **Assets → _Uber Simulator → _Data → Orders** folder was created, containing 3 files: `order_food_a`, `order_package_a`, `order_fragile_a`.
3. Console shows: `[Setup] OrderManager.orderPool filled with 3 sample orders.`

**NOTE:** In the Scene view you'll see these points as colored wireframe spheres (green = pickup, orange = delivery). If a point ended up outside the ground/inside a wall: click the object in Hierarchy → drag it to a suitable spot with the (Move) tool in the Scene. The system works by ID, so it's fine to move the position.

## STEP B7 — Save the scene

1. Press **Ctrl+S** on the keyboard.

**✓ CHECK:** The `*` mark next to the MainScene name in the Unity title bar disappeared.

---

# SECTION C — FIRST GAMEPLAY TEST (step-by-step scenario)

> **UPDATE 2 (rollover / realistic driving fix):** The root of the rollover issue was found:
> the active vehicle was running with a 1 kg mass (!) and there was no mechanic in the code to prevent tipping.
> The code is now independent of interpolation (**Interpolation stays ON** for smooth camera —
> the old "run Setup 5 to set to None" instruction is now INVALID, that command now guarantees the opposite, that interpolation stays on)
> and an anti-roll bar + roll-center mechanic was added. The ONLY thing you need to do:
>
> 1. Wait for Unity to finish compiling.
> 2. Top menu → **DeliverySim → Setup → 6 - Apply Realistic Vehicle Tuning** → click.
>    (Console logs the OLD values first — if you don't like it, Ctrl+Z or enter those values back by hand.)
> 3. Save the scene with **Ctrl+S** → Play → test it.
>
> **UPDATE 3 (pro controls + camera + points):** The key layout was moved to the driving-game standard,
> the camera feel/transitions were made more professional, permanent markers + solid kiosks were added to points,
> and delivery time is now calculated from real distance. After compiling, TWO ONE-TIME clicks:
>
> 1. **DeliverySim → Setup → 7 - Pro Controls + Camera Feel** (makes the interact key E,
>    puts the camera in a pro mode that follows the vehicle's turning, makes the mode-switch blend 0.8 sec).
> 2. **DeliverySim → Setup → 8 - Upgrade Point Visuals** (always-visible colored
>    markers on all points — green=pickup, orange=delivery — + a glowing ground ring on the active target +
>    a solid kiosk you can't walk through; station cubes also become solid).
> 3. **Ctrl+S**.
>
> **CURRENT KEY LAYOUT (driving-game standard):**
>
> | Key | Function |
> |---|---|
> | **WASD** | Throttle / brake / steering |
> | **Space** | Handbrake |
> | **E** | Interact (pick up/drop off cargo, fuel, repair) |
> | **Tab** | Phone / Order panel |
> | **C** | Camera mode (1st/3rd person) |
> | **B** | Shop |
> | **R** | Reset vehicle (after a rollover) |
> | **LShift / LCtrl** | Gear up / down (manual override; automatic is already active) |
> | **Esc** | Pause |
> | **Mouse** | Look around the camera — after releasing, it auto-recenters behind the vehicle after ~1 sec |
>
> **Driving feel tuning guide** (Inspector → PlayerVeichle Car → Vehicle Controller):
>
> | What you want to change | Which field | Direction |
> |---|---|---|
> | Rollover resistance | `Anti Roll Stiffness`, `Lateral Force Height`, `Center Of Mass Offset.y` | Increase / increase / more negative = more stable |
> | Suspension stiffness | `Suspension Force` | Increase = stiffer |
> | Bounce/wobble damping | `Damp Amount` | Increase = settles faster |
> | Acceleration power | `Engine Torque` per wheel | Increase = stronger |
> | Cornering grip | `Wheel Grip X` | Increase = more grip (too much forces a rollover) |
> | Steering angle | `Turn Angle` on the front wheels | Decrease = more stable at high speed |
> | Vehicle weight feel | Rigidbody `Mass` | 1200 = sedan; 2000+ for a larger vehicle |

Now let's play the game to verify the core loop.

## STEP C1 — Start the game

1. Click the **▶ (Play)** button at the top center of the screen.
2. Wait 1-2 seconds.

**✓ CHECK:** The following appear automatically on screen:
- A semi-transparent black panel at the BOTTOM LEFT: speed, money (₺ 100), fuel, status, ★ rating, "No order".
- A help line at the very BOTTOM: `WASD: Drive | Space: Handbrake | E: Interact | Tab: Orders | B: Shop | C: Camera | R: Reset Vehicle | LShift/LCtrl: Gear | Esc: Pause`

**✗ PROBLEM:** If this text is NOT there → stop Play, select the `_UI` object in Hierarchy, verify that the **Build On Start** checkbox on the **UI Bootstrap** component is CHECKED in the Inspector.

## STEP C2 — Accept an order

1. Press **Tab** on the keyboard. (Camera mode is now on the **C** key.)
2. An "ORDERS" panel opens on the right of the screen. Inside it are 3 order cards (name, fee, time + Accept/Reject buttons).
3. Click the **Accept** button on the first order with the MOUSE.

**✓ CHECK:**
- A green notification at the top of the screen: "Order accepted: ... Head to the pickup point!"
- A WHITE CYLINDER (marker) hovering in the air appeared at a point in the scene.
- A BLUE LINE (GPS route) from the vehicle to that point appears on the ground.
- The bottom-left panel says "Waiting for pickup: ...".

**NOTE:** The vehicle controls keep working while the panel is open. To close the panel, press **Tab** again.

## STEP C3 — Pick up the cargo

1. Drive the car with **W A S D**, follow the blue line, go to the point with the white cylinder.
2. When you get within ~5 meters of the point, YELLOW text appears at the bottom center of the screen: **"Pick Up Cargo [E]"**.
3. Stop and press **E**. (The kiosk cube in the middle is solid — you can't drive through it, park next to it.)

**✓ CHECK:**
- Notification: "Cargo picked up! You have ... seconds for the delivery."
- A countdown started at the TOP CENTER of the screen: something like `Time: 02:30`.
- The marker disappeared from the old point, appeared at a NEW point (the delivery point).
- The blue line now goes to the delivery point.

## STEP C4 — Deliver

1. Follow the blue line to the delivery point.
2. As you approach, the **"Deliver [E]"** text appears → press **E**.

**✓ CHECK:**
- Notification: "Delivery complete! +35 money, 5.0 stars." (less if you were late).
- Money increased at the bottom left (₺ 100 → ₺ 135 or similar).
- The ★ line updated.
- The time counter disappeared, back to the "No order" text.

**IF YOU'VE REACHED THIS POINT, THE CORE LOOP IS WORKING — the game's first playable prototype is complete. 🎉**

## STEP C5 — Try the fuel station (optional)

1. Drive to the `FuelStation_Main` cube with the yellow gizmo in the scene (coordinates ~ x=12, z=-25).
2. As you approach, **"Refuel [E] (3.0/liter)"** appears → press **E**.

**✓ CHECK:** A "X liters of fuel added (-Y money)" notification + fuel increased at the bottom left, money decreased. (If the tank is full, it says "Tank is already full." — normal.)

## STEP C6 — Open the shop

1. Press **B**.

**✓ CHECK:** A "SHOP" panel opens in the center; Engine / Fuel Tank / Durability rows are visible.
**NOTE:** It's normal for the rows to say "MAX" — we haven't produced the upgrade assets yet (we'll do this in Section D2). Press **B** again to close.

## STEP C7 — Pause and save

1. Press **Esc** → a "PAUSED" screen appears, the game freezes.
2. Click the **Save** button → a "Game saved." notification appears.
3. Click **Resume** → the game continues from where it left off.
4. To end the test, click the **▶ Play** button at the top again (exits play mode).

**✓ CHECK:** There's a `[SaveSystem] Game saved: ...` line in Console.

**⚠ WARNING:** Any change you make in the scene while in Play mode does not persist. If you want to move objects, exit Play mode first.

---

# SECTION D — CONTENT PRODUCTION (after the game works, at your own pace)

## STEP D1 — Fill the shop: produce upgrade assets

The reason the shop says "MAX" is that the catalog is empty. Let's fill it. As an example, let's make Engine Level 1 together:

1. In the Project panel, go to the **Assets → _Uber Simulator → _Data** folder.
2. Right-click an EMPTY spot in the folder → **Create → DeliverySim → Vehicle Upgrade Data**.
3. A new file is created, its name is editable → type `Upgrade_Engine_1`, press Enter.
4. With the file selected, set the following in the Inspector:
   - **Category**: `Engine`
   - **Display Name**: `Engine Upgrade I`
   - **Level**: `1`
   - **Cost**: `500`
   - **Effect Multiplier**: `1.15` (= 15% more powerful engine)
5. Produce as many as you like the same way. Recommended starting set:

   | File name | Category | Level | Cost | Effect Multiplier |
   |---|---|---|---|---|
   | Upgrade_Engine_1 | Engine | 1 | 500 | 1.15 |
   | Upgrade_Engine_2 | Engine | 2 | 1200 | 1.3 |
   | Upgrade_FuelTank_1 | FuelTank | 1 | 400 | 1.25 |
   | Upgrade_FuelTank_2 | FuelTank | 2 | 900 | 1.5 |
   | Upgrade_Durability_1 | Durability | 1 | 450 | 1.3 |
   | Upgrade_Durability_2 | Durability | 2 | 1000 | 1.6 |

6. Now let's link these to the shop:
   a. In Hierarchy, click the `_Managers` object.
   b. Find the **Shop Manager** component in the Inspector.
   c. Open the list with the arrow to the left of the **Upgrade Catalog** row.
   d. Press the **+** button at the bottom of the list 6 times (opens 6 empty rows).
   e. Drag each Upgrade file from the Project panel into a row.
      (Alternative: click the small ⊙ icon on the right of each row, select from the list that opens.)
7. Save the scene with **Ctrl+S**.

**✓ CHECK:** Press Play → **B** → you should now see "Engine — Level 0 / Next: Engine Upgrade I (₺500)" and a **Buy** button. If you have enough money, buy it → level goes up, money goes down.

## STEP D2 — Adding a new order

1. In Project, go to **Assets → _Uber Simulator → _Data → Orders**.
2. Right-click → **Create → DeliverySim → Order Data** → give it a name (e.g. `order_food_b`).
3. Fill it out in the Inspector:
   - **Order Id**: `order_food_b` (must be unique)
   - **Order Name**: the name the player will see (e.g. `Pizza Delivery`)
   - **Pickup Point Id**: the ID of ONE pickup point in the scene (e.g. `pickup_restaurant`)
   - **Delivery Point Id**: a delivery point ID (e.g. `delivery_house_b`)
   - **Payment Amount**: the fee (e.g. `45`)
   - **Time Limit Seconds**: seconds (e.g. `120`)
   - **Cargo Type**: Food / Package / Fragile
4. In Hierarchy, select `_Gameplay` → in the Inspector, **Order Manager** → add a row to the **Order Pool** list with **+** → drag in the new asset.
5. **Ctrl+S**.

**Existing point IDs (produced by Setup 4):**
`pickup_restaurant`, `pickup_depot` (pickup) — `delivery_house_a`, `delivery_house_b`, `delivery_office` (delivery).

## STEP D3 — Adding a new pickup/delivery point

1. Right-click in Hierarchy → **Create Empty** → give it a name (e.g. `Delivery_Market`).
2. Move the object to the desired position in the Scene (Move tool).
3. In the Inspector, **Add Component** → type `DeliveryPoint` in the search box → select it. (For a pickup point, use `PickupPoint`.)
4. Type a unique ID into the **Point Id** field (e.g. `delivery_market`).
5. **Add Component** → `Sphere Collider` → add it. In the Inspector:
   - **Is Trigger**: CHECK ✓
   - **Radius**: `5`
6. (Optional marker) Right-click the object → **3D Object → Cylinder** → becomes a child. Position Y=`6`, Scale (`1.5`, `6`, `1.5`). Remove the Cylinder's **Capsule Collider** (right-click the component header → Remove Component). Select the Cylinder and UNCHECK the activation checkbox at the very TOP LEFT of the Inspector (the marker stays hidden by default). Then select the main point, drag this Cylinder into the **Marker Visual** field.
7. You can now use the `delivery_market` ID in orders (D2).

## STEP D4 — Snapping the GPS line to the roads (optional)

Right now the route is a straight line. If you want to draw a road network:

1. Right-click in Hierarchy → **Create Empty** → name it `WP_01`. Move it to a corner of the road.
2. **Add Component** → `Waypoint`.
3. Create WP_02, WP_03... the same way along the road.
4. Select each waypoint → in the Inspector, press **+** on the **Neighbors** list → drag the neighboring waypoint from the Hierarchy. (You only need to connect in one direction — the system counts both ways.)
5. Yellow lines in the Scene show the connections. In Play, the route now follows this network.

## STEP D5 — Tuning the camera feel

**If you're using Cinemachine (you did B5):**
1. In Hierarchy, select `CM_FollowCamera`.
2. In the Inspector, play with the **Follow Offset** values on the **Cinemachine Follow** component (Y = height, Z = distance; Z should be negative, e.g. -7.5).
3. For smoothness, increase/decrease the **Tracker Settings → Position Damping** values on the same component.

**If you want to use the code camera instead (alternative):**
1. Right-click in Project → **Create → DeliverySim → Camera Settings** → an asset is created; all parameters (smoothing, FOV, wall protection, dead zone) are documented in the Inspector.
2. In Hierarchy, select **Main Camera** → **Add Component** → `VehicleCameraController`.
3. Drag the asset you just created into the **Settings** field, drag your vehicle into the **Target** field.
4. Select the `CM_FollowCamera` object → turn OFF the activation checkbox at the top of the Inspector. Also turn off the **Cinemachine Brain** component on Main Camera (the checkbox left of the component name).
   (Don't have both camera systems ON at the same time.)

---

# SECTION E — MAIN MENU SCENE (optional, post-MVP)

1. **File → New Scene** → **Basic (Built-in)** / empty template → **Create**.
2. **Ctrl+S** → location: `Assets/_Uber Simulator/Scenes` → name: `MainMenu` → Save.
3. Right-click in Hierarchy → **UI → Canvas**.
4. Right-click the Canvas → **UI → Legacy → Button** → repeat 3 times (3 buttons).
5. Line up the buttons vertically (switch to 2D mode in the Scene and drag). Click each button's inner **Text** child, type in the Inspector's Text field, in order: `New Game`, `Continue`, `Quit`.
6. Right-click in Hierarchy → **Create Empty** → name it `MenuController` → **Add Component** → `MainMenuController`.
7. In the Inspector, set **Gameplay Scene Name** = `MainScene`.
8. Wire up each button:
   a. Select the button → in the Inspector, **Button** component → click the **+** in the **On Click ()** box.
   b. Drag the `MenuController` object from Hierarchy into the empty slot.
   c. Click the "No Function" dropdown on the right → **MainMenuController** → select, in order: `StartNewGame ()` / `ContinueGame ()` / `QuitGame ()`.
9. This scene needs managers too: top menu **DeliverySim → Setup → 1 - Create Managers** (the `_UI` object with UI Bootstrap is unnecessary in the menu scene — you can delete the `_UI` object).
10. **File → Build Profiles** (previously called Build Settings) → **Scene List** → use **Add Open Scenes** to first add MainMenu. Then open MainScene and do the same. MainMenu should be at the TOP (index 0) in the list — if it isn't, drag to reorder.

**✓ CHECK:** Play in the MainMenu scene → "New Game" → MainScene loads and the game starts.

---

# SECTION F — LATER PHASES (short roadmap)

- **Sound:** `_Managers` → **Audio Manager** has 7 empty clip fields in the Inspector (music, accepted, pickup, delivery, failed, money, error). Drop sound files into the Project, drag them into the fields. Done — the code wiring is automatic.
- **Real UI:** The day you build your own Canvas: `_UI` → **UI Bootstrap** → uncheck **Build On Start**. Add the controller scripts (HUDController etc.) to your own panels and wire up the Text references from the Inspector. If you want, that day tell me "switch to TMP" — I'll change the code.
- **Performance:** Select static environment objects → check **Static** at the top right of the Inspector. On a large map: **Window → Rendering → Occlusion Culling → Bake**.
- **Build:** **Edit → Project Settings → Player** (name/icon/version) → **File → Build Profiles → Windows → Build**. Save file location: `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\deliverysim_save.json`.
- **Steam:** partner.steamgames.com account ($100 application fee, approval takes days) → page materials are in ASSET_NEEDS.md. When you want Steamworks integration, I'll write the code side.

---

# TROUBLESHOOTING (quick reference)

| Symptom | Likely cause | Fix |
|---|---|---|
| Camera changes when the order panel opens | Keys used to conflict in an older version | Fixed: orders are on **Tab**, camera on **C**. Make sure you ran Setup 7 |
| Pressing E doesn't pick up cargo | The scene's interact key is still F | Run **DeliverySim → Setup → 7** (sets interactKey to E) → Ctrl+S |
| The camera doesn't move behind the vehicle when it turns | Orbital binding still World Space | Run **DeliverySim → Setup → 7** → Ctrl+S |
| Points aren't visible / disappear once picked up | Setup 8 hasn't been run | Run **DeliverySim → Setup → 8** → Ctrl+S |
| Vehicle flipped, stuck upside down | Normal crash | The **R** key resets the vehicle in place (you need to have run Setup 5 or Setup 2 once) |
| Vehicle flies/flips ENTERING an order area | The suspension raycast was hitting the invisible trigger sphere | Fixed in code (triggers are now ignored) — scripts just need to compile, no extra step |
| Vehicle rolls over in corners | Realistic profile not applied | Run **DeliverySim → Setup → 6** → Ctrl+S. If it still rolls, increase `Anti Roll Stiffness` |
| Camera is shaky/jittery | Rigidbody Interpolation left off | Run **DeliverySim → Setup → 5** (turns Interpolation ON) → Ctrl+S |
| No DeliverySim menu | Compiling not finished or there's an error | Send me the red line from Console |
| UI doesn't show up in Play | UIBootstrap disabled or `_UI` missing | Rerun B3, check whether Build On Start is checked |
| "Pick Up Cargo [F]" never appears | No trigger collider on the point / no VehicleInteractor on the vehicle | Rerun B4 and B6 (safe, doesn't create duplicates) |
| No offers in the Tab panel | OrderManager's pool is empty | Run B6; check whether `_Gameplay` → Order Manager → Order Pool is filled |
| "Points missing in scene" when clicking Accept | The OrderData's ID doesn't match the scene's Point Id | Compare the IDs (must match exactly, including case) |
| Camera jitters | Rigidbody Interpolate off | Rerun B4 (turns it on automatically) |
| Camera keeps spinning / two cameras fighting | Two camera systems active at once | Last item in D5: turn one off |
| InvalidOperationException on key press | Active Input Handling is "New only" | Apply A2 |
| Money never increases | EconomyManager isn't in the scene | Rerun B3 |
