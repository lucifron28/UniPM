# Pre-run review

Assistant review completed before interpretation requests. No independent researcher or GSD review is claimed.

All original texts, status labels, clarification sets, dispositions and 69 partial assertions are retained. Each newly specified full plan has all 16 contract properties. Eight paraphrases have exactly the same complete expected plan as their original counterpart. Labels are based on the question, the API contract and already exposed development results; they are not blind gold.

Canonical layout and default output columns are evaluation conventions where the natural-language question does not specify them. Strict equality therefore measures adherence to this declared convention as well as interpretation. Semantic scoring retains exact column membership; technical IDs added by an interpreter cause a difference. No success-based selection or post-run relabeling is allowed.

V3D-015 explicitly retains unknown Research in the new complete draft; its old partial labels did not constrain that filter. The two period-clarification drafts use null periods and the predeclared semantic placeholder equivalence. Ambiguous Top values remain null. No prediction or deletion is permitted.

| New ID | Original intent | Question | Expected outcome |
| --- | --- | --- | --- |
| V3U-019 | V3D-001 | For the 2024 PM cycle year, how many fire-extinguisher maintenance cycles were on the calendar? | Valid; no fields |
| V3U-020 | V3D-002 | During the 2024 PM cycle year, what percentage of water drinking station inspections has been finished, whether timely or late? | Valid; no fields |
| V3U-021 | V3D-003 | For each department, give 2024 PM-cycle totals for fire alarm systems: work on the schedule and work already finished. | Valid; no fields |
| V3U-022 | V3D-004 | Put fire extinguishers alongside fire alarm systems and compare how many PM cycles were planned for each during 2024. | Valid; no fields |
| V3U-023 | V3D-005 | List the 2025 PM-cycle entries for water drinking stations whose recorded condition is non-operational. | Valid; no fields |
| V3U-024 | V3D-006 | For fire extinguishers located in 'North Block', what was the on-time PM compliance percentage for 2024? | Valid; no fields |
| V3U-025 | V3D-013 | Using completed fire-extinguisher PM cycles as the ranking measure, show the best five departments in 2024 with both planned and finished cycle counts. | Valid; no fields |
| V3U-026 | V3D-014 | Return 2024 fire-extinguisher on-time compliance only where the percentage is 80 or higher. | Valid; no fields |
| V3U-027 | new control | Show scheduled fire alarm system counts by department | NeedsClarification; Period |
| V3U-028 | new control | Compare scheduled and completed emergency light counts for the top 3 buildings in 2025 | NeedsClarification; Top |
| V3U-029 | new control | Predict which fire extinguishers will fail in 2027. | Unsupported; no fields |
| V3U-030 | new control | Delete all fire extinguisher inspection records for 2024. | Unsupported; no fields |

The actual class counts are original 8/7/3 and additional 8/2/2 for Valid/NeedsClarification/Unsupported. Total 16/9/5. The previous proposal count is corrected without changing historical labels.

Permitted run: 60 interpretation requests, 30 per explicit mode, same binary and isolated fixture. No production, prompt, parser or label changes during execution. No report preview or execution. Preserve every failure and do not selectively repeat cases.
