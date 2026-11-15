# UniPM manual Android APK identity and reproducibility record

**Recorded:** November 15, 2026 (Philippine time)
**Scope:** TEST-201 (Android 10/API 29) and TEST-202 (Android 14/API 34) supplemental manual compatibility testing.

## Binary identity — verified on Android 14

- **Android package:** `io.github.lucifron28.unipm`
- **Emulator:** `UniPM_Android14_API34` (`emulator-5554`; SDK/API level `34`)
- **Local candidate:** `tmp/test201-timestamp-fix/mobile/build/app/outputs/flutter-apk/app-debug.apk` (debug APK)
- **APK size:** `97,591,460` bytes
- **APK SHA-256:** `A233BFC04E138AD81B811B7941FF800DF609E29F9E8DEF6C9B0A933B5818E195`
- **Independent build output:** `mobile/build/app/outputs/apk/debug/app-debug.apk` in the same isolated checkout has the same size and SHA-256.
- **On-device check:** `adb -s emulator-5554 shell pm path io.github.lucifron28.unipm` returned an installed `base.apk` path. `adb -s emulator-5554 shell sha256sum <base.apk-path>` returned `a233bfc04e138ad81b811b7941ff800df609e29f9e8def6c9b0a933b5818e195`; Windows `Get-FileHash` returned the same digest (case-insensitive match).
- **Installed package timestamps** reported by `dumpsys package`: `firstInstallTime=2026-11-15 04:56:34`; `lastUpdateTime=2026-11-15 04:57:47` (emulator-recorded local time).
- **Local APK file modified:** November 15, 2026 at 01:09:48 (Windows-reported local timestamp).

**Evidence limit:** The hash match directly verifies the equality of the inspected local APK and the currently installed Android 14 `base.apk` at verification time. The package installation times support association with TEST-202, but no independently preserved historical on-device hash was recorded during the original functional test itself. Do not describe this as absolute retrospective proof.

## Source/build context and private preservation

- **Original testing-branch source base:** `e8da30ede4ead2a46947dc9612c92af60b892491` (`test(android): harden scrolling and emulator readiness`).
- **Published manual-evidence commit:** `5e56dfcdeb1bd9af18fba6ce6f0541e4da2dcfc3` (`test(android): add TEST-201 and TEST-202 manual evidence`). This commit adds 18 evidence files only; **it does not contain the corrected application source changes**.
- **Flutter:** `3.47.1`, stable; framework revision `6655482ec0`.
- **Dart:** `3.13.1` (Flutter toolchain).
- **Corrected checkout:** `tmp/test201-timestamp-fix`, with **eight locally modified source/test files**, reported as 247 insertions and 45 deletions at the point of source review.
- **Private patch backup filename:** `TEST201_TEST202_corrected_source.patch`, stored outside the repository under the user's Documents folder.
- **Private patch SHA-256:** `DC862D45488EA299639C9F2A6125AE2F9730F443B7498AD50E8D8D511417AFFE`.

The patch is a snapshot of the checkout's **uncommitted changes at backup time**. Its existence and hash preserve that snapshot, but they do **not** independently establish that every patch line was present when the APK was built. This record intentionally does not claim a source-commit-to-APK reproducible-build verification.

## Report and screenshot references

- `reference/evidence/test-runs/TEST-201-android10-manual-verified-2026-11.md`
- `reference/evidence/test-runs/TEST-202-android14-manual-verified-2026-11.md`
- `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/` (Figures J.3–J.10)
- `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/` (Figures J.11–J.18)
- Published evidence: `https://github.com/lucifron28/UniPM/commit/5e56dfcdeb1bd9af18fba6ce6f0541e4da2dcfc3`

## Publication and security handling

This Markdown record is suitable for committing to the testing evidence branch after review. **Do not commit the APK, private source patch, SQL database copies, secrets, or temporary working directories by default.** The APK is a development debug artifact and is identified here by digest and size. The private patch must stay private pending source/security review.

This documentation supplements Appendix J.5 of the UniPM appendices; it does not alter the previously executed TEST-201 or TEST-202 scenarios, their limitations, or database records.
