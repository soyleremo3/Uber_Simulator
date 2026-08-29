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

## Yapılacaklar — kullanıcı listesi (eklendi 2026-08-30)

Sadece kayıt. Bu turda uygulanmayacak. Numaralar kullanıcının verdiği sırayla.

### Araç / fizik

- [x] **1.** Araç durmadan teslim alamasın / veremesin (pickup/teslim için hız ~0 şartı).
- [x] **2.** Ani hızlanma çok sert — ivmelenme yumuşatılsın / kademelendirilsin.
- [x] **3.** Bir yere çarpınca hasar olayı (`VehicleCondition` çarpışma hasarı).
  - [ ] Düşük condition'da sürüşün kötüleşmesi (motor gücü / direksiyon zayıflasın). Kod
    yorumunda "henüz fiziksel ceza yok" diyor. Şimdilik yapılmayacak — sonra eklenecek.
- [x] **4.** Belirli bir hasar eşiğinin altına düşünce araç pert olsun. (condition ≤ 15 => `OnVehicleTotaled` => Game Over ekranı, `Time.timeScale=0`, Yeniden Başla / Çıkış.)
- [ ] **26.** Çarpma / çarpmama tespitinin sağlaması (collision detection doğrulama).
- [ ] **35.** Araç kirlenmesi + yıkama / temizleme istasyonu.
- [ ] **38.** Trailer (römork) eklenmesi.
- [ ] **39.** Lastik ve araç değişimi.
- [ ] **40.** Araç modifiye sistemi.

### Benzin / tamir istasyonları

- [ ] **5.** Tamir etme animasyonu.
- [ ] **6.** Benzin alma animasyonu.
- [ ] **7.** Benzin ve tamir noktalarının çeşitlenmesi / yerlerinin değişmesi.
- [ ] **8.** İstasyon yanında animasyonlu karakter + doldurma / onarma animasyonları.
- [ ] **9.** Tamir sırasında aletlerin gelmesi.
- [ ] **34.** Ücretli geçiş yerleri (toll) — gider kalemi.

### Karakter / NPC

- [ ] **10.** Karakter animasyonları.
- [ ] **23.** NPC yayalar (insanlar).
- [ ] **24.** NPC araçlar (trafik).

### Dünya / mekan / harita

- [ ] **11.** Yeni alım noktaları ve teslim noktaları eklenmesi.
- [ ] **12.** Yeni mekanların gelmesi.
- [ ] **16.** Yaya yolunun düzenlenmesi.
- [ ] **19.** Yeni evlerin eklenmesi.
- [ ] **20.** Boş alanların doldurulması.
- [ ] **21.** Daha çok asset (karton kutular, kargo eşyaları vs.).
- [ ] **22.** Kargo mekanlarının yapılması ve eklenmesi.
- [ ] **37.** Oyuncunun kendi evi.
- [ ] **36.** Havanın kararması (gündüz / gece döngüsü).
- [ ] **18.** Blender MCP bağlanması ve düzenlemenin orada yapılması.

### Trafik / ceza

- [ ] **13.** Ceza kesilmesi.
- [ ] **14.** Trafik polisinin gelmesi.
- [ ] **15.** Radarın gelmesi.

### Navigasyon / rota

- [ ] **17.** Yol göstergecinin düzenlenmesi + mini map üzerinde gösterilmesi.
- [ ] **29.** Ters yönden rota göstermemesi.
- [ ] **30.** Rota göstergecinin yol yönlerine göre ayarlanması.
- [ ] **31.** HUD'daki "sola dön / sağa dön" göstergesinin düzeltilmesi.
- [ ] **32.** Navigasyon ribbon'unun bina içinden geçmesi bug'ı (ground-snap raycast).
- [ ] **33.** Rotanın en kısa yolu göstermesi (NOT: Dijkstra hâlihazırda mesafe-ağırlıklı — doğrula).

### Ekonomi / sipariş

- [ ] **25.** İtibarın (Gümüş vb. tier) hemen yükselmemesi — kademeli olsun.
- [ ] **27.** Siparişlerin ve sipariş eden kişilerin çeşitlenmesi / değişmesi.
- [ ] **28.** Aynı anda 3'ten fazla sipariş olabilmesi.
- [ ] **41.** Teslim süresinin uzaklığa göre ayarlanması (NOT: distance-based limit kodda var — gözden geçir).
- [ ] **42.** Teslim ALMAK için de ayrı bir süre limiti.
- [ ] **43.** Sipariş listesinde yazan isim ile kabul sonrası görünen isim uyuşmazlığı bug'ı.
- [ ] **44.** Ödemenin mesafeye göre hesaplanması.
