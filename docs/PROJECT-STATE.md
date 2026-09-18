# HomeQuranLearning.QA - Current Project State

Last verified: 2026-09-18

Canonical operational handoff: `docs/PROJECT-HANDOFF.md`.

## Current checkpoint

Authoritative branch:

`codex/qa-direct-audio`

Production application code is deployed at:

`71fee019aaf2262cb617ef675434487097f7ae81`

Latest application commit:

`fix(dashboard): correct session mobile column labels`

Current immutable Agent release:

- release ID: `6875afda0cad-qa2`
- version: `1.0.0-6875afda0cad-qa2`
- source commit: `6875afda0cad919a8ddabcb74b11ae6281f22f90`
- SHA256: `7CAE8F97BB1CEC281A6CFB735A57F1865D07B4DFAD3AD5B45430A673CB1A353A`
- bytes: `498983054`

qa2 is active in production `manifest.json`.

The previous NameGate release remains retained for rollback:

`c3b10ffd04a3-namegate1`

The previous manifest was backed up before qa2 publication.

## Current priority

Audio -> Live -> QA -> Recording.

This describes product importance. It does not mean an audio incident is
currently open.

## Agent rollout

Eight teacher-assigned devices were queued for qa2 on 2026-09-18.

Queue transaction completed:

- `UPDATE 8`
- `COMMIT`
- `TEACHER_UPDATE_QUEUE=PASS`
- `QA2_ROLLOUT=PASS`

Owner device `DESKTOP-PUFUU3U` already has qa2 and was intentionally excluded.

Expected successful teacher state:

`AgentVersion = 1.0.0-6875afda0cad-qa2`

`PendingAgentUpdateVersion = NULL`

Do not requeue or manually reinstall immediately when a laptop remains on
NameGate. First inspect `LastSeenUtc`, updater execution/logs, pending state,
manifest/package requests and resumable partial-download state.

## Audio

Shared foundation:

- `ClassroomAudioRuntime`
- `ClassroomAudioHub`
- `ClassroomAudioCaptureCoordinator`

Known-good media path uses NAudio/WASAPI audio and local UDP into FFmpeg. Do not
regress to the historical blocking FFmpeg-stdin audio architecture.

No current echo, repeated-voice, buffering or delay defect is established.
Historical observations remain closed unless fresh qa2/runtime evidence shows a
regression.

## Live

Current production path:

Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> dashboard.

`ddagrab` remains the proven screen path.

WHIP/no-transcode is not deployed.

## QA

Current supported restricted-word path:

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> local Vosk
-> restricted High rules
-> local WAV evidence
-> direct alert API
-> `QaAlert`
-> Review / Ignore / Reopen

qa2 includes the packaged Vosk model
`vosk-model-en-us-0.22-lgraph` and enabled local QA configuration.

Retired and unsupported:

- server QA worker
- faster-whisper server QA
- continuous server QA chunks
- QA Candidates
- transcript persistence
- semantic/off-topic experiments
- recording QA polling
- obsolete `AcademyQaWorker` / `spikes/SttSpike` runtime

Do not restore them.

## Recording

Server archive path:

MediaMTX -> archive recorder -> MP4/`.ready` -> archive registrar ->
MinIO/backend.

Production fix `e4590085ed99aa3de3ad4bf80769f79771615ca2` preserves
stable archive identity through `.device-id` sidecars and bounded registrar
backoff.

The historical orphan/retry-storm incident is closed unless new evidence
appears.

## Dashboard / authentication

Deployed application state includes:

- Asia/Karachi 12-hour AM/PM display helpers
- persistent dashboard authentication with refresh token flow
- Attendance `View Evidence`
- Sessions evidence deep-link behavior
- synchronized top horizontal scrollbar for wide desktop tables
- corrected Sessions mobile column mapping

Latest production API/Dashboard deployment was verified healthy.

Known remaining auth technical debt before final signoff:

- inspect specialized recording media route refresh behavior
- inspect specialized QA evidence route refresh behavior

Do not assume this gap is fixed until source/runtime verification proves it.

## Capacity

Actual academy concurrency:

- normal: 7-8 simultaneous classes
- higher: around 9
- peak: 10-11
- peak duration: usually 2-3 hours

The old 15-concurrent assumption is retired.

Current 4-vCPU VPS is CPU-limited mainly by RTMP -> LiveKit Ingress
transcoding at higher concurrency. RAM is not the primary proven bottleneck.

A future larger-VPS migration is planned only after the current Agent rollout is
stable.

## Next task

Immediate:

1. Observe qa2 adoption on the eight queued teacher devices.
2. Observe real classes for fresh Audio / Live / QA / Recording regressions.
3. Diagnose only issues demonstrated by fresh evidence.

Still pending before final project signoff:

- manual UI sanity checks for the latest dashboard changes
- specialized persistent-auth media/evidence route inspection/fix if still needed
- final production signoff
- later VPS live migration
- later teacher tamper/USB hardening

Do not jump to later work before rollout results unless the Owner explicitly asks.