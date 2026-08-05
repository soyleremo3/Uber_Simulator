# Geliştirme Checklist'i — Teslimat Şoförü Simülatörü
### Sıfırdan Bitmiş Oyuna Kadar, Adım Adım

> Bu liste sırasıyla takip edilmek üzere tasarlandı. Her ana başlık bir önceki başlığın üzerine inşa edilir. Bir bölümü tamamen bitirmeden sonrakine geçmemeye çalış — özellikle "Çekirdek Döngü" bölümleri (3-6) birbirine bağımlı.

---

## 0. Ön Hazırlık

- [ ] Unity 6.4 projesini oluştur (3D Core template)
- [ ] Version control kur (Git + .gitignore — Unity için hazır `.gitignore` şablonu kullan)
- [ ] Klasör yapısını oluştur:
  - `_Project/Scripts`
  - `_Project/Scripts/Data` (ScriptableObject'lar)
  - `_Project/Scripts/Managers`
  - `_Project/Scripts/Vehicles`
  - `_Project/Scripts/Orders`
  - `_Project/Scripts/UI`
  - `_Project/Prefabs`
  - `_Project/Art/Models`, `Art/Materials`, `Art/Animations`
  - `_Project/Audio`
- [ ] Namespace belirle (ör. `DeliverySim`) ve tüm scriptlerde kullan
- [ ] Input System paketini kur (yeni Input System veya Legacy — karar ver ve sabitle)
- [ ] Git ilk commit ("Initial project setup")

---

## 1. Mimari Temeller (Kod İskeleti)

- [ ] `GameManager` singleton'ı oluştur (sahne geçişleri, genel oyun durumu)
- [ ] `EconomyManager` oluştur:
  - [ ] Bakiye (para) alanı + `AddMoney()` / `SpendMoney()` metotları
  - [ ] Para değişimi event'i (`OnMoneyChanged`) — UI'nin dinlemesi için
- [ ] `OrderData` ScriptableObject'ini tanımla:
  - [ ] Alım noktası (Transform/Vector3 referansı ya da ID)
  - [ ] Teslim noktası
  - [ ] Ödeme miktarı
  - [ ] Zaman limiti
  - [ ] Yük tipi (enum: Yemek, Paket, Kırılabilir vb.)
- [ ] `IInteractable` arayüzünü tanımla (mevcut Interaction System yapından uyarlanabilir)
- [ ] `IUsable` veya benzeri bir arayüz (yük taşıma/bırakma eylemleri için)
- [ ] Basit bir `SaveSystem` iskeleti kur (JSON tabanlı — en azından bakiye ve itibar kaydı için)

---

## 2. Araç Kontrolcüsü (Sürüş)

- [ ] Placeholder araç modeli içeri al (cube/basit low-poly araba — asset bekleme)
- [ ] `Rigidbody` + `WheelCollider` kurulumu yap (4 teker)
- [ ] `VehicleController.cs` scriptini yaz:
  - [ ] Gaz/fren input'u
  - [ ] Direksiyon (steering) input'u
  - [ ] Motor tork eğrisi (AnimationCurve ile ayarlanabilir)
  - [ ] El freni (opsiyonel, drift için)
- [ ] Kamera sistemini kur (Cinemachine ile 3rd person follow)
- [ ] Test sahnesinde düz bir yolda sürüş hissini test et ve ayarla (hız, tutunma, dönüş)
- [ ] Basit hasar/denge takibi ekle (opsiyonel — yük kalitesini etkileyecekse şimdi temelini at)
- [ ] `VehicleData` ScriptableObject'i oluştur (hız, ivme, yakıt kapasitesi, fiyat gibi alanlar) — PRO RACER'daki araç profili yapısını uyarlayabilirsin

---

## 3. Dünya / Harita (Blockout Aşaması)

- [ ] Küçük bir test haritası blockout et (gri kutularla yol ağı, bina hacimleri)
- [ ] Yol ağını Unity'nin NavMesh'i veya basit waypoint sistemiyle işaretle (GPS/rota çizgisi için gerekecek)
- [ ] 5-8 adet "alım noktası" ve "teslim noktası" konumu belirle ve sahnede işaretle (boş GameObject + tag/etiket)
- [ ] Spawn noktası (oyuncunun başlangıç garajı) belirle
- [ ] Basit çevre kolizyonlarını (yol kenarları, bina duvarları) ekle
- [ ] Post-processing / temel aydınlatmayı kur (bu aşamada minimal, sadece okunabilir olsun yeter)

---

## 4. Sipariş Sistemi (Çekirdek Döngü — Bölüm 1)

- [ ] `OrderManager.cs` yaz:
  - [ ] Aktif sipariş havuzu (Liste/Queue)
  - [ ] Rastgele sipariş üretme mantığı (elindeki `OrderData` listesinden seçim)
  - [ ] Sipariş kabul/red metotları
  - [ ] Sipariş süresi geri sayımı (Coroutine veya `Update` bazlı timer)
- [ ] Sipariş kabul edildiğinde alım noktasını sahnede işaretleme (rota/ikon)
- [ ] Alım noktasına ulaşınca "yükü al" etkileşimini tetikle (`IInteractable` üzerinden)
- [ ] Yük alındıktan sonra teslim noktasını işaretle
- [ ] Teslim noktasına ulaşınca "teslim et" etkileşimini tetikle
- [ ] Teslimat tamamlandığında:
  - [ ] Süreye göre puan hesapla (zamanında / geç / çok geç)
  - [ ] `EconomyManager.AddMoney()` çağır
  - [ ] Sipariş listesinden kaldır, yeni sipariş üret

> Bu bölüm bitince minimal bir "tek sipariş al → git → teslim et → para kazan" döngüsü çalışır durumda olmalı. Bu senin **ilk oynanabilir prototipin**.

---

## 5. Puanlama & İtibar Sistemi

- [ ] `ReputationManager.cs` yaz:
  - [ ] Ortalama yıldız/puan hesaplama (son N teslimatın ortalaması ya da kümülatif)
  - [ ] İtibar seviyesi eşikleri (Bronz/Gümüş/Altın/Elmas gibi)
  - [ ] Seviye değiştiğinde event fırlat (`OnReputationLevelChanged`)
- [ ] İtibar seviyesine göre sipariş havuzunu filtrele (yüksek seviyede daha iyi ödeyen siparişler açılsın)
- [ ] Düşük puanın olumsuz sonucunu tanımla (daha az sipariş, düşük ödeme çarpanı vb.)

---

## 6. Ekonomi & Gider Sistemi

- [ ] Yakıt sistemini ekle:
  - [ ] Yakıt seviyesi alanı + sürüşte azalma mantığı
  - [ ] Yakıt istasyonu etkileşimi (para karşılığı doldurma)
- [ ] Bakım/tamir maliyeti sistemi (araç hasar aldıkça değer kaybetsin, tamir noktasında ücret ödensin)
- [ ] Gelir/gider dengesini basitçe test et (kağıt üzerinde ya da Excel'de bir tahmini tablo çıkar — teslimat başı ortalama kazanç vs. yakıt/tamir maliyeti)

---

## 7. Mağaza & Yükseltme Sistemi

- [ ] `ShopManager.cs` yaz (satın alma/yükseltme mantığı, bakiye kontrolü)
- [ ] `VehicleUpgradeData` ScriptableObject'i tanımla (yükseltme tipi, maliyet, etki miktarı)
- [ ] En az 3 temel yükseltme kategorisi kodla: Motor, Yakıt Deposu, Dayanıklılık
- [ ] Yeni araç satın alma akışını kur (araç listesi → satın al → garajda seçilebilir hale gelsin)
- [ ] Kozmetik sistemi (opsiyonel ilk fazda, sonraya bırakılabilir): renk/kaplama değiştirme

---

## 8. Kullanıcı Arayüzü (UI)

- [ ] UI Canvas yapısını kur (Screen Space - Overlay veya Camera, projenin ihtiyacına göre)
- [ ] **Telefon/Sipariş Ekranı:**
  - [ ] Aktif sipariş kartları (liste halinde)
  - [ ] Kabul/Red butonları
  - [ ] Kazanç geçmişi paneli
- [ ] **HUD (sürüş sırasında):**
  - [ ] Hız göstergesi
  - [ ] Rota/mesafe göstergesi
  - [ ] Kalan süre (sipariş timer'ı)
  - [ ] Yük durumu ikonu
- [ ] **Mağaza Ekranı:**
  - [ ] Araç/yükseltme/kozmetik sekmeleri
  - [ ] Bakiye göstergesi (EconomyManager'a bağlı)
- [ ] **İtibar Paneli:**
  - [ ] Yıldız/puan gösterimi
  - [ ] Seviye ilerleme çubuğu
- [ ] Ana Menü / Ayarlar / Duraklatma menüsü
- [ ] UI'yi tüm manager event'lerine bağla (event-driven güncelleme, her frame kontrol değil)

---

## 9. Asset Üretimi / Temini

> Bu aşamayı blockout tamamlandıktan, sistemler çalıştıktan sonra yapmak (placeholder'lardan gerçek assetlere geçiş) zaman kaybını önler.

- [ ] Araç modelleri:
  - [ ] Kaynak belirle (Asset Store / kendi modelleme / AI destekli 3D üretim)
  - [ ] En az 3-4 araç tipi (bisiklet, motor, araba, kamyon) için model temin et
  - [ ] Araçlara doğru collider/wheel pivot noktalarını ayarla
- [ ] Çevre/şehir assetleri:
  - [ ] Bina modülleri (modüler kit tercih et — tekrar kullanılabilirlik için)
  - [ ] Yol/kaldırım/sokak lambası gibi modüler parçalar
  - [ ] Doğa/dekor öğeleri (ağaç, bank, çöp kutusu vb.)
- [ ] Karakter modeli (oyuncu görünmüyorsa bu adımı atla, 3. şahıs görünüm varsa gerekli)
- [ ] UI ikonları (sipariş tipleri, para simgesi, yıldız ikonu vb.)
- [ ] Ses/müzik varlıkları (bkz. Bölüm 11)

---

## 10. Animasyonlar

- [ ] Araç animasyonları:
  - [ ] Teker dönüşü (kod tabanlı, WheelCollider rotasyonundan otomatik)
  - [ ] Kapı açma/kapama (teslimat alma anında, varsa)
  - [ ] Süspansiyon/sarsıntı efekti (opsiyonel, kozmetik)
- [ ] Karakter animasyonları (3. şahıs görünüm varsa):
  - [ ] Yürüme/koşma
  - [ ] Yük taşıma pozu
  - [ ] Teslimat/etkileşim animasyonu (kapıyı çalma, paketi bırakma vb.)
- [ ] Animator Controller kurulumu ve state machine tasarımı
- [ ] UI animasyonları (buton geçişleri, panel açılış/kapanış — basit tween'ler yeterli)

---

## 11. Ses & Müzik

- [ ] Motor sesi (hız/RPM'e bağlı pitch değişimi)
- [ ] Ortam sesleri (şehir ambiyansı, trafik)
- [ ] UI ses efektleri (sipariş kabul, teslimat tamamlandı, para kazanma, hata sesi)
- [ ] Arka plan müziği (sakin/casual tonlu, oyunun genel hissine uygun)
- [ ] `AudioManager.cs` yaz (merkezi ses tetikleme, ses seviyesi ayarları)

---

## 12. Görsel Efektler & Cila (Polish)

- [ ] Partikül efektleri: lastik izi/duman, teslimat tamamlanma efekti, para kazanma efekti
- [ ] Kamera sarsıntısı (çarpışma anında, opsiyonel)
- [ ] UI geçiş efektleri (fade in/out, panel animasyonları)
- [ ] Post-processing son ayarları (renk düzeltme, bloom, ambient occlusion — performansı gözeterek)
- [ ] Işıklandırma son hali (gündüz/gece geçişi varsa bu noktada ekle)

---

## 13. Test, Denge & Hata Ayıklama

- [ ] Çekirdek döngüyü baştan sona 20-30 kez oynayarak test et (kendi playtesting)
- [ ] Ekonomi dengesini gözden geçir: ortalama saatlik kazanç mantıklı mı?
- [ ] Sipariş süre limitlerini zorluk açısından test et (çok kolay/çok zor mu?)
- [ ] Farklı bilgisayarlarda/ayarlarda performans testi (FPS, yükleme süreleri)
- [ ] Bug tracking listesi tut (basit bir Trello/Notion tablosu yeterli)
- [ ] Mümkünse 2-3 kişiye dışarıdan test yaptır ve geri bildirim topla

---

## 14. Optimizasyon

- [ ] Draw call / batching kontrolü (statik nesneleri static olarak işaretle)
- [ ] LOD (Level of Detail) sistemleri ekle (özellikle şehir/bina modellerinde)
- [ ] Occlusion Culling kur (özellikle büyük harita için)
- [ ] Object pooling uygula (sık üretilen/yok edilen nesneler için — partiküller, siparişler vb.)
- [ ] Profiler ile performans darboğazlarını tespit et ve düzelt

---

## 15. Build & Yayına Hazırlık

- [ ] Build ayarlarını yapılandır (Player Settings, ikon, isim, versiyon numarası)
- [ ] Windows build al ve temiz bir makinede test et
- [ ] Steamworks hesabı oluştur (yayınlamayı düşünüyorsan bu aşamada başlat, süreç zaman alır)
- [ ] Steam mağaza sayfası materyalleri: kapak görseli, ekran görüntüleri, kısa/uzun açıklama, fragman (opsiyonel ama önerilir)
- [ ] Achievements / Steam entegrasyonu (opsiyonel)
- [ ] Yerelleştirme (en azından Türkçe + İngilizce metin desteği düşünülmeli)
- [ ] Son bug taraması ve kritik hataların kapatılması
- [ ] Yayın öncesi checklist: save sistemi çalışıyor mu, ayarlar kaydediliyor mu, crash yok mu

---

## Öncelik Sırası Özeti (Kısa Hatırlatma)

1. **Bölüm 0-2** → Temel iskelet + sürüş hissi
2. **Bölüm 3-4** → Blockout harita + çalışan sipariş döngüsü (bu noktada "oyun" diyebileceğin bir prototipin var)
3. **Bölüm 5-7** → İtibar + ekonomi + mağaza (döngüyü anlamlı kılan katman)
4. **Bölüm 8** → UI'yi gerçek hale getir
5. **Bölüm 9-12** → Placeholder'lardan gerçek asset/animasyon/efektlere geçiş
6. **Bölüm 13-15** → Test, optimizasyon, yayın

> Altın kural: **Bölüm 4 tamamlanmadan** (yani "kabul et → git → teslim et → para kazan" çalışmadan) asla asset/animasyon/görsel cila işine geçme. Önce mekanik iskelet, sonra görsel et.
