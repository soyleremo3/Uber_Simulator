# Geliştirme Checklist'i — Teslimat Şoförü Simülatörü

Bu liste sırasıyla takip edilmek üzere tasarlandı. Her ana başlık bir önceki başlığın üzerine inşa edilir. Bir bölümü tamamen bitirmeden sonrakine geçmemeye çalış — özellikle "Çekirdek Döngü" bölümleri (3-6) birbirine bağımlı.

## 0. Ön Hazırlık

- [x] Unity 6.4 projesini oluştur (3D Core template)
- [x] Version control kur (Git + .gitignore)
- [x] Klasör yapısını oluştur (`Assets/_Uber Simulator/_Scripts/...` — mevcut yapı korunuyor)
- [x] Namespace belirle (`DeliverySim`) ve tüm scriptlerde kullan
- [ ] Input System kararını sabitle (paket kurulu, kod şu an Legacy Input kullanıyor — karar bekliyor)
- [x] Git ilk commit

## 1. Mimari Temeller (Kod İskeleti)

- [x] `GameManager` singleton (sahne geçişleri, GameState enum)
- [x] `EconomyManager` (bakiye, AddMoney/SpendMoney, OnMoneyChanged, OnTransactionFailed)
- [x] `OrderData : ScriptableObject` (pickupPointId, deliveryPointId, payment, timeLimit, cargoType)
- [x] `IInteractable` arayüzü
- [x] `IUsable` arayüzü
- [x] `SaveSystem` iskeleti (JSON, versiyonlama + sahip olunan araçlar dahil)

## 2. Araç Kontrolcüsü (Sürüş)

- [x] Placeholder araç + fizik kurulumu (raycast süspansiyon — WheelCollider KULLANILMIYOR, bilinçli karar)
- [x] `VehicleController.cs` (gaz/fren, direksiyon, vites/RPM simülasyonu, el freni, sürüş asistleri)
- [x] Kamera sistemi (Cinemachine 3 + `VehicleCameraRig` ara hedef + `SmoothMouseLook` + `CameraModeController`)
- [x] Sürüş hissi test edildi (önceki commit'ler: "Vehicle Controller Fixed", "VehicleFollowCamerea Fixed")
- [ ] Basit hasar/denge takibi (opsiyonel — ertelendi)
- [ ] `VehicleData : ScriptableObject` — mevcut VehicleController bilinçli olarak SO kullanmıyor; mağaza fazında (7) yeniden değerlendirilecek

## 3. Dünya / Harita (Blockout)

- [ ] Küçük test haritası blockout (gri kutular, yol ağı)
- [ ] Waypoint/rota sistemi (kod: `Waypoint` + `RouteManager`)
- [ ] 5-8 alım/teslim noktası konumu (kod: `PickupPoint` + `DeliveryPoint` bileşenleri + gizmo)
- [ ] Spawn noktası (garaj)
- [ ] Çevre kolizyonları
- [ ] Temel aydınlatma

## 4. Sipariş Sistemi (ÇEKİRDEK DÖNGÜ)

- [ ] `OrderManager.cs` (sipariş havuzu, rastgele üretim, kabul/red, süre sayacı)
- [ ] Kabul → alım noktası işaretle → yükü al → teslim noktası işaretle → teslim et
- [ ] Süreye göre puan + `EconomyManager.AddMoney()` + yeni sipariş üretimi
- [ ] İlk oynanabilir prototip testi (MANUAL_STEPS'te test senaryosu)

## 5. Puanlama & İtibar

- [ ] `ReputationManager.cs` (ortalama puan, Bronz/Gümüş/Altın/Elmas eşikleri, OnReputationLevelChanged)
- [ ] İtibara göre sipariş havuzu filtreleme
- [ ] Düşük puan cezası (az sipariş / düşük ödeme çarpanı)

## 6. Ekonomi & Gider

- [ ] Yakıt sistemi (sürüşte azalma + istasyon etkileşimi)
- [ ] Bakım/tamir maliyeti
- [ ] Gelir/gider denge testi (tablo)

## 7. Mağaza & Yükseltme

- [ ] `ShopManager.cs`
- [ ] `VehicleUpgradeData : ScriptableObject`
- [ ] 3 yükseltme kategorisi: Motor, Yakıt Deposu, Dayanıklılık
- [ ] Yeni araç satın alma akışı
- [ ] Kozmetik (opsiyonel, sonraya)

## 8. UI

- [ ] Canvas yapısı
- [ ] Telefon/Sipariş ekranı (kartlar, kabul/red, kazanç geçmişi)
- [ ] HUD (hız, rota/mesafe, süre, yük ikonu)
- [ ] Mağaza ekranı
- [ ] İtibar paneli
- [ ] Ana menü / Ayarlar / Duraklatma
- [ ] Tüm UI event-driven bağlanacak

## 9. Asset Üretimi / Temini

- [ ] Araç modelleri (3-4 tip), çevre/şehir kitleri, UI ikonları, ses/müzik → ASSET_NEEDS.md

## 10. Animasyonlar

- [ ] Teker dönüşü (kodda mevcut), kapı, süspansiyon efekti, UI tween'leri

## 11. Ses & Müzik

- [ ] Motor sesi (RPM pitch), ambiyans, UI SFX, müzik, `AudioManager.cs`

## 12. Görsel Efekt & Cila

- [ ] Partiküller, kamera sarsıntısı, UI geçişleri, post-processing, ışık son hali

## 13. Test & Denge

- [ ] 20-30 tam döngü playtest, ekonomi dengesi, süre limitleri, performans, dış test

## 14. Optimizasyon

- [ ] Batching, LOD, Occlusion Culling, object pooling, Profiler

## 15. Build & Yayın

- [ ] Player Settings, Windows build, Steamworks, mağaza materyalleri, yerelleştirme (TR+EN), son bug taraması

---

**Altın kural:** Bölüm 4 tamamlanmadan (kabul et → git → teslim et → para kazan çalışmadan) asset/animasyon/görsel cila işine geçme.
