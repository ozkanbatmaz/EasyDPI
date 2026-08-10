<div align="center">

<img src="docs/logo.png" width="88" alt="EasyDPI">

# EasyDPI

**Windows'ta operatör seviyesindeki site engellerini aşar. Tek tık, ayar yok.**

Türkçe · [English](README.md)

### [EasyDPI'ı indir](../../releases/latest)

<img src="docs/screenshot-tr.png" width="300" alt="EasyDPI ana pencere">

</div>

---

## Ne işe yarar

Birçok ülkede operatörler siteleri **iki ayrı katmanda** engeller. Sadece birini çözen araçlar yarım kalır, sayfalar yine açılmaz:

| Katman | Operatör ne yapıyor | EasyDPI ne yapıyor |
|---|---|---|
| **DNS** | Alan adı sorgularına engel sayfasına işaret eden sahte bir adres dönüyor. Farklı bir DNS sunucusu yazmak da genelde çözmez, çünkü engelli adlar için 53 numaralı porta giden sorgular düşürülüyor. | Yerelde şifreli bir DNS çözümleyicisi çalıştırır. Operatör sorguların içeriğini göremediği için filtreleyemez. |
| **İnceleme** | Bağlantıların içi okunuyor ve engelli bir hedef tanındığında el sıkışma ortasında kesiliyor. | Giden paketleri, incelemenin eşleştirebileceği bütün bir ad göremeyeceği şekilde yeniden biçimlendirir. |

İki katman da sistem genelinde çalışır; tarayıcı **ve** masaüstü uygulamaları kapsanır. Uygulama başına ayar gerekmez.

## Başlangıç

1. Son sürümü indir, istediğin bir klasöre çıkar
2. `EasyDPI.exe`'yi çalıştır — servislerini kurabilmek için yönetici izni ister
3. Üç adımlık tanıtımı geç ve ağına uygun ayarı bulmasına izin ver

<div align="center">
<img src="docs/onboarding-tr.png" width="300" alt="İlk açılış">
</div>

EasyDPI arka planda çalışan iki servis kurar; pencereyi kapatsan da bilgisayarı yeniden başlatsan da çalışmaya devam eder. Kapattığında iki servisi de durdurur, DNS ayarlarını eski haline döndürür ve tekrar başlamalarını engeller — makine bıraktığın gibi kalır.

## Otomatik ayar

Bu projenin asıl varlık sebebi bu kısım.

Bir operatörün incelemesini atlatan paket müdahalesi, diğerinde hiçbir işe yaramaz. Bir ülkede çalışan ayar diğerinde düzenli olarak başarısız olur. Çoğu rehber sana sabit bir komut satırı verip en iyisini umar.

EasyDPI bunun yerine senin bağlantını ölçer:

```
1) DNS kontrolü
   discord.com         müdahaleli (sahte adres)
   roblox.com          müdahaleli (sahte adres)
   x.com               müdahaleli (sahte adres)
   medium.com          temiz
   -> DNS'e müdahale ediliyor; şifreli DNS açılıyor.

2) Engel kontrolü
   discord.com         engelli
   roblox.com          engelli
   x.com               açık
   medium.com          açık

3) Ayar aranıyor (13 aday)
   -9 --frag-by-sni      0/2
   -9                    0/2
   -5 -q --frag-by-sni   2/2

En iyi ayar: -5 -q --frag-by-sni
```

Nasıl karar veriyor:

- **DNS müdahalesi**, her test alan adını iki kez çözerek anlaşılır — biri sistem çözümleyicisiyle, biri şifreli DNS ile — ve cevaplar karşılaştırılır.
- **Engelleme**, doğru adrese gerçek bir TLS bağlantısı açılarak ölçülür. El sıkışma ortasında kesilen bir bağlantıya müdahale ediliyor demektir.
- **Adaylar** sırayla denenir. Her aday hem kaç engelli siteyi açtığına *hem de* çalışan siteleri bozup bozmadığına göre puanlanır; normal gezinmeyi bozan bir ayar, hiçbir şey yapmamaktan daha kötü puan alır. Arama ilk temiz başarıda durur.

Sonuç `bin/config.ini` dosyasına yazılır. Ağ değiştirdiğinde tekrar çalıştırman yeterli.

**Varsayılanların kapsamadığı bir ağdaysan:** yerleşik test listesi birkaç bölgede yaygın olarak engellenen servisleri kapsar ama her şeyi kapsayamaz. EasyDPI "engelleme yok" derken ihtiyacın olan bir site hâlâ açılmıyorsa, o siteyi `bin/config.ini` içindeki `probeDomains` satırına ekleyip tekrar çalıştır.

## Diller

İngilizce, Türkçe ve Rusça. Windows dilinden otomatik seçilir; kurulum sırasında veya `bin/config.ini` içindeki `language=` ile değiştirilebilir.

Yeni dil eklemek bilerek kolay tutuldu: [`src/Strings.cs`](src/Strings.cs) içindeki İngilizce bloğu kopyala, değerleri çevir, `BuildCatalog()` içine kaydet. Eksik anahtarlar İngilizceye düşer, yani yarım çeviri bile kullanılabilir. Katkılara açık.

## Komut satırı

Betik veya zamanlanmış görev için, yönetici olarak:

```
EasyDPI.exe /auto    ağı ölçer, çalışan ayarı bulur ve uygular
EasyDPI.exe /on      kayıtlı ayarla korumayı açar
EasyDPI.exe /off     korumayı kapatır, DNS'i geri alır
```

Çıktı `easydpi.log` dosyasına yazılır.

## İlk çalıştırmada Windows uyarısı

EasyDPI kod imzası taşımıyor, bu yüzden Microsoft Defender SmartScreen ilk açılışta **"Windows kişisel bilgisayarınızı korudu — Bilinmeyen yayıncı"** uyarısını gösteriyor. **Ek bilgi** deyip **Yine de çalıştır**'a bas; bir daha sormaz.

Bu EasyDPI'a özel değil. SmartScreen, henüz indirme itibarı oluşmamış her imzasız uygulamada aynı uyarıyı verir; imza sertifikası da yılda birkaç yüz dolar tutuyor ve ücretsiz bir araç için karşılığı yok.

Güvenmek yerine doğrulamak istersen, açmadan önce arşivin sağlama toplamına bak:

```powershell
Get-FileHash EasyDPI-1.0.0.zip -Algorithm SHA256
```

Şunu yazdırmalı:

```
9E6C13C8B98A15851D069422D4EDB17004C752B3C5D4639EC60CFEA2DD337C8E
```

Aynı değer sürüm notlarında da yayınlanıyor. Tutuyorsa dosya, buraya yüklenenin bayt bayt aynısıdır.

## Sınırlar

- **IP adresini ve konumunu gizlemez.** Paketlerin biçimini değiştirir, nereden geldiklerini değil. Girdiğin her site gerçek adresini görmeye devam eder. Bunun için VPN gerekir, o da kaçınılmaz olarak gecikme ekler.
- **Sadece Windows.** Bir Windows paket sürücüsüne dayanır.
- **Bazı operatörler bu yöntemle aşılamaz.** Hiçbir aday çalışmazsa EasyDPI bunu açıkça söyler, çalışıyormuş gibi yapmaz.
- **Ağa çıkan her çağrı ve sistemde yapılan her değişiklik** [şeffaflık raporunda](TRANSPARENCY.tr.md) belgelendi; gönderilen her ikili dosyanın sağlama toplamı dahil.
- **Ölçülen performans etkisi ihmal edilebilir** — indirme hızında yaklaşık %0.2, yani ölçüm gürültüsü seviyesinde. Ad çözümleme genelde biraz *hızlanır*, çünkü yerel çözümleyici önbellek tutar.

## Klasör yapısı

```
EasyDPI.exe          uygulama
bin/                 engel aşma motoru, paket sürücüsü, config.ini
dns/                 şifreli DNS çözümleyicisi ve yapılandırması
licenses/            üçüncü taraf lisansları
src/                 kaynak kod ve derleme betiği
docs/                bu README'nin görselleri
```

## Kaynaktan derleme

Visual Studio yok, SDK yok, paket yöneticisi yok. .NET Framework 4.x ile gelen C# derleyicisi — her Windows 10 ve 11'de kurulu — yeterli:

```
src\build.cmd
```

`EasyDPI.exe` bir üst klasörde oluşur.

## Teşekkür

EasyDPI bir arayüz ve otomasyon katmanıdır. Asıl işi yapan araçlar başkalarına ait:

| Proje | Geliştirici | Lisans |
|---|---|---|
| [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) | ValdikSS | Apache 2.0 |
| [WinDivert](https://github.com/basil00/WinDivert) | Basil Fierz | LGPLv3 / GPLv3 |
| [dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy) | Frank Denis | ISC |
| [unDraw](https://undraw.co) | Katerina Limpitsouni | unDraw lisansı (ücretsiz, atıf gerekmez) |

Lisans metinlerinin tamamı [`licenses/`](licenses/) klasöründedir. EasyDPI'ın kendi kodu [MIT](LICENSE) lisanslıdır.

## Sorumluluk reddi

Bu araç kendi cihazında, kendi bağlantında erişim sorunlarını gidermek için yazıldı. Nasıl kullanacağın ve bulunduğun yerin mevzuatına uyum senin sorumluluğunda.
