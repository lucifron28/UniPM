# Proposed institutional operation and security review

Prepared for remediation Phase 10. This is an operating proposal for an
institution-approved IIS installation. It does not record an installation,
deployment, restore drill or institutional approval.

Source review closes at `f0f8a34e809ac364adc6e1dd389de1ae4afbb497`.
Executed checks are recorded separately in TEST-152 through TEST-163.

## Source review

| Area | Current mechanism | Practical limit / proposed action |
|---|---|---|
| Authentication | `JwtRuntimeConfiguration` validates issuer, audience, signature and expiry. Non-Development startup requires a key of at least 32 bytes and a configured 1–1440-minute lifetime. Access tokens include a checked user security-stamp hash. | Store a random key outside Git, protect the runtime identity's access and document rotation. Development's ephemeral key invalidates sessions on restart. |
| Login / browser refresh | Identity requires 12-character complex passwords; lockout is five failed attempts for 15 minutes. Refresh tokens are stored hashed, rotated and family-revoked on replay. Cookie is HttpOnly, Lax, auth-path scoped and Secure outside Development. | Institution must approve account provisioning and recovery. Lockout is not a complete distributed brute-force defense; review IIS/network controls before public exposure. |
| Origins | One exact configured credentialed CORS origin. Non-Development startup rejects a missing, loopback or non-HTTPS web origin. Refresh handling checks supplied browser origins. | CORS does not authenticate clients. Configure the actual approved origin; do not replace it with a wildcard. |
| HTTPS | Mobile release URL rejects HTTP and Android release manifest disallows cleartext. API Program does not install HTTPS redirection middleware. | IIS must terminate TLS and reject or redirect external HTTP. Non-Development startup configuration alone does not prove transport enforcement. Test browser cookie and TLS behavior on the actual host. |
| Operational scope | GSD, Inspector and Supervisor policies plus form/schedule ownership predicates. NLA is GSD-only. Photos use scoped reads and Inspector-owned Draft writes. | Provisional role design still requires institutional approval. Supervisor schedule metadata reads retain their existing wider scope. |
| Technical Admin | Separate Admin management endpoints can edit/delete demonstration records, acknowledgements and related data. Some deletion paths reconcile forms/schedules. These routes are Admin-protected, not Development-gated. | This is a privileged maintenance capability, not an ordinary operational role. Before real-data use, institution must approve or restrict/reset-disable these tools and define audit/backup policy. This remediation did not remove requested Admin tools or invent that policy. |
| Local mobile storage | Secure per-origin/per-account cache, memory-only authentication, queued IDs and conflict review; storage-failure scope locking in TEST-158. Android backups disabled. | Not a remote backup or lost-device recovery service. Secure-storage availability depends on OS/device state. Use managed compatible devices and sync while connected. |
| Photos | File-system storage uses generated validated keys, temporary writes and a root outside public web content. JPEG payload limit is 4 MiB. Optional private Azure storage adapter remains. | Back up the photo store together with SQL metadata. No cloud provisioning occurred. Keep file permissions limited to API/support identities. |
| Acknowledgements | Submitted-state transition, one receipt, operator/signatory separation, PNG length/magic checks and checksum. Concurrent native SQL test in TEST-159. | Application integrity does not provide an immutable external archive or cryptographic signatory identity. Privileged administrative changes remain possible. |
| NLA privacy | Question length/control checks and email/phone/obvious-ID token masking before model calls. External request contains the sanitized question plus the schema prompt, not inspection histories. Typed output and backend validation constrain execution. | Pattern masking does not recognize every name or sensitive institutional phrase. Only approved screened/synthetic questions should be used with an external provider until an institutional privacy decision. No retired RAG path is restored. |
| Provider limits | 60-second logical timeout, 1,024 output tokens, 16 KiB response cap, bounded transient retry policy and no redirects. | Limits bound an individual call, not a daily financial quota. Institution must define usage approval and budget before enabling provider access. |
| Logging / secrets | No request-body/provider-payload logging was found in the reviewed runtime paths. `.env`, key.properties, keystore and generated raw artifacts are ignored. | A source review is not a penetration test, secret-history scan or production log review. Retain only necessary sanitized support metadata. |

A follow-up local read-only pattern scan at `ac5787c`, October 25 at
09:38:48 UTC, covered 1,107 tracked UTF-8 files up to 2 MiB.
It emitted file paths/counts only and found no private-key blocks, common
provider-token shapes, Azure AccountKey literals, JWT literals or tracked
sensitive-name files. This limited scan does not prove that Git history,
binary files or all possible credential formats are free of secrets. Raw
aggregate output is ignored under `artifacts/remediation/security-source-scan-final.json`.

Subsequent schema review corrected the staging initializer and its preflight
script to the existing 23-migration schema, including schedule reassignment
audits. TEST-163 records local isolated SQL verification. The staging script
was syntax-checked only; it was not run against a deployment target.

## Hosting prerequisites and ownership

The institution should name a GSD product owner, an IT server/database owner
and a maintainer before a pilot. GSD approves categories, official location
lists, adjustment authority, roles and report meanings. IT owns patching, TLS,
secrets, backups, access review and recovery. The maintenance team owns source,
release notes, dependency review and support triage. No particular person or
service commitment is assumed here.

Use an institution-supported Windows Server with IIS and the .NET 10 Hosting
Bundle matching the API's `net10.0` target, following
[Microsoft's IIS hosting instructions](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0). Use SQL Server 2019 or later with
compatibility level 150 and Full-Text Search. The local verification host is
SQL Server 2019 Developer Edition 15.0.2000.5. That test configuration does not
establish an institution's license entitlement, supported patch level or
production suitability. Microsoft limits Developer Edition to development,
testing and demonstration in its
[SQL licensing guidance](https://www.microsoft.com/licensing/guidance/SQL).
IT must approve licensing and patching before use, checking the
[official SQL update history](https://learn.microsoft.com/en-us/troubleshoot/sql/releases/download-and-install-latest-updates).
These sources were consulted on October 25, 2026; no particular current patch
package was installed or selected by this run.

Build the React client with the repository's Node 22 toolchain. Deploy only
the reviewed static output and published API to a versioned directory. Give
the application pool identity only the SQL and photo-directory permissions
needed for runtime work. Use a separately controlled identity for migrations.
Keep the SQL service private to authorized hosts and use encrypted connections
with the institution's trust configuration. The optional SQL2025 Docker setup
is development tooling and is not a prerequisite for IIS.

## Proposed backup and recovery procedure

1. IT and GSD define recovery-point and recovery-time targets before choosing
   a SQL recovery model or backup schedule. No schedule was approved here.
2. Back up SQL, the corresponding inspection photo files, required deployment
   configuration and encrypted secrets recovery material. Store copies off-host
   with access control and retention approved for maintenance/signature data.
3. For a consistent basic backup, place the app in maintenance mode, wait for
   writes to finish, record the software/migration version, then capture the
   database and photo store. A future online strategy needs a tested consistency
   procedure rather than independent unsynchronized file copies.
4. Restore into a new isolated database and photo directory. Verify database
   integrity, acknowledgement/photo linkage, role scope and critical workflows
   before any traffic cutover. Do not restore over the only working copy.
5. Record the restore commands, source backup IDs, duration and observed results.
   A backup file or SQL checksum alone does not establish successful recovery.

This run did not back up or restore institutional data. Fresh disposable SQL
test databases and their cleanup checks are engineering evidence only.

## Proposed update and installation procedure

Before the pagination contract upgrade, synchronize and verify all queued
work using the old compatible mobile/backend pair. The old array-list client
does not understand the new page envelope. Coordinate API, web and mobile
versions in a maintenance window. Do not assume old/new clients are mutually
compatible.

Changing Android identity from `com.example.mobile` to
`io.github.lucifron28.unipm` creates a separate installed app. It does not migrate
the old app's encrypted queued work. Keep the old app until its queue is synced
and checked. The new app must use institution-controlled signing material kept
outside Git. Later upgrades of that identity require the same signing key and
an appropriate increasing version code. No automatic uninstall or cache wipe
is part of this procedure.

For server updates, retain the last working release, back up first, inspect
migrations and run them under an authorized migration identity. Verify the
health endpoints and a controlled login, scope, Draft/save/submit/acknowledge,
history, photo and report smoke sequence. Roll back to the retained release
only if its schema/client compatibility is established. Otherwise restore the
approved backup into a new environment and follow the recovery plan.

## Availability, support and costs

Core PM operations and deterministic NLA do not require DeepSeek. With internet
unavailable, mobile supports already cached Draft work and queued changes;
fresh network lookup, photo upload, submission and acknowledgement require
connectivity. The current mobile client has no location-capture workflow. The
retained backend location endpoint records advisory client-supplied telemetry
and does not prove physical presence. Provider errors remain explicit. Users
may select the deterministic parser; no silent error fallback is claimed.

DeepSeek stays gated to Development with the enabled flag. When that gate is
false, default interpretation uses the parser. When it is true, a missing key
or provider failure returns an error. Enabling provider use beyond that boundary
requires a separate privacy, cost and hosting decision.

| Cost category | Estimate method / decision |
|---|---|
| Existing university server | Incremental hardware/storage/power allocation to be quoted by IT. No zero-cost hosting claim. |
| Windows and SQL licensing | Determine existing entitlement, edition and permitted use with the institution. Developer Edition test use is not a production license decision. |
| Storage and backup | Forecast SQL plus photo growth from actual permitted inspection/photo volume, replication and retention. Reserve off-host capacity. |
| Domain and TLS | Use an approved institutional name/certificate process; record recurring administration or certificate costs if any. |
| Support | Named maintainer hours, dependency/OS patches, restore drills and user orientation. Do not promise free permanent student support. |
| DeepSeek disabled | Zero provider calls from this mode; other operating costs remain. |
| Optional provider | Estimated monthly model cost = uncached input tokens × input rate + cached input tokens × cache rate + output tokens × output rate, using the same rate unit and dated official quotation. Include retries and budget limits. Actual billing must be checked against provider usage. |

For a dated illustration, the configured model ID is `deepseek-flash`.
On October 25, 2026, the
[official DeepSeek pricing page](https://api-docs.deepseek.com/quick_start/pricing/)
lists USD rates per million tokens of 0.15 off-peak / 0.30 peak for uncached
input and 0.60 off-peak / 1.20 peak for output. Suppose 2,000 monthly calls
average 5,000 input tokens and 500 output tokens each. That assumed volume
is ten million input and one million output tokens, or USD 2.10 off-peak
to USD 4.20 peak before retries, taxes, currency conversion or other services.
This is arithmetic on an assumed workload, not measured UniPM usage or a
spending commitment. Cache hits can reduce input cost; retries can increase
usage. Check the model/version, current tariff and actual billed tokens
before enabling calls. No daily budget enforcement is implemented.

Historical TEST-146's estimated cost belongs to that dated experiment. No
license quote, external provider call or deployment was made by this remediation.
