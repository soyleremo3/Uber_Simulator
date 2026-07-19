# ASSET_NEEDS — Gereken Asset Listesi

Kod placeholder varsayımıyla yazılıyor; buradaki her şey sonradan gerçek asset ile değiştirilecek.
Öncelik: **P0 = MVP için şart**, **P1 = MVP sonrası ilk faz**, **P2 = cila/geç faz**.

## 3D Modeller

| Öncelik | Asset | Not |
|---|---|---|
| P0 | 1 adet low-poly araba | Placeholder küp/basit model yeterli — zaten sahnede araç var |
| P0 | Teker modeli (prefab) | VehicleController `wheelPrefab` alanına atanıyor; pivot merkezde olmalı |
| P1 | Blockout yerine modüler şehir kiti | Bina, yol, kaldırım, sokak lambası |
| P1 | 2-3 ek araç (motor, kamyonet) | Mağaza fazı için |
| P2 | Dekor (ağaç, bank, çöp kutusu) | |

## UI / 2D

| Öncelik | Asset | Not |
|---|---|---|
| P0 | Para simgesi ikonu | HUD + mağaza |
| P0 | Sipariş tipi ikonları (Yemek / Paket / Kırılabilir) | CargoType enum'una karşılık |
| P0 | Yıldız/puan ikonu | Değerlendirme ekranı |
| P1 | Telefon UI çerçevesi | Sipariş ekranı görseli |

## Ses / Müzik

Hepsi `_Managers` objesindeki **AudioManager** Inspector alanlarına sürüklenecek (alanlar hazır, boşken oyun sessiz çalışır).

| Öncelik | Asset | AudioManager alanı |
|---|---|---|
| P1 | Sipariş kabul SFX | Order Accepted Clip |
| P1 | Yük alındı SFX | Cargo Picked Up Clip |
| P1 | Teslimat tamam SFX | Order Completed Clip |
| P1 | Sipariş başarısız SFX | Order Failed Clip |
| P1 | Para kazanma SFX | Money Earned Clip |
| P1 | Hata SFX | Error Clip |
| P2 | Arka plan müziği (casual loop) | Background Music |
| P2 | Motor sesi (loop, RPM pitch) | Ayrı faz — kanca `VehicleEngine.GetRPM()` hazır |
| P2 | Şehir ambiyansı (loop) | Ayrı source gerektirir, istek üzerine eklenir |

## Efekt / Diğer

| Öncelik | Asset | Not |
|---|---|---|
| P0 | Skid mark TrailRenderer prefab'ı | VehicleController `skidMarkPrefab` alanı — basit siyah trail yeterli |
| P2 | Partiküller: toz/duman, teslimat konfeti, para efekti | |
