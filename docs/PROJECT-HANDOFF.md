# HomeQuranLearning.QA - Canonical Engineering Handoff

Last verified: 2026-09-18

Read this first in any new AI session. Source/runtime evidence wins over prose.

## Current checkpoint

- Branch: `codex/qa-direct-audio`
- Local repo: `C:\Dev\HomeQuranLearning.QA`
- VPS repo: `/opt/homequranlearning`
- Latest deployed application-code SHA:
  `71fee019aaf2262cb617ef675434487097f7ae81`
- Latest deployed application commit:
  `fix(dashboard): correct session mobile column labels`
- Production API and Dashboard are deployed at that application SHA.
- VPS tracked `infrastructure/docker/Caddyfile` has intentional local
  production changes. Do not reset/stash/overwrite it casually.

Latest verified production application checks included:

- deployed source matched
- API running
- Dashboard running
- API restarts = 0 at verification
- Dashboard restarts = 0 at verification
- `/health` = HTTP 200
- `/login` = HTTP 200
- unauthenticated refresh through Dashboard and production Caddy = HTTP 401
- protected media/database containers were not restarted

## Priority

1. Audio reliability
2. Live Monitoring
3. QA
4. Recording

This order describes product importance; it does not assert an open audio defect.

## Actual academy concurrency

Use these numbers for capacity reasoning:

- normal: 7-8 simultaneous classes
- higher load: about 9
- peak: about 10-11
- peak normally lasts only 2-3 hours

Do not use the old 15-concurrent assumption.

## Current live path

Windows Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit
-> dashboard.

Screen capture uses the proven `ddagrab` path.

Current production transport is RTMP/Ingress. WHIP/no-transcode is a future
optimization, not the deployed path.

## Current audio architecture

Physical classroom audio is captured through the shared classroom audio
foundation and fanned out to consumers.

Core shared components:

- `ClassroomAudioRuntime`
- `ClassroomAudioHub`
- `ClassroomAudioCaptureCoordinator`

Canonical classroom conversation:

- teacher effective communication microphone
- effective communication playback/render route

Current known-good implementation uses NAudio/WASAPI and a local UDP audio path
into FFmpeg.

Do not restore the historical FFmpeg-stdin audio path; it could block FFmpeg and
destabilize LiveKit ingress behavior.

QA must not open an independent competing physical microphone pipeline.

## Current QA architecture

Supported path:

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> restricted High rules
-> local Vosk detection
-> local WAV evidence
-> direct restricted-alert API
-> `QaAlert`
-> dashboard

Evidence is approximately:

- 10 seconds before trigger
- 20 seconds after trigger

Current actions:

- Review
- Ignore
- Reopen

qa2 includes:

- packaged `vosk-model-en-us-0.22-lgraph`
- deterministic local model path
- `QaLocalVosk.Enabled = true`
- local QA configuration written by the installer

Local QA should run only during an eligible scheduled/live observation context.

Expected successful log marker:

`Local QA Alert accepted...`

Retired and must not be restored:

- server Python QA worker
- faster-whisper server QA
- continuous QA audio chunks
- QA Candidates
- transcript persistence
- semantic/off-topic classifier experiments
- recording QA polling
- chunk-backed alerts
- obsolete `AcademyQaWorker`
- obsolete `spikes/SttSpike` runtime architecture

`RemoveLegacyQaExperiments` is already deployed to production.
Do not repeat that migration deployment.

## Recording reliability - closed incident

A registrar retry storm was caused by stale `.ready` markers whose historical
stream keys no longer mapped to current device keys.

Four historical orphan archives were recovered and registered.

Permanent production fix in
`e4590085ed99aa3de3ad4bf80769f79771615ca2`:

- archive target includes stable `DeviceId`
- recorder writes `.device-id` per stream directory
- registrar resolves sidecar identity before current stream-key lookup
- registrar uses bounded exponential retry backoff
- duplicate/ambiguous stream-key resolution is rejected safely

Do not remove this identity/backoff behavior.

The historical orphan/retry-storm incident is closed unless fresh evidence
appears.

## Current immutable Agent release: qa2

Release ID:

`6875afda0cad-qa2`

Version:

`1.0.0-6875afda0cad-qa2`

Source commit:

`6875afda0cad919a8ddabcb74b11ae6281f22f90`

Source commit message:

`fix(agent): preserve credential during managed updates`

Installer SHA256:

`7CAE8F97BB1CEC281A6CFB735A57F1865D07B4DFAD3AD5B45430A673CB1A353A`

Installer size:

`498983054` bytes

Production package:

`/opt/homequranlearning-agent-releases/packages/6875afda0cad-qa2.exe`

Active manifest:

`/opt/homequranlearning-agent-releases/manifest.json`

qa2 was verified before publication:

- uploaded byte count matched
- SHA256 matched
- release package copied to immutable release store
- release package reverified
- active manifest written atomically
- API container release visibility verified

Final rollout command ended with:

- `ROLLOUT_RC=0`
- `QA2_ROLLOUT=PASS`

**Do not rebuild or modify qa2.**

## Why qa1 is invalid

The earlier qa1 release from source
`52bc50a1a9ea6ed5fc33706b9c919bbcbf222efa` must never be deployed.

Its managed update path could overwrite an existing protected production Agent
credential with the deployment/build credential.

qa2 contains the permanent fix:

- fresh install writes deployment credential
- managed update preserves existing protected credential
- managed update fails closed if the protected credential is unexpectedly absent

Post-fix Agent tests: 175 passed, 0 failed.

## Previous rollout baseline / rollback material

Previous active fleet release:

- release ID: `c3b10ffd04a3-namegate1`
- version: `1.0.0-c3b10ffd04a3-namegate1`
- SHA256: `1FDE7DFBEE95D7D3B6253CECE1AA302C8FC619B2A0FAE1FE6604D65644D283CF`

Previous package remains retained at:

`/opt/homequranlearning-agent-releases/packages/c3b10ffd04a3-namegate1.exe`

Manifest backup created before qa2 publication:

`/opt/homequranlearning-agent-releases/manifest-history/manifest-before-6875afda0cad-qa2-20260918T113725Z.json`

Do not delete rollback material yet.

## Teacher qa2 rollout

Exactly eight teacher-assigned devices were queued for qa2.

Request timestamp:

`2026-09-18 11:37:36.582694+00`

Devices:

| Teacher | Device | DeviceId |
| --- | --- | --- |
| Ahmad Ali | `DESKTOP-GH93NCV` | `3954a75c-0d90-4cf1-8930-843b57683dd5` |
| Huzaifa | `DESKTOP-1GJ2F5P` | `862dde24-6813-449f-b665-96e6926b4d67` |
| Huzaifa | `DESKTOP-1MPCEMI` | `4fddb491-f2ac-4c8b-97b9-f513fddb2637` |
| Kamil Muneer | `DESKTOP-445SD1P` | `ef718065-b05e-401b-a8c9-e4cf65fce57f` |
| Muhammad Umer 2 | `DESKTOP-LORKONK` | `8bcfd344-11ed-4ac7-adb7-170b5bf4c68a` |
| Qaiser Nadeem | `DESKTOP-37IJKJ0` | `cf30f945-2048-4cc2-84bc-91907aa5904b` |
| Sajjad | `DESKTOP-5VN6RLP` | `67e170d4-47b3-42d7-8833-61a0d9886154` |
| Umar | `DESKTOP-71RJV67` | `8fa05fc9-c72c-494c-a2d0-ff622e7ead77` |

Queue transaction completed:

- `UPDATE 8`
- `COMMIT`
- `TEACHER_UPDATE_QUEUE=PASS`

At queue time each device still reported NameGate and had:

`PendingAgentUpdateVersion = 1.0.0-6875afda0cad-qa2`

Successful adoption should eventually show:

`AgentVersion = 1.0.0-6875afda0cad-qa2`

and:

`PendingAgentUpdateVersion = NULL`

Some rows had stale `LastSeenUtc` despite stored `Status=Online`.

Use heartbeat freshness / `LastSeenUtc`, not `Status` alone, when deciding
whether a laptop is actually active.

The Agent updater supports resumable package downloads. Do not assume an
interrupted 499 MB download is permanently failed.

## Owner device

Owner device:

- name: `DESKTOP-PUFUU3U`
- DeviceId: `82f9b22d-2d5b-46b2-b372-ef864219e383`

Owner already has qa2 and must not reinstall it merely as part of the teacher
rollout.

The previous qa1 -> qa2 managed-update canary proved:

- installer hash
- protected credential preservation
- installer marker `AGENT_CREDENTIAL_PRESERVED`
- heartbeat recovery
- qa2 version current
- local QA enabled
- Vosk model path present

The Owner explicitly chose to proceed with teacher rollout and observe real
classes rather than block on another full Owner functional canary.

Do not repeatedly argue to revert to the older staged plan unless fresh evidence
shows a serious regression.

## Rollout endpoint behavior

Agent checks:

`GET /api/agent/update/manifest`

The update is offered only when the device's
`PendingAgentUpdateVersion` matches the active manifest version.

When the Agent reports `currentVersion` equal to the manifest version, backend
clears:

- `PendingAgentUpdateVersion`
- `AgentUpdateRequestedAtUtc`

Then the manifest becomes disabled for that already-updated laptop.

Important edge case:

If the active manifest version changes while a queued device still has an older
pending version, the backend may clear that stale pending request rather than
silently retarget it.

Therefore do not casually change `manifest.json` while qa2 rollout is in
progress.

## Immediate next task

Do not build another Agent.

Do not republish qa2.

Do not change the active Agent manifest casually.

Do not requeue devices merely because they have not updated immediately.

First observe qa2 adoption on the eight queued teacher devices.

For a device still on NameGate, inspect:

- `LastSeenUtc`
- whether updater ran
- updater logs
- partial package/download state
- whether pending update remains
- manifest/package request success

Then observe actual classes for fresh regressions.

Priority during real-class observation:

1. Audio
2. Live
3. QA
4. Recording

Audio:

- student/system audio clear
- teacher microphone clear
- active communication/headset route behaves normally
- no fresh echo/repeat/delay/buffering

Live:

- starts normally
- no unnecessary reconnect loop
- video remains smooth

Recording:

- starts
- video present
- expected audio present
- archive upload/register works
- playback works

Local QA:

- restricted rules reach Agent
- Vosk observes during eligible session
- enabled phrase can create alert
- evidence contains expected pre/post audio
- evidence playback works
- Review / Ignore / Reopen work

Only investigate what fresh evidence demonstrates.

## Dashboard / authentication state

Deployed application work after the recording fix includes:

### 12-hour academy time

Commit:

`00f8b6b82c3ff8bb5ae4b8c32ea6d7ea8d6d924f`

- Asia/Karachi
- 12-hour AM/PM
- central time helpers
- Live and Sessions converted

### Persistent authentication

Commit:

`8552ed3cecb0f1cb4dc82e8f520d4694c83e492e`

- access JWT remains 120 minutes
- separate refresh JWT
- distinct refresh audience
- HttpOnly `qa_refresh_token`
- rolling refresh lifetime about 400 days
- active dashboard silent refresh
- shared proxy can refresh and retry once
- logout clears access/refresh browser cookies
- refresh validates current DB user

Do not claim server-side refresh-token revocation exists; refresh tokens are
currently signed/stateless.

Known technical debt before final signoff:

- inspect specialized recording-media route refresh behavior
- inspect specialized QA-evidence route refresh behavior

### Attendance / table UX

Commits:

- `c6138b09365c264f11afec7529b62749b1934ec8`
- `71fee019aaf2262cb617ef675434487097f7ae81`

Deployed behavior includes:

- Attendance `View Evidence`
- `/sessions?evidence=<sessionId>`
- Sessions auto-load requested evidence
- reusable top horizontal table scroller on desktop
- bottom native scrollbar retained
- mobile Sessions labels corrected

Code/build/deployment are verified. Final visual user-reported PASS for every
latest UI detail is not yet recorded.

## Manager RBAC

Manager dashboard surface is already implemented and manually smoke-tested.

Manager sees:

- Live Monitoring
- Teachers
- Students
- Courses
- Schedules
- Sessions
- Attendance

Restricted administration areas include:

- Overview
- standalone Devices page
- Recordings
- QA / Quality administration
- Users
- Activity Log

Restricted pages show an Administration Access Required state.

Do not retest this unless RBAC/dashboard/backend work touches it.

## VPS capacity finding

Current historical VPS size:

- 4 vCPU
- about 7.8 GiB RAM
- 96 GB disk
- no swap

Major scaling cost is RTMP -> LiveKit Ingress transcoding CPU.

Earlier 9-stream inspection showed nine legitimate unique ingresses/rooms with no
evidence of duplicate stale streams. Ingress used roughly 270%+ CPU under that
load.

NameGate downgrade did not materially reduce ingress CPU.

Do not blame the Agent version for this server-side transcoding cost without
fresh evidence.

Short term: more VPS vCPU.

Later: proven no-transcode/WHIP publishing.

A larger-VPS live migration is deferred until the current rollout is stable.

## Storage / housekeeping

Do not run blind:

- `docker system prune`
- `docker volume prune`

MinIO contains legitimate recordings.

Existing safe maintenance:

- `/usr/local/sbin/hql-docker-housekeeping.sh`
- `/usr/local/sbin/hql-agent-release-housekeeping.sh`
- bounded journald retention
- Docker log rotation

Do not restart Docker merely for logging.

## Current issue / task state

No active teacher echo, repeated-voice, buffering or delay defect is currently
established.

Earlier chat observations are historical. They must not become a standing
engineering task.

Start audio troubleshooting only when:

1. the Owner reports a current reproducible symptom; or
2. fresh runtime evidence demonstrates a regression.

The existing audio product contract still requires effective communication
microphone/render routes, valid wired/USB/Bluetooth/internal endpoints, shared
canonical classroom audio where designed and preserved Live stability. Those
are architecture requirements, not evidence of current failure.

## Production Caddy safety

Production `infrastructure/docker/Caddyfile` has intentional local
production-only modifications, including QA evidence and installer/download
routing.

Never:

- `git reset --hard`
- blindly checkout the Caddyfile
- casually stash/reset it
- overwrite it during deployment

Preserve or explicitly back it up before source changes.

## VPS Git behavior

A stale remote-tracking ref was observed on the VPS.

Do not assume:

`git fetch origin codex/qa-direct-audio`

followed by:

`git rev-parse origin/codex/qa-direct-audio`

must reflect the newly fetched commit.

When exact tracking-ref refresh is required, use an explicit refspec.

## Closed boundaries - do not re-prove without new evidence

- obsolete local `AcademyQaWorker` caused an earlier CPU/live incident and is retired
- historical echo/repeat/buffering/delay observations are closed
- `ddagrab` video path is a proven baseline
- FFmpeg-stdin audio path is retired
- RTMP LiveKit Ingress transcode is the major VPS media CPU cost
- one-layer ingress tuning did not solve that cost
- four archive orphans are recovered
- registrar retry storm is understood and permanently mitigated
- QA cleanup migration is deployed
- server QA/candidate/transcript architecture is removed
- recording identity bug is closed
- Manager RBAC is complete unless touched by new work

## Verification history

QA cleanup baseline previously passed:

- full .NET solution build
- Unit: 148 passed
- Integration: 4 passed
- Agent tests passed
- Next.js production build
- active legacy QA references: zero
- `git diff --check`

Local QA packaging before credential fix:

- Agent tests: 172/172 PASS

After managed-update credential fix:

- Agent tests: 175/175 PASS

Persistent-auth change:

- solution build PASS
- Unit: 150/150
- Integration: 4/4
- Dashboard build: 22/22

Latest attendance/table/mobile changes:

- Next.js production build PASS
- TypeScript PASS
- 22/22 static pages generated
- `git diff --check` PASS
- production deployment PASS

Do not rerun broad old suites unless changed code makes them relevant.

## Working protocol for AI engineers

- inspect first, change second
- once evidence isolates a boundary, fix that boundary rather than continuing
  unrelated tests
- do not repeat closed tests without fresh contradictory evidence
- if a command partially fails, inspect the resulting state and resume from the
  failed boundary
- do not blindly reset successful earlier work
- use full copy/paste commands for the Owner
- PowerShell 5.1 compatibility is the default for Windows commands
- Bash only in the already-open VPS SSH shell
- keep unrelated sub-tasks separate
- preserve intentional dirty production state
- never expose secrets
- do not claim PASS without output proving it
- update canonical state/handoff docs after meaningful durable checkpoints

## New-session read order

1. `AGENTS.md`
2. `docs/PROJECT-HANDOFF.md`
3. `docs/PROJECT-STATE.md`
4. `docs/architecture/current-state.md`
5. `docs/PROJECT-DECISIONS.md`
6. latest Git history
7. exact affected source/runtime