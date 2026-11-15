# TEST-202 — Android 14 / API 34 Windows-host manual verification

**Execution date:** November 15, 2026 (Philippine time)
**Device:** Pixel 7 Pro Android 14/API 34 emulator `UniPM_Android14_API34`; manual density/viewport values were not separately measured in this session.
**Runtime:** Flutter debug APK from corrected `tmp/test201-timestamp-fix` isolated checkout, ASP.NET Core Development API listening on `http://127.0.0.1:5254` via ADB reverse, separate SQL Server 2019 Developer synthetic database `UniPM_TEST202_API34_20261115T055212Z` (not TEST-201).

## Online workflow and form submission

- Inspector synthetic Development account authenticated; dashboard, manual asset-code search and November 2026 schedule selection were functional.
- Fire Extinguisher batch for **Demo CCMS** included synthetic assets `DEMO-FE-001`, `DEMO-FE-002`, and `DEMO-FE-003` with both operational and non-operational outcomes.
- Incomplete batch could not be submitted; after all three rows were completed, whole-form submission succeeded with provisional file number `PMF-2026-0001`.
- Independent read-only SQL verified form ID `143CD122-B2AE-4F21-8689-D99143A6BFA0` in `Submitted` status, exactly **three** associated inspection rows, and `SubmittedAt=2026-11-15 07:06:04.2670875 +00:00`.
- An online edit to `DEMO-FE-001` temporarily displayed a draft conflict warning; using **Retry** restored the edited remarks and SQL confirmed persistence. No claim is made that conflicts cannot occur.

## Offline save, restart and server sync

- The November 2026 schedule for synthetic asset `CPMP-DEMO-DEMO-CCMS-FE-01` was opened while connected.
- The API was stopped, and PowerShell independently confirmed no listener on port 5254 and an unreachable health endpoint.
- The operational inspection was saved while the API was down. UniPM displayed **Saved on this device** and 1 of 2 assets inspected (50%). Remarks were preserved as `test202 Android 14 offline`; the recommendation was blank.
- The saved row was accessible while the app remained open and disconnected.
- After `adb shell am force-stop` and relaunch, the app returned to Login. Offline sign-in failed with a connection error. **Authenticated offline access after a cold restart was blocked.**
- After API restart, health was HTTP 200 `Healthy`. Read-only SQL Server session verification linked API PID `19288` to TEST-202 (`session_id=62`). After successful Inspector sign-in, the existing inspection could be reopened through both Asset Lookup and All PM Tasks.
- Read-only SQL verified `ScheduleCount=1`, `InspectionCount=1`, `MatchingAssetInspectionCount=1`, `IsOperational=1`, original remarks, `ScheduleStatus=Completed`, and `CompletedAt=2026-11-15 07:27:09.0960118 +00:00` (15:27:09 Philippine time). The synchronized inspection ID was `87261565-9AFC-4DBF-BFE5-8DD7C19C2A69`; its schedule ID was `9470F437-E349-4484-815F-EF843E140CEB`.
- `ActionsRecommendations` and `StartedAt` were NULL. These fields were **not** verified as populated.
- The associated form ID `773F4E7D-1547-4EEB-B60A-D489571E6DA6` remained `Draft`, with one inspection, no submission timestamp and no file number. Independently, submitted `PMF-2026-0001` retained status `Submitted` and three inspections. No duplicate inspection was detected in the tested synchronization scenario.

## Camera and untested operations

- Scan Asset QR opened an active camera preview without observed camera-permission blockage.
- A test QR payload `UNIPM-FIRE-EXTINGUISHER-EB77E40F` was looked up in TEST-202 and rendered into a virtual scene, but the code was **not** successfully brought into the scanner view. Recognition and matched-asset navigation were **not verified**.
- Photo-evidence upload, whole-form acknowledgment, real Android 14 physical hardware, and additional platform versions were not established by this manual session.

## Evidence figures

The PNG files below were extracted unchanged from the revised appendix figures. The corresponding original Android emulator screenshots were supplied in the TEST-202 chat. These PNGs document the visible UI; SQL values are taken from captured read-only test output, not recomputed from the images.

- `J.11` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J11-TEST-202.png` — Figure J.11. Android 14 TEST-202: Inspector dashboard after reconnecting and signing in; PMF-2026-0001 is listed for acknowledgment.
- `J.12` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J12-TEST-202.png` — Figure J.12. Android 14 TEST-202: Online whole-form submission confirmation shows provisional file number PMF-2026-0001.
- `J.13` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J13-TEST-202.png` — Figure J.13. Android 14 TEST-202: Locally saved inspection while the API was unavailable; batch progress is 1 of 2.
- `J.14` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J14-TEST-202.png` — Figure J.14. Android 14 TEST-202: Operational condition and offline remarks reopened before the application restart.
- `J.15` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J15-TEST-202.png` — Figure J.15. Android 14 TEST-202: Retained remarks and server-recorded completion displayed after reconnection.
- `J.16` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J16-TEST-202.png` — Figure J.16. Android 14 TEST-202: November 2026 Demo CCMS Fire Extinguisher batch remains In Progress at 50%.
- `J.17` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J17-TEST-202.png` — Figure J.17. Android 14 TEST-202: Live virtual camera preview in the QR scanner; successful QR recognition was not tested.
- `J.18` — `reference/testing/nonfunctional-2026-11/manual-android/TEST-202/screenshots/figure-J18-TEST-202.png` — Figure J.18. Android 14 TEST-202: Synchronized draft inspection accessed from All PM Tasks through Continue PM batch.

**Reference:** `UniPM_Appendices_TEST202_Revised.docx`, Appendix J.1 and the TEST-202 testing conversation. The complete raw SQL console transcript, passwords, access tokens, and database files are deliberately excluded. This record does not claim formal physical-device coverage or universal compatibility.
