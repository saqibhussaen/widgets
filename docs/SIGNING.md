# Publisher trust and the blocked 0.5 preview

Date: 2026-10-22.

The Windows Security notification says it cannot confirm who published ClockWidget.Core.dll. Review execution also reports App Control error 0x800711C7 for Widgets.dll. The normal self-contained release preview exits before rendering. These are execution restrictions, not successful app/test runs. No Windows security settings, certificate trust stores or policy rules were changed.

The existing 0.4.1 installation was retained and reopened successfully. Its local Default theme now matches the current appearance; previous settings/themes and the previous Default were backed up. Do not replace it with the blocked preview expecting the new UI to work.

Microsoft documents signing with a valid code-signing certificate from a trusted provider, including its Artifact Signing service, for Smart App Control. A self-signed certificate does not establish that publisher trust. A .NET strong name is also not a replacement for an Authenticode publisher signature. See [Microsoft signing guidance](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control) and [Smart App Control FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions).

No valid private-key-bearing code-signing certificate was found in the current user's personal certificate store. No publisher identity, signing account, subscription or certificate purchase was created. Personal Git identity remains a separate deferred decision; do not silently use an employer's identity for publishing.

The next release needs a user-selected trusted publisher identity/signing service. Once available:

1. Build/test in an allowed Windows development/CI environment; retain accurate results for blocked checks.
2. Publish the normal self-contained package, retaining runtime/font notices and the dependency inventory.
3. Authenticode-sign all original executable code: Widgets.exe, Widgets.dll, ClockWidget.Core.dll and ClockFont.dll. Use the selected trusted identity, SHA-256 digests and timestamping. Review existing runtime signatures rather than replacing Microsoft's signatures.
4. Verify signatures and their trust chains before creating the ZIP/checksum; preserve signing reports without private keys or credentials.
5. Validate the signed package on the protected Windows machine, including the clock UI, Custom Text migration/default promotion, media access and explicit playback/audio commands in a user-controlled session.

Rebuilds require fresh signatures. Release identity verification and signing-provider costs/eligibility must be resolved with the user before registration or purchase. The MIT/OFL/data licensing requirements remain unchanged and independent of code-signing trust.

## Selected route

The maintainer selected the free SignPath Foundation program on 2026-10-22. Personal repository ownership/authentication is saqibhussaen; no employer account is used for repository operations. An application is being prepared, not approved. Project reputation/release evidence, public CI, code-signing/privacy policies and maintainer MFA are review requirements. The application may be declined because the project is new. Contact details remain in the private application, not this repository. [Foundation conditions](https://signpath.org/terms).
