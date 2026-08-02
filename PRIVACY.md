# Privacy Policy

**Last updated: 2 August 2026**

Short version: **StartUPs collects nothing about you.** It has no accounts, no telemetry, no analytics, and no crash reporting. The only network request it makes itself is an update check, and only when you ask for it. It does run winget at startup to see which apps are already on your PC — that is a local command, but winget may contact its own source while answering.

## What StartUPs collects

Nothing. There is no data collection of any kind.

StartUPs does not:

- Collect, transmit, or store personal information
- Include analytics, telemetry, or crash reporting
- Require an account, sign-in, or licence key
- Track which apps you select, install, or remove
- Write settings, preferences, or logs to your PC
- Read or modify your registry
- Contact any server operated by the developer — there is no such server
- Check for updates on its own, in the background, or at startup

This is verifiable in the source: there is no analytics library, no logging, and no code that writes settings.

StartUPs does write two kinds of short-lived temporary file, neither of which describes you:

- **A list of installed packages.** Checking what is already on your PC runs `winget export`, which writes its answer to a temporary JSON file. StartUPs reads it and deletes it immediately. It never leaves your PC.
- **The update itself.** Choosing to install an update writes the new executable and a small script next to `StartUPs.exe`, so the replacement can happen after StartUPs exits. Both are removed once it completes.

## Checking for updates

The **Updates** tab asks GitHub whether a newer release exists. This is the only network request StartUPs makes itself, and:

- It happens **only when you press "Check for updates"**. StartUPs never contacts GitHub on its own, in the background, or at startup.
- It is an ordinary anonymous request to `api.github.com`, with no account and no identifier attached. GitHub sees a request from your IP address, as it would from any web browser, governed by the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-privacy-statement).
- Nothing about you, your PC, or the apps you selected is transmitted. The request asks a single question — what is the latest release — and sends nothing else.

If you press **Download and install**, the new executable is downloaded from GitHub, its checksum is verified against the one GitHub published, and StartUPs restarts.

## Checking what is already installed

So it can skip apps you already have, and so the **Installed** view can list them, StartUPs asks winget what is on your PC. This happens at startup, again after any install or removal, and whenever you press **Refresh**.

- It is a **local command**, not a request to the developer. The answer is written to a temporary file, read, and deleted.
- winget may contact its own source while answering, which is Microsoft rather than StartUPs — see below.
- The result never leaves your PC. StartUPs has nowhere to send it and no code that would.
- Only the apps in StartUPs' own catalog are matched against the answer. Everything else winget reports is ignored.

## What happens when you install apps

StartUPs is a front end for [**winget**](https://learn.microsoft.com/windows/package-manager/), the Windows Package Manager built into Windows. When you press **Install Selected**, StartUPs runs `winget` for each app you ticked.

That means network activity does occur — just not by StartUPs itself:

1. **Microsoft** — winget contacts the Windows Package Manager source to look up each package. This is governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).
2. **Software vendors** — winget then downloads each installer from the vendor's own official URL. Valve, Discord, Google, Mozilla and the rest will see a download request from your IP address, exactly as if you had visited their website and clicked download.

These parties may keep their own logs. StartUPs has no visibility into, or control over, what they record.

Removing an app works the same way in reverse: StartUPs runs `winget uninstall` for each app you confirmed, and winget hands the work to that app's own uninstaller. Nothing about what you removed is recorded or sent anywhere.

## Apps you install

Once an app is installed it is entirely independent of StartUPs. Each has its own privacy policy, its own data collection, and possibly its own telemetry. StartUPs neither configures nor restricts any of that. Please review the privacy policy of any app you install.

## Administrator rights

StartUPs requests administrator rights when it starts. This is used for one purpose only: so that installers can run without prompting you separately for each app. It is not used to read, collect, or transmit anything.

## Children's privacy

StartUPs collects no data from anyone, of any age.

## Changes

Any changes to this policy will be committed to this repository, so the full history is publicly auditable in git.

## Contact

Questions about this policy: **eddygut08@gmail.com**, or open an issue in this repository.
