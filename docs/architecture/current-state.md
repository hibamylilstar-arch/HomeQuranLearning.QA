# HomeQuranLearning.QA — Current Technical State

## Status

Canonical checkpoint:

- Working branch: `codex/7a-2-transcript-segments`
- Base commit: `32202b3`
- Base subject: `chore(codex): add autonomous project governance`
- Latest closed product phase: `Dashboard completion - evidence and operational views`
- Current product phase: `7A-2 — durable timestamped transcript segments`
- Phase status: `WAITING_RELEASE_APPROVAL`; dashboard operational hardening is released at `e127a19`.
- Current QA feature: durable timestamped transcript segment persistence (implemented and validated; release approval pending).

Canonical resumable state: `docs/PROJECT-STATE.md`

## Purpose

Private internal QA, attendance, monitoring and evidence system for an online Quran academy using academy-owned/managed Windows teacher laptops.

Expected scale:

- 20+ teachers
- approximately 15 simultaneous teacher laptops/classes at peak
- roughly 2–3 hour peak windows
- mostly stable teacher/device mapping with occasional substitutions

## Local-first policy

Production/VPS deployment is deferred until core and advanced local functionality is implemented, tested and stabilized, and the owner explicitly approves deployment.

## Stack

### Backend
- ASP.NET Core
- .NET 10
- EF Core
- PostgreSQL

### Infrastructure
- PostgreSQL
- Redis
- MinIO
- LiveKit
- LiveKit Ingress
- ingress-manager
- Docker Compose

### Dashboard
- Next.js
- React
- TypeScript

### Agent
- Academy.Agent.Service
- Academy.Agent.Cloud
- Academy.Agent.Audio
- Academy.Agent.Capture
- Academy.Agent.Media
- Academy.Agent.Teams
- Academy.Agent.TeamsHelper

## Live monitoring — proven

```text
Windows Agent
-> FFmpeg RTMP
-> LiveKit Ingress
-> LiveKit
-> Browser Dashboard
```

Current proven screen path uses FFmpeg `ddagrab`.

Known-good timing:

```text
framerate=10
dup_frames=1
hwdownload
format=bgra
setpts=N/(10*TB)
```

Current system-audio path:

```text
NAudio / WASAPI loopback
-> local UDP
-> FFmpeg AAC
-> LiveKit
```

Live screen + system audio and dashboard audio enable/disable have been proven end-to-end locally.

## Recording — proven

Recording was changed away from huge raw BGRA temporary files.

Current direction:

```text
screen/audio capture
-> FFmpeg
-> direct H264 MP4
-> upload/recovery pipeline
```

Historical recording/session identity must remain safe across later schedule or mapping changes.

## Domain foundation

Implemented foundations include:

- users
- Owner/Admin/Manager roles and partial Manager teacher filtering
- teachers
- students
- courses
- devices
- manager-teacher assignments
- device heartbeats
- schedules
- sessions
- session events
- recordings
- QA rules
- QA alerts

Auth includes JWT/HTTP-only dashboard flow plus agent and worker API keys.

Some Manager-facing resources are filtered to assigned-teacher scope. Reconnaissance found important routes and playback/live/token operations that still lack complete permission + resource-scope enforcement. Treat authorization as incomplete until planned stabilization/O1 work is proved.

## Scheduling/history

Completed historical sessions must retain original TeacherId, StudentId, CourseId, DeviceId and scheduled window.

## Attendance

Implemented states include Unknown, Present, Late, Absent, Excused and NeedsReview.

Review states include Pending, AutoResolved and Reviewed.

Late threshold: `3 minutes`

Teacher pre-class ready window: `5 minutes`

Attendance is evidence-driven.

## Teams attendance — completed

Architecture:

```text
Academy.Agent.Service
<-> secured Named Pipe
<-> Academy.Agent.TeamsHelper
-> Teams UI Automation / WebView
```

Proven:

- exact scheduled-student chat binding
- outgoing message detection
- stable IDs/timestamps
- lesson attachment evidence
- call snapshots/lifecycle
- durable evidence journal
- backend delivery
- PostgreSQL persistence
- dedupe/idempotency

Backend event types:

```text
TeacherGreetingSent  = 19
CallAttempted        = 20
StudentCallConnected = 21
CallEnded            = 22
LessonShared         = 23
```

Source: `TeamsUIAutomation`

Current semantics:

- TeacherGreetingSent = teacher evidence only
- CallAttempted = teacher evidence only
- StudentCallConnected = explicit student presence
- CallEnded = stop/duration evidence
- LessonShared = strong teacher and student presence evidence, but not an arrival timestamp

StudentCallConnected within 3 minutes => Present; later => Late.

Known example:

```text
StudentCallConnected +2m
CallEnded +12m
=> ActiveSeconds 600
```

CallEnded alone does not prove attendance.

LessonShared requires exact scheduled student chat + outgoing teacher message + lesson-related text + actual image in the same message.

Image alone is insufficient. Keyword-only text is insufficient. Filename/extension are not semantics.

## Teams call lifecycle

```text
Available / Idle -> Attempting -> Connected -> Available
```

Real evidence path has been proven:

```text
Teams UI
-> evidence journal
-> delivery worker
-> /api/agent/session-events
-> PostgreSQL
-> AttendanceReducer
```

## Daily attendance

Daily reporting foundation is implemented.

Timezone: `Asia/Karachi` / `Pakistan Standard Time`

Manager filtering must remain enforced.

## QA/STT worker

Current STT engine: `faster-whisper 1.2.1`

Current production-wired source:

`spikes/SttSpike/qa_worker.py`

Docker wiring:

`infrastructure/docker/Dockerfile.worker`

Current worker endpoints include pending recordings, mark processed, active QA rules and create QA alert.

## Phase 7A-1 — closed GREEN

Commit: `415bbec`

Implemented:

- `PendingQaRecordingDto.StartedAtUtc`
- `CreateQaAlertRequest.QaRuleId`
- reusable Whisper model
- normalized transcript index
- cross-segment phrase matching
- recording-relative alert timestamps
- exact QA rule linkage
- duplicate retry suppression
- successful-processing-only `QaProcessedAtUtc`
- failure remains pending

Self-test markers:

```text
QA_WORKER_TRANSCRIPT_INDEX_OK
QA_WORKER_CROSS_SEGMENT_MATCH_OK
QA_WORKER_RULE_LINK_OK
QA_WORKER_TIMESTAMP_ALIGNMENT_OK
QA_WORKER_SELF_TEST_OK
```

Gates:

- full build GREEN
- unit 67/67
- integration 2/2
- production runtime/API/DB proof GREEN

Historical post-cleanup snapshot:

```text
Recordings 148
QA Rules 1
QA Alerts 5
Sessions 20
Session Events 163
```

These counts are historical proof only, not permanent invariants.

## Known runtime helper behavior

`.dev-runtime/Runtime.ps1 StartApi` may briefly show API OFF immediately after launch even when HTTP readiness succeeds later. Use bounded HTTP readiness checks.

## Next engineering work

The local dashboard evidence/operational vertical slice is implemented, backend/runtime/browser-proven and pushed at `051fe9e`; operational hardening is released at `e127a19`. The VPS remains deferred until local product and stability gates are green.

The dashboard proof includes 0-error/0-warning lint, a successful production build, full backend build/tests, authenticated login and proxy HTTP checks, 401 unauthenticated behavior and 404 missing/inaccessible event behavior. Browser proof verified the unauthenticated redirect, approved Owner login, 20-session rendering, the six-event raw timeline, filter empty-state/recovery and logout back to `/login`.

7A-2 is implemented on `codex/7a-2-transcript-segments` and is waiting for release approval after full validation.

Likely concerns:

- Recording linkage
- deterministic SegmentIndex
- StartSeconds / EndSeconds
- transcript text
- language
- meaningful Whisper probability/log metadata
- idempotent retry
- transactional batch persistence

Required order:

```text
download
-> transcribe
-> persist transcript segments
-> evaluate/persist alerts
-> mark processed only after all success
```

Evidence clip extraction remains a later coherent phase.

## Production

Production/VPS remains deferred until local dashboard, transcript QA, stability and controlled multi-laptop readiness gates are green.

## Owner Control Plane direction

The dedicated Owner Control Plane is a required future product track, not a current completed capability. The target authorization model is authenticated user + granular permission + resource scope, enforced by the backend. It also includes organization assignment history, configurable recording retention, audit, and secure centralized device/Main-Agent lifecycle. See `docs/OWNER-CONTROL-PLANE.md`.
