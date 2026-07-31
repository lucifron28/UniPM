---
id: TEST-031
type: test-run
title: Flutter mobile foundation verification
status: executed
recordedAtUtc: 2026-07-31T14:54:46Z
testedCommit: b93eb9b48d7235c337d1854240615a0290534934
sourceBranch: feat/mobile-foundation
evidenceLevel: locally-executed
buildTestedCommit: b93eb9b48d7235c337d1854240615a0290534934
buildVerificationStatus: passed
---

# Flutter Mobile Foundation Verification

## Objective

Verify memory-only mobile authentication with fictional users, deterministic
injected HTTP clients, and no live backend.

## Execution Identity

- Tested source commit:
  `b93eb9b48d7235c337d1854240615a0290534934`
- Source branch: `feat/mobile-foundation`
- Starting main commit: `4085fc27c4865551a90162ba6f41dbbe3bb8adfc`
- Execution date: 2026-07-31 Asia/Manila (`2026-07-31T14:54:46Z`)

## Commands

```powershell
cd mobile
flutter test test/mobile_foundation_test.dart
flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/
```

## Results

- `flutter test test/mobile_foundation_test.dart`: passed all eight focused
  authentication tests.
- `flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/`:
  passed and produced `build/app/outputs/flutter-apk/app-debug.apk`.

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Focused mobile authentication tests | 8 | 0 | 0 | 8 |
| Debug Android APK build | 1 | 0 | 0 | 1 |

## Behavior Covered

- Successful login renders the authenticated shell.
- App startup begins signed out.
- Direct login 401 displays `Invalid email or password.`.
- Unsupported roles remain bounded.
- Logout clears the memory-only session.
- A login `Set-Cookie` is isolated to its client instance; fresh clients for
  `/auth/me` and logout receive no Cookie header.
- `/api/v1/auth/me` receives the in-memory bearer token.
- Redirect following is disabled for API requests.
- A post-login `/auth/me` 401 clears the session and displays the expired
  message without refresh or replay.
- An authenticated protected-request 401 signs out without replay.

## Verification Scope

Tests use deterministic fake gateway/HTTP responses and fictional users. No
live backend, emulator connectivity, emulator integration test, release build,
full repository suite, or production deployment verification was run.

## Generated Artifacts

No credentials, secrets, tokens, cookies, logs, or real institutional records
were recorded. The debug APK is a local build artifact and is not committed as
evidence.
