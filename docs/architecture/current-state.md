# Current Supported Architecture

Last verified: 2026-09-18

This document describes supported architecture. Operational rollout details live
in `docs/PROJECT-HANDOFF.md` and `docs/PROJECT-STATE.md`.

## Agent

The Windows Agent owns:

- heartbeat and device presence
- session/class-window awareness
- screen capture
- shared classroom audio capture
- Live publishing
- recording participation
- local restricted-word QA
- evidence upload
- managed self-update

Current immutable rollout build:

`1.0.0-6875afda0cad-qa2`

qa2 packages local Vosk and contains the managed-update credential-preservation
fix.

## Shared audio

`ClassroomAudioRuntime`, `ClassroomAudioHub` and
`ClassroomAudioCaptureCoordinator` are the shared foundation.

Physical classroom audio should be captured once and fanned out to the
appropriate consumers.

Effective classroom conversation is based on:

- teacher effective communication microphone
- effective communication playback/render route

USB, Bluetooth, wired and internal endpoints are valid when they are the
effective communication route.

The known-good implementation uses NAudio/WASAPI and local UDP delivery into
FFmpeg where required.

Do not restore the historical blocking FFmpeg-stdin audio path.

This architecture contract is not evidence of an active runtime defect.

## Live

Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> dashboard.

Screen capture uses `ddagrab`.

Current production still uses ingress transcoding.

WHIP/no-transcode publishing is future work.

## QA

Supported path:

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> restricted High rules
-> local Vosk
-> local WAV evidence
-> direct restricted-alert API
-> `QaAlert`
-> dashboard

Typical evidence window:

- about 10 seconds before trigger
- about 20 seconds after trigger

Human actions:

- Review
- Ignore
- Reopen

No supported production path exists for:

- server STT worker
- faster-whisper QA
- continuous server QA chunks
- QA Candidates
- transcript persistence
- semantic/off-topic experiments
- recording QA polling
- obsolete `AcademyQaWorker`

## Recording

MediaMTX
-> archive recorder
-> finalized MP4/`.ready`
-> archive registrar
-> MinIO/backend.

Recorder persists stable `.device-id` sidecars.

Registrar resolves sidecar identity first and uses bounded exponential retry
backoff.

Stream/ingress rotation must not lose recording identity.

## Agent update architecture

Backend release store reads:

`<releaseRoot>/manifest.json`

Packages resolve as:

`<releaseRoot>/packages/<releaseId>.exe`

A device is offered an update only when its queued
`PendingAgentUpdateVersion` matches the active manifest version.

When the Agent reports the target version, backend clears the pending request.

Managed-update credential policy:

- fresh install writes deployment credential
- managed update preserves existing protected credential
- missing protected credential during managed update fails closed

Release binaries are immutable. A source change requires a new release/version.

## Dashboard authentication

Dashboard uses:

- short-lived access JWT
- separate refresh JWT/audience
- HttpOnly refresh cookie
- silent refresh
- shared refresh-aware proxy retry

Refresh tokens are currently stateless/signed; logout clears browser cookies but
does not provide server-side copied-token revocation.

Specialized media/evidence routes remain a signoff inspection item until their
refresh behavior is explicitly confirmed.

## RBAC

Roles include Owner/Admin/Manager.

Manager dashboard surface is restricted to Live Monitoring and Academy
operations. Backend authorization remains the actual security boundary.

## Capacity

Actual concurrency:

- normal: 7-8 classes
- higher: about 9
- peak: about 10-11 for roughly 2-3 hours

The current 4-vCPU VPS is constrained mainly by LiveKit Ingress transcoding CPU
at higher concurrency.

This is a capacity fact, not a reason to redesign media transport during the
current Agent rollout.

## Runtime-status rule

No active echo, repeated-voice, buffering or delay issue is established by this
document.

Historical diagnostic observations are considered resolved unless fresh
evidence demonstrates otherwise.

Architecture inspection should be driven by a current feature/change request or
a newly observed regression, not stale chat history.