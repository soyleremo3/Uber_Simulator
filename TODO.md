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
- [ ] **29.** Ters yönden rota göstermemesi. **HÂLÂ ÇÖZÜLMEDİ** (kullanıcı oyunda hâlâ görüyor). Yapılanlar: `RouteManager` deep fix (`FindStartNode`/`FindEndNode` forward-biased, geri-kanca trim, `startNode==endNode` artık düz çizgi değil, alloc'suz Dijkstra, simetrik adjacency, ground-snap `lastGroundY`). Sentetik testler geçti ama gerçek kavşakta kullanıcı hâlâ yanlış dönüş görüyor. `WaypointGraphHealer` auto-link aracı denendi → `Road` layer'ı tüm alanı kaplayan `Ground_Safety` zemini olduğu için yanlış link ekledi → geri alındı + silindi. z=4/15/26 orta yolları araç hizasında çit/dükkan cephesiyle kapalı (linecast doğrulandı) — graph aslında doğru, dönüş gerçekten gerekli olabilir. Sonraki: `F9` diagnostic (`RouteManager.DumpRouteDiagnostic`, commit'li) ile kullanıcının tam bad-spot çıktısını al, oradan devam.
- [ ] **30.** Rota göstergecinin yol yönlerine göre ayarlanması. #29 ile birlikte açık.
- [x] **31.** HUD "sola/sağa dön" göstergesi yeniden yazıldı: eskiden tüm rotayı tarayıp yüzlerce metre ilerideki ilk viraja "dön" diyordu, yumuşak viraja takılıyordu, aracın dibindeki gürültülü ilk segmentte zıplıyordu. Artık lookahead penceresi (55 m), araç dibi köşeler elenir, eşik 28°. `RouteManager` inspector'da ayarlanabilir. Playtest ile his ayarı gerekebilir. (NOT: #29 açık olduğu için bu da gerçek oyunda hâlâ tuhaf olabilir.)
- [ ] **32.** Nav ribbon bina içinden geçme bug'ı. **HÂLÂ ÇÖZÜLMEDİ** (kullanıcı hâlâ görüyor). `RouteManager` deep fix uygulandı (bkz. #29) — sentetik testlerde bina içinden geçme yok ama gerçek oyunda hâlâ var. #29 ile birlikte `F9` çıktısıyla devam edilecek.
- [x] **33.** Rotanın en kısa yolu göstermesi. DOĞRULANDI: `RouteManager.FindGraphPath` gerçek Dijkstra (kenar maliyeti = gerçek dünya mesafesi, çift yönlü); NavMesh yolu da optimal. Kod değişmedi.

### Ekonomi / sipariş

- [x] **25.** İtibar kademeli. `docs/design/reputation-redesign.md` adım 1-7 uygulandı + play'de doğrulandı: RP/XP + level eğrisi (Bronz L1 başlar, L2≈6 teslimat, Gümüş≈30, tek 5★ tier atlatmaz), çok faktörlü yıldız (çarpışma/hasar/hız/gecikme, Kırılır ×1.6, ±jitter), Diamond form-gate, aynı-rota farm cezası, Save v3, HUD level+XP barı. Adım 8 (teslimat sonrası sonuç kartı) opsiyonel — yapılmadı. ⚠️ **TEKRAR GÖZDEN GEÇİRİLECEK** (sayılar playtest ile ayarlanacak).
- [ ] **27.** Sipariş + sipariş eden çeşitliliği. `docs/design/order-board-redesign.md` D bölümü.
  - [x] `CustomerPoolData` SO (40 ad × 30 soyad + 15 işletme) + `CustomerPool.asset`; her sipariş `BuildOffer`'da rastgele müşteri (birey %72 / işletme) alır; panel satırında "Kime: Ayşe Y." gösterilir. Play test OK.
  - [ ] Prosedürel sipariş kompozisyonu (rastgele nokta çifti, `namePatterns`, `weightByTier`, Regular müşteri kalıcılığı) — asset/nokta az olduğu için ertelendi. ⚠️ **TEKRAR GÖZDEN GEÇİRİLECEK**.
- [x] **28.** Tab panelinde maks 10 sipariş + dalgalı akış + oyuncuyu tutan sistem. `docs/design/order-board-redesign.md` 10 adım uygulandı, hepsi play-test edildi:
  1. Kaydırılabilir panel (`UIFactory.CreateScrollView`) + maxOffers 10.
  2. `OrderOffer`/`CustomerInstance` modeli, per-offer TTL (75-150s).
  3. `GameClock` (1 gün=20 dk, 06:00 başlar) + zamana bağlı Poisson akış (λ rush/lull) + gündüz tabanı, gece 0.
  4. Cluster gelişleri (%35 → 3-8 sn'de 1-2 ekstra).
  5. Ücret/süre formülü: `20+14×km` × kargo çarpanı, Fragile +15.
  6. Müşteri çeşitliliği (`CustomerPoolData`, satırda "Kime: X").
  7. Surge (talep/kıtlık → yeni teklifler daha pahalı, HUD banner + ⚡ tag).
  8. Teslimat serisi (üst üste ≥4★ → +%4/teslimat, maks %40, 🔥×n).
  9. Öncelikli/VIP (%4, Gümüş+, ödeme ×2.2-3.5, kısa TTL, altın satır).
  10. Sadık müşteri (2 zamanında → Regular, +%15, daha sık gelir, kaydedilir) + günlük hedef (6 teslimat → +₺250).
  ⚠️ **TEKRAR GÖZDEN GEÇİRİLECEK** — sayılar playtest ile ayarlanacak; prosedürel nokta eşleştirme + `weightByTier` + `namePatterns` #27 alt-maddesinde ertelendi.
- [ ] **41.** Teslim süresinin uzaklığa göre ayarlanması (NOT: distance-based limit kodda var — gözden geçir).
- [x] **42.** Alım için ayrı süre limiti: kabul edilince araç->alım mesafesinden hesaplanan süre başlar (`usePickupTimeLimit`, `useDistanceBasedPickupTime`, buffer/min/grace inspector'da). HUD'da "Alım mm:ss" gösterilir. Süre + grace dolarsa sipariş iptal (itibar cezası YOK). `OnPickupTimerTick` eventi eklendi.
- [x] **43.** Sipariş kartındaki parantez ile isim alakasız görünüyordu. Sebep: örnek sipariş asset'lerinde `cargoType` çoğu 0 (Food) kalmıştı (Moda Teslimatı → Food vb.). order_002..005 → Paket olarak düzeltildi; ayrıca CargoType artık UI'da Türkçe (`Yemek/Paket/Kırılır`) gösteriliyor.
- [x] **44.** Ödeme mesafeye göre: `OrderManager.GetOrderPayment` = `baseFare (15) + paymentPerKm (12) × iş mesafesi(km)`. `useDistanceBasedPayment` kapatılırsa eski `OrderData.PaymentAmount`. Gecikme + itibar çarpanı üstüne uygulanıyor. Değerler inspector'da.

## Yapılacaklar — kullanıcı listesi (eklendi 2026-08-31)

Sadece kayıt. Bu turda uygulanmayacak — kullanıcı "ekle, yapma" dedi. Numaralar 45'ten devam.

### Başlangıç durumu (yeni oyun)

- [ ] **45.** Başlangıçta para **0** olsun. Şu an `EconomyManager.startingBalance = 100f`
  (`_Scripts/_Managers/EconomyManager.cs:14`, `CurrentBalance = startingBalance` satır 40).
  Sahne/prefab kopyalarında da `startingBalance: 100` var: `Prefabs/_Managers.prefab`,
  `Prefabs/Gameplay/_Managers.prefab`, `Scenes/Map Scene.unity`, `Scenes/MainScene.unity`.
  Değeri 0'a çek — kaydedilmiş oyun yoksa oyuncu ilk siparişe borçsuz-parasız başlar.
  NOT: para 0 + yakıt düşük (madde 47) => ilk siparişi bitirene kadar benzin alamama
  riski; ekonomi dengesi (madde 44 / genel #Economy) ile birlikte gözden geçirilmeli.
- [ ] **46.** Başlangıçta "altın" **0** olsun. ⚠️ Kodda ayrı bir altın/premium para birimi
  **yok** — `gold` eşleşmeleri hep itibar "Gold" tier'ı (`ReputationTier`). İki olası niyet:
  (a) eklenecek yeni premium kur => sıfır başlasın (yeni sistem, tasarlanacak),
  (b) itibar/yıldız maxed başlamasın => şu an HUD "★ 5.0 (Elmas)" gösteriyor;
  #25 redesign'da "Bronz L1 başlar" deniyor ama gerçek başlangıç yıldızı doğrulanmalı
  (`ReputationManager` — son N teslimatın ortalaması, teslimat yokken varsayılan?).
  Uygulamadan önce kullanıcıya (a) mı (b) mi diye sor.
- [ ] **47.** Başlangıç yakıtı çok dolu olmasın. Şu an `VehicleFuel.startingFuel = 45f`
  ve `Capacity` de ~45 => depo full başlıyor (`_Scripts/_Vehicles/VehicleFuel.cs:17,41`).
  Sahne/prefab: `Scenes/MainScene.unity`, `Prefabs/Gameplay/PlayerVehicle.prefab` → `startingFuel: 45`.
  Daha düşük bir başlangıç değeri ver (örn. kapasitenin ~%30-40'ı) — ilk benzin
  istasyonu ziyaretini erkenden anlamlı kıl. Madde 45 ile etkileşimine dikkat.

### Araç / pert ekranı

- [ ] **48.** Araç pert olunca (Game Over ekranı) mouse hareketi kamera açısını
  değiştirmesin. Akış: `VehicleCondition.OnVehicleTotaled`
  (`_Scripts/_Vehicles/VehicleCondition.cs:115`) → `GameOverController.HandleVehicleTotaled`
  (`_Scripts/_UI/GameOverController.cs:39`) → `GameManager.SetGameState(GameState.GameOver)`
  → `Time.timeScale = 0f` (`_Scripts/_Managers/GameManager.cs:55-58`).
  `SmoothMouseLook` muhtemelen `Update` içinde `Time.timeScale`'den bağımsız fare deltası
  okuyor, o yüzden donmuş ekranda kamera hâlâ dönüyor. Çözüm: GameOver'a girince
  `SmoothMouseLook`'u disable et (Pause menüsündeki mevcut cursor/`CameraModeController`
  disable mantığı örnek — `_Scripts/_Vehicles/CameraModeController.cs:21,37`), veya
  `SmoothMouseLook` `unscaledTime`/state kontrolü yapsın. Cursor da görünür + serbest
  yapılmalı (buton tıklaması için).

### Navigasyon / rota (tekrar açıldı)

- [ ] **49.** Rota ters yolu göstermesin — kullanıcı oyunda hâlâ görüyor.
  #29 ve #32 ile aynı kök sorun (`RouteManager` deep fix yapıldı, sentetik testler
  geçti, gerçek kavşakta hâlâ yanlış). `F9` diagnostic (`RouteManager.DumpRouteDiagnostic`)
  ile kullanıcının tam bad-spot çıktısı alınıp oradan devam edilecek. Bkz. madde 29/30/32.
