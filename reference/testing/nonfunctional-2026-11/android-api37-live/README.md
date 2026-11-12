# Supplemental Android API 37 authenticated smoke

This local smoke check supplements the requested API 29/API 34 matrix. It does not substitute for those operating-system versions.

## Environment

- Test date: 2026-11-13, Asia/Manila
- Emulator: Android 17 / API 37, device `emulator-5554`
- Display: 1080×2400 at 420 dpi (approximately 411×914 dp)
- Package: `io.github.lucifron28.unipm`
- Build: Flutter 3.47.1 debug APK, API base URL `http://10.0.2.2:5254/`
- Backend: local ASP.NET Core API on port 5254 with an isolated SQL Server 2019 compatibility-level-150 database populated with synthetic/demo records
- Account: synthetic development Maintenance Inspector; no credentials are stored in committed evidence

## Actual checks

| Check | Outcome | Evidence |
|---|---|---|
| APK installation and app launch | Passed | The app launched to its sign-in view, then authenticated navigation was observed after signing in with the synthetic account. |
| Login and authenticated navigation | Passed | The authenticated home showed the Maintenance Inspector role. At screenshot time, task panels still displayed loading indicators, so task counts and item contents are not claimed. [Screenshot](authenticated-home.png) |
| Asset lookup | Passed | Search returned synthetic CCMS assets, and the first result opened its asset details. [Screenshot](asset-search.png) |
| Maintenance history | Passed | The asset history loaded two acknowledged synthetic records. [Screenshot](maintenance-history.png) |
| PM schedule selection | Passed up to schedule details | A December 2026 due schedule loaded its cycle and due date. The draft list remained in “Checking available Draft forms…” state. [Screenshot](pm-form-list-loading.png) |
| PM form entry | Blocked | A bounded, read-only authenticated request to `/api/v1/preventive-maintenance-forms?page=1&pageSize=100` ended with `TaskCanceledException` after 20,046 ms. No form was entered, created, or submitted. |
| Offline draft persistence and reconnect sync | Not executed on device | Existing Flutter tests use repository fakes; they do not establish device persistence or live synchronization. |
| Live conflict/error recovery | Not executed on device | No server conflict or error was induced from the installed app. |

The outcome table is supplemental API 37 smoke evidence only. It does not establish responsive behavior or workflow compatibility on API 29/API 34. The 198-test Flutter suite and the test-double coverage are reported separately in the [Android compatibility matrix](../android-compatibility.md).

## Source logs and reproduction

The local command output, database seed logs, APK build/install records, accessibility hierarchies, and form-request probe are retained in the ignored workspace folder `../../../../artifacts/nonfunctional-2026-11/android-api37-live/`. No password file is linked or committed.

Recreate the isolated development environment using the database migration and synthetic/demo seed commands documented in the parent [testing README](../README.md). Build and launch the Flutter debug app with the emulator API URL:

```powershell
Set-Location .\mobile
flutter pub get
flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
adb install -r .\build\app\outputs\flutter-apk\app-debug.apk
adb shell monkey -p io.github.lucifron28.unipm 1
```

Authenticate only with a synthetic development account. The local form endpoint should be resolved before claiming live form-entry, offline-draft, or synchronization coverage.
