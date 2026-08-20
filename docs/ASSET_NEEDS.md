# ASSET_NEEDS — Required Asset List

The code is written assuming placeholders; everything here will be replaced with a real asset later.
Priority: **P0 = required for MVP**, **P1 = first phase after MVP**, **P2 = polish/late phase**.

## 3D Models

| Priority | Asset | Note |
|---|---|---|
| P0 | 1 low-poly car | A placeholder cube/simple model is enough — there's already a vehicle in the scene |
| P0 | Wheel model (prefab) | Assigned to VehicleController's `wheelPrefab` field; pivot must be centered |
| P1 | A modular city kit instead of the blockout | Buildings, roads, sidewalks, street lamps |
| P1 | 2-3 additional vehicles (motorcycle, van) | For the shop phase |
| P2 | Decor (tree, bench, trash can) | |

## UI / 2D

| Priority | Asset | Note |
|---|---|---|
| P0 | Money icon | HUD + shop |
| P0 | Order type icons (Food / Package / Fragile) | Corresponds to the CargoType enum |
| P0 | Star/rating icon | Score screen |
| P1 | Phone UI frame | Order screen visual |

## Sound / Music

All of these will be dragged onto the **AudioManager**'s Inspector fields on the `_Managers` object (fields are ready, the game runs silently while they're empty).

| Priority | Asset | AudioManager field |
|---|---|---|
| P1 | Order accepted SFX | Order Accepted Clip |
| P1 | Cargo picked up SFX | Cargo Picked Up Clip |
| P1 | Delivery complete SFX | Order Completed Clip |
| P1 | Order failed SFX | Order Failed Clip |
| P1 | Money earned SFX | Money Earned Clip |
| P1 | Error SFX | Error Clip |
| P2 | Background music (casual loop) | Background Music |
| P2 | Engine sound (loop, RPM pitch) | Separate phase — the `VehicleEngine.GetRPM()` hook is ready |
| P2 | City ambience (loop) | Requires a separate source, will be added on request |

## Effects / Other

| Priority | Asset | Note |
|---|---|---|
| P0 | Skid mark TrailRenderer prefab | VehicleController's `skidMarkPrefab` field — a simple black trail is enough |
| P2 | Particles: dust/smoke, delivery confetti, money effect | |
