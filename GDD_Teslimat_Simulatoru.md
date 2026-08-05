# Game Design Document
## [Çalışma Adı] — Teslimat Şoförü Simülatörü

**Versiyon:** 0.1 (İlk Taslak)
**Motor:** Unity 6.4
**Platform:** PC (Steam) — Mobil port ihtimali sonraki fazda değerlendirilebilir
**Tür:** Casual Driving / Job Simulator / Economy

---

## 1. Konsept Özeti

Oyuncu, bir teslimat/kurye platformuna kayıtlı bağımsız bir şoförü canlandırır. Uygulamadan gelen siparişleri alır, belirtilen noktadan yükü/paketi/yemeği alır ve müşterinin adresine zamanında ve hasarsız teslim eder. Teslimat kalitesi müşteri puanına, puan ise kazanılan paraya ve açılan yeni fırsatlara doğrudan yansır. Kazanılan para; araç yükseltmeleri, yeni araçlar, lisanslar ve kozmetik özelleştirmeler üzerinden tekrar oyuna yatırılır.

**Çekirdek fantezi:** "Kendi patronun ol, sokakları öğren, itibarını inşa et, filon büyüsün."

**Bir cümlelik hedef:** Basit ama tatmin edici bir teslimat döngüsü + anlamlı bir ekonomi döngüsü = tekrar oynanabilir, sakinleştirici bir iş simülatörü.

---

## 2. Çekirdek Oyun Döngüsü (Core Loop)

```
Sipariş Al → Alım Noktasına Sür → Yükü Al → Teslimat Noktasına Sür (süre baskısı)
   → Teslim Et → Puan/Değerlendirme Al → Ödeme Al → Mağazada Harca/Yükselt → Tekrar Sipariş Al
```

Döngü uzunluğu: Tek bir teslimat **2-5 dakika** arası sürmeli (oturum başına 20-40 dk oynanabilir olacak şekilde).

---

## 3. Temel Mekanikler

### 3.1 Sipariş Sistemi
- Telefon/uygulama arayüzü üzerinden gelen sipariş listesi (aynı anda 1-3 aktif teklif)
- Her sipariş: alım noktası, teslim noktası, tahmini süre, ödeme miktarı, yük türü (yemek/paket/kırılabilir eşya vb.)
- Oyuncu siparişi kabul/red edebilir — bu, rota planlama ve risk/ödül kararı katar

### 3.2 Teslimat & Puanlama
- Zamanında teslim = tam puan (5 yıldız)
- Geç teslim = kademeli puan düşüşü (süreye göre lineer veya eşik bazlı)
- Yanlış adrese bırakma / aracın hasar görmesi (yükün "sağlık" değeri varsa) = düşük puan
- Ortalama puan, oyuncunun **itibar seviyesini** belirler

### 3.3 İtibar & Seviye Sistemi
- Ortalama puana bağlı itibar kademeleri (ör. Bronz → Gümüş → Altın → Elmas)
- Yüksek itibar → daha yüksek ücretli siparişlere erişim + daha az "sıradan" iş
- Düşük itibar → sipariş havuzu daralır, ceza riski artar

### 3.4 Ekonomi & Harcama (Kritik Sistem)
Kazanılan para şu kanallara akmalı — **bu, oyunun en önemli tasarım eksenidir:**

| Kategori | Örnek | Amaç |
|---|---|---|
| Araç satın alma | Yeni bisiklet/motor/araba/kamyon | İlerleme hissi, yeni sipariş türlerine erişim |
| Araç yükseltmesi | Motor, lastik, yakıt deposu, hasar dayanımı | Performans artışı |
| Kozmetik | Kaplama, jant, dekal | Kişiselleştirme (düşük maliyet, yüksek his) |
| Gider | Yakıt, bakım, tamir | Parayı geri emen "sürtünme" — ekonomiyi anlamlı kılar |
| Lisans | Motor ehliyeti, ağır vasıta lisansı | Yeni araç kategorilerinin kilidini açar |
| Üs/Garaj | Depo alanı, birden fazla araç barındırma | Uzun vadeli yatırım hissi |

> **Tasarım notu:** Gider kalemleri olmadan (yakıt/tamir) ekonomi tek yönlü şişer ve anlamını kaybeder. Her teslimatın küçük bir maliyeti olmalı ki "kâr marjı" kavramı oyuncu için gerçek hissettirsin.

### 3.5 Sürüş Sistemi
- Basit ama tatmin edici araç fiziği (WheelCollider tabanlı — mevcut PRO RACER alt yapısı temel alınabilir)
- Hasar/denge sistemi opsiyonel: sert manevralar yük kalitesini etkileyebilir (özellikle yemek/kırılabilir eşya taşırken)
- Mini-harita + rota çizgisi (GPS simülasyonu)

---

## 4. İlerleme Yapısı

**Erken oyun:** Tek araç (bisiklet/scooter), küçük harita alanı, düşük ödemeli siparişler
**Orta oyun:** Araba kilidi açılır, harita genişler, itibar sistemi devreye girer, yükseltme mağazası aktifleşir
**Geç oyun:** Kamyon/ağır vasıta, çoklu araç filosu (opsiyonel idle/pasif gelir mekaniği), üst düzey itibar ödülleri

---

## 5. Kullanıcı Arayüzü (UI)

- **Telefon Ekranı:** Aktif sipariş listesi, kabul/red butonları, kazanç geçmişi
- **HUD (sürüş sırasında):** Rota işareti, kalan süre, yük durumu ikonu, hız göstergesi
- **Mağaza Ekranı:** Araç/yükseltme/kozmetik kategorileri, sahip olunan bakiye
- **Puan/İtibar Paneli:** Ortalama yıldız, itibar seviyesi, sonraki seviyeye kalan ilerleme

---

## 6. Teknik Mimari Notları (Unity)

Mevcut proje deneyimlerinle doğrudan örtüşen sistemler:

- **Sipariş verisi** → `ScriptableObject` tabanlı `OrderData` (alım/teslim koordinatları, ödeme, süre, yük tipi) — Inventory sistemindeki `ItemData` yaklaşımıyla birebir aynı mantık
- **Yük etkileşimi** → `IInteractable` / `IUsable` arayüzleri — mevcut Interaction System'in üzerine inşa edilebilir
- **Araç fiziği** → WheelCollider + ScriptableObject araç profilleri (PRO RACER'daki yapı)
- **Envanter/Yükseltme** → `InventorySlot` benzeri bir `VehicleUpgradeSlot` sistemi
- **Ekonomi Yöneticisi** → Tekil bir `EconomyManager` (MonoBehaviour ya da Singleton) — bakiye, gider/gelir event'leri
- **Sipariş Yöneticisi** → `OrderManager` — aktif sipariş havuzu, zamanlayıcı, puan hesaplama mantığı

> Namespace önerisi: mevcut projelerinle çakışmaması için `DeliverySim` gibi bağımsız bir namespace kullanılabilir.

---

## 7. MVP Kapsamı (İlk Prototip Hedefi)

Aşırı kapsam genişlemesinden kaçınmak için ilk prototipte **sadece şunlar** olmalı:

1. Tek araç (araba)
2. Küçük, el yapımı harita (5-8 teslimat noktası)
3. Basit sipariş döngüsü (kabul et → git → teslim et → puan al)
4. Temel ekonomi: para kazan, 2-3 yükseltme satın al
5. Minimal UI (metin tabanlı sipariş listesi + HUD)

Bu kapsam netleşmeden harita büyütme, çoklu araç, hikaye gibi eklerle uğraşmamak, projenin bitirilebilirliğini doğrudan artırır.

---

## 8. Farklılaşma / Öne Çıkış Fikirleri (opsiyonel, sonraki faz)

- Belirgin bir görsel kimlik (ör. retro/düşük-poli stil, ya da yerel/Türkiye temalı bir şehir kurgusu)
- Hafif mizah veya kısa NPC diyalogları (mevcut NPC dialogue sistemin uyarlanabilir)
- Hava durumu / trafik yoğunluğu gibi dinamik zorluk katmanları

---

## 9. Riskler & Dikkat Edilmesi Gerekenler

- **Ekonomi dengesi:** Gelir/gider oranı test edilmeden ilerlemek en büyük risk — erken prototipte bile temel bir gelir/gider tablosu tutulmalı
- **İçerik hacmi:** Harita ve sipariş çeşitliliği elle üretildiği için zaman yutar — MVP'de kapsamı küçük tutmak şart
- **Tekrarlayıcılık:** Döngü basit olduğu için 5-10 saatten uzun oynanışta monotonluk riski var; ilerleme/kozmetik sistemleri bunu telafi etmeli
