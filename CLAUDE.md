# CLAUDE.md — Uber Simulator (Teslimat Şoförü Simülatörü)

Bu dosya Claude Code için proje context'idir. Kod yazarken/gözden geçirirken bu kurallara uy.

## Oyun Özeti

Casual driving / job simulator / economy. Oyuncu bağımsız kurye şoförü: sipariş kabul et → alım noktasından yükü al → teslimat noktasına zamanında götür → puan ve ödeme al → kazancı araç/yükseltme/kozmetiğe yatır.

**Çekirdek döngü (kodda çalışır durumda):**
```
Teklif listesi (max 3, havuzdan otomatik dolar) → Kabul Et → Alım Noktasına Sür → Yükü Al
   → Teslimat Noktasına Sür (mesafe-bazlı süre, geç kalınca kademeli puan/ödeme düşüşü)
   → Teslim Et → Yıldız + Ödeme (itibar çarpanlı) → İtibar Güncellenir → Mağazada Harca/Yükselt
```
Tek teslimat: 2-5 dk. Oturum: 20-40 dk.

Motor: Unity 6000.4.6f1 (Unity 6.4). URP + Cinemachine 3.1.7 + yeni Input System. Platform: PC (Steam) öncelik, mobil port sonraki faz.

## Gerçek Proje Durumu (önemli — bu bölüm periyodik güncellenmeli)

Kod, GDD'deki "MVP" tanımının epey ötesinde: sipariş döngüsü, ekonomi, itibar/kademe sistemi, araç yükseltmeleri, yakıt/hasar/tamir gideri, JSON kayıt (versiyonlu migration ile), runtime'da inşa edilen tam UI ve editor tek-tık kurulum araçları hepsi çalışır ve birbirine bağlı durumda. Yeni işe başlamadan önce "bu zaten var mı" diye `_Scripts/` altına bak — MVP listesindeki maddeler tamamlanmış kabul edilebilir, GDD'nin "ileri faz" dediği bazı sistemler (itibar kademeleri, araç yükseltmeleri) zaten üretimde.

En aktif/kırılgan alan şu an **harita/sahne** tarafı: tek sahne `MainScene.unity` (45 MB) içinde Tirgames "Stylized Street" (downtown) + Kenney "City Kit Commercial/Suburban/Roads" iki farklı görsel stil bir arada birleştiriliyor (`DowntownMapSetup.cs`, `KennyDistrictSetup.cs`). Git geçmişi burada hâlâ deneme-yanılma olduğunu gösteriyor — yeni harita işine başlamadan önce bu iki editor script'ini ve içindeki notları oku.

**Asset lisansı — yayından önce doğrulanmalı:**
- `Assets/_Uber Simulator/Art/Assets/Kenny/CityKit*` → Kenney.nl City Kit paketleri, büyük ihtimalle CC0 ama proje içinde lisans dosyası yok, kenney.nl üzerinden teyit edilmeli.
- `Assets/TirgamesAssets/StylizedWorld` → Unity Asset Store paketi ("Stylized Street"), Asset Store EULA'sına tabi; ticari dağıtım hakkı satın alma kaydından (Asset Store lisans türü) doğrulanmalı, projede yazılı kanıt yok.

## Namespace ve Mimari

Tüm proje kodu `DeliverySim` namespace altında (mevcut diğer projelerle çakışmasın diye). Editor-only araçlar `DeliverySim.EditorTools` alt namespace'inde.

**Kurulu mimari desenler — yeni kod bunlarla tutarlı olmalı:**

| Sistem | Desen | Konum |
|---|---|---|
| Sipariş verisi | `ScriptableObject` (`OrderData`) — pickup/delivery ID, ödeme, süre, yük tipi, min itibar kademesi | `_Scripts/_Data/OrderData.cs` |
| Sipariş döngüsü | `OrderManager` singleton (sahne-lokal, DontDestroyOnLoad değil) — teklif havuzu/rotasyon, mesafe-bazlı süre limiti, gecikme cezası, event'ler (`OnOffersChanged`, `OnOrderAccepted`, `OnOrderCompleted`...) | `_Scripts/_Orders/OrderManager.cs` |
| Sahne noktaları | `InteractionPoint` abstract taban + statik ID registry (`TryGetPoint`) — `PickupPoint`/`DeliveryPoint` alt sınıfları | `_Scripts/_Orders/` |
| Yük/nokta etkileşimi | `IInteractable` (Interact/GetInteractionPrompt), `IUsable` (CanUse/Use) | `_Scripts/_Interfaces/` |
| Ekonomi | `EconomyManager` singleton, `DontDestroyOnLoad`, `OnMoneyChanged`/`OnTransactionFailed` event'leri | `_Scripts/_Managers/EconomyManager.cs` |
| İtibar | `ReputationManager` singleton — son N teslimatın ortalaması → `ReputationTier` (Bronz/Gümüş/Altın/Elmas), ödeme çarpanı + sipariş kilidi | `_Scripts/_Managers/ReputationManager.cs`, `_Scripts/_Data/ReputationTier.cs` |
| Mağaza/yükseltme | `ShopManager` singleton — araç yükseltme kataloğu (`VehicleUpgradeData`, kategori+seviye başına asset) ve araç kataloğu (`VehicleData`); `VehicleUpgradeApplier` bileşeni satın alınan seviyeyi araca uygular | `_Scripts/_Managers/ShopManager.cs`, `_Scripts/_Vehicles/VehicleUpgradeApplier.cs` |
| Araç gideri | `VehicleFuel` (yakıt tüketimi/dolum), `VehicleCondition` (çarpışma hasarı/tamir) — `FuelStation`/`RepairStation` (`IInteractable`) üzerinden parayla giderilir | `_Scripts/_Vehicles/`, `_Scripts/_Core/FuelStation.cs`, `RepairStation.cs` |
| Oyun durumu | `GameManager` singleton — `GameState` enum (MainMenu/Playing/Paused/OrderActive/GameOver/Shop), sahne geçişleri, pause'da `Time.timeScale` | `_Scripts/_Managers/GameManager.cs` |
| Kayıt | `SaveSystem` singleton, JSON tabanlı (`SaveData`), sürüm alanlı (`CurrentSaveVersion`) + `Migrate()`, `Application.persistentDataPath` | `_Scripts/_Save/SaveSystem.cs` |
| Rota/GPS çizgisi | `RouteManager` — sahnedeki `Waypoint` graph'ı üzerinde Dijkstra (gerçek mesafe ağırlıklı), gerçek zemin-mesh ribbon (LineRenderer DEĞİL), `NextTurn` ile HUD dönüş göstergesi. Ground-snap raycast'i dedike "Road" physics layer'ına daraltılmış (bina/prop collider'larına çarpmasın diye) | `_Scripts/_Core/RouteManager.cs`, `Waypoint.cs` |
| Bildirim (toast) | Statik event hub `NotificationService.Raise(string)` — gameplay kodundan çağrılır, UI dinler | `_Scripts/_Core/NotificationService.cs` |
| Araç fiziği | `VehicleController` — **WheelCollider KULLANMIYOR**, kendi raycast-suspension sistemi (`VehicleEngine`/`VehicleWheel` serializable alt sınıflar). Artık `VehicleData` ScriptableObject okumuyor, tüm tuning Inspector alanlarında | `_Scripts/_Vehicles/VehicleController.cs` |
| Kamera | `VehicleCameraRig`, `SmoothMouseLook`, `CameraModeController` (3. şahıs/1. şahıs geçişi), Cinemachine 3 tabanlı | `_Scripts/_Vehicles/` |
| UI | Tamamı runtime'da kod ile inşa ediliyor — `UIBootstrap` + `UIFactory` (elle kurulmuş Canvas yok); `HUDController`, `OrderPanelController`, `ShopPanelController`, `PauseMenuController`, `NotificationUI`, `InteractionPromptUI` event-driven | `_Scripts/_UI/` |
| Editor tooling | `DeliverySimSetup` — tek-tık sahne kurulumu (yönetici objeleri, araç bileşenleri, kamera, örnek sipariş içeriği), idempotent | `_Scripts/Editor/` |

**Klasör yapısı** (`Assets/_Uber Simulator/_Scripts/`):
- `_CarScripts/` — **eski/deneysel** araç kodu (namespace'siz `Car.cs`/`Camera.cs`/`ui.cs`, `VehicleController`'ın atası) — kullanılmıyor, yeni iş `_Vehicles/`'a gitmeli. Silinmesi güvenli olabilir ama onay almadan silme.
- `_Core/` — sahne-genel yardımcılar: `RouteManager`, `Waypoint`, `NotificationService`, `ObjectPool`, `FuelStation`, `RepairStation`
- `_Orders/` — sipariş döngüsü: `OrderManager`, `InteractionPoint`, `PickupPoint`, `DeliveryPoint`
- `_Data/` — ScriptableObject veri sınıfları: `OrderData`, `VehicleData`, `VehicleUpgradeData`, `ReputationTier` (enum), `CameraSettings`
- `_Interfaces/` — `IInteractable`, `IUsable`
- `_Managers/` — singleton yöneticiler: `GameManager`, `EconomyManager`, `ReputationManager`, `ShopManager`, `AudioManager`
- `_Save/` — `SaveSystem`
- `_Test/` — test runner'lar (`EconomyTestRunner` — geçici, klavyeden 1-4 tuşlarıyla ekonomi/kayıt doğrulama)
- `_UI/` — runtime UI (`UIBootstrap`, `UIFactory`, controller'lar)
- `_Vehicles/` — güncel araç fiziği, kamera, yakıt/hasar/reset/yükseltme bileşenleri
- `Editor/` — `DeliverySimSetup`, `DowntownMapSetup`, `KennyDistrictSetup` (sahne kurulum araçları, sadece Editor'de derlenir)

## Kontroller (mevcut input şeması)

WASD sür, Space el freni, E etkileşim, Tab sipariş paneli, B mağaza, C kamera modu (3./1. şahıs), R aracı düzelt (takla sonrası), LShift/LCtrl manuel vites (otomatik zaten aktif), Esc duraklat.

## Kod Kuralları

- Türkçe yorum/log mesajı stili projede zaten yerleşik (`Debug.LogWarning("[EconomyManager] ...")`) — bu stile devam et. İngilizce yorumlar da var (özellikle yeni/karmaşık sistemlerde, ör. `VehicleController`, `RouteManager`) — mevcut dosyanın dilini koru.
- Singleton yöneticiler: `Instance` static property + `Awake()`'de çakışma kontrolü + `DontDestroyOnLoad` — mevcut `EconomyManager`/`GameManager`/`ReputationManager`/`ShopManager`/`AudioManager`/`SaveSystem` deseni. `OrderManager` ve `RouteManager` istisna: sahne-lokal singleton, `DontDestroyOnLoad` YOK (her sahnede yeniden kurulur).
- Para/ekonomi işlemleri SADECE `EconomyManager` üzerinden geçmeli — başka yerde bakiye state tutma.
- Sahne içi referanslar ID string'i üzerinden çözülür (`pickupPointId`/`deliveryPointId` → sahnedeki `InteractionPoint.PointId`, statik registry), doğrudan obje referansı değil.
- Yeni ScriptableObject veri sınıfı eklerken `[CreateAssetMenu(menuName = "DeliverySim/...")]` + `OnValidate()` içinde alan doğrulama ekle (`OrderData`/`VehicleData`/`VehicleUpgradeData` deseni).
- Yeni bir sahne kurulum adımı gerekiyorsa elle obje sürüklemek yerine `DeliverySimSetup` (veya ilgili map setup script'i) içine idempotent bir `[MenuItem]` ekle — mevcut desen bu.
- Oyuncuya gösterilecek geçici mesajlar için `NotificationService.Raise(...)` kullan, UI'ye doğrudan yazma.

## Ekonomi Tasarım Notu (kritik, GDD madde 3.4)

Gider kalemleri (yakıt, bakım, tamir) olmadan ekonomi tek yönlü şişer. Bu gider katmanı zaten kodda var (`VehicleFuel` + `FuelStation`, `VehicleCondition` + `RepairStation`). Yeni sistem eklerken kâr marjı kavramının anlamlı kalmasına dikkat et — her teslimat küçük bir maliyet taşımalı. Gelir/gider oranı henüz sayısal olarak playtest edilip dengelenmedi (bkz. Riskler).

Para akış kategorileri: araç satın alma, araç yükseltmesi (motor/yakıt deposu/dayanıklılık — kurulu), kozmetik (henüz yok), gider (yakıt/bakım/tamir — kurulu), lisans (henüz yok), üs/garaj (henüz yok).

## Puanlama / İtibar

Zamanında teslim = tam puan (5 yıldız, tam ödeme). Geç teslim = late-grace penceresi içinde doğrusal düşüş (yıldız ve ödeme oranı birlikte azalır), pencere aşılırsa sipariş tamamen iptal olur. Son N teslimatın ortalaması → itibar kademesi (Bronz/Gümüş/Altın/Elmas) → ödeme çarpanı + sipariş havuzu erişim kilidi. Bu sistem GDD'de "ileri faz" olarak işaretliydi ama zaten üretimde ve çalışıyor — yeni değişiklik yaparken bunu MVP-sonrası "gelecek iş" gibi ele alma.

## Riskler (GDD madde 9)

- Ekonomi dengesi test edilmeden ilerlemek en büyük risk — gelir/gider oranını erken doğrula (sistemler kurulu, sayısal denge testi henüz yapılmadı).
- Harita/sipariş içeriği elle üretiliyor, zaman yutar — şu an en sıcak alan; iki farklı asset stilini (Tirgames + Kenney) tutarlı birleştirmek ek risk taşıyor.
- Basit döngü uzun oynanışta monotonluk riski taşır — ilerleme/kozmetik bunu telafi etmeli (kozmetik sistemi henüz yok).
- Ticari yayın öncesi asset lisansları (yukarıdaki bölüm) doğrulanmadan Steam'e çıkılmamalı.
