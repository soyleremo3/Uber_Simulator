# TODO.md — deferred / revisit later

Not bugs. Things intentionally left for later, with enough context to pick up.

## Assets

- [ ] **Texture budget re-tune.** Generated station props (`FuelPump`, `RepairLift`)
  currently import at **512** (albedo + normal), unused metallic/roughness at 64.
  Fine now. When the number of AI-generated assets grows, revisit:
  - shared texture atlas / fewer unique textures,
  - per-platform Max Size overrides (PC vs a future mobile port),
  - drop maps the matte materials never sample.
  All of it is non-destructive importer settings. Policy lives in RULES.md rule 6.
- [ ] Station props: no LODs. Add LODGroups (or an impostor) if the scene stays
  render-bound after other fixes.

## Rendering / performance (scene is render-bound: ~6.5k draw calls, ~3.2M tris)

- [ ] **Lightmap bake** — kills the realtime shadow cost for static geometry.
  Deferred until the map geometry is frozen (still being edited).
- [ ] **Occlusion Culling bake** — dense street hides a lot; ~3.1k of ~5k renderers
  visible per frame. Do it with the lightmap bake.
- [ ] **LOD groups** — none in the scene; 3.2M tris have no distance reduction.
- [ ] **Material count / atlasing** — bigger render lever than texture size.
- [ ] ~25 `BoxCollider does not support negative scale` warnings — pre-existing road
  tile proxies (`WalkingStreet/Roads/.../UCX_M02Road01_*`). Convert to convex
  MeshCollider or bake out the negative scale. Collision still works; console noise.

## Map / scene

- [ ] `Map Scene(Güncel).unity` saves in **binary** format — git diffs are unreadable,
  not VC-friendly. Consider Project Settings → Editor → Asset Serialization → Force Text
  (one large one-time diff).
- [ ] Routing uses a hand-placed road **Waypoint graph** (`RouteManager.useNavMesh=false`).
  The NavMesh code path is kept. If a real connected drivable road network is ever built,
  a NavMesh re-bake could replace the hand graph.
- [ ] `Store1` / `Store5` (Markets2) sit at the map edge in the bare ground. User's
  placement — do not move without asking. Visually isolated until the outskirts are dressed.
- [ ] Bare tan ground beyond the pedestrian street — needs district fill / dressing.

## Gameplay / design (from the GDD)

- [ ] Cosmetic system — does not exist yet.
- [ ] Economy income/expense ratio — not numerically playtested/balanced.
- [ ] Asset licensing (Tirgames Asset Store EULA, Kenney CC0) — verify before any Steam release.
