# HomeQuranLearning.QA - AI Engineering Governance

Read `docs/PROJECT-HANDOFF.md` before implementation work.

## Current authority

Authoritative development branch:

`codex/qa-direct-audio`

Current deployed application-code checkpoint:

`71fee019aaf2262cb617ef675434487097f7ae81`

Current immutable Agent rollout release:

- release: `6875afda0cad-qa2`
- version: `1.0.0-6875afda0cad-qa2`
- SHA256: `7CAE8F97BB1CEC281A6CFB735A57F1865D07B4DFAD3AD5B45430A673CB1A353A`
- source commit: `6875afda0cad919a8ddabcb74b11ae6281f22f90`

qa2 is already published in production and queued for eight teacher-assigned
devices. Do not rebuild, replace, retarget or republish qa2 while this rollout
is being evaluated.

Owner device `DESKTOP-PUFUU3U` already has qa2 and must not be reinstalled
without a concrete recovery reason.

## Priority

1. Audio reliability
2. Live Monitoring
3. QA
4. Recording

This is product priority, not a claim that Audio currently has an open defect.

## Non-negotiable current architecture

- shared classroom audio foundation:
  `ClassroomAudioRuntime` / `ClassroomAudioHub`
- no duplicate QA microphone capture
- current Live path:
  Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> dashboard
- QA path:
  `QaLocalVoskWorker` -> local evidence -> direct `QaAlert`
- no QA Candidates/server Whisper/chunk/transcript fallback
- server archive identity uses stable `DeviceId` + `.device-id` sidecar
- registrar uses bounded exponential retry backoff
- `RemoveLegacyQaExperiments` is already deployed
- managed Agent update preserves the existing protected Agent credential

## Current rollout state

The active production Agent manifest points to qa2.

Eight teacher-assigned devices were queued at
`2026-09-18 11:37:36.582694+00`.

Successful adoption for a teacher device is:

`AgentVersion = 1.0.0-6875afda0cad-qa2`

and:

`PendingAgentUpdateVersion = NULL`

Do not requeue or manually reinstall a device merely because it has not updated
immediately. First inspect heartbeat freshness, updater execution/logs, pending
state and partial/resumable package download state.

The next engineering boundary is rollout observation and real-class behavior,
not a new Agent build.

## Current issue state

There is no standing audio echo, repeated-voice, buffering or delay defect
currently established.

Historical observations from earlier diagnostic chats must not be converted into
an open engineering task. Reopen audio troubleshooting only from fresh Owner
reports or fresh runtime evidence.

During qa2 teacher observation, watch:

- teacher microphone
- student/system audio
- Live continuity
- recording capture/playback
- local Vosk restricted-rule alerts and evidence

Diagnose only what fresh evidence demonstrates.

## Capacity

Actual academy concurrency:

- normal: 7-8 simultaneous classes
- higher load: about 9
- peak: about 10-11
- peak usually lasts only 2-3 hours

Do not plan around the old 15-concurrent assumption.

The current 4-vCPU VPS is constrained mainly by RTMP -> LiveKit Ingress
transcoding CPU at higher concurrency.

Short-term scale = more vCPU.
No-transcode/WHIP work = later phase.

## Production safety

The VPS tracked `infrastructure/docker/Caddyfile` has intentional local
production modifications.

Do not reset/stash/overwrite it casually.

Never expose secrets, Agent credentials, JWTs, stream keys or LiveKit
credentials.

Never use top-level `set -e` / `exit 1` in the Owner's interactive VPS root
shell. Put risky strict-mode work in a subshell.

Do not use blind `docker system prune` or `docker volume prune`; MinIO and
recording data are legitimate production state.

## Git / deployment safety

On the VPS, do not assume a normal fetch refreshed the remote-tracking ref.
When exact tracking-ref refresh is required, use an explicit refspec.

The authoritative working branch is `codex/qa-direct-audio`, not `main`.

Use narrow `--no-deps` Compose recreation when only one production component
must be replaced and dependencies do not need recreation.

## Engineering style

Inspect exact source/runtime first. Do not guess inspectable facts.

Use the smallest fix for the proven boundary.

Do not repeat already-closed investigations without contradictory evidence.

If a command partially fails, inspect the resulting state and continue from the
failed boundary; do not blindly reset or repeat successful earlier steps.

Do not rerun full test suites as ceremony. Run verification appropriate to the
affected boundary.

Windows commands should be PowerShell 5.1 compatible unless explicitly stated
otherwise. Bash is for the already-open VPS SSH shell.

Use full copy/paste commands when asking the Owner to run something.

Do not mix unrelated sub-tasks.

Do not claim PASS without actual output.

After a meaningful successful checkpoint, refresh the canonical state/handoff
docs when the durable project state materially changed.

Full durable state: `docs/PROJECT-HANDOFF.md`.