# CLAUDE.md — Uber Simulator (Teslimat Şoförü Simülatörü)

Bu dosya Claude Code için proje context'idir. Kod yazarken/gözden geçirirken bu kurallara uy.

## Oyun Özeti

Casual driving / job simulator / economy. Oyuncu bağımsız kurye şoförü: sipariş kabul et → alım noktasından yükü al → teslimat noktasına zamanında götür → puan ve ödeme al → kazancı araç/yükseltme/kozmetiğe yatır.

**Çekirdek döngü:**
```
Sipariş Al → Alım Noktasına Sür → Yükü Al → Teslimat Noktasına Sür (süre baskısı)
   → Teslim Et → Puan/Değerlendirme Al → Ödeme Al → Mağazada Harca/Yükselt → Tekrar Sipariş Al
```
Tek teslimat: 2-5 dk. Oturum: 20-40 dk.

Motor: Unity 6.4. Platform: PC (Steam) öncelik, mobil port sonraki faz.

## MVP Kapsamı (şu an odak — kapsam genişletme yok)

1. Tek araç (araba)
2. Küçük el yapımı harita (5-8 teslimat noktası)
3. Basit sipariş döngüsü: kabul et → git → teslim et → puan al
4. Temel ekonomi: para kazan, 2-3 yükseltme satın al
5. Minimal UI: metin tabanlı sipariş listesi + HUD

Harita büyütme, çoklu araç, hikaye, itibar sisteminin ileri detayları — MVP netleşmeden ELLENMEZ.

## Namespace ve Mimari

Tüm proje kodu `DeliverySim` namespace altında (mevcut diğer projelerle çakışmasın diye).

**Kurulu mimari desenler — yeni kod bunlarla tutarlı olmalı:**

| Sistem | Desen | Konum |
|---|---|---|
| Sipariş verisi | `ScriptableObject` (`OrderData`) — pickup/delivery ID, ödeme, süre, yük tipi | `_Scripts/_Data/OrderData.cs` |
| Yük/nokta etkileşimi | `IInteractable` (Interact/GetInteractionPrompt), `IUsable` (CanUse/Use) | `_Scripts/_Interfaces/` |
| Ekonomi | `EconomyManager` singleton, `DontDestroyOnLoad`, `OnMoneyChanged`/`OnTransactionFailed` event'leri | `_Scripts/_Managers/EconomyManager.cs` |
| Oyun durumu | `GameManager` singleton — `GameState` enum (MainMenu/Playing/Paused/OrderActive/GameOver), sahne geçişleri | `_Scripts/_Managers/GameManager.cs` |
| Kayıt | `SaveSystem` singleton, JSON tabanlı (`SaveData`), `Application.persistentDataPath` | `_Scripts/_Save/SaveSystem.cs` |
| Araç fiziği | `VehicleController` + `WheelCollider`, iç içe `VehicleEngine` gibi serializable alt sınıflar | `_Scripts/_Vehicles/` |
| Kamera | `VehicleCameraRig`, `SmoothMouseLook`, `CameraModeController` | `_Scripts/_Vehicles/` |

**Klasör yapısı** (`Assets/_Uber Simulator/_Scripts/`):
- `_CarScripts/` — eski/deneysel araç kodu (Car.cs, ui.cs) — yeni iş `_Vehicles/`'a gitmeli
- `_Core/`, `_Orders/` — henüz boş, sipariş sistemi (`OrderManager`) buraya kurulacak
- `_Data/` — ScriptableObject veri sınıfları
- `_Interfaces/` — IInteractable, IUsable
- `_Managers/` — singleton yöneticiler (EconomyManager, GameManager, ileride OrderManager)
- `_Save/` — SaveSystem
- `_Test/` — test runner'lar (EconomyTestRunner)
- `_Vehicles/` — güncel araç/kamera kontrol kodu

## Kod Kuralları

- Türkçe yorum/log mesajı stili projede zaten yerleşik (`Debug.LogWarning("[EconomyManager] ...")`) — bu stile devam et.
- Singleton yöneticiler: `Instance` static property + `Awake()`'de çakışma kontrolü + `DontDestroyOnLoad` — mevcut `EconomyManager`/`GameManager` deseni.
- Para/ekonomi işlemleri SADECE `EconomyManager` üzerinden geçmeli — başka yerde bakiye state tutma.
- Sahne içi referanslar ID string'i üzerinden çözülür (`pickupPointId`/`deliveryPointId` → sahnedeki `DeliveryPoint.PointId`), doğrudan obje referansı değil — Inventory/ItemData mimarisiyle aynı mantık.
- Yeni ScriptableObject veri sınıfı eklerken `[CreateAssetMenu(menuName = "DeliverySim/...")]` + `OnValidate()` içinde alan doğrulama ekle (OrderData deseni).

## Ekonomi Tasarım Notu (kritik, GDD madde 3.4)

Gider kalemleri (yakıt, bakım, tamir) olmadan ekonomi tek yönlü şişer. Yeni sistem eklerken kâr marjı kavramının anlamlı kalmasına dikkat et — her teslimat küçük bir maliyet taşımalı.

Para akış kategorileri: araç satın alma, araç yükseltmesi, kozmetik, gider (yakıt/bakım/tamir), lisans, üs/garaj.

## Puanlama / İtibar (ileri faz, MVP'de basit tutulacak)

Zamanında teslim = tam puan. Geç teslim = kademeli düşüş. Ortalama puan → itibar kademesi (Bronz/Gümüş/Altın/Elmas) → sipariş havuzu erişimi.

## Riskler (GDD madde 9)

- Ekonomi dengesi test edilmeden ilerlemek en büyük risk — gelir/gider oranını erken doğrula.
- Harita/sipariş içeriği elle üretiliyor, zaman yutar — MVP kapsamını küçük tut.
- Basit döngü uzun oynanışta monotonluk riski taşır — ilerleme/kozmetik bunu telafi etmeli.
