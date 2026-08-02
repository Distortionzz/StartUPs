# Security Policy

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report privately using either:

1. **GitHub private vulnerability reporting** — the *Security* tab of this repository, then *Report a vulnerability*. This is preferred.
2. **Email** — **eddygut08@gmail.com**, with "StartUPs security" in the subject.

Please include what the issue is, how to reproduce it, the StartUPs version, your Windows version, and what an attacker could achieve.

StartUPs is maintained by one person as a side project. Reports are handled on a best-effort basis — expect an acknowledgement within about a week, not a same-day response. Please don't disclose publicly until a fix is released, or until we've agreed a timeline.

## Supported versions

| Version | Supported |
|---|---|
| 1.4.x | ✅ |
| older | ❌ |

Only the latest release receives fixes.

## Security model

Understanding these properties will help you judge the risk of running StartUPs.

### What reduces risk

- **No bundled installers.** StartUPs ships no third-party binaries. Every app is downloaded by winget from the vendor's own official URL, and **winget verifies the SHA-256 hash** of each installer before running it. A tampered download fails the hash check and does not execute.
- **Only one network connection of its own.** The single connection StartUPs opens itself is the update check, and only when you press the button in the Updates tab. Everything else on the wire is winget's doing — including the installed-app check described below, which runs `winget export` and may contact the winget source.
- **Updates are checksum-verified, then staged behind the same permissions as the target.** A downloaded version is hashed against the SHA-256 GitHub published for that release asset; on mismatch it is deleted and the update refused. The verified file is then staged in the folder holding `StartUPs.exe`, not the temporary folder, so it cannot be substituted between the hash check and the elevated copy that follows. See *Self-update* below.
- **No data collection.** No telemetry, no analytics, no accounts. See [PRIVACY.md](PRIVACY.md).
- **Nothing persistent.** StartUPs creates no registry keys, no configuration, and no logs, so there is nothing on disk that can be edited to change how it behaves next time. It does write short-lived temporary files: a JSON listing from `winget export` when it checks what is installed, and — during a self-update only — the new executable and a small script, both beside `StartUPs.exe`. All are deleted after use.
- **The catalog is baked in.** `catalog.json` is embedded into the executable at build time, not fetched at runtime. Nobody can remotely alter which packages StartUPs offers — that requires publishing a new build.
- **Removal is explicit.** Uninstalling never happens as a side effect of anything else. It is confined to the Installed view, the queue is listed by name first, and the dialog defaults to Cancel.

### Accepted risks

These are deliberate design trade-offs, and you should be aware of them.

- **It runs elevated.** StartUPs requests administrator rights at launch so a batch install needs only one UAC prompt. Consequently the whole process — and every installer it launches — runs with administrator rights. Any vulnerability in StartUPs, or in an installer it runs, is therefore an elevated one. This is inherent to unattended installation.
- **The executable is unsigned.** Code-signing certificates are expensive, so releases are unsigned and SmartScreen warns about them. Verify downloads using the checksum below.
- **Licence agreements are auto-accepted.** StartUPs passes `--accept-package-agreements` to winget, so package agreements are accepted without being shown. See [TERMS.md](TERMS.md).
- **Trust is delegated to winget.** StartUPs trusts the Windows Package Manager repository and the vendors publishing to it. A malicious or compromised package in that repository would be installed by StartUPs just as it would by winget on the command line.
- **Self-update replaces the running executable.** Installing an update stages the verified file next to `StartUPs.exe`, writes a small script beside it, and exits; the script waits for the process to end, overwrites the executable, and relaunches it. That copy runs elevated, which is why the staging folder matters: it is the folder already holding `StartUPs.exe`. Anyone able to write there could replace the executable directly without waiting for an update, so staging there grants no access that was not already available. If the folder cannot be written to, the update is abandoned rather than staged somewhere weaker.
- **The update is only as trustworthy as the GitHub release.** The published SHA-256 arrives in the same API response as the download link, so it proves the file was not corrupted in transit — not that it is genuine. Anyone who compromised the repository or its credentials could publish a release, and StartUPs would install it, elevated, for anyone who pressed Download. There is no signature to fall back on. If you would rather not grant that, ignore the Updates tab and fetch releases manually.
- **Uninstalling can destroy data.** Removal is user-initiated and confirmed, and the work is handed to winget, but it runs elevated and it is not reversible. Game launchers are the sharp edge — removing Steam, Epic, EA, Ubisoft, GOG or Battle.net can take the content they installed with them, so those are named individually in the confirmation. Removing anything from the Runtimes category may break unrelated software that depends on it.
- **The installed-app check runs automatically.** On startup, and after each run, StartUPs asks winget what is present on the PC. This is a local query rather than a StartUPs network call, but it is not something you trigger, and winget may reach its source while answering. It reports only what winget knows about, so treat it as *not detected* rather than proof an app is absent.

## Verifying your download

Every GitHub release records a SHA-256 digest for its attached file. Compare it against your copy:

```powershell
Get-FileHash .\StartUPs.exe -Algorithm SHA256
```

Match the result against the digest shown on the [release page](https://github.com/Distortionzz/StartUPs/releases). If they differ, **do not run the file**.

Only download StartUPs from this repository's Releases page. Copies hosted anywhere else are not published by the developer and have not been verified.
