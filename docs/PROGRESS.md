# PROGRESS — Geliştirme Durumu

Son güncelleme: 2026-07-19 (kesintisiz tam kodlama oturumu)

## Faz Durumu

| Faz | Durum | Not |
|---|---|---|
| 0. Ön Hazırlık | ✅ | Klasörler, git, docs, .gitkeep |
| 1. Mimari Temeller | ✅ | GameManager (Shop state eklendi), EconomyManager (TrySpendMoney alias), OrderData (itibar kilidi + İngilizce CargoType), IInteractable/IUsable, SaveSystem v2 (versiyon+migrasyon, itibar, araçlar, yükseltmeler) |
| 2. Araç + Kamera | ✅ | Mevcut VehicleController korundu + upgrade/fuel çarpanları eklendi. YENİ: VehicleCameraController (6 klasik kamera bugı çözümlü) + CameraSettings SO + Cinemachine kurulum MenuItem'ı. VehicleData SO (ekonomi metadatası) |
| 3. Dünya İskeleti (kod) | ✅ | Waypoint + RouteManager (BFS + LineRenderer GPS), InteractionPoint (ID registry + gizmo) → PickupPoint/DeliveryPoint |
| 4. Sipariş Sistemi | ✅ KOD TAMAM | OrderManager: teklif havuzu, kabul/red, alım→teslim, süre sayacı, geç teslim puan/ödeme düşüşü, başarısızlık. Editor'de tek tık örnek içerik (Setup menü 4). SAHNE KURULUMU KULLANICIDA — MANUAL_STEPS Bölüm B+C |
| 5. İtibar | ✅ | ReputationManager: son N ortalama, Bronz/Gümüş/Altın/Elmas, ödeme çarpanı, sipariş havuzu filtresi |
| 6. Ekonomi & Gider | ✅ | VehicleFuel (sürüşte tüketim, motor kesme), FuelStation (kısmi dolum), VehicleCondition (çarpışma hasarı), RepairStation |
| 7. Mağaza | ✅ | ShopManager + VehicleUpgradeData (3 kategori), araç satın alma, VehicleUpgradeApplier (etkileri araca uygular). Yükseltme ASSET'lerini kullanıcı üretecek (MANUAL_STEPS D2) |
| 8. UI | ✅ | Event-driven: HUD, sipariş paneli (Tab), mağaza (B), duraklatma (Esc), ana menü, etkileşim promptu, bildirimler. UIBootstrap runtime'da sıfır-kurulum UI kurar |
| 9. Asset | 🟡 İnsan işi | ASSET_NEEDS.md güncel; kod placeholder'larla tam çalışır |
| 10. Animasyon | 🟡 | Teker dönüşü kodda var; gerisi asset fazı |
| 11. Ses | ✅ kancalar | AudioManager: müzik+SFX, PlayerPrefs ses seviyesi, sipariş event'lerine otomatik bağlanır. Clip'ler insan işi |
| 12. Efekt/Cila | 🟡 | Skid mark kodda mevcut; partikül/post-processing insan işi (MANUAL_STEPS F) |
| 13. Test & Denge | ⬜ | Kullanıcı playtest (MANUAL_STEPS Bölüm C senaryosu) |
| 14. Optimizasyon | ✅ kancalar | ObjectPool hazır; static/LOD/occlusion adımları MANUAL_STEPS F4 |
| 15. Build & Yayın | ⬜ | Adımlar MANUAL_STEPS F5-F6 |

## Verilen Kararlar / Varsayımlar (durmamak için not edildi)

1. **GDD.md yok** — kök CLAUDE.md'deki GDD özeti esas alındı. Tam GDD gelirse `docs/GDD.md`'ye koy, uyumsuzluk varsa düzeltirim.
2. **Legacy Input** — mevcut (test edilmiş) VehicleController Legacy kullanıyor; tüm yeni kod da öyle. Player Settings "Both" olmalı (MANUAL_STEPS A2).
3. **Düz `DeliverySim` namespace** — alt namespace istenmişti ama mevcut tüm kod düz; proje CLAUDE.md'si tutarlılık şart koşuyor.
4. **WheelCollider'a dönülmedi** — mevcut raycast süspansiyonlu controller test edilmiş ve çalışıyor; yeniden yazmak riskti. Tork eğrisi yerine mevcut RPM/vites simülasyonu korundu.
5. **VehicleData SO fizik değil ekonomi metadatası** — controller bilinçli olarak SO okumuyor (kodda not var).
6. **Sipariş süresi ALIMDA başlar** (teslimat bacağı zamanlı — GDD "teslimata zamanında götür" ifadesine uygun).
7. **Etkileşim tuşu F** — E, viteste kullanılıyor (VehicleController).
8. **UI legacy Text** — TMP importu gerektirmez, kutudan çıktığı gibi çalışır. TMP'ye geçiş cila fazında.
9. **EconomyManager float bakiye + SpendMoney korundu**; `TrySpendMoney` alias eklendi.
10. **Araç değiştirme (garaj) MVP dışı** — satın alma + sahiplik kaydı var, spawn/switch yok.

## Sistem Haritası (dosya → sorumluluk)

- `_Managers/`: GameManager, EconomyManager, ReputationManager, ShopManager, AudioManager
- `_Orders/`: OrderManager (+DeliveryResult), InteractionPoint, PickupPoint, DeliveryPoint
- `_Core/`: Waypoint, RouteManager, FuelStation, RepairStation, NotificationService, ObjectPool
- `_Vehicles/`: VehicleController (+çarpanlar), VehicleCameraController, VehicleFuel, VehicleCondition, VehicleInteractor, VehicleUpgradeApplier, VehicleCameraRig, SmoothMouseLook, CameraModeController
- `_Data/`: OrderData, VehicleData, VehicleUpgradeData, CameraSettings, ReputationTier
- `_Save/`: SaveSystem (v2, migrasyonlu)
- `_UI/`: UIFactory, UIBootstrap, HUDController, OrderPanelController, ShopPanelController, PauseMenuController, MainMenuController, InteractionPromptUI, NotificationUI
- `Editor/`: DeliverySimSetup (4 kurulum MenuItem'ı)

## Düzeltme Kaydı (2026-07-20 — "araç kontrolü bozuldu" raporu)

Teşhis: araç ayarlarına (grip/tork/süspansiyon) dokunulmamıştı; iki yeni etkileşim sorunu vardı:
1. **Tab çakışması:** OrderPanelController ve CameraModeController ikisi de Tab kullanıyordu — panel açarken kamera first-person'a geçiyordu. Çözüm: sipariş paneli **O** tuşuna taşındı.
2. **Rigidbody Interpolation:** Setup 2 komutu None→Interpolate yapmıştı; mass=1'lik hassas özel fizik bununla dengesizleşti. Çözüm: Setup artık interpolation'a dokunmuyor + **Setup 5** menü komutu eski ayara (None) döndürüyor.
3. **Yeni:** `VehicleReset` (R tuşu) — takla sonrası aracı olduğu yerde düzeltir. Setup 2 ve 5 otomatik ekler.
4. **ASIL TAKLA SEBEBİ (kullanıcı tespiti doğrulandı):** Nokta trigger'ları 5m yarıçaplı küre; süspansiyon raycast'i varsayılan ayarla trigger'lara da çarpıyordu → alana girişte tekerlek ışını görünmez küre kabuğunu zemin sanıp aracı fırlatıyordu. Çözüm: süspansiyon raycast'ine `QueryTriggerInteraction.Ignore` eklendi (VehicleController.FixedUpdate).

Eski scriptler (`_CarScripts/Car.cs`, `Camera.cs`, `ui.cs`) kontrol edildi: yalnızca pasif objelerde, aktif araca etkileri yok.

## Sonraki Oturum İçin

- Kullanıcı MANUAL_STEPS B+C'yi uygulayıp test sonucunu bildirecek.
- Derleme hatası çıkarsa Console'daki ilk hata satırı yeterli.
- TMP geçişi, garaj/araç değiştirme, motor sesi pitch bağlama, yerelleştirme — istek üzerine.
