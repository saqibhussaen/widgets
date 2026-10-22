# Code signing policy

Status: applying for the free SignPath Foundation open-source program. No approval,
certificate or signed release is claimed yet. Current release artifacts are unsigned.

## Project and responsible roles

- Repository owner, committer and reviewer: [Saqib Hussain / saqibhussaen](https://github.com/saqibhussaen).
- Release and signing approver: Saqib Hussain / saqibhussaen.
- Original application code/assets: MIT. Bundled fonts and runtime components retain
  the licenses listed in [the inventory](../licenses/INVENTORY.md).

Signing is restricted to original Widgets binaries built from this repository
by the public Windows GitHub Actions workflow. Tests and license checks must pass.
The approver reviews the exact commit, build and files before approving each signing
request. No automatic release approval is permitted. Maintainer accounts must have
MFA enabled before signing access is granted.

Original signing targets are Widgets.exe, Widgets.dll,
ClockWidget.Core.dll and ClockFont.dll. Third-party runtime binaries are not
re-signed as our own. Signed artifacts require timestamp/signature verification,
preserved notices, checksums and a final Windows compatibility check before a
stable release. Private keys and signing API tokens must never enter Git.

If the application is approved and configured, free code signing will be provided
by [SignPath.io](https://signpath.io/), with the certificate by
[SignPath Foundation](https://signpath.org/). Its certificate identifies SignPath
Foundation as publisher. This statement is conditional; the project has not yet
received that service. Approval and continued eligibility are controlled by
SignPath Foundation under its [conditions](https://signpath.org/terms).

## Privacy

See [the privacy policy](PRIVACY.md). The program sends no telemetry, track history,
accounts or personal settings to a project-operated service. Weather and city
search contact the disclosed providers only after the user explicitly enables or
requests them. Automatic weather can subsequently refresh using the existing
consent. Media metadata remains in memory and is not recorded or uploaded by this
app. Signing workflows receive source/build artifacts, never user preferences.
