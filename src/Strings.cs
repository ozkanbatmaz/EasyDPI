using System;
using System.Collections.Generic;
using System.Globalization;

namespace EasyDPI
{
    /// <summary>
    /// Minimal localization layer.
    ///
    /// The UI language is picked from the operating system on first run and can be
    /// overridden with "language=" in config.ini. Any key missing from a translation
    /// falls back to English, so a partial translation is still usable.
    ///
    /// Adding a language: copy the English block, translate the values, and register
    /// it in BuildCatalog(). Nothing else needs to change.
    /// </summary>
    static class Strings
    {
        static readonly Dictionary<string, Dictionary<string, string>> Catalog = BuildCatalog();

        static Dictionary<string, string> active;
        static Dictionary<string, string> fallback;
        static string activeCode = "en";

        /// <summary>Two-letter code of the language currently in use.</summary>
        public static string CurrentLanguage { get { return activeCode; } }

        /// <summary>Language codes available in this build, English first.</summary>
        public static string[] AvailableLanguages
        {
            get
            {
                List<string> codes = new List<string>();
                codes.Add("en");
                foreach (KeyValuePair<string, Dictionary<string, string>> entry in Catalog)
                    if (entry.Key != "en") codes.Add(entry.Key);
                return codes.ToArray();
            }
        }

        /// <summary>
        /// Selects the UI language. Pass "auto" (or null) to detect it from the
        /// operating system; pass a two-letter code to force one.
        /// </summary>
        public static void Initialize(string preferredCode)
        {
            fallback = Catalog["en"];

            string code = preferredCode;
            if (string.IsNullOrEmpty(code) || code.ToLowerInvariant() == "auto")
            {
                try { code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; }
                catch { code = "en"; }
            }

            code = (code == null) ? "en" : code.ToLowerInvariant().Trim();

            if (Catalog.ContainsKey(code)) { active = Catalog[code]; activeCode = code; }
            else { active = fallback; activeCode = "en"; }
        }

        /// <summary>Looks up a key. Falls back to English, then to the key itself.</summary>
        public static string Get(string key)
        {
            if (active == null) Initialize(null);

            string value;
            if (active.TryGetValue(key, out value)) return value;
            if (fallback.TryGetValue(key, out value)) return value;
            return key;
        }

        /// <summary>Looks up a key and substitutes {0}, {1}, ... placeholders.</summary>
        public static string Get(string key, params object[] values)
        {
            try { return string.Format(Get(key), values); }
            catch { return Get(key); }
        }

        // -----------------------------------------------------------------
        // Translations
        // -----------------------------------------------------------------

        static Dictionary<string, Dictionary<string, string>> BuildCatalog()
        {
            Dictionary<string, Dictionary<string, string>> catalog =
                new Dictionary<string, Dictionary<string, string>>();

            // ---------------- English ----------------
            Dictionary<string, string> en = new Dictionary<string, string>();

            en["status.on"] = "Protected";
            en["status.off"] = "Not protected";
            en["subtitle.on"] = "Blocked sites are reachable";
            en["subtitle.off"] = "Normal internet connection";
            en["button.turnOn"] = "Turn on";
            en["button.turnOff"] = "Turn off";
            en["button.turningOn"] = "Turning on...";
            en["button.turningOff"] = "Turning off...";
            en["button.testing"] = "Testing...";
            en["link.autoTune"] = "Find the best settings for my network";
            en["link.details"] = "Details";
            en["link.hideDetails"] = "Hide details";
            en["footer.persists"] = "This state survives a restart.";

            en["details.bypassService"] = "Bypass service";
            en["details.dnsService"] = "Encrypted DNS";
            en["details.dnsAddress"] = "DNS address";
            en["details.activeSettings"] = "Active settings";

            en["service.running"] = "running";
            en["service.stopped"] = "stopped";
            en["service.starting"] = "starting";
            en["service.stopping"] = "stopping";
            en["service.notInstalled"] = "not installed";
            en["service.unknown"] = "unknown";

            en["dns.noAdapter"] = "no adapter found";
            en["dns.notSet"] = "not set";
            en["dns.unreadable"] = "could not read";

            en["confirm.autoTune.title"] = "Automatic configuration";
            en["confirm.autoTune.body"] =
                "Your network will be tested to find the settings that work.\n\n" +
                "Your connection may drop for a few seconds during the test.\nThis takes about 1-3 minutes.\n\n" +
                "Continue?";

            en["warn.missingBypassBinary"] = "Warning: bin\\goodbyedpi.exe is missing.";
            en["warn.missingDnsBinary"] = "Warning: dns\\dnscrypt-proxy.exe is missing.";
            en["incomplete.title"] = "This copy is missing the programs it runs.";
            en["incomplete.fromArchive"] = "   It was started from inside the downloaded archive. Windows unpacks only the one file you double-click and runs it on its own, without the bin and dns folders. Extract the whole archive to a folder first, then run EasyDPI.exe from there.";
            en["incomplete.filesMissing"] = "   The bin and dns folders are not next to EasyDPI.exe. Keep the folder from the archive together, or download it again.";
            en["incomplete.where"] = "   Running from: {0}";
            en["incomplete.status"] = "Not ready";
            en["incomplete.statusFromArchive"] = "Extract the archive first";
            en["incomplete.statusMissing"] = "Files are missing next to EasyDPI.exe";

            en["log.turningOn"] = "Turning on...";
            en["log.errorMissingBinary"] = "ERROR: bin\\goodbyedpi.exe not found.";
            en["log.encryptedDnsActive"] = "Encrypted DNS is active.";
            en["log.dnsServiceFailed"] = "Warning: the DNS service did not start, so DNS settings were left untouched.";
            en["log.dnsNotNeeded"] = "DNS redirection is not needed on this network.";
            en["log.turnedOn"] = "Turned on. Settings: {0}";
            en["log.serviceFailed"] = "ERROR: the service did not start.";
            en["log.turningOff"] = "Turning off...";
            en["log.turnedOff"] = "Turned off. DNS restored to its default.";
            en["log.error"] = "ERROR: {0}";

            en["tune.start"] = "Network test started.";
            en["tune.step1"] = "1) DNS check";
            en["tune.dnsNoAnswer"] = "tampered (no answer)";
            en["tune.dnsFakeAddress"] = "tampered (fake address)";
            en["tune.dnsClean"] = "clean";
            en["tune.noInternet"] = "ERROR: could not reach the internet.";
            en["tune.dnsTamperedResult"] = "   -> DNS is being tampered with; enabling encrypted DNS.";
            en["tune.dnsCleanResult"] = "   -> DNS is clean; leaving it alone.";
            en["tune.warnDnsStart"] = "   Warning: the DNS service did not start.";
            en["tune.warnDnsMissing"] = "   Warning: dnscrypt-proxy was not found.";
            en["tune.step2"] = "2) Blocking scan ({0} addresses, {1} services)";
            en["tune.allOpen"] = "all open";
            en["tune.closedList"] = "closed: {0}";
            en["tune.noBlocking"] = "   -> No blocking detected on this network.";
            en["tune.addYourOwn"] = "   If a site you need is still blocked, add it to probeDomains in bin/config.ini.";
            en["tune.step3"] = "3) Screening every setting ({0} candidates)";
            en["tune.step4"] = "4) Measuring the best {0} for speed";
            en["tune.speedOnly"] = "   -> Still choosing the fastest setting for this connection.";
            en["tune.colSetting"] = "setting";
            en["tune.colOpen"] = "opened";
            en["tune.colLatency"] = "response";
            en["tune.colSpeed"] = "download";
            en["tune.candidateFailed"] = "could not start";
            en["tune.sitesBroken"] = "  ({0} normal sites broken)";
            en["tune.nothingWorked"] = "No working configuration was found. This network may require a VPN.";
            en["tune.best"] = "Best settings: {0}";
            en["tune.encryptedDnsVerdict"] = "Encrypted DNS: {0}";
            en["tune.required"] = "required";
            en["tune.notRequired"] = "not required";
            en["tune.vpnDetected"] = "A VPN is connected ({0}).";
            en["tune.vpnCandidates"] = "   Settings that send fake packets are being left out: they are aimed at the route out of this machine, which is not the route your traffic takes inside a tunnel.";
            en["log.vpnActive"] = "A VPN is connected ({0}). Both are on; if something stops working, turn one of them off.";
            en["button.uninstall"] = "Remove EasyDPI";
            en["button.saveReport"] = "Save report";
            en["update.available"] = "A newer version is available: {0}";
            en["update.title"] = "Update available";
            en["button.updating"] = "Updating...";
            en["update.promptManual"] = "EasyDPI {0} has been released. You are running {1}.\n\nThis release cannot be verified automatically, so it will not be installed for you. Open the download page?";
            en["update.downloading"] = "Downloading version {0}...";
            en["update.downloadFailed"] = "The download failed. Nothing was changed.";
            en["update.verifying"] = "Checking the download against its published checksum...";
            en["update.checksumFailed"] = "The download does not match its published checksum. It was discarded and nothing was changed.";
            en["update.contentsUnexpected"] = "The archive does not contain EasyDPI. Nothing was changed.";
            en["update.swapFailed"] = "The update could not be started. Nothing was changed.";
            en["update.failed"] = "The update failed: {0}";
            en["update.restarting"] = "Update ready. Restarting to install it...";
            en["update.prompt"] = "EasyDPI {0} has been released. You are running {1}.\n\nDownload and install it now? EasyDPI will restart when it is done.";
            en["report.dialogTitle"] = "Save diagnostic report";
            en["report.fileType"] = "Text file";
            en["report.saved"] = "Report saved: {0}";
            en["report.failed"] = "The report could not be saved: {0}";
            en["button.uninstalling"] = "Removing...";
            en["uninstall.confirmTitle"] = "Remove EasyDPI?";
            en["uninstall.confirmBody"] = "This stops and unregisters both services, removes the packet driver, hands DNS back to your network, and deletes EasyDPI's own files from:\n\n{0}\n\nOnly files EasyDPI installed are deleted; anything else in that folder is left alone. This cannot be undone.";
            en["uninstall.started"] = "Removing EasyDPI...";
            en["uninstall.bypassRemoved"] = "Bypass service stopped and unregistered.";
            en["uninstall.driverRemoved"] = "Packet driver unregistered.";
            en["uninstall.dnsRemoved"] = "Encrypted DNS service stopped and unregistered.";
            en["uninstall.dnsRestored"] = "DNS handed back to your network.";
            en["uninstall.filesScheduled"] = "Files will be deleted as soon as this window closes.";
            en["uninstall.filesFailed"] = "The files could not be scheduled for deletion. Delete this folder by hand: {0}";
            en["uninstall.closing"] = "Done. Closing.";
            en["log.dnsFailedConsequence"] = "Sites whose addresses your provider forges will stay unreachable until this is fixed; the bypass settings cannot work around a wrong address.";
            en["dnsfail.missingBinary"] = "dnscrypt-proxy.exe is missing from the dns folder.";
            en["dnsfail.notInstalled"] = "The dnscrypt-proxy service could not be registered.";
            en["dnsfail.portTaken"] = "Port 53 is already in use by {0}. Close it, or stop that service, and try again.";
            en["dnsfail.didNotStart"] = "dnscrypt-proxy was installed but did not reach the running state.";
            en["tune.dnsAbort"] = "Stopping here. Your provider is forging DNS answers and the encrypted resolver is not running, so every address on this machine is currently whatever the provider says it is. Any measurement taken now would describe that, not your connection.";
            en["tune.dnsAbortHint"] = "   Settings were left unchanged. Fix the resolver above, then run this again.";
            en["tune.bestSpeed"] = "Response {0}, download {1}";
            en["tune.stillBlocked"] = "Still unreachable with these settings: {0}";
            en["tune.stillBlockedHint"] = "   These may be blocked at the provider itself, which needs a VPN rather than different settings.";
            en["tune.elapsed"] = "Measured in {0} min {1} s.";
            en["probe.yours"] = "Yours";

            en["menu.showIntro"] = "Show the introduction again";
            en["menu.openConfig"] = "Open the configuration folder";
            en["menu.targetedScope"] = "Apply only to blocked addresses";
            en["scope.targeted"] = "Scope: only the {0} addresses found blocked. Everything else travels untouched.";
            en["scope.all"] = "Scope: every connection this machine makes.";
            en["scope.noList"] = "Nothing has been measured yet, so there is no list to narrow to. Find the best settings first.";
            en["tune.targetedScope"] = "   Scope is targeted: the engine will be limited to these {0} addresses.";
            en["pill.on"] = "You are protected";
            en["pill.off"] = "Not protected";
            en["details.dnsShort"] = "DNS";
            en["settings.tooltip"] = "Open the configuration folder";
            en["tab.status"] = "Status";
            en["tab.log"] = "Activity";
            en["tab.advanced"] = "Advanced";
            en["advanced.modeTitle"] = "Where the bypass applies";
            en["advanced.automatic"] = "Automatic";
            en["advanced.manual"] = "Choose myself";
            en["advanced.automaticHint"] = "Every connection this machine makes. Nothing is missed.";
            en["advanced.manualHint"] = "Only the services ticked below. Everything else travels untouched.";
            en["advanced.servicesTitle"] = "Services";
            en["advanced.blockedHere"] = "blocked here";
            en["advanced.addresses"] = "addresses";
            en["group.robloxclient.name"] = "Roblox game";
            en["group.robloxclient.examples"] = "Joining games, installing, updating";
            en["group.robloxsite.name"] = "Roblox site";
            en["group.robloxsite.examples"] = "Profiles, server lists, sign-in, chat";
            en["group.robloxcdn.name"] = "Roblox images";
            en["group.robloxcdn.examples"] = "Thumbnails, avatars, page content";
            en["group.discord.name"] = "Discord";
            en["group.discord.examples"] = "Chat, voice, attachments, updates";
            en["group.social.name"] = "Social media";
            en["group.social.examples"] = "X, Instagram, Telegram, WhatsApp, Reddit";
            en["group.games.name"] = "Game stores";
            en["group.games.examples"] = "Steam, Epic Games, EA";
            en["group.media.name"] = "Video and music";
            en["group.media.examples"] = "YouTube, Twitch, Spotify, Vimeo, SoundCloud";
            en["group.reference.name"] = "News and reference";
            en["group.reference.examples"] = "Wikipedia, BBC, ChatGPT, news sites";
            en["group.yours.name"] = "Your own list";
            en["group.yours.examples"] = "From probeDomains in config.ini";
            en["advanced.customTitle"] = "Addresses of your own";
            en["advanced.customHint"] = "A site name, not a link. Subdomains are covered too.";
            en["advanced.customExample"] = "reddit.com, ebay.de";
            en["advanced.rejected"] = "Not usable as addresses, so they were left out: {0}";
            en["advanced.apply"] = "Apply";
            en["advanced.applied"] = "Coverage: {0} services, {1} addresses.";
            en["advanced.nothingChosen"] = "Nothing is ticked, so there would be nothing to cover. Choose a service or add an address.";
            en["onboarding.language"] = "Language";
            en["onboarding.next"] = "Continue";
            en["onboarding.finish"] = "Find my settings";
            en["onboarding.skip"] = "Skip for now";
            en["onboarding.welcome.title"] = "Welcome to EasyDPI";
            en["onboarding.welcome.body"] =
                "Some providers block websites before they ever reach you. EasyDPI gets past that, " +
                "with nothing for you to configure.";
            en["onboarding.how.title"] = "How it works";
            en["onboarding.how.body"] =
                "Your provider redirects domain lookups and inspects the traffic itself. EasyDPI encrypts " +
                "the lookups and reshapes the traffic, so neither check gets what it needs.";
            en["onboarding.ready.title"] = "Before you start";
            en["onboarding.ready.body"] =
                "Two background services will be installed, which needs administrator rights. Working " +
                "settings differ by provider, so EasyDPI measures your network and picks the right ones.";

            catalog["en"] = en;

            // ---------------- Türkçe ----------------
            Dictionary<string, string> tr = new Dictionary<string, string>();

            tr["status.on"] = "Koruma açık";
            tr["status.off"] = "Koruma kapalı";
            tr["subtitle.on"] = "Engellenen siteler açılabiliyor";
            tr["subtitle.off"] = "Normal internet bağlantısı";
            tr["button.turnOn"] = "Aç";
            tr["button.turnOff"] = "Kapat";
            tr["button.turningOn"] = "Açılıyor...";
            tr["button.turningOff"] = "Kapatılıyor...";
            tr["button.testing"] = "Test ediliyor...";
            tr["link.autoTune"] = "Ağıma en uygun ayarı bul";
            tr["link.details"] = "Ayrıntılar";
            tr["link.hideDetails"] = "Ayrıntıları gizle";
            tr["footer.persists"] = "Bu durum yeniden başlatmada da korunur.";

            tr["details.bypassService"] = "Engel aşma servisi";
            tr["details.dnsService"] = "Şifreli DNS";
            tr["details.dnsAddress"] = "DNS adresi";
            tr["details.activeSettings"] = "Kullanılan ayar";

            tr["service.running"] = "çalışıyor";
            tr["service.stopped"] = "durduruldu";
            tr["service.starting"] = "başlıyor";
            tr["service.stopping"] = "duruyor";
            tr["service.notInstalled"] = "kurulu değil";
            tr["service.unknown"] = "bilinmiyor";

            tr["dns.noAdapter"] = "adaptör bulunamadı";
            tr["dns.notSet"] = "ayarlı değil";
            tr["dns.unreadable"] = "okunamadı";

            tr["confirm.autoTune.title"] = "Otomatik ayar";
            tr["confirm.autoTune.body"] =
                "Ağın test edilerek çalışan ayar bulunacak.\n\n" +
                "Bu sırada bağlantın birkaç saniyeliğine kesilebilir.\nİşlem yaklaşık 1-3 dakika sürer.\n\n" +
                "Devam edilsin mi?";

            tr["warn.missingBypassBinary"] = "Uyarı: bin\\goodbyedpi.exe bulunamadı.";
            tr["warn.missingDnsBinary"] = "Uyarı: dns\\dnscrypt-proxy.exe bulunamadı.";
            tr["incomplete.title"] = "Bu kopyanın yanında çalıştıracağı programlar yok.";
            tr["incomplete.fromArchive"] = "   İndirdiğin arşivin içinden çalıştırılmış. Windows, çift tıkladığın tek dosyayı geçici bir yere açıp orada tek başına çalıştırıyor; bin ve dns klasörleri yanında olmuyor. Önce arşivin tamamını bir klasöre çıkar, sonra EasyDPI.exe'yi oradan çalıştır.";
            tr["incomplete.filesMissing"] = "   bin ve dns klasörleri EasyDPI.exe'nin yanında değil. Arşivden çıkan klasörü bir arada tut ya da yeniden indir.";
            tr["incomplete.where"] = "   Çalıştığı yer: {0}";
            tr["incomplete.status"] = "Hazır değil";
            tr["incomplete.statusFromArchive"] = "Önce arşivi çıkar";
            tr["incomplete.statusMissing"] = "EasyDPI.exe yanındaki dosyalar eksik";

            tr["log.turningOn"] = "Açılıyor...";
            tr["log.errorMissingBinary"] = "HATA: bin\\goodbyedpi.exe bulunamadı.";
            tr["log.encryptedDnsActive"] = "Şifreli DNS devrede.";
            tr["log.dnsServiceFailed"] = "Uyarı: DNS servisi başlatılamadı, DNS ayarına dokunulmadı.";
            tr["log.dnsNotNeeded"] = "Bu ağda DNS yönlendirmesi gerekmiyor.";
            tr["log.turnedOn"] = "Açıldı. Ayar: {0}";
            tr["log.serviceFailed"] = "HATA: servis başlatılamadı.";
            tr["log.turningOff"] = "Kapatılıyor...";
            tr["log.turnedOff"] = "Kapandı. DNS normale döndü.";
            tr["log.error"] = "HATA: {0}";

            tr["tune.start"] = "Ağ testi başladı.";
            tr["tune.step1"] = "1) DNS kontrolü";
            tr["tune.dnsNoAnswer"] = "müdahaleli (cevap yok)";
            tr["tune.dnsFakeAddress"] = "müdahaleli (sahte adres)";
            tr["tune.dnsClean"] = "temiz";
            tr["tune.noInternet"] = "HATA: internete ulaşılamadı.";
            tr["tune.dnsTamperedResult"] = "   -> DNS'e müdahale ediliyor; şifreli DNS açılıyor.";
            tr["tune.dnsCleanResult"] = "   -> DNS temiz; dokunulmayacak.";
            tr["tune.warnDnsStart"] = "   Uyarı: DNS servisi başlatılamadı.";
            tr["tune.warnDnsMissing"] = "   Uyarı: dnscrypt-proxy bulunamadı.";
            tr["tune.step2"] = "2) Engel taraması ({0} adres, {1} servis)";
            tr["tune.allOpen"] = "hepsi açık";
            tr["tune.closedList"] = "kapalı: {0}";
            tr["tune.noBlocking"] = "   -> Bu ağda engelleme tespit edilmedi.";
            tr["tune.addYourOwn"] = "   İhtiyacın olan site hâlâ engelliyse bin/config.ini içindeki probeDomains satırına ekle.";
            tr["tune.step3"] = "3) Tüm ayarlar eleniyor ({0} aday)";
            tr["tune.step4"] = "4) En iyi {0} aday hız için ölçülüyor";
            tr["tune.speedOnly"] = "   -> Yine de bu bağlantı için en hızlı ayar seçilecek.";
            tr["tune.colSetting"] = "ayar";
            tr["tune.colOpen"] = "açılan";
            tr["tune.colLatency"] = "yanıt";
            tr["tune.colSpeed"] = "indirme";
            tr["tune.candidateFailed"] = "başlatılamadı";
            tr["tune.sitesBroken"] = "  ({0} normal site bozuldu)";
            tr["tune.nothingWorked"] = "Çalışan bir ayar bulunamadı. Bu ağ için VPN gerekebilir.";
            tr["tune.best"] = "En iyi ayar: {0}";
            tr["tune.encryptedDnsVerdict"] = "Şifreli DNS: {0}";
            tr["tune.required"] = "gerekli";
            tr["tune.notRequired"] = "gerekmiyor";
            tr["tune.vpnDetected"] = "Bir VPN bağlı ({0}).";
            tr["tune.vpnCandidates"] = "   Sahte paket gönderen ayarlar listeden çıkarıldı: onlar bu makineden çıkan yola göre hesaplanıyor, tünel içindeki trafiğin gittiği yol ise o değil.";
            tr["log.vpnActive"] = "Bir VPN bağlı ({0}). İkisi de açık; bir şey çalışmamaya başlarsa birini kapat.";
            tr["button.uninstall"] = "Uygulamayı Sil";
            tr["button.saveReport"] = "Rapor İndir";
            tr["update.available"] = "Daha yeni bir sürüm var: {0}";
            tr["update.title"] = "Güncelleme var";
            tr["button.updating"] = "Güncelleniyor...";
            tr["update.promptManual"] = "EasyDPI {0} yayınlandı. Sende {1} yüklü.\n\nBu sürüm otomatik olarak doğrulanamıyor, o yüzden senin yerine kurmuyorum. İndirme sayfasını açayım mı?";
            tr["update.downloading"] = "{0} sürümü indiriliyor...";
            tr["update.downloadFailed"] = "İndirme başarısız oldu. Hiçbir şey değişmedi.";
            tr["update.verifying"] = "İndirilen dosya, yayınlanan sağlama toplamıyla karşılaştırılıyor...";
            tr["update.checksumFailed"] = "İndirilen dosya yayınlanan sağlama toplamıyla uyuşmuyor. Dosya atıldı, hiçbir şey değişmedi.";
            tr["update.contentsUnexpected"] = "Arşivin içinde EasyDPI yok. Hiçbir şey değişmedi.";
            tr["update.swapFailed"] = "Güncelleme başlatılamadı. Hiçbir şey değişmedi.";
            tr["update.failed"] = "Güncelleme başarısız: {0}";
            tr["update.restarting"] = "Güncelleme hazır. Kurmak için yeniden başlatılıyor...";
            tr["update.prompt"] = "EasyDPI {0} yayınlandı. Sende {1} yüklü.\n\nŞimdi indirilip kurulsun mu? İşlem bitince EasyDPI yeniden başlayacak.";
            tr["report.dialogTitle"] = "Tanılama raporunu kaydet";
            tr["report.fileType"] = "Metin dosyası";
            tr["report.saved"] = "Rapor kaydedildi: {0}";
            tr["report.failed"] = "Rapor kaydedilemedi: {0}";
            tr["button.uninstalling"] = "Siliniyor...";
            tr["uninstall.confirmTitle"] = "EasyDPI silinsin mi?";
            tr["uninstall.confirmBody"] = "İki servis de durdurulup kaydı silinecek, paket sürücüsü kaldırılacak, DNS ağına geri verilecek ve EasyDPI'ın kendi dosyaları şuradan silinecek:\n\n{0}\n\nYalnız EasyDPI'ın kurduğu dosyalar silinir; o klasördeki başka hiçbir şeye dokunulmaz. Bu işlem geri alınamaz.";
            tr["uninstall.started"] = "EasyDPI kaldırılıyor...";
            tr["uninstall.bypassRemoved"] = "Atlatma servisi durduruldu ve kaydı silindi.";
            tr["uninstall.driverRemoved"] = "Paket sürücüsünün kaydı silindi.";
            tr["uninstall.dnsRemoved"] = "Şifreli DNS servisi durduruldu ve kaydı silindi.";
            tr["uninstall.dnsRestored"] = "DNS ağına geri verildi.";
            tr["uninstall.filesScheduled"] = "Dosyalar, bu pencere kapanır kapanmaz silinecek.";
            tr["uninstall.filesFailed"] = "Dosya silme işlemi başlatılamadı. Bu klasörü elle sil: {0}";
            tr["uninstall.closing"] = "Bitti. Kapanıyor.";
            tr["log.dnsFailedConsequence"] = "Sağlayıcının adresini sahtelediği siteler bu düzelene kadar açılmaz; yanlış bir adresi hiçbir atlatma ayarı kurtaramaz.";
            tr["dnsfail.missingBinary"] = "dns klasöründe dnscrypt-proxy.exe yok.";
            tr["dnsfail.notInstalled"] = "dnscrypt-proxy servisi kaydedilemedi.";
            tr["dnsfail.portTaken"] = "53 numaralı portu zaten {0} kullanıyor. Onu kapat ya da o servisi durdurup tekrar dene.";
            tr["dnsfail.didNotStart"] = "dnscrypt-proxy kuruldu ama çalışır duruma geçmedi.";
            tr["tune.dnsAbort"] = "Burada duruyorum. Sağlayıcın DNS cevaplarını sahteliyor ve şifreli çözümleyici çalışmıyor; yani şu an bu makinedeki her adres sağlayıcının söylediği şey. Şimdi yapılacak ölçüm senin bağlantını değil onu anlatır.";
            tr["tune.dnsAbortHint"] = "   Ayarlara dokunulmadı. Yukarıdaki çözümleyici sorununu gider, sonra tekrar çalıştır.";
            tr["tune.bestSpeed"] = "Yanıt {0}, indirme {1}";
            tr["tune.stillBlocked"] = "Bu ayarla hâlâ ulaşılamıyor: {0}";
            tr["tune.stillBlockedHint"] = "   Bunlar doğrudan sağlayıcı tarafından kapatılmış olabilir; farklı ayar değil VPN gerekir.";
            tr["tune.elapsed"] = "Ölçüm süresi: {0} dk {1} sn.";
            tr["probe.yours"] = "Senin listen";

            tr["menu.showIntro"] = "Tanıtımı tekrar göster";
            tr["menu.openConfig"] = "Yapılandırma klasörünü aç";
            tr["menu.targetedScope"] = "Yalnız engelli adreslere uygula";
            tr["scope.targeted"] = "Kapsam: yalnız engelli bulunan {0} adres. Diğer her şey el değmeden geçiyor.";
            tr["scope.all"] = "Kapsam: bu makinenin kurduğu her bağlantı.";
            tr["scope.noList"] = "Henüz ölçüm yapılmadı, daraltılacak bir liste yok. Önce en iyi ayarları bul.";
            tr["tune.targetedScope"] = "   Kapsam daraltılmış: motor yalnız bu {0} adresle sınırlı olacak.";
            tr["pill.on"] = "Korunuyorsunuz";
            tr["pill.off"] = "Koruma yok";
            tr["details.dnsShort"] = "DNS";
            tr["settings.tooltip"] = "Yapılandırma klasörünü aç";
            tr["tab.status"] = "Durum";
            tr["tab.log"] = "Günlük";
            tr["tab.advanced"] = "Gelişmiş";
            tr["advanced.modeTitle"] = "Atlatma nereye uygulansın";
            tr["advanced.automatic"] = "Otomatik";
            tr["advanced.manual"] = "Kendim seçeyim";
            tr["advanced.automaticHint"] = "Bu makinenin kurduğu her bağlantıya. Hiçbir şey kaçmaz.";
            tr["advanced.manualHint"] = "Yalnız aşağıda işaretli servislere. Diğer her şey el değmeden geçer.";
            tr["advanced.servicesTitle"] = "Servisler";
            tr["advanced.blockedHere"] = "burada engelli";
            tr["advanced.addresses"] = "adres";
            tr["group.robloxclient.name"] = "Roblox oyun";
            tr["group.robloxclient.examples"] = "Oyuna giriş, kurulum, güncelleme";
            tr["group.robloxsite.name"] = "Roblox sitesi";
            tr["group.robloxsite.examples"] = "Profil, sunucu listesi, giriş, sohbet";
            tr["group.robloxcdn.name"] = "Roblox görselleri";
            tr["group.robloxcdn.examples"] = "Küçük resimler, avatarlar, sayfa içeriği";
            tr["group.discord.name"] = "Discord";
            tr["group.discord.examples"] = "Sohbet, ses, dosyalar, güncelleme";
            tr["group.social.name"] = "Sosyal medya";
            tr["group.social.examples"] = "X, Instagram, Telegram, WhatsApp, Reddit";
            tr["group.games.name"] = "Oyun mağazaları";
            tr["group.games.examples"] = "Steam, Epic Games, EA";
            tr["group.media.name"] = "Video ve müzik";
            tr["group.media.examples"] = "YouTube, Twitch, Spotify, Vimeo, SoundCloud";
            tr["group.reference.name"] = "Haber ve bilgi";
            tr["group.reference.examples"] = "Wikipedia, BBC, ChatGPT, haber siteleri";
            tr["group.yours.name"] = "Kendi listen";
            tr["group.yours.examples"] = "config.ini içindeki probeDomains satırından";
            tr["advanced.customTitle"] = "Kendi adreslerin";
            tr["advanced.customHint"] = "Site adı yaz, bağlantı değil. Alt alan adları da kapsanır.";
            tr["advanced.customExample"] = "reddit.com, ebay.de";
            tr["advanced.rejected"] = "Adres olarak kullanılamadığı için alınmadı: {0}";
            tr["advanced.apply"] = "Uygula";
            tr["advanced.applied"] = "Kapsam: {0} servis, {1} adres.";
            tr["advanced.nothingChosen"] = "Hiçbir şey işaretli değil, kapsanacak bir şey kalmıyor. Bir servis seç ya da adres ekle.";
            tr["onboarding.language"] = "Dil";
            tr["onboarding.next"] = "Devam";
            tr["onboarding.finish"] = "Ayarımı bul";
            tr["onboarding.skip"] = "Şimdilik geç";
            tr["onboarding.welcome.title"] = "EasyDPI'a hoş geldin";
            tr["onboarding.welcome.body"] =
                "Bazı sağlayıcılar siteleri sana ulaşmadan engelliyor. EasyDPI bunu aşar; senin " +
                "hiçbir ayar yapmana gerek kalmadan.";
            tr["onboarding.how.title"] = "Nasıl çalışır";
            tr["onboarding.how.body"] =
                "Sağlayıcın alan adı sorgularını yönlendiriyor, trafiği de inceliyor. EasyDPI sorguları " +
                "şifreler ve trafiği yeniden biçimlendirir; iki kontrol de aradığını bulamaz.";
            tr["onboarding.ready.title"] = "Başlamadan önce";
            tr["onboarding.ready.body"] =
                "Arka planda iki servis kurulacak, bunun için yönetici izni gerekiyor. Çalışan ayar " +
                "sağlayıcıya göre değiştiği için EasyDPI ağını ölçüp doğrusunu seçecek.";

            catalog["tr"] = tr;

            // ---------------- Русский ----------------
            Dictionary<string, string> ru = new Dictionary<string, string>();

            ru["status.on"] = "Защита включена";
            ru["status.off"] = "Защита выключена";
            ru["subtitle.on"] = "Заблокированные сайты доступны";
            ru["subtitle.off"] = "Обычное интернет-соединение";
            ru["button.turnOn"] = "Включить";
            ru["button.turnOff"] = "Выключить";
            ru["button.turningOn"] = "Включение...";
            ru["button.turningOff"] = "Выключение...";
            ru["button.testing"] = "Проверка...";
            ru["link.autoTune"] = "Подобрать настройки для моей сети";
            ru["link.details"] = "Подробности";
            ru["link.hideDetails"] = "Скрыть подробности";
            ru["footer.persists"] = "Состояние сохраняется после перезагрузки.";

            ru["details.bypassService"] = "Служба обхода";
            ru["details.dnsService"] = "Шифрованный DNS";
            ru["details.dnsAddress"] = "Адрес DNS";
            ru["details.activeSettings"] = "Текущие настройки";

            ru["service.running"] = "работает";
            ru["service.stopped"] = "остановлена";
            ru["service.starting"] = "запускается";
            ru["service.stopping"] = "останавливается";
            ru["service.notInstalled"] = "не установлена";
            ru["service.unknown"] = "неизвестно";

            ru["dns.noAdapter"] = "адаптер не найден";
            ru["dns.notSet"] = "не задан";
            ru["dns.unreadable"] = "не удалось прочитать";

            ru["confirm.autoTune.title"] = "Автоматическая настройка";
            ru["confirm.autoTune.body"] =
                "Сеть будет проверена, чтобы подобрать работающие настройки.\n\n" +
                "Во время проверки соединение может пропасть на несколько секунд.\nЭто занимает примерно 1-3 минуты.\n\n" +
                "Продолжить?";

            ru["warn.missingBypassBinary"] = "Внимание: файл bin\\goodbyedpi.exe отсутствует.";
            ru["warn.missingDnsBinary"] = "Внимание: файл dns\\dnscrypt-proxy.exe отсутствует.";
            ru["incomplete.title"] = "Рядом с этой копией нет программ, которые она запускает.";
            ru["incomplete.fromArchive"] = "   Запущено прямо из скачанного архива. Windows распаковывает только тот файл, по которому вы щёлкнули, и запускает его отдельно — без папок bin и dns. Сначала распакуйте весь архив в папку и запустите EasyDPI.exe оттуда.";
            ru["incomplete.filesMissing"] = "   Папок bin и dns нет рядом с EasyDPI.exe. Держите папку из архива целиком или скачайте заново.";
            ru["incomplete.where"] = "   Запущено из: {0}";
            ru["incomplete.status"] = "Не готово";
            ru["incomplete.statusFromArchive"] = "Сначала распакуйте архив";
            ru["incomplete.statusMissing"] = "Рядом с EasyDPI.exe не хватает файлов";

            ru["log.turningOn"] = "Включение...";
            ru["log.errorMissingBinary"] = "ОШИБКА: bin\\goodbyedpi.exe не найден.";
            ru["log.encryptedDnsActive"] = "Шифрованный DNS активен.";
            ru["log.dnsServiceFailed"] = "Внимание: служба DNS не запустилась, настройки DNS не менялись.";
            ru["log.dnsNotNeeded"] = "В этой сети перенаправление DNS не требуется.";
            ru["log.turnedOn"] = "Включено. Настройки: {0}";
            ru["log.serviceFailed"] = "ОШИБКА: служба не запустилась.";
            ru["log.turningOff"] = "Выключение...";
            ru["log.turnedOff"] = "Выключено. DNS возвращён к настройкам по умолчанию.";
            ru["log.error"] = "ОШИБКА: {0}";

            ru["tune.start"] = "Проверка сети началась.";
            ru["tune.step1"] = "1) Проверка DNS";
            ru["tune.dnsNoAnswer"] = "подменяется (нет ответа)";
            ru["tune.dnsFakeAddress"] = "подменяется (поддельный адрес)";
            ru["tune.dnsClean"] = "чисто";
            ru["tune.noInternet"] = "ОШИБКА: нет доступа в интернет.";
            ru["tune.dnsTamperedResult"] = "   -> DNS подменяется; включаем шифрованный DNS.";
            ru["tune.dnsCleanResult"] = "   -> DNS чистый; не трогаем.";
            ru["tune.warnDnsStart"] = "   Внимание: служба DNS не запустилась.";
            ru["tune.warnDnsMissing"] = "   Внимание: dnscrypt-proxy не найден.";
            ru["tune.step2"] = "2) Проверка блокировок ({0} адресов, сервисов: {1})";
            ru["tune.allOpen"] = "все доступны";
            ru["tune.closedList"] = "закрыто: {0}";
            ru["tune.noBlocking"] = "   -> Блокировок в этой сети не обнаружено.";
            ru["tune.addYourOwn"] = "   Если нужный сайт всё ещё заблокирован, добавьте его в probeDomains в bin/config.ini.";
            ru["tune.step3"] = "3) Отбор настроек ({0} вариантов)";
            ru["tune.step4"] = "4) Замер скорости: лучшие {0}";
            ru["tune.speedOnly"] = "   -> Всё равно выберем самую быструю настройку.";
            ru["tune.colSetting"] = "настройка";
            ru["tune.colOpen"] = "открыто";
            ru["tune.colLatency"] = "отклик";
            ru["tune.colSpeed"] = "загрузка";
            ru["tune.candidateFailed"] = "не запустилось";
            ru["tune.sitesBroken"] = "  (сломано обычных сайтов: {0})";
            ru["tune.nothingWorked"] = "Рабочая конфигурация не найдена. Для этой сети может потребоваться VPN.";
            ru["tune.best"] = "Лучшие настройки: {0}";
            ru["tune.encryptedDnsVerdict"] = "Шифрованный DNS: {0}";
            ru["tune.required"] = "требуется";
            ru["tune.notRequired"] = "не требуется";
            ru["tune.vpnDetected"] = "Подключён VPN ({0}).";
            ru["tune.vpnCandidates"] = "   Настройки с поддельными пакетами исключены: они рассчитаны на маршрут из этой машины, а внутри туннеля трафик идёт не по нему.";
            ru["log.vpnActive"] = "Подключён VPN ({0}). Работают оба; если что-то перестанет открываться, выключите одно из двух.";
            ru["button.uninstall"] = "Удалить EasyDPI";
            ru["button.saveReport"] = "Сохранить отчёт";
            ru["update.available"] = "Доступна более новая версия: {0}";
            ru["update.title"] = "Есть обновление";
            ru["button.updating"] = "Обновление...";
            ru["update.promptManual"] = "Вышла EasyDPI {0}. У вас установлена {1}.\n\nЭтот выпуск нельзя проверить автоматически, поэтому он не будет установлен за вас. Открыть страницу загрузки?";
            ru["update.downloading"] = "Загрузка версии {0}...";
            ru["update.downloadFailed"] = "Загрузка не удалась. Ничего не изменено.";
            ru["update.verifying"] = "Сверка загруженного файла с опубликованной контрольной суммой...";
            ru["update.checksumFailed"] = "Загруженный файл не совпадает с опубликованной контрольной суммой. Файл удалён, ничего не изменено.";
            ru["update.contentsUnexpected"] = "В архиве нет EasyDPI. Ничего не изменено.";
            ru["update.swapFailed"] = "Не удалось запустить обновление. Ничего не изменено.";
            ru["update.failed"] = "Обновление не удалось: {0}";
            ru["update.restarting"] = "Обновление готово. Перезапуск для установки...";
            ru["update.prompt"] = "Вышла EasyDPI {0}. У вас установлена {1}.\n\nСкачать и установить сейчас? После этого EasyDPI перезапустится.";
            ru["report.dialogTitle"] = "Сохранить диагностический отчёт";
            ru["report.fileType"] = "Текстовый файл";
            ru["report.saved"] = "Отчёт сохранён: {0}";
            ru["report.failed"] = "Не удалось сохранить отчёт: {0}";
            ru["button.uninstalling"] = "Удаление...";
            ru["uninstall.confirmTitle"] = "Удалить EasyDPI?";
            ru["uninstall.confirmBody"] = "Обе службы будут остановлены и сняты с регистрации, драйвер пакетов удалён, DNS возвращён сети, а собственные файлы EasyDPI удалены из:\n\n{0}\n\nУдаляются только файлы, которые установил EasyDPI; остальное в этой папке не трогается. Отменить это нельзя.";
            ru["uninstall.started"] = "Удаление EasyDPI...";
            ru["uninstall.bypassRemoved"] = "Служба обхода остановлена и снята с регистрации.";
            ru["uninstall.driverRemoved"] = "Драйвер пакетов снят с регистрации.";
            ru["uninstall.dnsRemoved"] = "Служба шифрованного DNS остановлена и снята с регистрации.";
            ru["uninstall.dnsRestored"] = "DNS возвращён сети.";
            ru["uninstall.filesScheduled"] = "Файлы будут удалены сразу после закрытия окна.";
            ru["uninstall.filesFailed"] = "Не удалось запланировать удаление файлов. Удалите эту папку вручную: {0}";
            ru["uninstall.closing"] = "Готово. Закрытие.";
            ru["log.dnsFailedConsequence"] = "Сайты, адреса которых подменяет провайдер, останутся недоступны, пока это не исправлено: обход не спасает от неверного адреса.";
            ru["dnsfail.missingBinary"] = "В папке dns нет dnscrypt-proxy.exe.";
            ru["dnsfail.notInstalled"] = "Не удалось зарегистрировать службу dnscrypt-proxy.";
            ru["dnsfail.portTaken"] = "Порт 53 уже занят: {0}. Закройте эту программу или остановите службу и повторите.";
            ru["dnsfail.didNotStart"] = "dnscrypt-proxy установлен, но не перешёл в состояние «выполняется».";
            ru["tune.dnsAbort"] = "Останавливаемся. Провайдер подменяет DNS-ответы, а шифрованный резолвер не работает — значит, сейчас любой адрес на этой машине такой, каким его назвал провайдер. Замер описал бы это, а не вашу связь.";
            ru["tune.dnsAbortHint"] = "   Настройки не изменены. Устраните проблему с резолвером выше и запустите снова.";
            ru["tune.bestSpeed"] = "Отклик {0}, загрузка {1}";
            ru["tune.stillBlocked"] = "С этими настройками всё ещё недоступно: {0}";
            ru["tune.stillBlockedHint"] = "   Возможно, это блокировка на стороне провайдера — тут нужен VPN, а не другие настройки.";
            ru["tune.elapsed"] = "Замер занял {0} мин {1} с.";
            ru["probe.yours"] = "Ваши";

            ru["menu.showIntro"] = "Показать введение снова";
            ru["menu.openConfig"] = "Открыть папку конфигурации";
            ru["menu.targetedScope"] = "Применять только к заблокированным адресам";
            ru["scope.targeted"] = "Область: только {0} заблокированных адресов. Всё остальное идёт нетронутым.";
            ru["scope.all"] = "Область: каждое соединение этой машины.";
            ru["scope.noList"] = "Замеров ещё не было, сужать не к чему. Сначала запустите подбор настроек.";
            ru["tune.targetedScope"] = "   Область сужена: движок ограничен этими {0} адресами.";
            ru["pill.on"] = "Вы под защитой";
            ru["pill.off"] = "Защиты нет";
            ru["details.dnsShort"] = "DNS";
            ru["settings.tooltip"] = "Открыть папку конфигурации";
            ru["tab.status"] = "Состояние";
            ru["tab.log"] = "Журнал";
            ru["tab.advanced"] = "Дополнительно";
            ru["advanced.modeTitle"] = "К чему применять обход";
            ru["advanced.automatic"] = "Автоматически";
            ru["advanced.manual"] = "Выбрать самому";
            ru["advanced.automaticHint"] = "К каждому соединению этой машины. Ничего не упускается.";
            ru["advanced.manualHint"] = "Только к отмеченным ниже сервисам. Всё остальное идёт нетронутым.";
            ru["advanced.servicesTitle"] = "Сервисы";
            ru["advanced.blockedHere"] = "заблокирован здесь";
            ru["advanced.addresses"] = "адресов";
            ru["group.robloxclient.name"] = "Roblox: игра";
            ru["group.robloxclient.examples"] = "Вход в игры, установка, обновления";
            ru["group.robloxsite.name"] = "Roblox: сайт";
            ru["group.robloxsite.examples"] = "Профили, списки серверов, вход, чат";
            ru["group.robloxcdn.name"] = "Roblox: изображения";
            ru["group.robloxcdn.examples"] = "Миниатюры, аватары, содержимое страниц";
            ru["group.discord.name"] = "Discord";
            ru["group.discord.examples"] = "Чат, голос, вложения, обновления";
            ru["group.social.name"] = "Соцсети";
            ru["group.social.examples"] = "X, Instagram, Telegram, WhatsApp, Reddit";
            ru["group.games.name"] = "Игровые магазины";
            ru["group.games.examples"] = "Steam, Epic Games, EA";
            ru["group.media.name"] = "Видео и музыка";
            ru["group.media.examples"] = "YouTube, Twitch, Spotify, Vimeo, SoundCloud";
            ru["group.reference.name"] = "Новости и справка";
            ru["group.reference.examples"] = "Wikipedia, BBC, ChatGPT, новостные сайты";
            ru["group.yours.name"] = "Ваш список";
            ru["group.yours.examples"] = "Из probeDomains в config.ini";
            ru["advanced.customTitle"] = "Ваши адреса";
            ru["advanced.customHint"] = "Имя сайта, не ссылка. Поддомены тоже покрываются.";
            ru["advanced.customExample"] = "reddit.com, ebay.de";
            ru["advanced.rejected"] = "Не годятся как адреса, поэтому пропущены: {0}";
            ru["advanced.apply"] = "Применить";
            ru["advanced.applied"] = "Охват: сервисов {0}, адресов {1}.";
            ru["advanced.nothingChosen"] = "Ничего не отмечено — покрывать нечего. Выберите сервис или добавьте адрес.";
            ru["onboarding.language"] = "Язык";
            ru["onboarding.next"] = "Далее";
            ru["onboarding.finish"] = "Подобрать настройки";
            ru["onboarding.skip"] = "Пропустить";
            ru["onboarding.welcome.title"] = "Добро пожаловать в EasyDPI";
            ru["onboarding.welcome.body"] =
                "Некоторые провайдеры блокируют сайты ещё до того, как они дойдут до вас. EasyDPI " +
                "обходит это, и настраивать ничего не нужно.";
            ru["onboarding.how.title"] = "Как это работает";
            ru["onboarding.how.body"] =
                "Провайдер перенаправляет DNS-запросы и анализирует сам трафик. EasyDPI шифрует запросы " +
                "и меняет форму трафика, так что ни одна из проверок не срабатывает.";
            ru["onboarding.ready.title"] = "Перед началом";
            ru["onboarding.ready.body"] =
                "Будут установлены две фоновые службы, для этого нужны права администратора. Рабочие " +
                "настройки у разных провайдеров разные, поэтому EasyDPI подберёт их для вашей сети.";

            catalog["ru"] = ru;

            return catalog;
        }
    }
}
