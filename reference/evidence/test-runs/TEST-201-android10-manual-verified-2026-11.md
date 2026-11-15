# TEST-201 — Android 10 / API 29 Windows-host manual verification

**Execution window:** November 14–15, 2026 (Philippine time)
**Environment:** Android 10/API 29 emulator `UniPM_Android10_API29_Fixed` (reported separately from the earlier CI Nexus 5 profile), Flutter 3.47.1, local ASP.NET Core API on port 5254 with ADB reverse, dedicated SQL Server 2019 Developer synthetic database `UniPM_TEST201_API29_20261114T134245Z`.
**Source baseline:** `e8da30ede4ead2a46947dc9612c92af60b892491`; corrected API29 retest used uncommitted changes from isolated checkout `tmp/test201-timestamp-fix`, not a published source revision at the time of this record.

## Verified observations

- Debug APK built, installed and launched; Inspector authentication, dashboard navigation, asset lookup and maintenance-history navigation were demonstrated on the emulator.
- Synthetic `DEMO-EL-003` inspection was saved; form `PMF-2026-0001` was submitted and shown as **Awaiting acknowledgment**. Actual acknowledgment was not executed.
- Synthetic `DEMO-FE-003` was prepared online and saved locally after ADB reverse was removed. UniPM displayed **Saved on this device**.
- After a force-stop and restart with the backend still unreachable, offline authentication failed. The saved inspection became accessible after connectivity was restored and the Inspector signed in again.
- Read-only SQL verification after initial sync showed `IsOperational=0`, inspection date `2026-11-14`, preserved offline remarks and recommendation; server `UpdatedAt=2026-11-14 15:17:12.3337019 +00:00`.
- An original-build inspection edit caused an **Update inspection needs review** conflict; the initial retry left the server row unchanged. This is an observed failed attempt, not a pass.
- The likely concurrency issue related to differing date/time token resolutions; the HTTP conflict payload was not captured, so its mechanism was not proven.
- A corrected build was validated by **27/27 targeted Flutter tests** and **39/39 targeted backend tests**. The APK was installed with `adb install -r` without clearing app data. The retained edit was retried successfully, and SQL confirmed the updated remarks and recommendation (`UpdatedAt=2026-11-14 17:12:52.7923362 +00:00`). This is a separate successful corrected-build retest.

## Scope and limitations

Manual TEST-201 established local-save persistence and recoverability **after reconnection and reauthentication**; it did not establish authenticated use while offline after a cold restart. A physical Android 10 device and actual QR camera decoding were not tested. Acknowledgment was not executed. The baseline failed retry and corrected-build retest must remain distinguished.

## Evidence figures

The PNG files below are images extracted unchanged from figures in the revised appendix supplied during this testing project. These are embedded copies, not independently recovered raw emulator capture files.

- `J.3` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J03-TEST-201.png` — Figure J.3. Android 10 TEST-201: DEMO-EL-003 inspection recorded (1/1 asset inspected).
- `J.4` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J04-TEST-201.png` — Figure J.4. Android 10 TEST-201: PMF-2026-0001 awaiting acknowledgment after submission.
- `J.5` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J05-TEST-201.png` — Figure J.5. Android 10 TEST-201: offline DEMO-FE-003 inspection saved on the device.
- `J.6` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J06-TEST-201.png` — Figure J.6. Android 10 TEST-201: sign-in unavailable after offline restart; access blocked.
- `J.7` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J07-TEST-201.png` — Figure J.7. Android 10 TEST-201: DEMO-FE-003 values visible after reconnecting and signing in.
- `J.8` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J08-TEST-201.png` — Figure J.8. Android 10 TEST-201 baseline: local/server inspection-edit comparison and retry action.
- `J.9` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J09-TEST-201.png` — Figure J.9. Android 10 corrected APK: saved edit still queued for review before retry.
- `J.10` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-201/screenshots/figure-J10-TEST-201.png` — Figure J.10. Android 10 corrected APK: unsubmitted draft displayed without sync-review warning after retry.

**Reference:** `UniPM_Appendices_TEST202_Revised.docx`, Appendix J.1, supplementary TEST-201 evidence. See the related SQL and emulator observations documented there. This evidence package does not contain a raw SQL dump, authentication secrets, or full shell-history logs.
