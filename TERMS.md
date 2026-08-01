# Terms of Use

**Last updated: 1 August 2026**

By downloading or using StartUPs ("the Software"), you agree to these terms. If you do not agree, do not use it.

## 1. What StartUPs is

StartUPs is a free convenience tool that drives [**winget**](https://learn.microsoft.com/windows/package-manager/), the Windows Package Manager built into Windows. It presents a catalog of applications and runs winget to install the ones you choose.

**StartUPs does not host, distribute, bundle, or modify any third-party software.** Every application is downloaded by winget from that vendor's own official source, and its hash is verified before it is run.

## 2. Third-party software and licences

This is the most important section, so please read it.

Each application StartUPs can install is the property of its own developer and is governed by that developer's own licence agreement or EULA. StartUPs is not a party to those agreements.

**StartUPs passes the `--accept-package-agreements` flag to winget.** This means that when you press Install, any licence agreements attached to the packages you selected are accepted on your behalf, without being displayed to you. By using StartUPs to install software, you confirm that:

- You accept responsibility for reviewing and complying with each application's licence terms
- You have the right to install and use each application you select

Some applications in the catalog are **not free for all users**. For example, WinRAR is trialware, and Visual Studio Community has eligibility conditions restricting commercial use. Confirming your eligibility is your responsibility, not the Software's.

## 3. Administrator rights

StartUPs requires administrator rights, requested once when it starts, so that installers can run unattended. Installers run with those elevated rights. You are responsible for deciding whether to grant them.

## 4. No warranty

The Software is provided **"as is", without warranty of any kind**, express or implied, including but not limited to warranties of merchantability, fitness for a particular purpose, and non-infringement.

The developer does not warrant that the Software will be error-free, that installations will succeed, or that the catalog is accurate or current. Package identifiers can change or be removed from the winget repository at any time, without notice.

## 5. Limitation of liability

To the fullest extent permitted by law, the developer shall not be liable for any damages whatsoever — including data loss, system damage, lost profits, or business interruption — arising from the use of, or inability to use, the Software.

You install software at your own risk. **Back up important data before making significant changes to your system.**

## 6. Trademarks

All product names, logos, and brands in the catalog are the property of their respective owners, and are used purely to identify the software being installed. Their use does not imply any affiliation with, endorsement by, or sponsorship from those owners.

StartUPs is **not affiliated with Microsoft**, nor with any vendor whose software appears in the catalog.

## 7. Unsigned software

The released executable is not code-signed. Windows SmartScreen will warn you about it. You are responsible for satisfying yourself that the file you downloaded is genuine — see [SECURITY.md](SECURITY.md) for how to verify it.

## 8. Acceptable use

Do not use the Software to install software you are not licensed to use, or in any way that breaks applicable law.

## 9. Changes

These terms may be revised. Changes are committed to this repository, so the full history is auditable in git. Continuing to use the Software after a change means you accept the revised terms.

## 10. Governing law

These terms are governed by the laws of the developer's country of residence. Where local consumer-protection law grants you rights that cannot be waived by contract, nothing here limits those rights.

## Contact

**eddygut08@gmail.com**, or open an issue in this repository.
