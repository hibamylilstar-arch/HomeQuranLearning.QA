# HomeQuranLearning.QA - Current Project State

Last verified: 2026-09-15

Canonical operational handoff: `docs/PROJECT-HANDOFF.md`.

## Current checkpoint

Production application code is at:

`e4590085ed99aa3de3ad4bf80769f79771615ca2`

## Current priority

Audio -> Live -> QA -> Recording.

Immediate work is the Windows Agent audio pipeline, not another broad VPS
investigation.

## Audio

Shared foundation:

- `ClassroomAudioRuntime`
- `ClassroomAudioHub`

Next proof must map microphone/loopback ownership, all consumers,
queues/buffers, endpoint selection, route changes, echo and latency.

## Live

Current production path:

Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> dashboard.

`ddagrab` remains the proven screen path.

WHIP/no-transcode is not yet deployed.

## QA

Current supported restricted-word path:

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> local Vosk
-> local WAV evidence
-> direct alert API
-> `QaAlert`
-> Review / Ignore / Reopen

There is no active QA Candidates product.

Server QA worker, faster-whisper, continuous QA chunks, transcripts,
semantic/off-topic experiments and recording QA polling are retired.

`RemoveLegacyQaExperiments` is already deployed.

## Recording

Server archive path:

MediaMTX -> archive recorder -> MP4/`.ready` -> archive registrar -> MinIO/backend.

Production fix `e4590085...` persists stable device identity in `.device-id`
and uses registrar exponential backoff.

Four historical orphans were recovered.

Post-fix `.ready` count was zero and registrar returned to idle CPU.

Archive retry-storm incident is closed unless new evidence appears.

## Capacity

4-vCPU VPS is CPU-limited mainly by RTMP -> LiveKit Ingress transcoding at
higher concurrency.

One-layer ingress tuning did not materially reduce CPU.

More vCPU is the practical short-term scale path; no-transcode publishing is
later.

## Next task

Inspect and simplify the active Windows communication-audio path so the Agent
follows the effective headset/handfree mic and output, supports wired/USB/
Bluetooth/default communication routes, recovers from route changes, removes
teacher echo and reduces buffering while preserving Live stability.