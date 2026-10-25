# Remaining risks and defense boundaries

This register distinguishes unresolved acceptance and policy decisions from
corrected implementation defects. It does not reopen the accepted survey CSVs
or assert that researcher-attested black-box cases were never run.

| ID | Priority | Remaining boundary | Required next evidence or decision |
|---|---|---|---|
| RR-01 | High before institutional use | No physical device is attached for the remediation build. Earlier TEST-040 covers a different source version. | Run the new APK on representative devices, including offline restart, failed persistence guidance, reconnect/conflict, QR, photo, submission and acknowledgement. Record device/OS/source. The current mobile app has no GPS-capture workflow. |
| RR-02 | High before enabling external AI beyond current scope | DeepSeek is Development-gated. Pattern masking cannot identify every personal name or sensitive phrase. | Approve institutional data/privacy and spend boundaries before broader provider use. Keep real unscreened text out of remote prompts. |
| RR-03 | High for interpretation claims | No live DeepSeek call was made by remediation. Historical TEST-146/151 belong to their exact commits. The reviewed 18 questions are a development set. | If new-version model claims are needed, approve a separate bounded live protocol and unused questions. Retain raw-model versus final-system scoring and actual call counts. |
| RR-04 | High for offline compliance decisions | The first backend-accepted save is the compliance clock. Late synchronization can classify earlier offline work as late. | GSD/adviser must decide whether another provenance/review policy is required. Do not replace server time with device time silently. |
| RR-05 | Medium for historical departmental reports | Current asset metadata controls historical grouping. No location-at-inspection snapshot exists. | State this in report scope. Approve snapshot timing/schema prospectively if historical assignment reporting becomes required; do not invent old values. |
| RR-06 | High before real-data hosting | Technical Admin tools can alter/delete demonstration data, acknowledgements and related audit rows. They are privileged and not environment-gated. | Institution approves which tools remain available and who can use them. Define audit retention, backups and restoration controls. This task preserved the requested administrative workflow. |
| RR-07 | High before rollout | The paged form API is incompatible with old array-list clients. New Android package identity does not migrate old encrypted queues. | Synchronize and verify old queues before coordinated client/API upgrade; retain old app until confirmed. Test the signed upgrade procedure. |
| RR-08 | High before distribution | No institution-owned release-signing credentials or installed release acceptance is established by this run. | Maintain an external keystore and recovery process, sign the approved HTTPS build, record certificate identity without private key material and verify installation/update. |
| RR-09 | High before IIS pilot | IIS/TLS, native SQL licensing/patch level, service identities, network restrictions and real backup restore are proposed. Local SQL is Developer Edition 15.0.2000.5. | IT approves a supported/patched/licensed host and performs an isolated restore drill and hosting acceptance. Do not describe this source review as deployment. |
| RR-10 | Medium for evidence provenance | The researcher attests to 32/32 BB passes in a final report. The repository collection lacks case-level metadata; the accessible survey PDF is not the BB report. | Identify/link the particular historical BB report attachment. Preserve the attested study result and do not fill missing metadata from new automated checks. |
| RR-11 | Medium for manuscript submission | Six attachment placeholders remain in separate appendices. Current manuscript still describes fixed local interpretation shortcuts removed here. | Supply required attachments and select the final software version before applying the separate manuscript revision proposal. |
| RR-12 | Medium for operational policy | Final location lists, schedule adjustment authority, audit persistence and institutional source authorization remain deferred. | Obtain GSD/adviser decisions. Do not treat fictional fixtures or engineering tests as policy approval. |
| RR-13 | Medium for durability | Unsynchronized work on a lost device, or work that cannot be persisted before a physical process loss, has no server backup. | Provide compatible managed devices, storage guidance and regular synchronization. Do not promise recovery of bytes never stored or uploaded. |
| RR-14 | Medium for performance claims | Synthetic single-process timing is not concurrent load, long-term field performance or an institution-approved SLA. | Repeat a declared realistic workload on the target host if response-time commitments are required. Retain the 50,000-row cap. |
| RR-15 | High before selecting the defense build | Three pre-existing uncommitted web/mobile files remain in the original checkout and were excluded from this branch's baseline and verification. | Review and integrate those changes separately if they belong in the selected build, then verify the affected behavior. Do not present a different working tree as the tested revision. |
| RR-16 | Low for future toolchain upgrades | The successful debug APK build warns that `mobile_scanner` applies the Kotlin Gradle Plugin. Future Flutter tooling may reject that plugin setup. | Review plugin compatibility before upgrading Flutter/Kotlin. This warning did not prevent the current debug build. |

The defense can describe corrected authorization, explicit routing provenance,
durable retry behavior under injected storage failures, server-time compliance,
separate receipt capture and bounded reporting using the dated test records.
It should not claim perfect standalone model accuracy, guaranteed physical
inspection from QR/location, cryptographic signatory identity, ISO certification,
unseen-question accuracy or institutional production readiness.
