# HomeQuranLearning.QA — Project State

## Purpose and authority

Canonical resumable project checkpoint. Use it with Git and actual repository state after reboot, new Codex chat, interrupted phase or missing chat history.

Chat history is not authoritative. Repository state + this file are the durable project state.

## Canonical checkpoint

- Branch: `codex/final-classroom-agent-installer`
- HEAD: `9ff6abf9633b4231ddab3eb73ea0cdde2f11145f`
- Status: `READY_FOR_PRODUCTION_DEPLOYMENT`
- Development mode: production-targeted local development; current branch changes are not deployed.
- Latest committed checkpoint: production classroom installer / WHIP live-path foundation at `9ff6abf`.
- Current verified but uncommitted work:
  - development Runtime now keeps TeamsHelper manual on the admin/development laptop and supports `StartTeams` / `StopTeams`; normal `StartAll` leaves TeamsHelper OFF;
  - stale Agent `--no-build` launcher behavior was removed so current source is used;
  - dashboard authentication cookie handling works on local HTTP LAN while preserving Secure cookies behind HTTPS;
  - dashboard layout is responsive on mobile; Sessions and Attendance use stacked mobile cards; known mojibake in these operational views and the Sessions review separator were repaired;
  - installer/dashboard branding uses Home Quran Learning product identity and developer/owner credit;
  - root `AGENTS.md` has been revised to a production-first, low-bureaucracy engineering model.
- Verification already completed for the current uncommitted dashboard slice:
  - dashboard lint GREEN;
  - Next.js production build GREEN;
  - mobile Sessions and Attendance human visual test GREEN;
  - Sessions review text renders cleanly as `Active <duration> - Drops <count>`.
- Runtime/TeamsHelper verification already completed for the current uncommitted runtime slice:
  - Runtime parser GREEN;
  - Agent build GREEN;
  - `StartAll` => API ON / Agent ON / Teams OFF;
  - `StartTeams` => Teams ON;
  - `StopTeams` => Teams OFF;
  - development Teams scheduled task disabled intentionally.
- Intentionally uncommitted/generated backup files currently include:
  - `.dev-runtime/Runtime.ps1.before-teams-control`
  - `.dev-runtime/Start-AcademyAgent.ps1.before-teams-control`
  - dashboard `*.mobile-backup` files.
- Known local line-ending/index noise remains in:
  - `infrastructure/docker/Dockerfile.api`
  - `infrastructure/docker/Dockerfile.dashboard`
  - `infrastructure/docker/Dockerfile.worker`
  - `spikes/SttSpike/requirements.txt`
  These must not be blindly reset or staged; prove real content differences before action.
- Current release candidate is locally verified and awaiting commit/push plus VPS rollout. Production routing no longer depends on teacher/reviewer source-IP `/32` allowlists; roaming Classroom Agents use HTTPS and application authentication.
- Owner can manage Admin/Manager accounts through real backend contracts: enable/disable, password reset and safe delete; Owner accounts are protected.
- Production Owner seeding no longer has a default email/password fallback.
- Final Classroom Agent installer built successfully as a self-contained Windows EXE (338.98 MB), SHA256 `02E378637A0252445B75A7BE7203ECF5845DB2694FC8312E037546CC52C7E549`; current build is unsigned and may trigger SmartScreen.
- Verification: unit 87/87 GREEN; integration 5/5 GREEN; dashboard lint GREEN; dashboard production build GREEN; production config self-test GREEN; installer build GREEN.
- Historical VPS pilot/deployment work documented later in this file is separate history; do not infer that the current branch or current uncommitted changes are deployed.
- Next production-development checkpoint:
  1. reconcile `PROJECT-DECISIONS.md`, `OWNER-CONTROL-PLANE.md`, and relevant architecture state with the revised governance;
  2. review intended diffs, backups/noise, and secret exposure;
  3. update this file with the final verified checkpoint;
  4. create an atomic commit and normal push on the current feature branch;
  5. start Owner Control Center v1 with real backend contracts only.

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

## Owner/engineering workflow decisions

Treat the repository as a real production software project.

Normal low-risk development flow:

`inspect relevant state -> implement production-quality slice -> targeted verification -> fix regressions -> update PROJECT-STATE.md -> review intended diff/secrets -> atomic commit -> normal push -> continue`

Rules:

- No fake production endpoints, fake health/metrics, placeholder production controls, hidden credentials, hard-coded device IDs, IP-based trust, or localhost-only production assumptions.
- Development-only proof data or mocks must be clearly isolated and labelled.
- Do not require ceremonial `APPROVE` or `GO` gates for ordinary verified low-risk local development commits/pushes.
- Explicit approval remains mandatory for production/VPS deployment, production migrations/destructive DB actions, secret rotation, production-impacting auth/RBAC/security changes, historical evidence mutation, proven live/recording architecture replacement, Teams attendance semantic changes, system-wide firewall/security changes, driver installation, force/history Git operations, and deployment-changing canonical release/merge actions.
- Keep `PROJECT-STATE.md` factual and recoverable, not a command diary.
- Ask the owner to run local commands only when this environment cannot execute them; commands must be exact and copy/paste-safe, preferably single-line when PowerShell continuation could confuse.
- Human-only actions remain physical/mobile tests, MFA/OTP/login steps, and genuine business decisions.

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

Development is production-targeted. Actual VPS/production deployment remains a separate high-risk action.

Historical VPS pilot/deployment work is documented above. The current `codex/final-classroom-agent-installer` branch and its uncommitted changes are not deployed by this checkpoint.

Before any next VPS mutation, verify the exact remote commit/config/runtime state and obtain explicit high-risk approval for the bounded production action.

## Resume checklist

1. Read `AGENTS.md` and this file.
2. Inspect branch, HEAD, worktree/staged state, and origin when available.
3. Read only the decisions/architecture documents relevant to the current slice.
4. Inspect runtime/database/Docker/browser state only if the current slice touches them.
5. Resume from the latest verified checkpoint; do not redo closed work.
6. Never discard/reset/clean/stash unexpected human work.
7. Continue ordinary verified low-risk development without ceremonial release gates.
8. Stop for explicit approval only when the next action is high-risk under `AGENTS.md`.
