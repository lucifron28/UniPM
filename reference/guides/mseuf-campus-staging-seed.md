# MSEUF campus names in the synthetic staging dataset

Reviewed October 18, 2026. Facility names below come from official MSEUF
publications. **Equipment, placements, inventory codes, inspections, dates,
findings, signatures and approvals are synthetic. They do not record work
performed by MSEUF personnel.** The four `.test` users are demonstration
accounts, not institutional identities. Real names of university personnel
must not replace the invented signatories.

## Sources and field mapping

| Official source | Publicly supported information | UniPM fields | Synthetic assumptions |
|---|---|---|---|
| [GSD physical facilities](https://mseuf.edu.ph/gsd/pages/mseuf-physical-plants-and-facilities), also [supplied official host](https://cpcalendars.mseuf.edu.ph/gsd/pages/mseuf-physical-plants-and-facilities) | Administration Building, CCMS, CNAHS, University Library Complex and University Gymnasium are listed facilities | Asset/form `Building`: Administration Building, CCMS Building, CNAHS Building, University Library Complex, University Gymnasium | "CCMS Building" and "CNAHS Building" are readable aliases for the listed college facilities. The source does not define these building-title conventions. All equipment counts and positions are invented. |
| [CCMS facilities](https://cpcalendars.mseuf.edu.ph/ccms/pages/facilities-11) | CL-01 Digital Animation and Game Development Laboratory; CL-03 Web, Mobile App, Software Development and Data Science Lab; CL-04 Cisco and Huawei Laboratory | `Location`: CL-01, CL-03, CL-04; `Department`: CCMS | Assignment of one extinguisher to each lab is synthetic. No floor, wall, coordinate or installed equipment inventory is asserted. |
| [CCMS laboratory news, January 31, 2024](https://mseuf.edu.ph/news/2024/1/31/enverga-university-acquires-p37m-imac-m3-laboratory) | Computer Laboratory 07 and an Innovation Laboratory existed in the CCMS building at publication | Supporting context only; not additional seed rows | No current inventory or exact laboratory floor is inferred. CL-07 is not needed for the fixed 25-asset dataset. |
| [GSD contacts](https://mseuf.edu.ph/gsd/pages/contacts) | GSD office address is the Library Complex; the footer identifies the Registrar's Office and Office of Student Affairs and Services | `Department`: General Services Department, Registrar's Office and Office of Student Affairs and Services | Assigning GSD to gym/common-area assets and Registrar or student-affairs groups to Administration Building assets is scenario grouping. The source does not establish these asset assignments or place the Registrar and student-affairs offices in the Administration Building. |
| [About GSD](https://mseuf.edu.ph/gsd/pages/about-the-general-services-department), accessible [official alternate host](https://cpcontacts.mseuf.edu.ph/gsd/pages/about-the-general-services-department) | GSD's preventive/corrective maintenance responsibility | General Services Department terminology | No named staff, completed maintenance work or institutional authorization is inferred. |
| [GSD CPMP overview](https://mseuf.edu.ph/gsd/pages/comprehensive-preventive-maintenance-program) | Public description of preventive maintenance services | Context only | Category months still come from the approved UniPM CPMP manual requirements, not the website's broader prose. |

Direct fetches of the physical facilities and CCMS pages timed out or missed
the browser cache. Search results from official domains returned their page text,
including the named facilities/laboratories. GSD contacts, laboratory news
and CPMP pages loaded directly. No proposed building in a research paper was
used as an existing facility.

Library reading/circulation areas, corridors, common areas, entrances, exit
approaches and stairwell approaches are general scenario descriptions. The
source pages do not verify whether each area exists or where it sits in a floor
plan. The `Library` and `CNAHS` labels group seed assets for display. GSD must
approve any institutional department directory.

## Staging identifier convention

Asset codes use `<CATEGORY>-<CAMPUS-GROUP>-<NNN>`:

- `FE`, `FA`, `EL`, `WDS`: extinguisher, alarm, emergency light, drinking station.
- `CCMS`, `LIB`, `CNAHS`: the corresponding college or library grouping.
- `OSAS`: Office of Student Affairs and Services.
- `REG`: Registrar's Office grouping for the Administration example.
- `GYM`: University Gymnasium facility grouping under GSD responsibility.

These are UniPM staging identifiers. **MSEUF has not verified this inventory policy.**
Baseline examples use numbers 001–003; historical examples use 101–102.
File numbers use `PM-<YEAR>-<GROUP>-<CATEGORY>-<MONTH>`, for example
`PM-2026-LIB-FE-08`. Unsubmitted Drafts retain no file number.

The nine baseline QR payloads now use `UNIPM-<ASSET-CODE>`. Regenerate their
printouts with the existing QR generator. The sixteen historical assets retain
their canonical `UNIPM-<CATEGORY>-<GUID-TOKEN>` payloads because GUIDs are
unchanged. The scanner uses stored payload lookup; no scanner contract changed.

## Compact inventory, grouped by building and category

Every row below describes synthetic equipment at a publicly named facility.

| Building display | Category | Asset codes | Department and location examples |
|---|---|---|---|
| CCMS Building | Fire extinguishers | FE-CCMS-001, 002, 003 | CCMS; CL-01, CL-03, CL-04 |
| University Library Complex | Fire extinguishers | FE-LIB-001, 002, 003 | Library; reading area, circulation area, corridor |
| Administration Building | Emergency lights | EL-OSAS-001, 002; EL-REG-001 | Student-affairs/Registrar groups; common corridor, stairwell approach, common area |
| CNAHS Building | Fire extinguishers | FE-CNAHS-101, 102 | CNAHS; common area entrance, common corridor |
| CNAHS Building | Fire alarms | FA-CNAHS-101, 102 | CNAHS; common area, entrance approach |
| CNAHS Building | Emergency lights | EL-CNAHS-101, 102 | CNAHS; exit approach, common corridor |
| CNAHS Building | Drinking stations | WDS-CNAHS-101, 102 | CNAHS; common area, entrance approach |
| University Gymnasium | Fire extinguishers | FE-GYM-101, 102 | GSD; common area entrance, common corridor |
| University Gymnasium | Fire alarms | FA-GYM-101, 102 | GSD; common area, entrance approach |
| University Gymnasium | Emergency lights | EL-GYM-101, 102 | GSD; exit approach, common corridor |
| University Gymnasium | Drinking stations | WDS-GYM-101, 102 | GSD; common area, entrance approach |

The seed keeps the existing CPMP month sets: fire extinguishers and water
drinking stations in February, May, August and November; fire alarms and
emergency lights in June and December. `CampusSeedAssertions` checks these
months against `CpmpScheduleFrequency`.

Historical groups are separate from baseline CCMS/Library batches to preserve
batch membership and avoid collisions with Submitted/Acknowledged forms.
The old `Demo CCMS` / `Demo Library` strings remain only as immutable internal
hash keys. They are not persisted as campus display labels. All 278 record
GUIDs remain stable across 25 assets, 105 schedules, 77 inspections, 42 forms
and 29 acknowledgements. Workflow states, timeliness and condition scenarios
are unchanged.

Category-specific remarks describe simulated gauge/pin checks, alarm response,
battery tests and dispenser operation. Non-operational records recommend human
follow-up; they do not certify safety or claim completed repairs. Signatories
Dr. Elena Mercado, Adrian M. Reyes and Marina L. Santos are invented identities
with placeholder image signatures. None is sourced from a staff directory.

## Existing local data

This refinement targets a fresh disposable/local or approved empty staging
database. Older local demo rows with earlier codes or departments may fail
ownership checks. The initializer rejects these conflicts and does not convert,
reset or delete the rows. Use a new disposable database for testing. Recover
existing local data only under a separate approved task. Old QR sheets and
saved department filters must be refreshed. Historical
evaluation corpora/results are unchanged and are not evidence for these renamed
campus groups. The live Azure database was not accessed or seeded by this task.
