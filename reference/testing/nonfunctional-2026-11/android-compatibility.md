# Android compatibility test matrix

## Requested configurations

| Configuration | Status | Evidence |
|---|---|---|
| Android 10 / API 29, small profile (Nexus 5) | NOT EXECUTED | The local SDK contained only API 37 system images. The GitHub Actions matrix is prepared; no remote run has been dispatched. |
| Android 14 / API 34, large profile (Pixel 7 Pro) | NOT EXECUTED | The local SDK contained only API 37 system images. The GitHub Actions matrix is prepared; no remote run has been dispatched. |
| Android 17 / API 37, Resizable Experimental AVD | EXECUTED, SUPPLEMENTAL | APK installed, MainActivity launched, sign-in screen rendered, and 198 Flutter tests passed. This does not substitute for API 29/API 34. |

The local SDK did not contain API 29/API 34 images or Android command-line tools to install them. The added [GitHub Actions emulator workflow](../../../.github/workflows/nonfunctional-android-compatibility.yml) uses [ReactiveCircus/android-emulator-runner](https://github.com/ReactiveCircus/android-emulator-runner) with the API 29 Nexus 5 and API 34 Pixel 7 Pro profiles. It records the Android version, API, emulator size/density, app startup hierarchy and screenshot, runs the Flutter suite, and uploads logs. YAML and its two matrix entries were parsed locally. The workflow itself remains NOT EXECUTED.

## API 37 execution

- Device: emulator-5554, Resizable Experimental AVD
- Android release/API: Android 17 / API 37
- Default display: 1080×2400 at 420 dpi (about 411×914 dp)
- Compact override: 720×1280 at 320 dpi (360×640 dp); the physical display settings were reset after capture
- Package: io.github.lucifron28.unipm
- APK: Debug build using the local emulator API URL 10.0.2.2:5254
- Installation: adb install succeeded
- Launch: MainActivity became the top resumed activity
- First cold rendered sign-in screen: ActivityTaskManager recorded Displayed +21s151ms
- Screen review: the sign-in screen controls and button were visible at both captured viewports; no clipping was visually observed on that one screen

Authentic screenshots: [compact viewport](android-api37-small-viewport.png) and [default viewport](android-api37-default-viewport.png). They are from API 37 and show the debug build.

## Automated Flutter checks

- flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/ — passed
- adb install and launcher activity check — passed
- flutter test --device-id emulator-5554 --no-pub --reporter expanded — 198 passed
- flutter test --device-id emulator-5554 --no-pub --reporter expanded test/app_config_test.dart — 4 passed
- Host flutter test --no-pub --reporter expanded — 198 passed
- flutter analyze --no-pub — no issues found
- dart format --output=none --set-exit-if-changed lib test — 64 files formatted, zero changes

The 198 tests include repository/widget cases for asset lookup, asset maintenance history, maintenance drafts, offline repository behavior, and conflict/error states. Those tests execute their existing test doubles/fakes. They are useful automated regression evidence, but they do not represent a live login or authenticated device-to-server workflow.

## Requested feature checks and limits

| Check | Result |
|---|---|
| Install and launch | Passed on API 37 only. The sign-in UI rendered after the first cold launch. |
| Login and authenticated navigation | NOT EXECUTED with the local API on a device. Existing automated tests do not establish a live session. |
| Responsive layout and overflow | Partial: sign-in screen visually inspected at the two API 37 viewports; no operational page layout was inspected. No claim for API 29/API 34. |
| Asset lookup | Existing automated Flutter tests passed; no live account/device lookup was performed. |
| PM form entry | Existing draft/form tests passed; no live form submission was performed. |
| Offline draft persistence | Existing offline/draft tests passed using test doubles; persistence across a real app/device restart was NOT EXECUTED. |
| Synchronization after reconnect | NOT EXECUTED as a live device/API sequence. |
| Maintenance/inspection history | Existing history/repository tests passed; no live account history was opened on the emulator. |
| Error and conflict handling | Existing simulated error/conflict tests passed; no server conflict was induced from the installed app. |

## Reproduction

From the repository root, use the matrix workflow for the two required OS versions. To repeat the local supplemental API 37 run:

    Set-Location .\mobile
    flutter pub get
    flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
    adb install -r .\build\app\outputs\flutter-apk\app-debug.apk
    adb shell monkey -p io.github.lucifron28.unipm 1
    flutter test --device-id emulator-5554 --no-pub --reporter expanded

Do not cite this API 37 supplement as evidence for Android 10/API 29 or Android 14/API 34 compatibility.
