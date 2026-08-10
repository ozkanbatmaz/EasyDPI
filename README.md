<div align="center">

<img src="docs/logo.png" width="88" alt="EasyDPI">

# EasyDPI

**Get past ISP-level website blocking on Windows. One click, nothing to configure.**

[Türkçe](README.tr.md) · English

### [Download EasyDPI](../../releases/latest)

<img src="docs/screenshot-en.png" width="300" alt="EasyDPI main window">

</div>

---

## What it does

In many countries your internet provider blocks websites in **two separate places**, and tools that only fix one of them leave you with pages that still refuse to load:

| Layer | What the provider does | What EasyDPI does |
|---|---|---|
| **DNS** | Domain lookups are answered with a fake address that points at a block page. Switching DNS servers usually does not help either, because queries to port 53 are dropped for blocked names. | Runs an encrypted DNS resolver locally. The provider cannot read the queries, so it cannot filter them. |
| **Inspection** | Your connections are examined and cut off mid-handshake when a blocked destination is recognised. | Reshapes the outgoing packets so the inspection never sees a complete name to match on. |

Both layers apply system-wide, so browsers **and** desktop applications are covered. There is nothing to configure per app.

## Getting started

1. Download the latest release and extract it anywhere
2. Run `EasyDPI.exe` — it asks for administrator rights, which it needs to install its services
3. Follow the three-step introduction and let it find the settings for your network

<div align="center">
<img src="docs/onboarding-en.png" width="300" alt="First run">
</div>

EasyDPI installs two background services and keeps working after you close the window or restart the machine. Turning it off stops both services, restores your original DNS settings, and prevents them from starting again — the machine is left exactly as it was.

## Automatic configuration

This is the part that matters, and the reason this project exists.

The packet manipulation that defeats one provider's inspection does nothing against another's. Settings that work in one country routinely fail in the next. Most guides hand you a fixed command line and hope for the best.

EasyDPI measures your actual connection instead:

```
1) DNS check
   discord.com         tampered (fake address)
   roblox.com          tampered (fake address)
   x.com               tampered (fake address)
   medium.com          clean
   -> DNS is being tampered with; enabling encrypted DNS.

2) Blocking check
   discord.com         blocked
   roblox.com          blocked
   x.com               open
   medium.com          open

3) Searching for settings (13 candidates)
   -9 --frag-by-sni      0/2
   -9                    0/2
   -5 -q --frag-by-sni   2/2

Best settings: -5 -q --frag-by-sni
```

How it decides:

- **DNS tampering** is detected by resolving each probe domain twice — once through the system resolver, once over encrypted DNS — and comparing the answers.
- **Blocking** is detected by opening a real TLS connection to the correct address. A connection that is reset mid-handshake is being interfered with.
- **Candidates** are tried in order. Each one is scored on how many blocked sites it opens *and* whether it breaks sites that were working; a configuration that damages normal browsing scores worse than doing nothing. The search stops at the first clean success.

The result is written to `bin/config.ini`. Run it again whenever you change networks.

**On a network the defaults do not cover:** the built-in probe list covers commonly blocked services across several regions, but it cannot cover everything. If EasyDPI reports no blocking while a site you need is still unreachable, add that site to `probeDomains` in `bin/config.ini` and run the tuner again.

## Languages

English, Turkish and Russian, selected automatically from your Windows language and changeable during setup or via `language=` in `bin/config.ini`.

Adding one is deliberately easy: copy the English block in [`src/Strings.cs`](src/Strings.cs), translate the values, register it in `BuildCatalog()`. Missing keys fall back to English, so a partial translation is still usable. Pull requests welcome.

## Command line

For scripting or scheduled tasks, run as administrator:

```
EasyDPI.exe /auto    measure the network, find working settings, apply them
EasyDPI.exe /on      turn protection on using the saved settings
EasyDPI.exe /off     turn protection off and restore DNS
```

Output goes to `easydpi.log`.

## Windows warns you on first run

EasyDPI is not code signed, so Microsoft Defender SmartScreen shows **"Windows protected your PC — Unknown publisher"** the first time you run it. Click **More info**, then **Run anyway**. It only asks once.

This is not specific to EasyDPI. SmartScreen shows the same warning for every unsigned executable that has not yet built up download reputation, and a signing certificate costs a few hundred dollars a year — hard to justify for a free tool.

If you would rather verify the download than trust it, check the archive before extracting:

```powershell
Get-FileHash EasyDPI-1.0.0.zip -Algorithm SHA256
```

It should print:

```
9E6C13C8B98A15851D069422D4EDB17004C752B3C5D4639EC60CFEA2DD337C8E
```

The same value is published in the release notes. If it matches, the file is byte for byte what was uploaded here.

## Limitations

- **It does not hide your IP address or your location.** It changes the shape of your packets, not where they come from. Every site you visit still sees your real address. That requires a VPN, which unavoidably adds latency.
- **Windows only.** It depends on a Windows packet driver.
- **Some providers cannot be bypassed this way.** If no candidate works, EasyDPI says so plainly rather than pretending otherwise.
- **Every network call and system change is documented** in the [transparency report](TRANSPARENCY.md), including checksums for every shipped binary.
- **Measured performance cost is negligible** — around 0.2% on download throughput, within measurement noise. Name resolution usually gets slightly *faster*, because the local resolver caches.

## Layout

```
EasyDPI.exe          the application
bin/                 bypass engine, packet driver, config.ini
dns/                 encrypted DNS resolver and its configuration
licenses/            third-party licenses
src/                 source and build script
docs/                images used by this README
```

## Building from source

No Visual Studio, no SDK, no package manager. The C# compiler that ships with .NET Framework 4.x — present on every Windows 10 and 11 machine — is enough:

```
src\build.cmd
```

`EasyDPI.exe` appears in the parent folder.

## Credits

EasyDPI is a user interface and an automation layer. The tools doing the actual work belong to other people:

| Project | Author | License |
|---|---|---|
| [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) | ValdikSS | Apache 2.0 |
| [WinDivert](https://github.com/basil00/WinDivert) | Basil Fierz | LGPLv3 / GPLv3 |
| [dnscrypt-proxy](https://github.com/DNSCrypt/dnscrypt-proxy) | Frank Denis | ISC |
| [unDraw](https://undraw.co) | Katerina Limpitsouni | unDraw licence (free, no attribution required) |

Full license texts are in [`licenses/`](licenses/). EasyDPI's own code is [MIT](LICENSE) licensed.

## Disclaimer

This tool was written to fix access problems on your own device and your own connection. How you use it, and compliance with the laws where you live, is your responsibility.
