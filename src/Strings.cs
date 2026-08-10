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
            en["tune.skipped"] = "skipped";
            en["tune.dnsNoAnswer"] = "tampered (no answer)";
            en["tune.dnsFakeAddress"] = "tampered (fake address)";
            en["tune.dnsClean"] = "clean";
            en["tune.noInternet"] = "ERROR: could not reach the internet.";
            en["tune.dnsTamperedResult"] = "   -> DNS is being tampered with; enabling encrypted DNS.";
            en["tune.dnsCleanResult"] = "   -> DNS is clean; leaving it alone.";
            en["tune.warnDnsStart"] = "   Warning: the DNS service did not start.";
            en["tune.warnDnsMissing"] = "   Warning: dnscrypt-proxy was not found.";
            en["tune.step2"] = "2) Blocking check";
            en["tune.reachable"] = "open";
            en["tune.blocked"] = "blocked";
            en["tune.noBlocking"] = "   -> No blocking detected on this network.";
            en["tune.addYourOwn"] = "   If a site you need is still blocked, add it to probeDomains in bin/config.ini.";
            en["tune.step3"] = "3) Searching for settings ({0} candidates)";
            en["tune.candidateFailed"] = "could not start";
            en["tune.sitesBroken"] = "  ({0} normal sites broken)";
            en["tune.nothingWorked"] = "No working configuration was found. This network may require a VPN.";
            en["tune.best"] = "Best settings: {0}";
            en["tune.encryptedDnsVerdict"] = "Encrypted DNS: {0}";
            en["tune.required"] = "required";
            en["tune.notRequired"] = "not required";

            en["menu.showIntro"] = "Show the introduction again";
            en["menu.openConfig"] = "Open the configuration folder";
            en["pill.on"] = "You are protected";
            en["pill.off"] = "Not protected";
            en["details.dnsShort"] = "DNS";
            en["settings.tooltip"] = "Open the configuration folder";
            en["tab.status"] = "Status";
            en["tab.log"] = "Activity";
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
            tr["tune.skipped"] = "atlandı";
            tr["tune.dnsNoAnswer"] = "müdahaleli (cevap yok)";
            tr["tune.dnsFakeAddress"] = "müdahaleli (sahte adres)";
            tr["tune.dnsClean"] = "temiz";
            tr["tune.noInternet"] = "HATA: internete ulaşılamadı.";
            tr["tune.dnsTamperedResult"] = "   -> DNS'e müdahale ediliyor; şifreli DNS açılıyor.";
            tr["tune.dnsCleanResult"] = "   -> DNS temiz; dokunulmayacak.";
            tr["tune.warnDnsStart"] = "   Uyarı: DNS servisi başlatılamadı.";
            tr["tune.warnDnsMissing"] = "   Uyarı: dnscrypt-proxy bulunamadı.";
            tr["tune.step2"] = "2) Engel kontrolü";
            tr["tune.reachable"] = "açık";
            tr["tune.blocked"] = "engelli";
            tr["tune.noBlocking"] = "   -> Bu ağda engelleme tespit edilmedi.";
            tr["tune.addYourOwn"] = "   İhtiyacın olan site hâlâ engelliyse bin/config.ini içindeki probeDomains satırına ekle.";
            tr["tune.step3"] = "3) Ayar aranıyor ({0} aday)";
            tr["tune.candidateFailed"] = "başlatılamadı";
            tr["tune.sitesBroken"] = "  ({0} normal site bozuldu)";
            tr["tune.nothingWorked"] = "Çalışan bir ayar bulunamadı. Bu ağ için VPN gerekebilir.";
            tr["tune.best"] = "En iyi ayar: {0}";
            tr["tune.encryptedDnsVerdict"] = "Şifreli DNS: {0}";
            tr["tune.required"] = "gerekli";
            tr["tune.notRequired"] = "gerekmiyor";

            tr["menu.showIntro"] = "Tanıtımı tekrar göster";
            tr["menu.openConfig"] = "Yapılandırma klasörünü aç";
            tr["pill.on"] = "Korunuyorsunuz";
            tr["pill.off"] = "Koruma yok";
            tr["details.dnsShort"] = "DNS";
            tr["settings.tooltip"] = "Yapılandırma klasörünü aç";
            tr["tab.status"] = "Durum";
            tr["tab.log"] = "Günlük";
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
            ru["tune.skipped"] = "пропущено";
            ru["tune.dnsNoAnswer"] = "подменяется (нет ответа)";
            ru["tune.dnsFakeAddress"] = "подменяется (поддельный адрес)";
            ru["tune.dnsClean"] = "чисто";
            ru["tune.noInternet"] = "ОШИБКА: нет доступа в интернет.";
            ru["tune.dnsTamperedResult"] = "   -> DNS подменяется; включаем шифрованный DNS.";
            ru["tune.dnsCleanResult"] = "   -> DNS чистый; не трогаем.";
            ru["tune.warnDnsStart"] = "   Внимание: служба DNS не запустилась.";
            ru["tune.warnDnsMissing"] = "   Внимание: dnscrypt-proxy не найден.";
            ru["tune.step2"] = "2) Проверка блокировок";
            ru["tune.reachable"] = "доступен";
            ru["tune.blocked"] = "заблокирован";
            ru["tune.noBlocking"] = "   -> Блокировок в этой сети не обнаружено.";
            ru["tune.addYourOwn"] = "   Если нужный сайт всё ещё заблокирован, добавьте его в probeDomains в bin/config.ini.";
            ru["tune.step3"] = "3) Подбор настроек ({0} вариантов)";
            ru["tune.candidateFailed"] = "не запустилось";
            ru["tune.sitesBroken"] = "  (сломано обычных сайтов: {0})";
            ru["tune.nothingWorked"] = "Рабочая конфигурация не найдена. Для этой сети может потребоваться VPN.";
            ru["tune.best"] = "Лучшие настройки: {0}";
            ru["tune.encryptedDnsVerdict"] = "Шифрованный DNS: {0}";
            ru["tune.required"] = "требуется";
            ru["tune.notRequired"] = "не требуется";

            ru["menu.showIntro"] = "Показать введение снова";
            ru["menu.openConfig"] = "Открыть папку конфигурации";
            ru["pill.on"] = "Вы под защитой";
            ru["pill.off"] = "Защиты нет";
            ru["details.dnsShort"] = "DNS";
            ru["settings.tooltip"] = "Открыть папку конфигурации";
            ru["tab.status"] = "Состояние";
            ru["tab.log"] = "Журнал";
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
