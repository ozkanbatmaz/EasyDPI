# Transparency report

EasyDPI installs a kernel driver and changes your DNS settings. Tools that do
that deserve to explain themselves, so this page lists exactly what changes,
what leaves your machine, and how to check any of it yourself.

Last verified: 10 August 2026, against EasyDPI 1.0.0.

## What it changes on your system

| Change | Detail |
|---|---|
| Windows service `GoodbyeDPI` | Created pointing at `bin\goodbyedpi.exe` in the folder you extracted, start type Automatic |
| Windows service `dnscrypt-proxy` | Created pointing at `dns\dnscrypt-proxy.exe`, start type Automatic |
| Kernel driver | `WinDivert64.sys` is loaded by GoodbyeDPI to inspect and reshape outgoing packets |
| DNS settings | The active adapter's DNS is set to `127.0.0.1` and `::1`, so lookups go to the local encrypted resolver |
| Files written | `bin\config.ini` (your settings), `easydpi.log` (only in command line mode), `dns\*.md` (resolver list cache) |

Turning protection off reverses all of it: both services are stopped **and
disabled**, and DNS is handed back to DHCP. Nothing is left running or set to
start again.

The services point at wherever the application folder is. If you move or delete
that folder, turn protection off first, or the services will point at files that
are no longer there.

## What leaves your machine

**EasyDPI itself**, only while the automatic tuner runs:

- DNS-over-HTTPS queries to `cloudflare-dns.com` and `dns.google`, used as a
  trustworthy answer to compare your provider's answers against
- TLS handshakes to the probe domains, to see which ones are being cut off

That is the entire list. No telemetry, no analytics, no update check, no
crash reporting, no identifiers. The source is in [`src/`](src/) and the network
calls are all in [`src/NetworkTools.cs`](src/NetworkTools.cs).

**dnscrypt-proxy**, while protection is on:

- Your DNS lookups, encrypted, to Cloudflare and Google DoH servers
- Plain DNS to `9.9.9.11` and `8.8.8.8` at startup, to find those servers
- A connectivity probe to `9.9.9.9`
- The public resolver list from `download.dnscrypt.info` and
  `raw.githubusercontent.com`, verified with a published minisign key

**GoodbyeDPI** opens no connections of its own. It only reshapes packets that
were already leaving your machine.

## What it does not do

- It does not hide your IP address or your location. Every site still sees your
  real address.
- It does not route your traffic through any server the author controls. There
  is no such server.
- It does not collect, store or transmit anything about you or what you visit.
- It does not modify other applications or their traffic beyond the packet
  reshaping described above.

## Where the binaries come from

EasyDPI ships prebuilt binaries from three other projects. They are the
upstream authors' own builds, downloaded from their official release pages and
shipped unmodified. This was verified by downloading the upstream archives again
and comparing hashes:

| File | Source | SHA-256 | Matches upstream |
|---|---|---|---|
| `bin\goodbyedpi.exe` | [GoodbyeDPI 0.2.3rc3](https://github.com/ValdikSS/GoodbyeDPI/releases/tag/0.2.3rc3) | `8D412B094BB9C137FF25BA9A794D1122ECC84BB776DEBFF6C249723A13CC31CD` | yes |
| `bin\WinDivert.dll` | bundled in the GoodbyeDPI archive | `6110BFA44667405179C3E15E12AF1B62037E447ED59B054B19042032995E6C7E` | yes |
| `bin\WinDivert64.sys` | bundled in the GoodbyeDPI archive | `E69B5BA3F0CD6CFB2983E442636E7F0B342B61B15264B0328317D4559C82CF50` | yes |
| `dns\dnscrypt-proxy.exe` | [dnscrypt-proxy 2.1.18](https://github.com/DNSCrypt/dnscrypt-proxy/releases/tag/2.1.18) | `D847F834AEF02F8705A649DC1060F520CDB7931D7361035728770DCE2C16EEB6` | yes |
| `EasyDPI.exe` | built from [`src/`](src/) in this repository | `D95408D8D3A26EE3C8F98591835782EC9CDD414D7D5EFC215F1D154C92402CA9` | — |

Upstream archive checksums, if you want to start from those:

```
goodbyedpi-0.2.3rc3-2.zip        37F96B32D050DADCC930A639EBA68E1CCD57ED5C04A5F77DFCA908F01905A4C5
dnscrypt-proxy-win64-2.1.18.zip  15F0C8F1F40620A54DDFD8752C327DABE1146F84618D68874F79C4F52490B396
```

## Code signing

| File | Signature |
|---|---|
| `WinDivert64.sys` | Valid, Cloudveil Technology Inc. |
| everything else | Unsigned |

The kernel driver is signed because 64-bit Windows will not load an unsigned
one. The rest is unsigned, which is why SmartScreen warns on first run — see the
[README](README.md#windows-warns-you-on-first-run).

## Antivirus

A Windows Defender scan of the whole folder on 10 August 2026 reported no
threats, with real-time protection on and same-day signatures.

Other engines may still flag `goodbyedpi.exe` or the WinDivert files as
**HackTool** or **RiskWare**. That classification is about capability, not
intent: this software genuinely does install a packet-capture driver and
genuinely does rewrite network traffic, which is the same capability profile a
malicious tool would have. If your antivirus objects, that is the reason.

## Checking any of this yourself

- **Read it.** The application is about 2,500 lines of C# in [`src/`](src/).
  Every network call is in `NetworkTools.cs`; every system change is in
  `ServiceManager.cs` and `BypassController.cs`.
- **Build it.** `src\build.cmd` compiles it with the C# compiler that ships with
  Windows. No SDK, no package manager, nothing downloaded during the build. Use
  your own binary instead of ours if you prefer.
- **Verify the download.** `Get-FileHash EasyDPI-1.0.0.zip -Algorithm SHA256`
  and compare with the release notes.
- **Watch it.** Run a packet capture while it is on and confirm the list above
  is complete.

If you find something this page does not describe, please open an issue.
