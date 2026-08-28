# HomeQuranLearning.QA — Project State

## Purpose and authority

Canonical resumable project checkpoint. Use it with Git and actual repository state after reboot, new Codex chat, interrupted phase or missing chat history.

Chat history is not authoritative. Repository state + this file are the durable project state.

## Canonical checkpoint

- Branch: `codex/real-data-vps-pilot`
- Current phase base commit: `f4426c1` (VPS-preparation/login-copy closure)
- S1 implementation commit: `ca18589d2d027a07b300cc86fbeadda49540f968`
- S1 closure commit: `c68f6e2b5f6088447243afa494e17eeb7716748a`
- S1.1 release parent: `a97cd465cef4811b58491a781eb5e02fc63771e6`
- Origin: 7A-6 implementation was pushed at `e638e635261830ce8e1af8af41e80d835149563d`
- Subject: `Secured real-data VPS pilot preparation and responsive login`
- Latest closed product phase: `7A-5C — multilingual QA classifier and evaluation`
- Latest closed governance phase: `CODEX AUTOPILOT GOVERNANCE BOOTSTRAP`
- Current product phase: `Secured real-data VPS pilot preparation`
- Current phase status: `WAITING_RELEASE_APPROVAL`
- Next engineering gate: Owner release review; no staging, commit or push before exact `APPROVE`
- Waiting human test: no
- Waiting release approval: yes
- Last verified checkpoint: secured public-IPv4 HTTPS pilot configuration, package guardrails, responsive login and the complete local release gates are GREEN; changes remain unstaged on `codex/real-data-vps-pilot`
- Tests already passed for S1.1: targeted deleted-recording regression 1/1; full unit 76/76; integration 2/2; Agent 1/1; full solution build GREEN with 0 warnings and 0 errors; runtime HTTP Deleted 400 / Uploaded 200 proof GREEN; final diff/status review and `git diff --check` GREEN
- Dashboard gates completed: full dashboard lint/build, backend unit/integration gates, authenticated session-evidence API proof, role-scope proof, authenticated browser evidence-timeline/filter proof and final runtime cleanup
- 7A-2 gates completed: worker self-test 6/6 markers; full unit tests 81/81; integration tests 3/3; full solution build 0 warnings/0 errors; local API persistence/retry proof; exact proof-row cleanup and baseline restoration; runtime OFF
- 7A-3 gates completed: dashboard lint and production build GREEN; authenticated Owner browser proof for QA Alerts → Recording QA Review; two timestamped segments rendered; click-to-seek set video position to 3.0s and highlighted the segment; browser console errors 0; six test recordings, dependent alerts, test object and orphan test device cleaned
- Owner-approved high-risk recording cleanup: 141 remaining recordings and dependent QA evidence removed; exactly two approved samples retained (`15541c8f-2a67-4bdd-9b0d-cc3ff020960d` Uploaded and `eeb925e3-e28f-4eb6-81d3-33650560c73d` Pending); no MinIO objects remained; sessions/events/devices were preserved
- 7A-4 automated readiness: three unique device identities, repeated heartbeats, isolated recording submissions and idempotent retries passed; exact proof cleanup restored baseline.
- 7A-4 physical readiness: `DESKTOP-RAAFV2I` registered a fresh identity, recovered after Wi-Fi loss, uploaded device-scoped recordings, appeared in the authenticated dashboard and produced playable screen evidence. All physical-test objects/rows/device/heartbeats were removed by exact identity and the database baseline was restored.
- 7A-4 storage correction: recording defaults and the local package now use H.264 veryfast/CRF 32 with a 700 kbps video cap and AAC 64 kbps mono. The physical 50.4-second sample was 3,075,494 bytes at 488 kbps with readable 1366x768 text. Conservative capacity planning uses 764 kbps for 90 recorded-hours/day with unchanged 3-day normal / 7-day QA retention; 200 GB is unsafe if all recordings remain QA evidence.
- 7A-4 system-audio proof: the physical 48.2-second sample contained 64.8 kbps AAC at 32 kHz mono and non-silent audio at -22.1 dB mean level. Whisper detected the speech and persisted eight timestamped Urdu transcript segments before marking the recording processed.
- 7A-4 Unicode worker repair: the physical transcript exposed repeated Windows `charmap` failures when the service printed non-Latin text. The worker now configures stdout/stderr as UTF-8 and the NSSM batch launcher sets Python UTF-8 environment flags. Forced-cp1252 self-test and manual end-to-end processing of the same recording passed. The already-running Windows service could not be restarted without elevation and will load the fix at its next service/host restart.
- 7A-4 package preparation: `scripts/Prepare-LocalAgentTestPackage.ps1` publishes self-contained Agent and TeamsHelper binaries, writes LAN/test-only configuration with live streaming disabled, and emits operator instructions under `publish\local-agent-test` (ignored by Git). The compressed v3 package was generated and checksum-verified.
- 7A-4 final gates: solution Release build 0 warnings/0 errors; Agent tests 7/7; unit tests 81/81; integration tests 3/3; dashboard lint and production build GREEN; QA worker self-test including Unicode output GREEN; PowerShell parser and `git diff --check` GREEN; API, Agent, dashboard and FFmpeg OFF.
- 7A-5A automated implementation gates: solution Release build 0 warnings/0 errors; Agent tests 9/9; backend unit tests 84/84; integration tests 5/5; worker self-test 8/8 markers; Python compile and PowerShell parser GREEN; migration applied without data loss; two-track FFmpeg/finalizer proof showed video 16.200s and both audio tracks 16.192s; no-microphone proof produced `Unavailable` and worker fail-closed; API first-submit/identical-retry/divergent-retry/upload/pending/download/extraction proof GREEN; exact proof rows/objects removed and baseline restored (recordings 2, devices 3, heartbeats 1361, audio gaps 0); runtime OFF.
- 7A-5A human-test package: rebuilt self-contained `publish/local-agent-test-v4.zip` from the current source with recording enabled, live streaming disabled, default-communications microphone selection instructions and old-Agent replacement guidance. SHA-256: `9815A5A1661706710E09D4C28491D879B2C55509FD8A8F3FCD36B4C66477F79A`.
- 7A-5A provisional acceptance: built-in laptop microphone capture was accepted for the technical pipeline proof (recording, two-track layout, persistence, upload and worker validation) before the real headset run; no candidate/classifier or retention implementation is included.
- 7A-5A headset proof completed (2026-08-29): controlled Agent run selected `Headset (pro2)`, reported `TeacherAudioStatus: Proven`, finalized a 30.592-second two-track MP4, uploaded it successfully through the local API, and PyAV extracted the declared teacher track to 16 kHz mono WAV. Exact six proof recordings/objects and 12 generated heartbeats were removed; baseline returned to recordings 2, devices 3, heartbeats 1361, audio gaps 0. The Owner-requested headset verification is now complete.
- 7A-5B candidate foundation gates completed (2026-08-29): proven layout-1 teacher-track candidates now persist with policy/analysis versions, deterministic idempotency, trigger plus ±10-second context, transcript/language/intent/confidence and review audit fields. Owner/Admin and assigned-Manager scoped APIs expose candidates; Confirm alone creates a linked QA alert and Dismiss creates none. Unit tests 87/87, integration tests 5/5, Release build 0 warnings/0 errors, EF migration `20260828201740_AddQaCandidates` applied, dashboard production build and TypeScript checks GREEN; DB baseline remains recordings 2, devices 3, coverage gaps 0, candidates 0. Runtime remains OFF.
- 7A-6 attendance operations gates completed (2026-08-29): daily report now includes teacher attendance status and the complete completed-session list while preserving reducer semantics. Dashboard adds clickable status cards, Student Attendance and Teacher Attendance tabs, search/status filters, evidence detail table and 30-second refresh. Backend unit tests 87/87, integration tests 5/5, Release build 0 warnings/0 errors, dashboard lint/TypeScript/production build GREEN. No migration or runtime data mutation required.
- 7A-6 browser proof completed (2026-08-29): authenticated Owner dashboard loaded Attendance Report; status cards rendered, Present card changed the operational filter, Teacher Attendance tab activated, empty-state table rendered safely, and browser console error count was 0. Dashboard/API remain available for Owner inspection; no test data was created.
- 7A-5C implementation checkpoint (2026-08-29): the production-wired `spikes/SttSpike/qa_worker.py` now extracts timestamped teacher-track context windows, applies the versioned `7A-5C-lexical-v1` fail-closed classifier, and posts review candidates to `/api/worker/qa-candidates`; it no longer creates final alerts directly. Arabic-recitation windows and isolated `fee`/`fi` tokens are excluded, while supported parent/contact/financial contexts become candidates. The checked-in 10-case synthetic corpus reports TP=4, FP=0, TN=6, FN=0 (policy coverage only, not a production accuracy claim). Python compile, classifier self-test, evaluator and worker candidate-only-order self-test are GREEN; Docker worker now copies the classifier module. Released in `411bbac`.
- VPS preparation checkpoint (2026-08-29): Owner approved high-risk VPS staging/production preparation and selected direct public-IPv4 HTTP staging before domain/TLS. The production Compose now routes only through Caddy port 80, keeps API/dashboard ports internal, uses an environment-supplied IPv4 host, and explicitly disables API HTTPS redirection for this bounded staging mode. Commercial branding is `Home Quran Learning Operations Suite`; the Admin/Manager entry surface is `Operations & Quality Console`, the laptop package identity is `Home Quran Learning Classroom Agent`, and the developer credit is `Abdul Wahid`. The supplied academy logo is shown through a circular crop without the source image's white square. Login browser proof, console-error check, Compose config, secure-value validator, PowerShell parser, dashboard lint/build, Release solution build (0 warnings/errors), unit 87/87 and integration 5/5 are GREEN. No VPS, DNS, database, recording or secret was mutated. Direct-IP staging is synthetic/test-only because credentials and Agent API keys are not encrypted in transit. Released in `20f2fdb`.
- Real-data VPS pilot preparation checkpoint (2026-08-29): Owner explicitly approved `APPROVE HIGH-RISK REAL-DATA VPS PILOT`. Direct public IPv4 now uses Caddy `2.11.3` with publicly trusted short-lived ACME certificates, HTTPS 443, HTTP-01 renewal, exact public `/32` source allowlisting and private API/dashboard/data services. Production Compose disables automated recording retention because deletion of real recordings needs a separate approval, and every service has bounded Docker logs. Validators reject private/documentation hosts, broad CIDRs, placeholder/weak/reused secrets, unexpected public ports, unpinned Caddy, invalid syntax, enabled retention and public-HTTP pilot packages. The pilot Agent package requires HTTPS and is treated as credential-bearing. The login page now scrolls safely on short screens and exposes all feature text on mobile/tablet. Browser proof passed at 320x568, 360x568, 768x1024 and 1366x650 with zero horizontal overflow and zero console errors; dashboard lint/build, Release solution build (0 warnings/errors), unit 87/87, integration 5/5, PowerShell parsing, deployment self-test and generator sync are GREEN. The Owner VPS was reachable through an Owner terminal, but Codex has no non-interactive SSH authentication; no remote file, service, firewall, database, recording, secret or deployment was changed. The requested old VPS project copy remains untouched until an exact read-only active-path/mount/volume inventory proves a safe retirement target.
- S1.1 release files: `docs/PROJECT-STATE.md`, `src/Backend/Academy.Api/Program.cs`, `src/Backend/Academy.Application/Exceptions/RecordingUnavailableException.cs`, `src/Backend/Academy.Application/Services/RecordingService.cs`, `tests/Academy.UnitTests/RecordingServiceTests.cs`
- Temporary data: all 7A-4 automated and physical proof rows/objects/devices were removed by exact identity; the current recordings baseline is the two Owner-approved retained controls
- Product runtime expected after latest proof: OFF

S1, S1.1, S1.2, dashboard operational hardening, 7A-2, 7A-3, 7A-4, 7A-5A, 7A-5B, 7A-5C, 7A-6 and VPS direct-IP staging preparation are CLOSED after full validation, Owner approval and push. Secured real-data VPS pilot preparation is `WAITING_RELEASE_APPROVAL` and remains unstaged.

## Current runtime snapshot

Verified 2026-08-29:

- `.dev-runtime/Runtime.ps1`: API OFF, Agent OFF, FFmpeg 0
- Docker: PostgreSQL, Redis, MinIO, LiveKit, LiveKit Ingress and ingress-manager running
- Windows service `AcademyQaWorker`: Running / Automatic

The `AcademyQaWorker` service remains Running/Automatic and is wired to the repository worker source. Its current source passes all self-test markers and a live read-only API probe, but Windows denied service-control and child-process termination, so the long-running service process itself could not be refreshed. Keep this operational caveat visible after release.

## Proven architecture

Backend:
- ASP.NET Core
- .NET 10
- EF Core
- PostgreSQL

Infrastructure:
- PostgreSQL
- Redis
- MinIO
- LiveKit
- LiveKit Ingress
- ingress-manager
- Docker Compose

Dashboard:
- Next.js
- React
- TypeScript

Agent:
- Academy.Agent.Service
- Academy.Agent.Cloud
- Academy.Agent.Audio
- Academy.Agent.Capture
- Academy.Agent.Media
- Academy.Agent.Teams
- Academy.Agent.TeamsHelper

Live path:

```text
Agent -> FFmpeg RTMP -> LiveKit Ingress -> LiveKit -> browser dashboard
```

Known-good screen timing:

```text
ddagrab
framerate=10
dup_frames=1
hwdownload
format=bgra
setpts=N/(10*TB)
```

Audio:

```text
NAudio/WASAPI loopback -> UDP -> FFmpeg AAC -> LiveKit
```

Recording uses direct H264 MP4 rather than huge raw BGRA temp files.

## Completed foundations

- JWT + HTTP-only cookie auth
- Owner/Admin/Manager roles and partial Manager teacher filtering
- teachers/students/courses/devices
- schedules/sessions
- historical session safety
- session events
- live screen/audio
- dashboard audio control
- recording/upload/recovery
- attendance reducer/review/daily report
- QA rules/alerts foundation

These foundations do not mean authorization, durable service startup, manual-session workflow, or the Owner Control Plane is complete.

## Teams attendance — completed

```text
Academy.Agent.Service
<-> secured Named Pipe
<-> Academy.Agent.TeamsHelper
-> Teams UI Automation
```

Event types:

```text
TeacherGreetingSent  = 19
CallAttempted        = 20
StudentCallConnected = 21
CallEnded            = 22
LessonShared         = 23
```

Source: `TeamsUIAutomation`

Current semantics:

- TeacherGreetingSent = teacher evidence, not student presence
- CallAttempted = teacher evidence, not student presence
- StudentCallConnected = explicit student presence
- CallEnded = stop/duration evidence
- LessonShared = strong teacher and student participation/presence evidence
- LessonShared timestamp is not a student arrival timestamp and must not by itself cause Late

Late threshold: 3 minutes.
Pre-class teacher-ready window: 5 minutes.

LessonShared requires exact scheduled student chat + outgoing lesson-related text + actual image in same message.

Known reducer test example:

```text
StudentCallConnected +2m
CallEnded +12m
=> Present
=> ActiveSeconds 600
```

Targeted attendance reducer suite after Teams integration: `27/27 green`.

## Phase 7A-1 — closed GREEN

Commit: `415bbec`

Implemented:

- pending recording exposes StartedAtUtc
- alert request exposes QaRuleId
- Whisper model reuse
- normalized transcript indexing
- cross-segment phrase matching
- recording-relative timestamps
- exact rule linkage
- duplicate retry suppression
- mark processed only after success
- failure remains pending

Worker self-test:

```text
QA_WORKER_TRANSCRIPT_INDEX_OK
QA_WORKER_CROSS_SEGMENT_MATCH_OK
QA_WORKER_RULE_LINK_OK
QA_WORKER_TIMESTAMP_ALIGNMENT_OK
QA_WORKER_SELF_TEST_OK
```

Gates:

- full solution build GREEN
- unit 67/67
- integration 2/2
- production runtime/API/DB proof GREEN

Proven:
- StartedAtUtc reaches worker
- exact QaRuleId persists
- recording-relative alert timestamp persists
- duplicate alert retry suppressed
- QaProcessedAtUtc persists only after explicit success
- processed recording disappears from pending queue
- temporary proof data cleaned

Historical proof counts:

```text
Recordings 148
QA Rules 1
QA Alerts 5
Sessions 20
Session Events 163
```

Not permanent invariants.

QA worker remains production-wired at `spikes/SttSpike/qa_worker.py`.

## Known non-blocking issue

`.dev-runtime/Runtime.ps1 StartApi` may briefly report API OFF immediately after launch while HTTP readiness later succeeds. Use explicit bounded readiness checks.

## S1 - closed

Implemented:

- Owner/Admin role policy on user, people, assignment, QA-rule, QA-alert write, device-management and manual-session routes.
- Manager recording playback restricted to recordings belonging to assigned teachers.
- Manager live-session visibility and LiveKit tokens restricted to active sessions belonging to assigned teachers.
- Manager denied LiveKit server credentials and publish-capable participant tokens; Owner/Admin token workflows remain available.
- Unknown roles fail closed for dashboard recordings, alerts and devices.
- Corrected Owner policy recorded durably: `LessonShared` proves both teacher and student presence but is not an arrival/late timestamp.

Proof:

- full solution build: GREEN
- focused attendance/access tests: 35/35
- unit tests: 75/75
- integration tests: 2/2
- Agent tests: 1/1
- local API Owner/Admin/Manager policy and assigned/unassigned resource proof: GREEN
- temporary runtime proof cleanup: GREEN, zero remaining rows
- QA worker current-source self-test and read-only API probe: GREEN
- exact service-process refresh: not achieved because host service/process control returned Access Denied

S1 implementation release files:

- `AGENTS.md`
- `README.md`
- `docs/PROJECT-DECISIONS.md`
- `docs/PROJECT-STATE.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/decisions/coding-conventions.md`
- `src/Backend/Academy.Api/Program.cs`
- `src/Backend/Academy.Application/Services/DashboardQueryService.cs`
- `tests/Academy.UnitTests/DashboardResourceAccessTests.cs`

## S1.1 - closed

Goal:

- Replace the HTTP 500 returned for an authorized playback request against a historical `Deleted` recording with the established unavailable-recording client response, without changing authorization or healthy uploaded playback.

Root cause:

- `RecordingService.GetPlaybackUrlAsync` represented every non-`Uploaded` status with a generic `InvalidOperationException`.
- The `/api/admin/recordings/{recordingId}/playback-url` endpoint did not map that known domain condition, so ASP.NET Core surfaced it as HTTP 500 after authorization had correctly succeeded.

Released:

- A typed `RecordingUnavailableException` carries the current recording status.
- `RecordingService` throws that exception before requesting a presigned URL for any non-`Uploaded` recording.
- The playback endpoint maps only that known exception to HTTP 400 with the existing `Recording file is not available.` response.
- A regression test proves `Deleted` status raises the typed unavailable condition and never calls storage.

Proof:

- targeted deleted-recording regression: 1/1 GREEN
- full unit tests: 76/76 GREEN
- integration tests: 2/2 GREEN
- Agent tests: 1/1 GREEN
- full solution build: GREEN, 0 warnings, 0 errors
- authenticated runtime HTTP proof: isolated `Deleted` recording returned 400; existing `Uploaded` control returned 200
- cleanup: isolated proof row count zero; recordings baseline restored to 148
- runtime after proof: API OFF, Agent OFF, FFmpeg 0

Protected behavior:

- Existing authorization and manager resource-scope checks still execute before playback resolution.
- Uploaded recording presigned-URL behavior is unchanged.
- Download endpoint behavior, recording lifecycle, storage, LiveKit, Teams, attendance and QA processing semantics are unchanged.

## S1.2 - closed

Goal:

- Make `Academy.Agent.TeamsHelper` start once and recover reliably in the active teacher's interactive Windows session, with observable health and reversible setup, while preserving the proven Teams evidence pipeline.

Baseline:

- The helper currently runs only when invoked manually with `--monitor`; no durable launcher, installer or startup registration exists.
- `Academy.Agent.Service` is Windows-service capable, but the local product runtime currently starts it as a normal process and no Academy Agent Windows service is installed.
- Teams UI Automation requires the active interactive desktop. The named-pipe server deliberately validates the expected helper process name and active-console session, so the service/session-0 process must not directly host UI Automation.
- Active user is `DESKTOP-PUFUU3U\SAMSUNG` in session 5. Initial TeamsHelper and Academy Agent process counts are zero, and no Academy/TeamsHelper scheduled task exists.

Approved boundaries:

- Prefer a least-privilege per-user interactive launcher with idempotent install/status/remove tooling and bounded restart behavior.
- Preserve helper process-name and active-session pipe validation.
- Preserve `TeacherGreetingSent`, `CallAttempted`, `StudentCallConnected`, `CallEnded` and `LessonShared` detection, dedupe and attendance semantics.
- Do not add production deployment, remote lifecycle, Owner Control Plane, manual-session or `7A-2` scope.

S1.2 implementation checkpoint (2026-08-28):

- Added a per-session single-instance mutex, bounded file logging with `.1` rotation, and atomic throttled health snapshots under the current user's SID-scoped ProgramData runtime directory.
- Added `setup-teams-helper.ps1` with idempotent staged install/rollback, limited interactive logon task registration, bounded restart settings, exact executable-path process checks, status output and uninstall preservation of health/log evidence.
- `Academy.Agent.TeamsHelper --lifecycle-probe` is GREEN with `TEAMS_HELPER_SINGLE_INSTANCE_OK`, `TEAMS_HELPER_HEALTH_OK`, `TEAMS_HELPER_LOG_ROTATION_OK` and `TEAMS_HELPER_LIFECYCLE_PROBE_OK`.
- Existing helper probes are GREEN: UIA, detector policy and state-machine probes.
- Direct Agent lifecycle VSTest is GREEN: 3/3. Existing unit and integration VSTest binaries are GREEN: 76/76 and 2/2.
- PowerShell parser and `git diff --check` are GREEN.
- Elevated ProgramData install/task/runtime proof remains pending. The elevated command runner currently rejects requests with a usage-limit error before execution; no indirect or destructive workaround was attempted.

S1.2 runtime proof completed (2026-08-28):

- Fresh Release publish was verified to resolve runtime state to the SID-scoped ProgramData path. The previously installed binary still resolved to AppData; republishing and reinstalling corrected that mismatch.
- The installer was corrected to stop the exact managed helper before clearing `.previous`, preventing DLL-lock cleanup failures.
- Final installed task action is the ProgramData executable with `--monitor`, current user `SAMSUNG`, Interactive/Limited principal, logon trigger, one-minute repeating recovery trigger, `RestartOnFailure` count 10/interval 1 minute, battery-safe settings and `IgnoreNew` duplicate policy.
- Runtime proof: task registered/running, exactly one helper in active session 5, fresh heartbeat and `LIFECYCLE_HEALTHY=YES`.
- Agent outage/recovery proof: helper transitioned `Idle -> WaitingForAgent -> Idle` while Agent was stopped and restarted; API remained OFF and FFmpeg remained 0.
- Helper recovery proof: exact managed helper PID termination was followed by a new session-5 helper and fresh heartbeat through the repeating recovery trigger.
- Intentional Stop/Start proof: Stop disabled the task and removed the process; Start re-enabled it and restored `Idle`/healthy state.
- Clean full solution build is GREEN with 0 warnings and 0 errors after stopping the runtime Agent process. Agent lifecycle tests 3/3, unit tests 76/76 and integration tests 2/2 are GREEN. All helper probes remain GREEN.
- Temporary publish output and earlier AppData test copies are not part of the repository or active task. They remain for explicit cleanup because the current command policy rejected recursive deletion outside the repository; no production data was touched.

Dashboard implementation checkpoint (2026-08-28):

- Added a read-only, role-scoped `/api/admin/sessions/{sessionId}/events` endpoint. It reuses the existing session access policy and returns purpose-limited event fields without exposing idempotency keys.
- Added dashboard session filters for teacher, student, course/device search, status, student attendance and date, plus a raw session evidence timeline with explicit attendance/SOP separation.
- Fixed the dashboard authentication lifecycle: login now reads the canonical `/auth/me` profile after the HttpOnly cookie is set, updates the shared AuthProvider state and keeps logout cleanup in a `finally` path. Unauthenticated dashboard routes redirect to `/login`.
- Pinned the dashboard development script to Next Webpack mode because the local Windows Turbopack runtime reproduced API route 404s; Webpack returned the expected 401/200 responses.
- Backend role-scope tests cover assigned and unassigned Manager session-event access. The event DTO intentionally excludes `IdempotencyKey`.

Dashboard proof completed (2026-08-28):

- Dashboard lint: 0 errors, 0 warnings.
- Dashboard production build: GREEN; auth, proxy and sessions routes generated.
- Full solution build: GREEN, 0 warnings, 0 errors.
- Full unit tests: 78/78 GREEN; integration tests: 2/2 GREEN.
- Authenticated local login flow: login 200, `/api/auth/me` 200, Owner profile active, HttpOnly cookie present.
- Authenticated Next proxy flow: sessions 200 with 20 records; selected completed session events 200 with 6 events.
- Direct API authorization: unauthenticated events request 401; missing/inaccessible event request 404.
- Browser proof: unauthenticated `/sessions` redirected to `/login`; approved local Owner login reached the authenticated dashboard; Sessions showed all 20 records; the selected completed session rendered six ordered raw evidence events; a no-match search produced the expected 0-of-20 empty state; Clear filters restored 20-of-20; logout returned to `/login`.
- Temporary runtime cleanup: API, dashboard dev servers and FFmpeg are OFF; Docker infrastructure and the Windows QA worker were not changed.

Next recoverable action:

- Discuss the next phase and wait for the Owner's `GO`. If VPS staging execution
  is selected, collect secure VPS access facts and begin the bounded direct-IP
  synthetic staging runbook without using real academy data.

## Current engineering gate - post-S1 reassessment

S1 closed the immediate role and resource-scope defects. Remaining stabilization and future authorization observations are:

1. Full granular permission-catalog enforcement remains future Owner Control Plane phase O1 work; S1 intentionally applies existing role and assignment boundaries only.
2. The running QA worker service process could not be refreshed under the current host permissions, although current source/runtime probes are green.
3. The historical `Deleted`-recording HTTP 500 follow-up was resolved and released in S1.1. Runtime proof returned Deleted 400, Uploaded 200, and S1 authorization ordering remains unchanged.
4. TeamsHelper durable launcher/installer/startup was implemented and runtime-proven in S1.2; the remaining cleanup warning is limited to old temporary publish/test copies outside the repository.
5. Manual-session workflow is incomplete.
6. Agent configuration contains environment-specific FFmpeg/device assumptions and recording is disabled by default.
7. Production Compose omits some live/reverse-proxy components and needs deliberate deployment design later.

7A-2 is released at `f4617e0`, 7A-3 at `a2b8aae`, 7A-4 at `ee42315`, 7A-5A at `a67ff8a`, 7A-5B at `b7e6deb` and 7A-5C at `a983841`. 7A-5A implements two-track teacher-audio provenance with fail-closed worker validation. VPS direct-IP staging preparation is in progress; Owner Control Plane and APK remain deferred.

## QA phase — 7A-2 (closed)

Durable timestamped transcript segment persistence.

Implemented contract:

- RecordingId
- deterministic SegmentIndex
- relative StartSeconds/EndSeconds
- Text
- language
- meaningful Whisper metadata
- retry/idempotency
- batch/transaction behavior

Correctness requirement: if transcript persistence succeeds but later alert processing fails, retry must not duplicate transcript segments.

Worker order is now:

```text
download
-> transcribe
-> persist segments
-> evaluate/persist alerts
-> mark QA processed only after all success
```

Evidence clips remain a later phase.

## Owner/Codex workflow decisions

Maximum safe automation is desired.

Routine Codex work should handle:
- inspection/search
- PowerShell
- source editing
- builds/tests
- Docker/PostgreSQL
- API/Agent/runtime lifecycle
- browser/dashboard tests
- safe Teams test automation
- temporary proof data
- cleanup
- failure diagnosis

No fake fixes, ignored failures, hidden errors or false green reports.

Human-only gates include real Teams mobile answer/speech/listening, MFA/OTP, physical device interaction and genuine business decisions.

Use checkpoint: `HUMAN TEST REQUIRED`

Dashboard/browser testing should be automated where possible. Never commit passwords/tokens/cookies.

Teams automation is limited to explicitly approved test target. If mobile/student human action is required, stop and wait.

Before every coherent major-phase commit/push, provide detailed release summary and stop at:

`RELEASE APPROVAL REQUIRED`

Owner approval keyword:

`APPROVE`

No major-phase commit/push before APPROVE.

After successful push, report actual commit hash, push result, clean worktree and phase-close summary.

Then explain next major phase in detail and wait for:

`GO`

During autonomous work send concise `CHECKPOINT GREEN X/Y` updates after meaningful milestones.

After laptop restart/new Codex chat, owner should only need:

`Continue project`

Codex must read project state/governance, inspect Git/runtime/source and resume from last verified checkpoint.

## Owner Control Plane — required, not implemented

`docs/OWNER-CONTROL-PLANE.md` is authoritative desired product policy.

The product must gain a separate Owner control surface with backend-enforced role + granular permission + resource scope; templates/custom grants and effective-access preview; durable Admin↔Manager and Manager↔Teacher assignments/history; configurable recording retention; durable audit; system health; and secure centralized device/Main-Agent lifecycle.

Likely future tracks are O1 authorization/permission foundation, O2 organization control, O3 retention, O4 device-management foundation, and O5 remote Agent lifecycle. Stabilization first closes immediate authorization defects. None of O1–O5 is currently implemented as a complete phase.

## Codex governance status

Project-wide governance was committed and pushed at `32202b3b4202515a684373bfbf6500e8a4e7eef7`. Status is `CLOSED`.

Root `AGENTS.md` is the repository-wide governance file. The existing `src/Dashboard/academy-dashboard/AGENTS.md` adds only Next.js/dashboard-specific guidance.

Project-local `.codex/config.toml` requests `workspace-write`, `on-request`, and the supported native Windows elevated sandbox. Project rules allow routine read-only Git inspection, prompt on commit/push, and forbid destructive Git patterns. These project-local layers load only when the repository is trusted.

Committed bootstrap files:

- `.codex/config.toml`
- `.codex/rules/default.rules`
- `AGENTS.md`
- `README.md`
- `docs/PROJECT-STATE.md`
- `docs/CODEX-WORKFLOW.md`
- `docs/OWNER-CONTROL-PLANE.md`
- `docs/architecture/ADR-001-system-architecture.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/architecture/qa-worker-service.md`
- `docs/decisions/coding-conventions.md`

No product source, migration, or test file belongs in this bootstrap.

## Production deployment

Direct-IP synthetic VPS staging preparation is approved and awaiting release.
Production activation, domain/TLS, real academy data and unrestricted laptop
rollout remain separate high-risk approvals.

## Resume checklist

1. Read this file.
2. Read `docs/architecture/current-state.md`.
3. Inspect branch/HEAD/status.
4. Inspect staged/uncommitted files.
5. Inspect runtime.
6. Determine whether current phase is closed, in progress, waiting human test, waiting release approval or blocked.
7. If the recorded status is `WAITING_RELEASE_APPROVAL`, do not stage/commit/push or start the next phase.
8. Resume only from verified state.
