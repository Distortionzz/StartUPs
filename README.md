# StartUPs

**Pick your apps. Install them all at once.**

StartUPs is a Windows desktop app for people who just built or bought a new PC. Tick the apps you want from a categorised catalog, press one button, and it installs every one of them silently while you go do something else.

![StartUPs](docs/screenshot.png)

---

## Why it exists

Setting up a fresh Windows install means visiting a dozen websites, dodging the wrong download button, and clicking through a dozen installers. StartUPs collapses that into one screen and one click.

## Features

- **94 apps across 11 categories** — Gaming, Development, Browsers, Media, Utilities, Communication, Imaging, Documents, Security, Online Storage, Runtimes
- **Real icons for almost every app** — 91 of 94 carry the genuine brand mark
- **One UAC prompt for the whole batch** — not one per app
- **Live download progress** with a real transfer-speed readout
- **Skips what you already have** — checks each app before installing
- **Sees what is already on the PC** — one winget call badges every app it detects, refreshed after every run
- **Uninstall too** — the Installed view lists what StartUPs can see and removes any of it in one batch
- **Choose where things land** — point 53 of the 94 apps at another drive, each into its own subfolder, and get told about any that ignored it
- **Select Essentials** — one click ticks the 19 apps almost everyone wants
- **Instant search** across app names, descriptions, and package IDs
- **Cancel any time** — stops the current download and leaves the rest untouched
- **Built-in updater** — checks GitHub on request, verifies the download's checksum, stages it behind the same permissions as the app, and restarts into the new version
- **Single portable .exe** — no installer, no .NET runtime required

![Splash screen](docs/splash.png)

## Download

Grab `StartUPs.exe` from the [latest release](https://github.com/Distortionzz/StartUPs/releases). It is a single self-contained file — no installer, nothing to unzip.

On first run Windows SmartScreen will warn about an unrecognised app, because the executable is unsigned. Click **More info → Run anyway**.

## How it works

StartUPs never hosts or downloads installers itself. It drives [**winget**](https://learn.microsoft.com/windows/package-manager/), Microsoft's package manager built into Windows, which fetches each app from the vendor's own official URL and verifies its hash before running it.

```
Launch
   |
   +-- UAC elevation prompt (once)
   +-- winget export -> what is already on this PC, badged in the background
   |
Click Install                          Click Uninstall (Installed view)
   |                                      |
   +-- Sequential queue:                  +-- Confirm the queue by name
   |     winget list    -> skip if here   |
   |     winget install -> silent,        +-- Sequential queue:
   |                       live progress  |     winget uninstall -> silent
   |                                      |
   +-- Summary -----------------------------+-- Summary
                     |
                     +-- re-check what is installed
```

Runs happen one app at a time because winget takes a machine-wide lock — parallel installs would simply fail.

## Choosing where apps install

**Install to** in the footer sets a root folder — useful when Windows sits on a small SSD and there is a roomier drive alongside it. Leave it blank and every app goes wherever its own installer puts it.

winget installs into exactly the path it is given, so each app gets its own subfolder under the root rather than all of them landing in one directory.

This cannot apply to the whole catalog, and the reason is worth understanding: **winget passing a folder to an installer does not mean the installer uses it.**

winget hands the path over as whatever switch that installer family expects. Whether it is obeyed depends on who wrote the installer, and winget reports success either way — so an installer that ignores it looks identical to one that honoured it.

| Installer family | How the path is passed | Obeyed? |
|---|---|---|
| Inno Setup | `/DIR=` | ✅ handled by Inno itself |
| NSIS | `/D=` | ✅ handled by NSIS itself |
| portable | winget places the file | ✅ winget is in control |
| MSI / WiX | `TARGETDIR` | ⚠️ usually not — see below |
| burn | forwarded to a nested installer | ⚠️ depends on that installer |
| exe, MSIX | not supported at all | ❌ |

**Why `TARGETDIR` usually fails.** An MSI builds its install path from a chain of directories. Epic Games Launcher's looks like this:

```
INSTALLDIR  <  SELECTEDINSTALLFOLDER  <  ProgramFiles64Folder  <  TARGETDIR
```

`ProgramFiles64Folder` is a *system* folder that Windows resolves itself, and it always lands on the real Program Files. Setting `TARGETDIR` therefore never reaches the app's own folder. That is how the package is built, not a winget bug.

Those MSIs almost always expose their own public property — `INSTALLDIR`, `INSTALLFOLDER`, `INSTALLLOCATION`, `INSTALL_ROOT` — which *can* be set. A catalog entry names it with `locationProperty`, and StartUPs passes that with `--custom` instead of `--location`. Thirteen apps use one, read out of their own MSIs.

| | Apps |
|---|---|
| Redirected natively (inno, nullsoft, portable) | **40** |
| Redirected via an MSI property override | **13** |
| Install where their own installer decides | **41** |

Chrome and Zoom were checked and genuinely expose nothing settable. EA App, Python and PowerToys ship `burn` bundles, whose payload was not inspected. Apps that cannot be redirected are simply installed normally, and the footer says how many that is before you start.

Because even an Inno or NSIS installer can ignore the switch, StartUPs checks afterwards. Any app that installed successfully but left the chosen folder missing is listed at the end of the run, so a silent miss is visible rather than something you find weeks later.

**Games are the exception worth knowing.** Steam, Epic, EA, Ubisoft, GOG and Battle.net each manage their own library folders, and that is where the hundreds of gigabytes actually go. Moving the launcher does not move the games — set the library location inside the launcher instead.

## Removing apps

The **Installed** entry in the sidebar shows only the catalog apps StartUPs can find on this PC. Tick any of them and the footer button turns red: **Uninstall Selected**.

Detection is a single `winget export` call made in the background, not one check per app — across a 94-app catalog that would mean 94 process launches. It costs about two seconds and never blocks the window.

It runs at startup, again after any install or uninstall run finishes, and on demand via **Refresh** in the Installed view — useful when something was installed outside StartUPs while it was open.

Removal is deliberately harder to do by accident than installing:

- Ticks never carry between the install and Installed views; switching clears the selection
- Every app in the queue is listed by name before anything runs, and the dialog defaults to Cancel
- Launchers that manage their own content — Steam, Epic, EA, Ubisoft, GOG, Battle.net — are called out separately, because removing them can delete the games they installed
- Anything from the Runtimes category is flagged too, since other software on the PC may depend on it

Some installers refuse to run without showing their own window, which a silent removal cannot answer. Those are reported as failures at the end of the run and need uninstalling from Windows Settings instead.

## Requirements

- Windows 10 (1809+) or Windows 11
- winget, included with **App Installer** — preinstalled on Windows 11
- Administrator rights (StartUPs requests them at launch)

No .NET installation needed. The runtime is bundled inside the executable.

## Building from source

```
git clone https://github.com/Distortionzz/StartUPs.git
cd StartUPs
dotnet build
```

To produce the distributable single file:

```
dotnet publish StartUPs/StartUPs.csproj -c Release
```

The result lands in `StartUPs/bin/Release/net8.0-windows/win-x64/publish/StartUPs.exe` — around 69 MB, self-contained and compressed.

## Adding an app to the catalog

The catalog is plain data, so adding apps needs no code changes. Edit [`StartUPs/catalog.json`](StartUPs/catalog.json) and add an entry under the category you want:

```json
{
  "wingetId": "Valve.Steam",
  "name": "Steam",
  "description": "The biggest PC game store and library.",
  "essential": true,
  "supportsLocation": true
}
```

Find the right `wingetId` with:

```
winget search "app name"
```

Confirm it resolves exactly before adding it:

```
winget search --id Valve.Steam --exact
```

Set `essential` to `true` to include the app in the **Select Essentials** one-click preset.

Set `supportsLocation` to `true` for `inno`, `nullsoft` and `portable` installers, which implement the directory switch themselves. Check the type first:

```
winget show --id Valve.Steam --exact | findstr /i "Installer Type"
```

For an `msi` or `wix` package, `--location` alone will usually be ignored, so open the MSI and find the public directory property its install folder hangs from — an all-uppercase id such as `INSTALLDIR` — then add it as `locationProperty`:

```json
{ "wingetId": "7zip.7zip", "name": "7-Zip", "supportsLocation": true, "locationProperty": "INSTALLDIR" }
```

Leave both off for `exe`, `msix` and `burn`. A path that is accepted and quietly ignored is worse than not offering the choice. The file is embedded into the executable at build time, so rebuild after editing.

## Project layout

```
StartUPs/
  StartUPs.sln
  docs/                      screenshots
  StartUPs/
    catalog.json             the app catalog (embedded at build time)
    icons.json               vector brand glyphs, keyed by winget ID
    Assets/AppIcons/         PNG icons for apps with no vector glyph
    Models/                  AppEntry, Catalog, InstallState, UpdateInfo
    Services/
      CatalogService.cs      loads the embedded catalog
      IconService.cs         resolves each app's icon
      WingetService.cs       runs winget: install, uninstall, detect
      UpdateService.cs       checks GitHub, verifies and stages a new build
    Theme.xaml               dark palette and control styles
    SplashWindow.xaml(.cs)   animated startup splash
    MainWindow.xaml(.cs)     layout, filtering, install and uninstall queues
    app.manifest             requests administrator at launch
    icon.ico                 app icon, 7 sizes
```

## App icons

91 of the 94 apps show their genuine icon, from one of two sources:

- **56 apps** use vector brand glyphs from [Simple Icons](https://simpleicons.org/), stored as raw path data in `icons.json` and rendered natively by WPF. They stay crisp at any size and cost about 65 KB of path data in total.
- **35 apps** with no brand glyph — mostly niche utilities such as HWiNFO, GPU-Z and Everything — ship as PNGs extracted from their official installers at 128x128.

Many installers expose only a generic wrapper icon, so the real mark usually has to be recovered from the payload: `msiexec /a` unpacks MSI packages, 7-Zip handles NSIS and embedded archives, and `innoextract` cracks Inno Setup. That is how PuTTY, WinDirStat, IrfanView, WizTree, MediaMonkey and WinMerge get their genuine icons rather than a stock monitor graphic.

The remaining **3** fall back to a generated letter tile, coloured deterministically so an app always looks the same. The two VC++ Redistributables have no brand mark to find, and KeePass ships an Inno Setup revision newer than innoextract 1.9 can unpack.

Simple Icons has removed the Adobe, Microsoft, Amazon and Java marks following trademark requests, so those apps use extracted PNGs instead.

Brand colours that are close to black are automatically swapped for a light substitute, so marks like Steam's stay visible against the dark theme.

Simple Icons is CC0; the brand marks themselves remain trademarks of their respective owners.

## Known limitations

- **SmartScreen warning.** The executable is unsigned, so Windows shows "Windows protected your PC" on first run. Click *More info -> Run anyway*. Removing this requires a paid code-signing certificate.
- **Startup takes a few seconds.** The single-file build compresses the whole .NET runtime into one executable, and Windows must unpack it before any code runs — roughly 3 seconds warm, longer on a first run. The splash screen only appears after that, so it cannot cover it. Turning off `EnableCompressionInSingleFile` would roughly halve the wait at the cost of doubling the file size.
- **Progress covers downloading only.** Once a file is downloaded, winget hands off to the vendor's own installer, which reports no progress — the bar sits at 100% showing "Installing..." until it finishes.
- **Microsoft Store apps excluded.** Apps that exist only in the Store (NVIDIA App, WhatsApp, MusicBee) are left out of the catalog because Store packages do not reliably install silently.
- **A few apps have no winget package at all.** FileZilla, for instance, is absent from the winget source entirely, so it cannot be offered no matter how popular it is.
- **Detection only sees what winget sees.** The Installed view is built from winget's own inventory, so an app put on the PC by other means may not appear even though it is there. It is a "not detected" list, not proof of absence.
- **Upstream packages can break.** A winget manifest points at the vendor's own URL and pins the installer's hash, so a vendor who silently replaces a file breaks the package for everyone until the manifest is updated. AIMP (hash mismatch) and RealVNC Viewer (dead URL) were both dropped from the catalog for this reason — winget correctly refuses to install either.

## Licence

StartUPs is released under the [MIT Licence](LICENSE) — free to use, modify and redistribute, including commercially, provided the copyright notice is kept.

This covers **StartUPs' own source code only**. It does not extend to:

- The applications in the catalog, each governed by its own licence
- Brand names, logos and trademarks, which remain the property of their owners
- Icons extracted from third-party installers, which remain the property of those vendors

## Policies

- [Privacy Policy](PRIVACY.md) — StartUPs collects nothing; what winget contacts and why
- [Terms of Use](TERMS.md) — no warranty, third-party licences, and how package agreements are accepted
- [Security Policy](SECURITY.md) — how to report a vulnerability, the security model, verifying your download

## Built with

- C# / .NET 8 / WPF
- winget (Windows Package Manager)

## Icon

![Icon sizes](docs/icon-sizes.png)
