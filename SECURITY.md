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
| 1.0.x | ✅ |
| older | ❌ |

Only the latest release receives fixes.

## Security model

Understanding these properties will help you judge the risk of running StartUPs.

### What reduces risk

- **No bundled installers.** StartUPs ships no third-party binaries. Every app is downloaded by winget from the vendor's own official URL, and **winget verifies the SHA-256 hash** of each installer before running it. A tampered download fails the hash check and does not execute.
- **Almost no network code.** The only connection StartUPs opens itself is the update check, and only when you press the button in the Updates tab — never at startup or in the background. All other network activity is performed by winget.
- **Updates are checksum-verified.** When StartUPs downloads a new version, it hashes the file and compares it against the SHA-256 that GitHub published for that release asset. On mismatch the file is deleted and the update is refused.
- **No data collection.** No telemetry, no analytics, no accounts. See [PRIVACY.md](PRIVACY.md).
- **No persistence.** StartUPs writes no files, no registry keys, and no logs. Nothing on disk can be tampered with to change its behaviour on the next run.
- **The catalog is baked in.** `catalog.json` is embedded into the executable at build time, not fetched at runtime. Nobody can remotely alter which packages StartUPs offers — that requires publishing a new build.

### Accepted risks

These are deliberate design trade-offs, and you should be aware of them.

- **It runs elevated.** StartUPs requests administrator rights at launch so a batch install needs only one UAC prompt. Consequently the whole process — and every installer it launches — runs with administrator rights. Any vulnerability in StartUPs, or in an installer it runs, is therefore an elevated one. This is inherent to unattended installation.
- **The executable is unsigned.** Code-signing certificates are expensive, so releases are unsigned and SmartScreen warns about them. Verify downloads using the checksum below.
- **Licence agreements are auto-accepted.** StartUPs passes `--accept-package-agreements` to winget, so package agreements are accepted without being shown. See [TERMS.md](TERMS.md).
- **Trust is delegated to winget.** StartUPs trusts the Windows Package Manager repository and the vendors publishing to it. A malicious or compromised package in that repository would be installed by StartUPs just as it would by winget on the command line.
- **Self-update replaces the running executable.** Installing an update writes a small script to your temporary folder, which waits for StartUPs to exit, overwrites the executable in place, and relaunches it. The checksum check above is what protects that step; anyone able to compromise the GitHub release could distribute a replacement binary. If you would rather not grant that, ignore the Updates tab and download releases manually.

## Verifying your download

Every GitHub release records a SHA-256 digest for its attached file. Compare it against your copy:

```powershell
Get-FileHash .\StartUPs.exe -Algorithm SHA256
```

Match the result against the digest shown on the [release page](https://github.com/Distortionzz/StartUPs/releases). If they differ, **do not run the file**.

Only download StartUPs from this repository's Releases page. Copies hosted anywhere else are not published by the developer and have not been verified.
