# HomeQuranLearning.QA - AI Engineering Governance

Read `docs/PROJECT-HANDOFF.md` before implementation work.

Priority:

1. Audio reliability
2. Live Monitoring
3. QA
4. Recording

Current deployed application-code checkpoint before this docs refresh:

`e4590085ed99aa3de3ad4bf80769f79771615ca2`

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

## Current issue state

There is no standing audio echo, repeated-voice, buffering or delay defect
currently established.

Historical observations from earlier diagnostic chats must not be converted into
an open engineering task. If such symptoms existed previously, the Owner
considers them resolved unless fresh runtime evidence proves otherwise.

Do not start an audio-remediation investigation without either:

- a fresh Owner report of a current problem; or
- fresh runtime evidence showing a real regression.

The effective communication-route architecture remains a product requirement,
not evidence that the current implementation is broken.

The next task comes from the Owner's current request / roadmap, not from an old
chat symptom.

## Capacity

The current 4-vCPU VPS is constrained mainly by RTMP -> LiveKit Ingress
transcoding CPU at higher concurrency.

One-layer ingress tuning did not materially reduce it.

Short-term scale = more vCPU.
No-transcode/WHIP work = later phase.

## Agent rollout

Owner canary: `DESKTOP-PUFUU3U`.

Agent source change -> targeted verification -> Owner physical proof -> immutable
release -> selective teacher rollout.

Do not push an unproven Agent change fleet-wide.

## Production safety

The VPS tracked `infrastructure/docker/Caddyfile` has intentional local
production modifications.

Do not reset/stash/overwrite it casually.

Never expose secrets or stream keys.

Never use top-level `set -e` / `exit 1` in the Owner's interactive VPS root
shell. Put risky strict-mode work in a subshell.

## Engineering style

Inspect exact source/runtime first. Do not guess inspectable facts.
Use the smallest fix for the proven boundary.
Do not repeat already-closed investigations without contradictory evidence.
Do not rerun full test suites as ceremony.

Full durable state: `docs/PROJECT-HANDOFF.md`.