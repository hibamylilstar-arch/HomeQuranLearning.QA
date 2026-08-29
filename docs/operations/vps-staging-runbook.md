# Home Quran Learning secured real-academy pilot runbook

## Scope and approval

The Owner approved `APPROVE HIGH-RISK REAL-DATA VPS PILOT` on 2026-08-29.
This runbook prepares the Cobanto UK 8 GB / 200 GB VPS for a bounded pilot of
the **Home Quran Learning Operations Suite** with four or five approved academy
laptops. The Admin/Manager entry surface is the **Operations & Quality Console**
and the laptop software is the **Home Quran Learning Classroom Agent**.

Real academy sessions may be processed only after every security gate in this
runbook is GREEN. This pilot is a secondary monitoring/accuracy system, not the
academy's sole authoritative record. It is not unrestricted production.

## Network and trust boundary

```text
approved Owner/Admin/Manager review locations (exact public /32 allowlist)
approved teacher-laptop networks             (exact public /32 allowlist)
                       |
                       v
VPS public IPv4: HTTPS 443 (Caddy 2.11.3)
                       |-- /api/* -> ASP.NET API
                       |-- /health -> ASP.NET API
                       `-- dashboard -> Next.js

PostgreSQL | Redis | MinIO | QA worker remain private on the Docker network
```

Only Caddy publishes host ports. Port 80 exists solely for Let's Encrypt
HTTP-01 validation and HTTPS redirect; port 443 serves the product. API,
dashboard, PostgreSQL, Redis and MinIO ports remain private.

## Mandatory security invariants

- Plain HTTP is forbidden for real data, credentials and Agent traffic.
- The browser and Agent must trust the certificate without bypass flags or
  manually installed private roots.
- Caddy is pinned to `caddy:2.11.3-alpine` and requests Let's Encrypt's
  `shortlived` profile for the public IPv4 address.
- TLS-ALPN validation is disabled; the verified IPv4 HTTP-01 path is used.
- Six-day IP certificates require automatic renewal. Certificate-expiry proof
  is part of daily pilot health.
- `PILOT_ALLOWED_CIDRS` contains only exact public IPv4 `/32` entries and never
  `0.0.0.0/0`, a private subnet or an unnecessarily broad ISP range.
- The Agent API key, worker key, JWT key, database password, MinIO password and
  Owner password are unique, generated values and never appear in Git, chat,
  screenshots or operator logs.
- The pilot Agent ZIP and its extracted configuration contain a credential.
  Transfer them only through the Owner-approved secure channel and delete the
  transfer copy after installation.
- A `device.json` is never copied between laptops.
- Automated recording retention stays disabled until the Owner separately
  approves deletion of real recordings after a dry-run impact review.

Let's Encrypt documents public IPv4 certificates as generally available and
valid for approximately six days:
https://letsencrypt.org/2026/01/15/6day-and-ip-general-availability

## Required infrastructure facts

Before VPS execution, obtain through the approved secure channel:

- VPS public IPv4 and Ubuntu version;
- SSH access from the Codex host or an Owner-controlled terminal session;
- monitored ACME contact email;
- intended Owner login email;
- exact current public IPv4 `/32` for the Owner review location and each pilot
  teacher location.

Never paste a password, private key, API key, JWT key, cookie or complete
`.env.production` file into chat.

## Secure environment preparation

Create `infrastructure/docker/.env.production` on the deployment host with mode
`0600`. Use the checked-in example only as a key list; replace every placeholder.
Run the validator before Compose starts:

```powershell
& .\scripts\Validate-VpsDeploymentConfig.ps1
```

The validator rejects private/documentation VPS addresses, non-`/32`
allowlists, placeholder emails, short or reused secrets, public ports outside
Caddy, missing HTTPS 443, unpinned Caddy, invalid Caddy syntax and enabled
destructive retention. It never prints secret values.

## Deployment order

1. Verify Ubuntu version, time sync, disk, memory, firewall and Docker/Compose.
2. Restrict SSH to the approved administrator path and expose only TCP 80/443
   for the application. Do not expose database, cache, object-store or app
   container ports.
3. Create the least-privilege deployment directory and mode-`0600` environment
   file. Generate every secret independently on the VPS.
4. Check out only the approved immutable release commit. Never edit source on
   the VPS and never copy the local development database or `.env`.
5. Run the deployment validator and inspect rendered service/port topology.
6. Start PostgreSQL, Redis and MinIO, then API, dashboard, worker and Caddy.
7. Verify migrations, private MinIO bucket, API health and worker authentication.
8. Verify `https://<VPS_PUBLIC_IP>/health` from an allowlisted location. Confirm
   the certificate is publicly trusted, covers the exact IPv4 address and has
   more than 48 hours remaining.
9. Verify a non-allowlisted network receives HTTP 403 and cannot load login,
   health or Agent endpoints.
10. Verify Owner/Admin/Manager authorization with approved accounts. Do not
    export credentials or cookies.
11. Capture the pre-pilot database/object counts, disk use, certificate expiry,
    service health and release commit.

## Existing VPS project inventory and retirement

The Owner has requested removal of an older copy of this project already on the
VPS. Do not infer its location from a folder name and do not delete it during the
first deployment pass. Before any cleanup, capture a read-only inventory that
proves:

- every candidate project directory's canonical path, owner, size and Git HEAD;
- the working directory, Compose project, image and bind mounts of every running
  container;
- every named Docker volume and its consumers;
- active systemd services, timers, cron entries and processes referring to a
  candidate path;
- the selected new release path and immutable release commit;
- the location of environment/secrets files, PostgreSQL data and MinIO objects.

Classify a candidate as `ACTIVE`, `SHARED_DATA`, `OLD_APPLICATION_COPY` or
`UNKNOWN`. Only `OLD_APPLICATION_COPY` is eligible for retirement. Database,
object-store, Docker-volume, backup, secret and evidence paths are never part of
this cleanup.

After the new release is healthy, report the exact canonical old path and the
evidence that nothing consumes it. Prefer a same-filesystem rename to a uniquely
dated quarantine path, re-run health and restart checks, and observe the
quarantine for the approved interval. Permanent deletion happens only against
that exact quarantined path after a fresh boundary check; never use an unresolved
variable, wildcard, `/root`, `/opt`, `/srv`, `/var/lib/docker`, or a workspace
root as a recursive-delete target. Record the removed path, size, Git HEAD,
quarantine interval and post-cleanup disk/health proof.

## Laptop 1 gate

Generate the credential-bearing package from the secure environment file:

```powershell
& .\scripts\Prepare-VpsPilotAgentPackage.ps1
```

Follow the generated `README-REAL-DATA-PILOT.txt`. On the first approved laptop:

1. prove port 443 and trusted `/health` without certificate bypass;
2. install/run the current Classroom Agent and optional TeamsHelper;
3. confirm a new unique device identity and repeated fresh heartbeats;
4. confirm the configured teacher headset/microphone reports proven provenance;
5. record one approved class window;
6. prove recording finalization, idempotent submission, upload, worker processing,
   playback and timestamped candidate review;
7. prove Wi-Fi interruption/recovery without duplicate device or recording rows;
8. inspect logs for authorization errors, retries, certificate errors, upload
   failures, worker failures and unexpected restarts.

Laptops 2-5 are blocked until all laptop-1 gates are GREEN and the exact proof
data is identified.

## Four-to-five laptop expansion

Add one laptop at a time. Update the exact `/32` allowlist if its public source
address differs, validate Caddy configuration, reload, and re-prove allowed and
denied access. For every laptop verify:

- unique DeviceId and correct teacher mapping;
- fresh heartbeat and independent retry/recovery;
- headset/teacher-audio provenance;
- session and historical identity;
- recording size, duration, upload and playback;
- worker processing and candidate idempotency;
- no cross-teacher Manager visibility;
- disk growth and container-log growth.

Do not jump directly from one laptop to five.

## Human-reviewed accuracy evaluation

The classifier creates review candidates only. It must not make automatic
disciplinary findings. Authorized reviewers confirm or dismiss candidates and
record the reason without including unnecessary student personal information.

Evaluate both sides of accuracy:

- review every candidate for false positives;
- sample ordinary no-candidate windows from each teacher/session to find false
  negatives;
- include Arabic recitation, Urdu/Hindi teaching speech, English teaching
  speech and mixed-language lesson control;
- keep Arabic Quran/Qaida recitation excluded from rule evaluation;
- preserve teacher-audio provenance and the ±10-second context;
- record policy version, classifier version, reviewer and review timestamp.

Report at minimum:

- confirmed true candidates;
- dismissed false candidates;
- sampled true negatives;
- missed policy-relevant speech;
- precision and recall on the reviewed set;
- false candidates per recorded teaching hour;
- coverage failures and unprocessed recordings.

No production-accuracy claim is allowed until the reviewed sample is large and
representative enough for an explicit Owner decision.

## Logs and daily health

Access to logs is limited to the Owner and specifically authorized technical
reviewers. Caddy access logs are JSON on stdout and rotate through Docker's
bounded `10m × 5` policy. Never add request authorization headers, cookies,
passwords or transcript bodies to access logs.

At pilot start and daily, record:

- release commit and running image versions;
- certificate subject/issuer/expiry and renewal health;
- container health/restart counts;
- disk used/free and MinIO bucket growth;
- recording/candidate/processed/coverage-failure counts;
- per-device last heartbeat and Agent version;
- HTTP 401/403/429/5xx, upload failures and worker exceptions.

## Storage and retention

The planning baseline remains 90 recorded-hours/day, three-day normal retention
and seven-day confirmed-QA retention, but this pilot does **not** rely on those
deletion periods. Compose explicitly sets `RecordingRetention__Enabled=false`.

Until separate real-recording deletion approval:

- keep the pilot time-bounded and watch measured GB/hour;
- stop admission before free space falls below the approved safety headroom;
- do not run the full 90-hours/day steady state;
- do not manually delete real recordings or mutate historical evidence;
- treat pilot evidence as secondary, not the sole academy record.

Before enabling deletion, produce a dry-run list, projected retained GB, object
and database impact, preservation behavior and rollback/backup evidence, then
request a separate high-risk approval.

## Immediate stop conditions

Stop Agent admission and preserve current evidence if any of these occur:

- TLS trust/expiry/renewal failure or an HTTP fallback;
- allowlist bypass or unauthorized cross-teacher access;
- wrong/missing teacher microphone provenance;
- duplicate device identity or non-idempotent recording submission;
- repeated upload/playback/worker failure;
- unexplained recording, candidate or historical-identity mutation;
- disk/MinIO/log growth beyond the approved bounded pilot capacity;
- any secret appears in source control or logs.

## Local-to-VPS release flow

- Local approved branches remain the engineering source; the VPS runs an
  immutable approved release commit.
- Fixes are made locally, pass normal gates and receive release approval before
  a new VPS release.
- Never patch source directly on the VPS.
- Rollback selects the previous known-good release and changes data only when a
  separately proven migration/data rollback procedure permits it.
