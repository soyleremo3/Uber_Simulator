# MANUAL_STEPS — Unity Editor Kurulum Rehberi (ÇOK DETAYLI, TIK TIK)

Bu rehber her şeyi tek tek anlatır: neye tıklayacaksın, ekranda ne göreceksin,
doğru gittiğini nereden anlayacaksın. Yukarıdan aşağı sırayla git, hiçbir adımı atlama.
Her adımın sonundaki **✓ KONTROL** satırı, o adımın başarılı olduğunu nasıl anlayacağını söyler.

**Panellerin yerleri (hatırlatma):**
- **Hierarchy** = genelde SOL tarafta, sahnedeki objelerin listesi.
- **Scene** = ortadaki 3D görünüm.
- **Inspector** = SAĞ tarafta, seçili objenin özellikleri.
- **Project** = ALTTA, projedeki dosyalar.
- **Console** = ALTTA (Project'in yanında sekme). Görünmüyorsa: üst menü **Window → General → Console**.

---

# BÖLÜM A — ÖN KONTROL (bir kere yapılır, ~5 dakika)

## ADIM A1 — Projeyi aç ve hata var mı bak

1. Unity Hub'ı aç.
2. Projeler listesinden **Uber_Simulator**'a tıkla, projenin açılmasını bekle.
3. Açılınca alt kısımda sağda dönen bir çark görebilirsin — bu, scriptlerin derlendiği anlamına gelir. **Çark kaybolana kadar hiçbir şeye dokunma.**
4. Üst menüden **Window → General → Console**'a tıkla (Console paneli açılır).
5. Console panelinin sağ üstündeki üç ikona bak: beyaz konuşma balonu, sarı üçgen, kırmızı ünlem.
6. **Kırmızı ünlemin** yanındaki sayıya bak.

**✓ KONTROL:** Kırmızı sayı **0** ise sorun yok, devam et.
**✗ SORUN:** Kırmızı sayı 0 değilse → kırmızı satıra bir kez tıkla → satırın tamamını kopyala → bana chat'e yapıştır. Ben düzeltmeden devam etme.

## ADIM A2 — Input ayarını kontrol et (ÇOK ÖNEMLİ)

Oyunun tüm tuş kontrolleri "eski input sistemi" ile yazıldı. Unity yanlış moddaysa oyun açılır açılmaz hata fırlatır. Kontrol edelim:

1. Üst menüden **Edit → Project Settings...**'e tıkla. Yeni bir pencere açılır.
2. Bu pencerenin SOL listesinden **Player**'a tıkla.
3. Sağ tarafta **Other Settings** yazan bölümü bul, üzerine tıklayıp genişlet (kapalıysa).
4. Aşağı in, **Configuration** başlığını bul.
5. İçinde **Active Input Handling** diye bir satır var. Sağındaki değere bak.

Üç ihtimal var:
- **"Input Manager (Old)"** yazıyorsa → dokunma, bu iyi. Pencereyi kapat.
- **"Both"** yazıyorsa → dokunma, bu da iyi. Pencereyi kapat.
- **"Input System Package (New)"** yazıyorsa → tıkla, açılan listeden **Both**'u seç. Unity "editor yeniden başlayacak" diye soracak → **Apply/Yes** de. Unity kendini yeniden başlatır (1-2 dk). Yeniden açılınca bu bölüme geri dön.

**✓ KONTROL:** Active Input Handling = "Input Manager (Old)" veya "Both".

## ADIM A3 — Cinemachine paketi kurulu mu bak

1. Üst menüden **Window → Package Management → Package Manager**'a tıkla (eski sürümlerde direkt Window → Package Manager).
2. Açılan pencerenin sol üstünde bir açılır menü var; **In Project** seçili olsun.
3. Soldaki listede **Cinemachine** var mı bak. Versiyonu **3.x** olmalı (örn. 3.1.7).

**✓ KONTROL:** Cinemachine 3.x listede görünüyor. (Projende zaten kurulu olmalı — manifest'te gördüm. Yoksa: pencerenin sol üstünden **Unity Registry** seç, arama kutusuna "Cinemachine" yaz, seç, sağ alttan **Install**.)

4. Pencereyi kapat.

---

# BÖLÜM B — SAHNE KURULUMU (~10 dakika, sırası önemli)

## ADIM B1 — Doğru sahneyi aç

1. Alttaki **Project** panelinde şu klasöre git: **Assets → _Uber Simulator → Scenes**.
   (Klasörlere çift tıklayarak ilerle veya soldaki klasör ağacını kullan.)
2. **MainScene** dosyasına ÇİFT tıkla.
3. "Save current scene?" diye sorarsa **Save** de.

**✓ KONTROL:** Unity penceresinin en üst başlığında "MainScene" yazıyor ve Hierarchy panelinde en üstte **MainScene** görünüyor.

## ADIM B2 — Zemin var mı kontrol et

Örnek teslimat noktaları sahnede -60 ile +90 metre arasına yerleşecek. Aracın altında ve o alanda zemin olmalı.

1. Scene görünümünde aracının durduğu yere bak. Araç bir zemin/yol üzerinde mi?
2. Zemin genişse (araba etrafında her yöne en az 100 metre) → bu adımı atla, B3'e geç.
3. Zemin yoksa veya küçükse:
   a. Hierarchy panelinde BOŞ bir yere SAĞ tıkla.
   b. Menüden **3D Object → Plane**'e tıkla. Sahneye "Plane" adında düz bir zemin eklenir.
   c. Plane seçiliyken (Hierarchy'de mavi) sağdaki Inspector'a bak.
   d. En üstte **Transform** bölümü var. Şunları elle yaz:
      - **Position**: X = `0`, Y = `0`, Z = `0`
      - **Scale**: X = `30`, Y = `1`, Z = `30`
   (Kutucuğa tıkla, değeri sil, yenisini yaz, Enter'a bas.)

**✓ KONTROL:** Scene'de araç ve etrafında geniş bir zemin var.

## ADIM B3 — Manager'ları oluştur (TEK TIK) ⚠️ EN KRİTİK ADIM

1. Unity'nin EN ÜST menü çubuğuna bak (File, Edit, Assets, GameObject...). Orada **DeliverySim** diye YENİ bir menü göreceksin.
   - **Göremiyorsan:** scriptler henüz derlenmemiş demektir. 10 saniye bekle; hâlâ yoksa Console'da kırmızı hata var demektir → A1'e dön.
2. **DeliverySim → Setup → 1 - Create Managers**'a tıkla.
3. Hierarchy paneline bak.

**✓ KONTROL:** Hierarchy'de şu ÜÇ yeni obje belirdi:
- `_Managers`
- `_Gameplay`
- `_UI`

Ekstra doğrulama istersen: `_Managers`'a tıkla → Inspector'da şu 6 bileşeni görmelisin: **Game Manager, Economy Manager, Save System, Reputation Manager, Shop Manager, Audio Manager**. `_Gameplay`'de: **Order Manager, Route Manager**. `_UI`'da: **UI Bootstrap**.

## ADIM B4 — Araca bileşenleri ekle (TEK TIK)

1. Üst menüden **DeliverySim → Setup → 2 - Setup Player Vehicle Components**'a tıkla.
2. Console'a bak (alttaki panel).

**✓ KONTROL:** Console'da şöyle bir satır göreceksin:
`[Setup] '...' araç bileşenleri tamam (yakıt, hasar, etkileşim, yükseltme).`
Ayrıca Hierarchy'de aracın otomatik seçildiğini ve Inspector'da şu bileşenlerin eklendiğini görürsün: **Vehicle Fuel, Vehicle Condition, Vehicle Interactor, Vehicle Upgrade Applier**.

**✗ SORUN:** Console'da `Sahnede VehicleController bulunamadı` yazıyorsa → sahnede araba yok veya arabada VehicleController component'i yok. Aracını sahneye koy, sonra bu adımı tekrarla.

## ADIM B5 — Takip kamerasını kur (TEK TIK)

**ÖNEMLİ ÖN NOT:** Sahnende ZATEN çalışan bir Cinemachine takip kameran varsa (önceki oturumlarda kurduysan) bu adımı ATLA ve B6'ya geç. İki takip kamerası birbiriyle kavga eder.

Emin değilsen kontrol: Hierarchy'nin üstündeki arama kutusuna `CM` yaz. "CM" ile başlayan veya "CinemachineCamera" içeren bir obje çıkıyorsa kameran zaten var → atla. Çıkmıyorsa devam:

1. Üst menüden **DeliverySim → Setup → 3 - Create Follow Camera (Cinemachine)**'a tıkla.

**✓ KONTROL:** Hierarchy'de `CameraRig` ve `CM_FollowCamera` objeleri belirdi. Console'da `[Setup] Cinemachine takip kamerası hazır` yazıyor.

## ADIM B6 — Örnek siparişleri ve noktaları üret (TEK TIK)

1. Üst menüden **DeliverySim → Setup → 4 - Create Sample Orders + Points**'a tıkla.

**✓ KONTROL (üç şey):**
1. Hierarchy'de şu 7 yeni obje var: `Pickup_Restaurant`, `Pickup_Depot`, `Delivery_HouseA`, `Delivery_HouseB`, `Delivery_Office`, `FuelStation_Main`, `RepairStation_Main`.
2. Project panelinde **Assets → _Uber Simulator → _Data → Orders** klasörü oluştu, içinde 3 dosya var: `order_food_a`, `order_package_a`, `order_fragile_a`.
3. Console'da: `[Setup] OrderManager.orderPool 3 örnek siparişle dolduruldu.`

**NOT:** Scene görünümünde bu noktaları renkli tel kürelerle görürsün (yeşil = alım, turuncu = teslim). Bir nokta zeminin dışında/duvarın içinde kaldıysa: Hierarchy'de objeye tıkla → Scene'de ok (Move) aracıyla sürükleyip uygun yere taşı. Sistem ID ile çalıştığı için konumu değiştirmek serbesttir.

## ADIM B7 — Sahneyi kaydet

1. Klavyeden **Ctrl+S** bas.

**✓ KONTROL:** Unity başlığındaki MainScene yazısının yanında `*` işareti kayboldu.

---

# BÖLÜM C — İLK OYUN TESTİ (adım adım senaryo)

> **GÜNCELLEME 2 (takla / gerçekçi sürüş düzeltmesi):** Takla sorununun kökü bulundu:
> aktif araç 1 kg kütleyle çalışıyordu (!) ve kodda devrilmeyi frenleyecek mekanik yoktu.
> Kod artık interpolation'dan bağımsız (kamera akıcılığı için **Interpolation AÇIK kalıyor** —
> eski "Setup 5 ile None yap" talimatı GEÇERSİZ, o komut artık tam tersine interpolation'ı açık garanti eder)
> ve anti-roll bar + roll-center mekaniği eklendi. Yapman gereken TEK ŞEY:
>
> 1. Unity'de derlemenin bitmesini bekle.
> 2. Üst menü → **DeliverySim → Setup → 6 - Apply Realistic Vehicle Tuning** → tıkla.
>    (Console'a önce ESKİ değerler loglanır — beğenmezsen Ctrl+Z veya o değerleri elle geri girersin.)
> 3. **Ctrl+S** ile sahneyi kaydet → Play → test et.
>
> **Güncel tuş listesi:** WASD sür • Space el freni • F etkileşim • **O sipariş paneli** • B mağaza • **R aracı düzelt** • **Tab kamera modu (1./3. şahıs)** • Esc duraklat • E/Q vites
>
> **Sürüş hissi ayar rehberi** (Inspector → PlayerVeichle Car → Vehicle Controller):
>
> | Ne değiştirmek istiyorsun | Hangi alan | Yönü |
> |---|---|---|
> | Devrilme direnci | `Anti Roll Stiffness`, `Lateral Force Height`, `Center Of Mass Offset.y` | Artır / artır / daha negatif = daha stabil |
> | Süspansiyon sertliği | `Suspension Force` | Artır = sert |
> | Zıplama/sallanma sönümü | `Damp Amount` | Artır = daha çabuk oturur |
> | Hızlanma gücü | teker başına `Engine Torque` | Artır = güçlü |
> | Viraj tutuşu | `Wheel Grip X` | Artır = daha çok tutar (aşırısı devirmeye zorlar) |
> | Direksiyon açısı | ön tekerlerde `Turn Angle` | Azalt = yüksek hızda daha stabil |
> | Araç ağırlık hissi | Rigidbody `Mass` | 1200 = sedan; büyük araçta 2000+ |

Şimdi oyunu oynayarak çekirdek döngüyü doğrulayacağız.

## ADIM C1 — Oyunu başlat

1. Ekranın üst ortasındaki **▶ (Play)** butonuna tıkla.
2. 1-2 saniye bekle.

**✓ KONTROL:** Ekranda şunlar otomatik belirir:
- SOL ALTTA yarı saydam siyah panel: hız, para (₺ 100), yakıt, durum, ★ puan, "Sipariş yok".
- EN ALTTA yardım satırı: `WASD: Sür | Space: El freni | F: Etkileşim | O: Siparişler | B: Mağaza | R: Aracı Düzelt | Tab: Kamera | Esc: Duraklat`

**✗ SORUN:** Bu yazılar YOKSA → Play'i durdur, Hierarchy'de `_UI` objesini seç, Inspector'da **UI Bootstrap** bileşeninde **Build On Start** kutusunun İŞARETLİ olduğunu doğrula.

## ADIM C2 — Sipariş kabul et

1. Klavyeden **O** tuşuna bas. (Tab DEĞİL — Tab kamera modunu değiştirir.)
2. Ekranın sağında "SİPARİŞLER" paneli açılır. İçinde 3 sipariş kartı var (isim, ücret, süre + Kabul/Reddet butonları).
3. İlk siparişin **Kabul** butonuna FARE ile tıkla.

**✓ KONTROL:**
- Ekranın üstünde yeşil bildirim: "Sipariş kabul edildi: ... Alım noktasına git!"
- Sahnede bir noktada havada duran BEYAZ SİLİNDİR (marker) belirdi.
- Zeminde araçtan o noktaya giden MAVİ ÇİZGİ (GPS rotası) var.
- Sol alt panelde "Alım bekleniyor: ..." yazıyor.

**NOT:** Panel açıkken araba tuşları çalışmaya devam eder. Paneli kapatmak için tekrar **O**.

## ADIM C3 — Yükü al

1. **W A S D** ile arabayı sür, mavi çizgiyi takip et, beyaz silindirli noktaya git.
2. Noktaya ~5 metre yaklaşınca ekranın alt ortasında SARI yazı belirir: **"Yükü Al [F]"**.
3. Dur ve **F** tuşuna bas.

**✓ KONTROL:**
- Bildirim: "Yük alındı! Teslimat için ... saniyen var."
- Ekranın ÜST ORTASINDA geri sayım başladı: `Süre: 02:30` gibi.
- Marker eski noktadan kayboldu, YENİ bir noktada belirdi (teslim noktası).
- Mavi çizgi artık teslim noktasına gidiyor.

## ADIM C4 — Teslim et

1. Mavi çizgiyi takip ederek teslim noktasına sür.
2. Yaklaşınca **"Teslim Et [F]"** yazısı çıkar → **F** bas.

**✓ KONTROL:**
- Bildirim: "Teslimat tamamlandı! +35 para, 5.0 yıldız." (geç kaldıysan daha az).
- Sol altta para arttı (₺ 100 → ₺ 135 gibi).
- ★ satırı güncellendi.
- Süre sayacı kayboldu, "Sipariş yok" yazısına dönüldü.

**BU NOKTAYA GELDİYSEN ÇEKİRDEK DÖNGÜ ÇALIŞIYOR — oyunun ilk oynanabilir prototipi tamam. 🎉**

## ADIM C5 — Yakıt istasyonunu dene (opsiyonel)

1. Sahnede sarı gizmo'lu `FuelStation_Main` küpüne sür (koordinat ~ x=12, z=-25).
2. Yaklaşınca **"Yakıt Al [F] (3.0/litre)"** çıkar → **F** bas.

**✓ KONTROL:** "X litre yakıt alındı (-Y para)" bildirimi + sol altta yakıt arttı, para azaldı. (Depo doluysa "Depo zaten dolu." der — normal.)

## ADIM C6 — Mağazayı aç

1. **B** tuşuna bas.

**✓ KONTROL:** Ortada "MAĞAZA" paneli açılır; Motor / Yakıt Deposu / Dayanıklılık satırları görünür.
**NOT:** Satırlarda "MAKS" yazması normal — henüz yükseltme asset'i üretmedik (Bölüm D2'de üreteceğiz). Kapatmak için tekrar **B**.

## ADIM C7 — Duraklat ve kaydet

1. **Esc** bas → "DURAKLATILDI" ekranı gelir, oyun donar.
2. **Kaydet** butonuna tıkla → "Oyun kaydedildi." bildirimi.
3. **Devam Et**'e tıkla → oyun kaldığı yerden sürer.
4. Testi bitirmek için üstteki **▶ Play** butonuna tekrar tıkla (oyundan çıkar).

**✓ KONTROL:** Console'da `[SaveSystem] Oyun kaydedildi: ...` satırı var.

**⚠ UYARI:** Play modundayken sahnede yaptığın hiçbir değişiklik kalıcı olmaz. Objeleri taşımak istiyorsan önce Play'den çık.

---

# BÖLÜM D — İÇERİK ÜRETİMİ (oyun çalıştıktan sonra, keyfe göre)

## ADIM D1 — Mağazayı doldur: yükseltme asset'leri üret

Mağazadaki "MAKS" yazısının sebebi katalogun boş olması. Dolduralım. Örnek olarak Motor Seviye 1'i beraber yapalım:

1. Project panelinde **Assets → _Uber Simulator → _Data** klasörüne git.
2. Klasör içinde BOŞ bir yere SAĞ tıkla → **Create → DeliverySim → Vehicle Upgrade Data**.
3. Yeni dosya oluşur, adı yazılabilir durumda → `Upgrade_Engine_1` yaz, Enter.
4. Dosya seçiliyken Inspector'da şunları ayarla:
   - **Category**: `Engine`
   - **Display Name**: `Motor Yükseltmesi I`
   - **Level**: `1`
   - **Cost**: `500`
   - **Effect Multiplier**: `1.15`  (= %15 daha güçlü motor)
5. Aynı yöntemle istediğin kadar üret. Önerilen başlangıç seti:

   | Dosya adı | Category | Level | Cost | Effect Multiplier |
   |---|---|---|---|---|
   | Upgrade_Engine_1 | Engine | 1 | 500 | 1.15 |
   | Upgrade_Engine_2 | Engine | 2 | 1200 | 1.3 |
   | Upgrade_FuelTank_1 | FuelTank | 1 | 400 | 1.25 |
   | Upgrade_FuelTank_2 | FuelTank | 2 | 900 | 1.5 |
   | Upgrade_Durability_1 | Durability | 1 | 450 | 1.3 |
   | Upgrade_Durability_2 | Durability | 2 | 1000 | 1.6 |

6. Şimdi bunları mağazaya bağla:
   a. Hierarchy'de `_Managers` objesine tıkla.
   b. Inspector'da **Shop Manager** bileşenini bul.
   c. **Upgrade Catalog** satırının solundaki ok ile listeyi aç.
   d. Listenin altındaki **+** butonuna 6 kez bas (6 boş satır açılır).
   e. Project panelinden her Upgrade dosyasını sürükleyip birer satıra bırak.
      (Alternatif: her satırın sağındaki küçük ⊙ ikonuna tıkla, açılan listeden seç.)
7. **Ctrl+S** ile sahneyi kaydet.

**✓ KONTROL:** Play'e bas → **B** → artık "Motor — Seviye 0 / Sonraki: Motor Yükseltmesi I (₺500)" ve **Satın Al** butonu görünüyor. Yeterli paran varsa satın al → seviye artar, para düşer.

## ADIM D2 — Yeni sipariş eklemek

1. Project'te **Assets → _Uber Simulator → _Data → Orders** klasörüne git.
2. Sağ tık → **Create → DeliverySim → Order Data** → ad ver (örn. `order_food_b`).
3. Inspector'da doldur:
   - **Order Id**: `order_food_b` (benzersiz olsun)
   - **Order Name**: oyuncunun göreceği isim (örn. `Pizza Teslimatı`)
   - **Pickup Point Id**: sahnedeki BİR alım noktasının ID'si (örn. `pickup_restaurant`)
   - **Delivery Point Id**: bir teslim noktası ID'si (örn. `delivery_house_b`)
   - **Payment Amount**: ücret (örn. `45`)
   - **Time Limit Seconds**: saniye (örn. `120`)
   - **Cargo Type**: Food / Package / Fragile
4. Hierarchy'de `_Gameplay` → Inspector'da **Order Manager** → **Order Pool** listesine **+** ile satır ekle → yeni asset'i sürükle.
5. **Ctrl+S**.

**Mevcut nokta ID'leri (Setup 4'ün ürettikleri):**
`pickup_restaurant`, `pickup_depot` (alım) — `delivery_house_a`, `delivery_house_b`, `delivery_office` (teslim).

## ADIM D3 — Yeni alım/teslim noktası eklemek

1. Hierarchy'de sağ tık → **Create Empty** → ad ver (örn. `Delivery_Market`).
2. Objeyi Scene'de istediğin konuma taşı (Move aracı).
3. Inspector'da **Add Component** → arama kutusuna `DeliveryPoint` yaz → seç. (Alım noktası için `PickupPoint`.)
4. **Point Id** alanına benzersiz bir ID yaz (örn. `delivery_market`).
5. **Add Component** → `Sphere Collider` → ekle. Inspector'da:
   - **Is Trigger**: İŞARETLE ✓
   - **Radius**: `5`
6. (Opsiyonel marker) Objeye sağ tık → **3D Object → Cylinder** → child olur. Position Y=`6`, Scale (`1.5`, `6`, `1.5`). Cylinder'ın **Capsule Collider**'ını kaldır (bileşen başlığına sağ tık → Remove Component). Cylinder'ı seçip Inspector'ın EN ÜST solundaki aktiflik kutusunun işaretini KALDIR (marker normalde gizli durur). Sonra ana noktayı seç, **Marker Visual** alanına bu Cylinder'ı sürükle.
7. Artık `delivery_market` ID'sini siparişlerde kullanabilirsin (D2).

## ADIM D4 — GPS çizgisini yollara oturtmak (opsiyonel)

Şu an rota düz çizgi. Yol ağı çizmek istersen:

1. Hierarchy'de sağ tık → **Create Empty** → ad `WP_01`. Yolun bir köşesine taşı.
2. **Add Component** → `Waypoint`.
3. Aynı şekilde yol boyunca WP_02, WP_03... oluştur.
4. Her waypoint'i seç → Inspector'da **Neighbors** listesine **+** bas → komşu waypoint'i Hierarchy'den sürükle. (Sadece tek yönde bağlaman yeterli — sistem çift yönlü sayar.)
5. Scene'de sarı çizgiler bağlantıları gösterir. Play'de rota artık bu ağı takip eder.

## ADIM D5 — Kamera hissini ayarlamak

**Cinemachine kullanıyorsan (B5'i yaptıysan):**
1. Hierarchy'de `CM_FollowCamera` seç.
2. Inspector'da **Cinemachine Follow** bileşeni → **Follow Offset** değerleriyle oyna (Y = yükseklik, Z = uzaklık; Z negatif olmalı, örn. -7.5).
3. Yumuşaklık için aynı bileşendeki **Tracker Settings → Position Damping** değerlerini artır/azalt.

**Kod kamerası kullanmak istersen (alternatif):**
1. Project'te sağ tık → **Create → DeliverySim → Camera Settings** → asset oluşur; Inspector'da tüm parametreler (yumuşatma, FOV, duvar koruması, ölü bölge) açıklamalı.
2. Hierarchy'de **Main Camera**'yı seç → **Add Component** → `VehicleCameraController`.
3. **Settings** alanına az önceki asset'i, **Target** alanına aracını sürükle.
4. `CM_FollowCamera` objesini seç → Inspector'ın en üstündeki aktiflik kutusunu KAPAT. Main Camera'daki **Cinemachine Brain** bileşenini de kapat (bileşen adının solundaki kutucuk).
   (İki kamera sistemi aynı anda AÇIK OLMASIN.)

---

# BÖLÜM E — ANA MENÜ SAHNESİ (opsiyonel, MVP sonrası)

1. **File → New Scene** → **Basic (Built-in)** / boş şablon → **Create**.
2. **Ctrl+S** → konum: `Assets/_Uber Simulator/Scenes` → ad: `MainMenu` → Save.
3. Hierarchy'de sağ tık → **UI → Canvas**.
4. Canvas'a sağ tık → **UI → Legacy → Button** → 3 kez tekrarla (3 buton).
5. Butonları alt alta diz (Scene'de 2D moduna geçip sürükle). Her butonun içindeki **Text** child'ına tıkla, Inspector'da Text alanına sırasıyla: `Yeni Oyun`, `Devam Et`, `Çıkış`.
6. Hierarchy'de sağ tık → **Create Empty** → ad `MenuController` → **Add Component** → `MainMenuController`.
7. Inspector'da **Gameplay Scene Name** = `MainScene` yaz.
8. Her butonu bağla:
   a. Butonu seç → Inspector'da **Button** bileşeni → **On Click ()** kutusunun **+**'sına bas.
   b. Boş alana Hierarchy'den `MenuController` objesini sürükle.
   c. Sağdaki "No Function" menüsüne tıkla → **MainMenuController** → sırasıyla: `StartNewGame ()` / `ContinueGame ()` / `QuitGame ()`.
9. Bu sahneye de manager lazım: üst menü **DeliverySim → Setup → 1 - Create Managers** (UI Bootstrap'lı `_UI` objesi menü sahnesinde gereksiz — `_UI` objesini silebilirsin).
10. **File → Build Profiles** (eski adıyla Build Settings) → **Scene List** → **Add Open Scenes** ile önce MainMenu'yü ekle. Sonra MainScene'i açıp aynısını yap. MainMenu listede EN ÜSTTE (index 0) olsun — değilse sürükleyerek sırala.

**✓ KONTROL:** MainMenu sahnesinde Play → "Yeni Oyun" → MainScene yüklenir ve oyun başlar.

---

# BÖLÜM F — İLERİ FAZLAR (kısa yol haritası)

- **Ses:** `_Managers` → **Audio Manager** Inspector'ında 7 boş clip alanı var (müzik, kabul, alım, teslim, başarısız, para, hata). Ses dosyalarını Project'e at, alanlara sürükle. Bitti — kod bağlantısı otomatik.
- **Gerçek UI:** Kendi Canvas'ını yapacağın gün: `_UI` → **UI Bootstrap** → **Build On Start** işaretini kaldır. Controller scriptlerini (HUDController vb.) kendi panellerine ekleyip Text referanslarını Inspector'dan bağla. İstersen o gün bana "TMP'ye geçir" de — kodu ben değiştiririm.
- **Performans:** Hareketsiz çevre objelerini seç → Inspector sağ üst **Static** işaretle. Büyük haritada: **Window → Rendering → Occlusion Culling → Bake**.
- **Build:** **Edit → Project Settings → Player** (isim/ikon/versiyon) → **File → Build Profiles → Windows → Build**. Kayıt dosyası konumu: `%USERPROFILE%\AppData\LocalLow\<Şirket>\<Ürün>\deliverysim_save.json`.
- **Steam:** partner.steamgames.com hesabı (100$ başvuru ücreti, onay günler sürer) → sayfa materyalleri ASSET_NEEDS.md'de. Steamworks entegrasyonu istediğinde kod tarafını ben yazarım.

---

# SORUN GİDERME (hızlı başvuru)

| Belirti | Muhtemel sebep | Çözüm |
|---|---|---|
| Sipariş paneli açılınca kamera değişiyor | Eski sürümde ikisi de Tab'daydı | Düzeltildi: siparişler **O**, kamera **Tab**. Unity'de scriptler yeniden derlensin |
| Araç takla attı, ters kaldı | Normal kaza | **R** tuşu aracı olduğu yerde düzeltir (Setup 5 veya Setup 2'yi bir kez çalıştırmış olman gerekir) |
| Sipariş ALANINA GİRERKEN araç fırlıyor/takla atıyor | Süspansiyon ışını görünmez trigger küresine çarpıyordu | Kodda düzeltildi (trigger'lar yok sayılıyor) — scriptler derlensin, ekstra adım yok |
| Araç virajda devriliyor | Gerçekçi profil uygulanmamış | **DeliverySim → Setup → 6** çalıştır → Ctrl+S. Hâlâ devriliyorsa `Anti Roll Stiffness` artır |
| Kamera sarsak/titriyor | Rigidbody Interpolation kapalı kalmış | **DeliverySim → Setup → 5** çalıştır (Interpolation'ı AÇIK yapar) → Ctrl+S |
| DeliverySim menüsü yok | Derleme bitmedi veya hata var | Console'daki kırmızı satırı bana gönder |
| Play'de UI görünmüyor | UIBootstrap kapalı veya `_UI` yok | B3'ü tekrar çalıştır, Build On Start işaretli mi bak |
| "Yükü Al [F]" hiç çıkmıyor | Noktada trigger collider yok / araçta VehicleInteractor yok | B4 ve B6'yı tekrar çalıştır (güvenli, kopya üretmez) |
| Tab panelinde teklif yok | OrderManager havuzu boş | B6'yı çalıştır; `_Gameplay` → Order Manager → Order Pool dolu mu bak |
| Kabul'e basınca "noktalar sahnede eksik" | OrderData ID'si sahnedeki Point Id ile uyuşmuyor | ID'leri karşılaştır (büyük/küçük harf dahil birebir aynı olmalı) |
| Kamera titriyor | Rigidbody Interpolate kapalı | B4'ü tekrar çalıştır (otomatik açar) |
| Kamera dönüp duruyor / iki kamera kavgası | İki kamera sistemi aynı anda aktif | D5'in son maddesi: birini kapat |
| Tuşlara basınca InvalidOperationException | Active Input Handling "New only" | A2'yi uygula |
| Para hiç artmıyor | EconomyManager sahnede yok | B3'ü tekrar çalıştır |
