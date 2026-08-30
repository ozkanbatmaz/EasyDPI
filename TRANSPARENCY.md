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
| Files written | `bin\config.ini` (your settings), `bin\blacklist.txt` (the addresses found blocked, when the scope is targeted), `easydpi.log` (the activity log, kept between runs and trimmed at 1 MB), `dns\*.md` (resolver list cache) |

Turning protection off reverses all of it: both services are stopped **and
disabled**, and DNS is handed back to DHCP — on the adapters this application
pointed at the local resolver, and no others, so a VPN's own DNS settings are
left where its client put them. Nothing is left running or set to
start again.

The services point at wherever the application folder is. If you move or delete
that folder, turn protection off first, or the services will point at files that
are no longer there.

**Save report**, next to it, writes a diagnostic file wherever you choose: the
activity log, the contents of `config.ini`, the state of the two services and the
driver, which DNS servers the adapter is set to, and which of the shipped files
are present. It is meant to be attached to a bug report, so the file says in its
own header what it holds — and what it does not, which is anything about the sites
you visit. Nothing is sent anywhere; the file is yours to look at and to share or
not.

**Remove EasyDPI**, on the log tab, does the whole thing in one step: it stops and
unregisters both services, unregisters the WinDivert packet driver, hands DNS back
to the network, and then deletes the application's own files. Only files EasyDPI
installed are deleted — the folders it shipped and the files it wrote. Anything
else in that folder is left where it is, and the folder itself is removed only if
nothing remains in it.

## What leaves your machine

**EasyDPI itself**, only while the automatic tuner runs:

- DNS-over-HTTPS queries to `cloudflare-dns.com` and `dns.google`, used as a
  trustworthy answer to compare your provider's answers against
- One `HEAD /` request over HTTPS to each address in the probe list, to see which
  ones are being cut off. The list is in [`src/ProbeList.cs`](src/ProbeList.cs),
  in full, in plain text — around eighty service endpoints. Nothing is sent with
  the requests: no cookies, no identifiers, no referrer, and the response body is
  never read. Blocked addresses are then re-requested while each candidate setting
  is running, which is what makes the search a measurement rather than a guess.
- A download of two megabytes from `speed.cloudflare.com`, twice for each of the
  three settings that reach the final round, to compare their throughput. This is
  the only traffic EasyDPI generates that is not a probe, and it happens nowhere
  except in that last step of the tuner.

**EasyDPI itself**, once when the window opens:

- One request to `api.github.com` asking which release is newest. It carries no
  version number and no identifier — the request says nothing except that somebody
  asked — and a failure is silent, because an unreachable GitHub is the normal
  state of affairs on a blocked network.
- If you accept the update it then downloads that release's archive from GitHub.
  The archive is checked against the SHA-256 the API publishes for it, and an
  archive that does not match is deleted without being opened. There is no path
  that installs an unverified download.
- Set `updateCheck=0` in `bin\config.ini` to switch this off. With it off, EasyDPI
  makes no network calls at all outside the tuner.

That is the entire list. No telemetry, no analytics, no crash reporting, no
identifiers, and nothing is ever uploaded — every request above only fetches.
The source is in [`src/`](src/); the network calls live in
[`src/NetworkTools.cs`](src/NetworkTools.cs), [`src/UpdateCheck.cs`](src/UpdateCheck.cs)
and [`src/Updater.cs`](src/Updater.cs).

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
| `EasyDPI.exe` | built from [`src/`](src/) in this repository | `BA98C2BF75A306A038A6888DD09A1C73C9F8652C8B9C3F7FE95626EDA410EC0C` | — |

The archive also carries `dns\public-resolvers.md` and `dns\relays.md` with
their `.minisig` signatures. These are the DNSCrypt project's public server list,
signed with the project's minisign key, and dnscrypt-proxy verifies that signature
before using them. They are shipped rather than downloaded on first run for a
specific reason: fetching them requires resolving a host through the provider's
plain DNS, which on exactly the networks that need encrypted DNS is the thing
returning forged answers. Without the list the resolver never becomes usable, and
the application then measures a network through a resolver known to be lying.
dnscrypt-proxy still refreshes them in the background once it is running.

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
