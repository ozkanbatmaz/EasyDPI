# Şeffaflık raporu

EasyDPI bir çekirdek sürücüsü yüklüyor ve DNS ayarlarını değiştiriyor. Bunu
yapan bir aracın kendini açıklaması gerekir; bu sayfa neyin değiştiğini, senin
makinenden ne çıktığını ve bunların hepsini nasıl kendin doğrulayabileceğini
sıralıyor.

Son doğrulama: 10 Ağustos 2026, EasyDPI 1.0.0 üzerinde.

## Sistemde ne değişiyor

| Değişiklik | Ayrıntı |
|---|---|
| `GoodbyeDPI` Windows servisi | Çıkardığın klasördeki `bin\goodbyedpi.exe`'yi gösterecek şekilde kurulur, başlangıç türü Otomatik |
| `dnscrypt-proxy` Windows servisi | `dns\dnscrypt-proxy.exe`'yi gösterecek şekilde kurulur, başlangıç türü Otomatik |
| Çekirdek sürücüsü | `WinDivert64.sys`, giden paketleri inceleyip yeniden biçimlendirmek için GoodbyeDPI tarafından yüklenir |
| DNS ayarları | Aktif adaptörün DNS'i `127.0.0.1` ve `::1` yapılır, sorgular yerel şifreli çözümleyiciye gider |
| Yazılan dosyalar | `bin\config.ini` (ayarların), `easydpi.log` (yalnızca komut satırı modunda), `dns\*.md` (çözümleyici listesi önbelleği) |

Korumayı kapatmak bunların hepsini geri alır: iki servis de durdurulur **ve
devre dışı bırakılır**, DNS DHCP'ye döner. Arkada çalışan veya yeniden
başlamaya ayarlı hiçbir şey kalmaz.

Servisler uygulama klasörünün bulunduğu yeri gösterir. Klasörü taşır veya
silersen önce korumayı kapat; yoksa servisler artık var olmayan dosyaları
göstermeye devam eder.

## Makinenden ne çıkıyor

**EasyDPI'ın kendisi**, yalnızca otomatik ayar bulucu çalışırken:

- `cloudflare-dns.com` ve `dns.google` adreslerine şifreli DNS sorguları —
  sağlayıcının verdiği cevapla karşılaştırmak için güvenilir bir referans olarak
- Test alan adlarına TLS el sıkışması — hangilerinin kesildiğini görmek için

Listenin tamamı bu. Telemetri yok, analitik yok, güncelleme kontrolü yok, çökme
raporu yok, tanımlayıcı yok. Kaynak [`src/`](src/) altında; ağa çıkan her çağrı
[`src/NetworkTools.cs`](src/NetworkTools.cs) içinde.

**dnscrypt-proxy**, koruma açıkken:

- DNS sorguların, şifreli olarak, Cloudflare ve Google DoH sunucularına
- Açılışta `9.9.9.11` ve `8.8.8.8`'e düz DNS — o sunucuları bulabilmek için
- `9.9.9.9`'a bağlantı yoklaması
- Genel çözümleyici listesi `download.dnscrypt.info` ve
  `raw.githubusercontent.com` adreslerinden, yayınlanmış bir minisign anahtarıyla
  doğrulanarak

**GoodbyeDPI** kendi başına hiçbir bağlantı açmaz. Yalnızca zaten makinenden
çıkmakta olan paketleri yeniden biçimlendirir.

## Ne yapmaz

- IP adresini veya konumunu gizlemez. Girdiğin her site gerçek adresini görmeye
  devam eder.
- Trafiğini geliştiricinin kontrolündeki bir sunucudan geçirmez. Öyle bir sunucu
  yok.
- Senin hakkında veya nereye girdiğine dair hiçbir şey toplamaz, saklamaz,
  göndermez.
- Yukarıda anlatılan paket biçimlendirmesi dışında başka uygulamalara veya
  onların trafiğine dokunmaz.

## İkili dosyalar nereden geliyor

EasyDPI, üç ayrı projenin önceden derlenmiş dosyalarını içeriyor. Bunlar o
projelerin kendi derlemeleri; resmi sürüm sayfalarından indirildi ve
değiştirilmeden konuldu. Doğrulamak için upstream arşivleri yeniden indirip
sağlama toplamlarını karşılaştırdım:

| Dosya | Kaynak | SHA-256 | Upstream ile aynı |
|---|---|---|---|
| `bin\goodbyedpi.exe` | [GoodbyeDPI 0.2.3rc3](https://github.com/ValdikSS/GoodbyeDPI/releases/tag/0.2.3rc3) | `8D412B094BB9C137FF25BA9A794D1122ECC84BB776DEBFF6C249723A13CC31CD` | evet |
| `bin\WinDivert.dll` | GoodbyeDPI arşivinin içinde | `6110BFA44667405179C3E15E12AF1B62037E447ED59B054B19042032995E6C7E` | evet |
| `bin\WinDivert64.sys` | GoodbyeDPI arşivinin içinde | `E69B5BA3F0CD6CFB2983E442636E7F0B342B61B15264B0328317D4559C82CF50` | evet |
| `dns\dnscrypt-proxy.exe` | [dnscrypt-proxy 2.1.18](https://github.com/DNSCrypt/dnscrypt-proxy/releases/tag/2.1.18) | `D847F834AEF02F8705A649DC1060F520CDB7931D7361035728770DCE2C16EEB6` | evet |
| `EasyDPI.exe` | bu depodaki [`src/`](src/) klasöründen derlendi | `D95408D8D3A26EE3C8F98591835782EC9CDD414D7D5EFC215F1D154C92402CA9` | — |

Doğrudan upstream arşivlerinden başlamak istersen:

```
goodbyedpi-0.2.3rc3-2.zip        37F96B32D050DADCC930A639EBA68E1CCD57ED5C04A5F77DFCA908F01905A4C5
dnscrypt-proxy-win64-2.1.18.zip  15F0C8F1F40620A54DDFD8752C327DABE1146F84618D68874F79C4F52490B396
```

## Kod imzası

| Dosya | İmza |
|---|---|
| `WinDivert64.sys` | Geçerli, Cloudveil Technology Inc. |
| diğerlerinin hepsi | İmzasız |

Çekirdek sürücüsü imzalı, çünkü 64-bit Windows imzasız sürücü yüklemez. Geri
kalanı imzasız; SmartScreen'in ilk açılışta uyarmasının sebebi bu — bkz.
[README](README.tr.md#i̇lk-çalıştırmada-windows-uyarısı).

## Antivirüs

10 Ağustos 2026'da klasörün tamamına yapılan Windows Defender taraması hiçbir
tehdit bulmadı; gerçek zamanlı koruma açıktı ve imzalar aynı güne aitti.

Başka motorlar yine de `goodbyedpi.exe` veya WinDivert dosyalarını **HackTool**
ya da **RiskWare** olarak işaretleyebilir. Bu sınıflandırma niyetle değil
yetenekle ilgili: bu yazılım gerçekten bir paket yakalama sürücüsü kuruyor ve
gerçekten ağ trafiğini yeniden yazıyor — kötü niyetli bir aracın yetenek profili
de aynı görünür. Antivirüsün itiraz ederse sebebi budur.

## Bunları kendin nasıl doğrularsın

- **Oku.** Uygulama [`src/`](src/) altında yaklaşık 2.500 satır C#. Ağa çıkan her
  çağrı `NetworkTools.cs` içinde; sistemde yapılan her değişiklik
  `ServiceManager.cs` ve `BypassController.cs` içinde.
- **Derle.** `src\build.cmd` uygulamayı Windows ile gelen C# derleyicisiyle
  derler. SDK yok, paket yöneticisi yok, derleme sırasında hiçbir şey
  indirilmiyor. İstersen bizimki yerine kendi derlediğini kullan.
- **İndirmeyi doğrula.** `Get-FileHash EasyDPI-1.0.0.zip -Algorithm SHA256`
  çalıştırıp sürüm notlarındaki değerle karşılaştır.
- **İzle.** Koruma açıkken bir paket yakalama aracı çalıştır ve yukarıdaki
  listenin eksiksiz olduğunu kendin gör.

Bu sayfanın anlatmadığı bir şey bulursan lütfen bir issue aç.
