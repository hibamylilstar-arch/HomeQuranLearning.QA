# HomeQuranLearning.QA — Project State

## Purpose and authority

Canonical resumable project checkpoint. Use it with Git and actual repository state after reboot, new Codex chat, interrupted phase or missing chat history.

Chat history is not authoritative. Repository state + this file are the durable project state.

## Canonical checkpoint

- Branch: `main`
- Commit: `415bbec68c1ec2d7731dc738f976d87dd389e485`
- Origin: `origin/main` at the same commit (verified 2026-08-27)
- Subject: `feat(qa): align alerts with transcript timestamps`
- Latest closed phase: `7A-1`
- Current product phase: none
- Current governance phase: `CODEX AUTOPILOT GOVERNANCE BOOTSTRAP`
- Current phase status: `WAITING_RELEASE_APPROVAL`
- Next engineering gate: narrow stabilization before `7A-2`
- Waiting human test: no
- Waiting release approval: yes
- Last verified checkpoint: governance artifacts created and validated
- Tests already passed: 7A-1 full build GREEN, unit 67/67, integration 2/2, production API/DB proof GREEN; governance TOML/rules/secrets/diff validation GREEN
- Tests still required: after `APPROVE`, reverify exact files and staged set before commit
- Expected changed files: the governance/configuration/documentation list near the end of this file
- Temporary data: 7A-1 proof data cleaned; governance bootstrap created none
- Product runtime expected after latest proof: OFF

Neither stabilization nor 7A-2 has started.

## Current runtime snapshot

Verified 2026-08-27:

- `.dev-runtime/Runtime.ps1`: API OFF, Agent OFF, FFmpeg 0
- Docker: PostgreSQL, Redis, MinIO, LiveKit, LiveKit Ingress and ingress-manager running
- Windows service `AcademyQaWorker`: Running / Automatic

Reconnaissance indicates the worker process predates commit `415bbec`; treat it as potentially stale until it is deliberately restarted and proved during stabilization. The bootstrap does not alter runtime.

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
- LessonShared = teacher evidence only, NOT student presence

**Verified stabilization conflict:** current `AttendanceReducer` behavior/tests mark student Present from `LessonShared`, contradicting the authoritative policy above. Do not silently select new semantics inside another phase; resolve this as explicit, tested stabilization work.

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

## Next engineering gate — stabilization, then 7A-2

Reconnaissance identified the following current-state work. Do not implement it during the governance bootstrap:

1. Several admin routes and playback/live/token operations lack complete granular permission + resource-scope enforcement.
2. `LessonShared` reducer behavior conflicts with authoritative attendance policy.
3. The running QA worker may be stale relative to HEAD.
4. TeamsHelper lacks a verified durable launcher/installer/startup mechanism.
5. Manual-session workflow is incomplete.
6. Agent configuration contains environment-specific FFmpeg/device assumptions and recording is disabled by default.
7. Production Compose omits some live/reverse-proxy components and needs deliberate deployment design later.

The immediate next engineering step should be a narrowly scoped stabilization plan for the highest-risk defects. `7A-2` remains the next QA feature, not the next unconditional action.

## Planned QA phase — 7A-2 (not started)

Durable timestamped transcript segment persistence.

Likely concerns:

- RecordingId
- deterministic SegmentIndex
- relative StartSeconds/EndSeconds
- Text
- language
- meaningful Whisper metadata
- retry/idempotency
- batch/transaction behavior

Correctness requirement: if transcript persistence succeeds but later alert processing fails, retry must not duplicate transcript segments.

Worker order should become:

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

Project-wide governance has been prepared and validated but is not committed/pushed. Status is `WAITING_RELEASE_APPROVAL`.

Root `AGENTS.md` is the repository-wide governance file. The existing `src/Dashboard/academy-dashboard/AGENTS.md` adds only Next.js/dashboard-specific guidance.

Project-local `.codex/config.toml` requests `workspace-write`, `on-request`, and the supported native Windows elevated sandbox. Project rules allow routine read-only Git inspection, prompt on commit/push, and forbid destructive Git patterns. These project-local layers load only when the repository is trusted.

Expected bootstrap changes:

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

Deferred until local product completeness/stabilization and explicit owner approval.

## Resume checklist

1. Read this file.
2. Read `docs/architecture/current-state.md`.
3. Inspect branch/HEAD/status.
4. Inspect staged/uncommitted files.
5. Inspect runtime.
6. Determine whether current phase is closed, in progress, waiting human test, waiting release approval or blocked.
7. Honor `WAITING_RELEASE_APPROVAL`: do not stage/commit/push or start stabilization/7A-2.
8. Resume only from verified state.
