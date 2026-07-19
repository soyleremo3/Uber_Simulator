# MANUAL_STEPS — Unity Editor Kurulum Rehberi (TEK DOSYA, SIRAYLA UYGULA)

Bu dosya, kodun tamamı yazıldıktan sonra oyunu çalışır hale getirmek için Unity
Editor'de yapman gereken HER ŞEYİ sırayla anlatır. Yukarıdan aşağı takip et.
Her adım: **NE / NEREDE / NASIL / NEDEN** formatında.

> Kod sana hiçbir şey yazdırmaz. Editor menüsüne eklenen otomasyonlar
> (**DeliverySim → Setup → ...**) işin çoğunu tek tıkla yapar.

---

## BÖLÜM A — Ön Kontrol (5 dk)

### A1. Projeyi aç, derleme hatası olmadığını doğrula
- **NE:** Console'da kırmızı hata var mı bak.
- **NEREDE:** Unity Editor → **Window → General → Console**.
- **NASIL:** Projeyi aç, script derlemesinin bitmesini bekle. Kırmızı hata görürsen bana kopyala.
- **NEDEN:** ~30 yeni script eklendi; her şey derlenmeden hiçbir adım çalışmaz.

### A2. Input ayarını doğrula
- **NE:** "Active Input Handling" ayarının **Both** (veya **Input Manager (Old)**) olduğunu doğrula.
- **NEREDE:** **Edit → Project Settings → Player → Other Settings → Configuration → Active Input Handling**.
- **NASIL:** Değer "Input System Package (New)" ise **Both** yap (Unity yeniden başlar).
- **NEDEN:** Tüm sürüş/UI kodu Legacy Input kullanıyor (mevcut VehicleController deseni). "New only" seçiliyse `Input.GetKey` exception fırlatır.

### A3. Paket kontrolü (muhtemelen hazır)
- **NE:** Cinemachine ve Input System kurulu mu bak.
- **NEREDE:** **Window → Package Management → Package Manager** → In Project.
- **NASIL:** Listede "Cinemachine 3.1.x" ve "Input System" görünmeli. Manifest'te zaten vardı; eksikse Unity Registry'den kur.
- **NEDEN:** Takip kamerası Cinemachine 3 kullanıyor.

---

## BÖLÜM B — Sahne Kurulumu (10 dk, MainScene)

### B1. MainScene'i aç
- **NEREDE:** `Assets/_Uber Simulator/Scenes/MainScene.unity` (Project panelinde çift tık).

### B2. Zemini kontrol et
- **NE:** y=0 düzleminde büyük bir zemin olmalı.
- **NASIL:** Araç zaten bir zeminde duruyorsa geç. Yoksa: Hierarchy → sağ tık → **3D Object → Plane**, Position (0,0,0), Scale (30,1,30).
- **NEDEN:** Örnek teslimat noktaları y=0 düzlemine, -60..+90 metre aralığına kurulacak; altına zemin gerekli.

### B3. Manager objelerini oluştur ⚠️ KRİTİK
- **NE:** Tüm singleton yöneticiler + sipariş/rota sistemi + UI bootstrap.
- **NEREDE:** Üst menü → **DeliverySim → Setup → 1 - Create Managers**.
- **NASIL:** Tek tık. Hierarchy'de `_Managers` (GameManager, EconomyManager, SaveSystem, ReputationManager, ShopManager, AudioManager), `_Gameplay` (OrderManager, RouteManager), `_UI` (UIBootstrap) oluşur.
- **NEDEN:** Bu üç obje olmadan hiçbir sistem çalışmaz. Menü komutu tekrar çalıştırılırsa kopya üretmez (güvenli).

### B4. Araca bileşenleri ekle ⚠️ KRİTİK
- **NE:** VehicleFuel, VehicleCondition, VehicleInteractor, VehicleUpgradeApplier + Rigidbody Interpolate.
- **NEREDE:** **DeliverySim → Setup → 2 - Setup Player Vehicle Components**.
- **NASIL:** Tek tık. Sahnedeki VehicleController'lı objeyi bulup eksik bileşenleri ekler, Rigidbody Interpolation'ı **Interpolate** yapar.
- **NEDEN:** Yakıt/hasar/etkileşim sistemleri araca bağlı. Interpolate = kamera titremesinin (jitter) ana çözümü.

### B5. Takip kamerasını kur
- **NE:** Cinemachine 3 takip kamerası + yalpalama filtresi + duvar-girme koruması.
- **NEREDE:** **DeliverySim → Setup → 3 - Create Follow Camera (Cinemachine)**.
- **NASIL:** Tek tık. Main Camera'ya CinemachineBrain, sahneye `CameraRig` (VehicleCameraRig) ve `CM_FollowCamera` (CinemachineCamera + Follow + RotationComposer + Deoccluder) ekler; hedefi otomatik araca bağlar.
- **NEDEN:** Deoccluder = kameranın duvara girmesini engeller. Rig = süspansiyon eğimini kameradan izole eder.
- **NOT:** Sahnede zaten çalışan bir Cinemachine kameran varsa bu adımı ATLA (çift kamera çakışır). İkisinden birini devre dışı bırak.

### B6. Örnek sipariş içeriğini üret
- **NE:** 2 alım + 3 teslim noktası, yakıt + tamir istasyonu, 3 sipariş asset'i; OrderManager havuzuna otomatik bağlanır.
- **NEREDE:** **DeliverySim → Setup → 4 - Create Sample Orders + Points**.
- **NASIL:** Tek tık. Noktalar sahnede -60..+90 metre aralığına dağılır (sarı/yeşil/turuncu gizmo'larla görünür). Asset'ler `Assets/_Uber Simulator/_Data/Orders/` altına yazılır.
- **NEDEN:** Çekirdek döngüyü test etmek için hazır içerik. Konumları Scene view'da elle taşıyabilirsin — sistem ID ile çalışır, konum serbest.

### B7. Sahneyi kaydet
- **NASIL:** **Ctrl+S**.

---

## BÖLÜM C — TEST SENARYOSU: İlk Oynanabilir Prototip (Checklist Bölüm 4 doğrulaması)

Play'e bas ve şu akışı izle. Ekranda runtime UI (HUD sol alt, yardım çubuğu en alt) otomatik kurulur:

1. **Tab** → Sipariş paneli açılır, 3 teklif görürsün (isim, ücret, süre).
2. Bir siparişte **Kabul**'e tıkla → "Sipariş kabul edildi" bildirimi + yeşil ALIM noktasında dikili marker + mavi rota çizgisi belirir.
3. Marker'a sür (WASD). Yaklaşınca ekranda **"Yükü Al [F]"** çıkar → **F** bas.
4. Süre sayacı başlar (ekran üstü). Turuncu TESLİM marker'ına sür → **"Teslim Et [F]"** → **F** bas.
5. Bildirim: "+X para, Y yıldız". HUD'da para ve ★ güncellenir. Panelde yeni teklifler birikir.
6. **B** → Mağaza: Motor/Yakıt Deposu/Dayanıklılık satırları görünür (yükseltme asset'leri henüz oluşturulmadıysa "MAKS" görünür — normal, bkz. D2).
7. **Esc** → Duraklat → **Kaydet** → **Devam Et**.
8. Play'i durdur, tekrar başlat → Esc → oyun açılışında bakiye korunmuş mu kontrol için Console'da `[SaveSystem]` logunu gör (yükleme ana menüden `Continue` ile de yapılabilir).

**Bu 8 adım çalışıyorsa çekirdek döngü (kabul et → git → teslim et → para kazan) TAMAM.**

Sorun çıkarsa: Console'daki ilk kırmızı/sarı satırı bana gönder.

---

## BÖLÜM D — İçerik Üretimi (Editor'de, kod gerekmez)

### D1. Yeni sipariş eklemek
- **NASIL:** Project panelinde sağ tık → **Create → DeliverySim → Order Data**. Inspector'da pickup/delivery ID'lerini sahnedeki nokta ID'leriyle eşleştir, ücret/süre gir. Sonra `_Gameplay` objesindeki **OrderManager → Order Pool** listesine sürükle.
- **NEDEN:** Sipariş çeşitliliği tamamen asset tabanlı; kod değişikliği gerektirmez.

### D2. Yükseltme asset'leri oluşturmak (mağazayı doldurur)
- **NASIL:** Sağ tık → **Create → DeliverySim → Vehicle Upgrade Data**. Her kategori (Engine/FuelTank/Durability) için Level 1'den başlayarak asset üret (ör. Engine L1: cost 500, multiplier 1.15; L2: cost 1200, multiplier 1.3). Hepsini `_Managers` objesindeki **ShopManager → Upgrade Catalog** listesine sürükle.
- **NEDEN:** Mağaza satırları katalogdan beslenir; katalog boşsa "MAKS" görünür.

### D3. Araç kataloğu (yeni araç satışı)
- **NASIL:** Sağ tık → **Create → DeliverySim → Vehicle Data** (id, isim, fiyat). **ShopManager → Vehicle Catalog**'a sürükle. `Starting Vehicle Id` alanına başlangıç aracının id'sini yaz.
- **NOT:** MVP'de araç değiştirme görsel olarak uygulanmadı (satın alma + sahiplik kaydediliyor); garaj/araç değiştirme sonraki faz.

### D4. Yeni alım/teslim noktası eklemek
- **NASIL:** Boş GameObject oluştur → **Add Component → PickupPoint** (veya DeliveryPoint) → Inspector'da **Point Id** ver (benzersiz!) → **Add Component → Sphere Collider**, `Is Trigger` işaretle, Radius ~5 → istersen marker child'ı ekleyip **Marker Visual** alanına ata.
- **NEDEN:** OrderData ID'leri bu noktalara bu Point Id üzerinden bağlanır.

### D5. GPS rotasını yol ağına oturtmak (opsiyonel)
- **NASIL:** Yol boyunca boş GameObject'ler oluştur, her birine **Waypoint** component'i ekle, Inspector'da **Neighbors** listesine komşu waypoint'leri sürükle (çift yönlü sayılır). Gizmo'lar bağlantıyı sarı çizgiyle gösterir.
- **NEDEN:** Waypoint yoksa rota düz çizgi çizer (çalışır ama binaların üstünden geçer). NavMesh bake GEREKMİYOR — sistem waypoint tabanlı.

### D6. Kamera hissini ayarlamak
- **Cinemachine yolu:** `CM_FollowCamera` seç → Inspector'da CinemachineFollow **Follow Offset** ve Damping değerleriyle oyna.
- **Kod kamerası yolu (alternatif):** Sağ tık → **Create → DeliverySim → Camera Settings** ile asset oluştur → Main Camera'ya **VehicleCameraController** ekle → Settings alanına asset'i, Target'a aracı ata → CinemachineBrain ve CM_FollowCamera'yı devre dışı bırak. Tüm parametreler (yumuşatma, FOV, çarpışma, ölü bölge) asset üzerinde.

---

## BÖLÜM E — Ana Menü Sahnesi (MVP sonrası, opsiyonel)

1. **File → New Scene** → "MainMenu" adıyla `Assets/_Uber Simulator/Scenes/` altına kaydet.
2. Hierarchy → sağ tık → **UI → Canvas**; içine **UI → Legacy → Button** ile 3 buton: "Yeni Oyun", "Devam Et", "Çıkış".
3. Boş GameObject → **MainMenuController** ekle → Inspector'da **Gameplay Scene Name** = `MainScene`.
4. Her butonun **On Click ()** listesine MainMenuController objesini sürükle; sırasıyla `StartNewGame`, `ContinueGame`, `QuitGame` seç.
5. **File → Build Profiles → Scene List**'e önce MainMenu, sonra MainScene'i ekle.
6. NOT: `_Managers` DontDestroyOnLoad olduğundan MainMenu sahnesine de **DeliverySim → Setup → 1** ile manager'ları eklemen gerekir (ilk açılan sahnede bir kez bulunmaları yeterli).

---

## BÖLÜM F — Cila / İleri Faz Adımları (Checklist 9-15)

### F1. Gerçek UI'ye geçiş
- Runtime UI yerine kendi Canvas'ını kurmak istediğinde: `_UI` objesindeki **UIBootstrap → Build On Start** kutusunu KAPAT. Kendi Canvas'ında aynı controller'ları (HUDController, OrderPanelController, ShopPanelController, PauseMenuController, NotificationUI, InteractionPromptUI) objelere ekleyip Inspector'daki Text/panel referanslarını bağla. Tüm alanlar [SerializeField] olarak açık.
- TMP'ye geçiş: TextMeshPro asset'lerini import et, controller'lardaki `Text` alanlarını TMP karşılıklarıyla değiştirmemi istersen söyle (kod değişikliği bende).

### F2. Ses
- `_Managers` → **AudioManager** Inspector'ında clip alanları hazır: müzik, sipariş kabul/alım/teslim/başarısız, para, hata. Asset'leri `ASSET_NEEDS.md` listesine göre temin edip sürükle. Motor sesi (RPM pitch) ayrı faz — kanca `VehicleEngine.GetRPM()` hazır.

### F3. Görsel asset değişimi
- Placeholder marker/istasyon küplerini gerçek modellerle değiştir: nokta objesinin child'ını değiştir, **Marker Visual** referansını güncelle. ID sistemi görselden bağımsız çalışır.

### F4. Performans (harita büyüyünce)
- Hareketsiz tüm çevre objelerini seç → Inspector sağ üst **Static** işaretle (batching).
- **Window → Rendering → Lighting** → Generate Lighting (gölge/GI pişirme).
- Occlusion: **Window → Rendering → Occlusion Culling** → Bake (büyük şehir haritasında).
- Sık üretilen efektler için `ObjectPool` component'i hazır (prefab + initial size ata).

### F5. Build
- **Edit → Project Settings → Player**: Company/Product Name, ikon, versiyon.
- **File → Build Profiles** → Windows → Build. Temiz bir makinede test et (özellikle kayıt dosyası: `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\deliverysim_save.json`).
- Steam: partner.steamgames.com hesabı ($100 ücret) → App oluştur → Steamworks SDK/Steamworks.NET entegrasyonu ayrı faz; hazır olduğunda achievements kancalarını koda ben eklerim.

### F6. Yerelleştirme (TR+EN)
- Şu an tüm oyuncuya görünen metin Türkçe ve kod içinde. Yerelleştirme istediğinde string tablosuna (ScriptableObject veya Unity Localization paketi) taşırım — şimdilik MVP gereği ertelendi.
