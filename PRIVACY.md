# Privacy Policy

**Last updated: 1 August 2026**

Short version: **StartUPs collects nothing about you.** It has no accounts, no telemetry, no analytics, and no crash reporting, and it makes no network connections of its own.

## What StartUPs collects

Nothing. There is no data collection of any kind.

StartUPs does not:

- Collect, transmit, or store personal information
- Include analytics, telemetry, or crash reporting
- Require an account, sign-in, or licence key
- Track which apps you select or install
- Write settings, logs, or any other files to your PC
- Read or modify your registry
- Contact any server operated by the developer — there is no such server

This is verifiable: the source code in this repository contains no HTTP client, no socket use, and no file-writing code. The only outbound action StartUPs performs is launching the `winget` command.

## What happens when you install apps

StartUPs is a front end for [**winget**](https://learn.microsoft.com/windows/package-manager/), the Windows Package Manager built into Windows. When you press **Install Selected**, StartUPs runs `winget` for each app you ticked.

That means network activity does occur — just not by StartUPs itself:

1. **Microsoft** — winget contacts the Windows Package Manager source to look up each package. This is governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).
2. **Software vendors** — winget then downloads each installer from the vendor's own official URL. Valve, Discord, Google, Mozilla and the rest will see a download request from your IP address, exactly as if you had visited their website and clicked download.

These parties may keep their own logs. StartUPs has no visibility into, or control over, what they record.

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
