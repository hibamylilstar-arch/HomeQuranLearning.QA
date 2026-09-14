# Current Supported Architecture

Last verified: 2026-09-15

## Agent

The Agent owns heartbeat, session awareness, screen capture, classroom audio,
live publishing, recording, local restricted-word QA and evidence upload.

## Shared audio

`ClassroomAudioRuntime` and `ClassroomAudioHub` are the shared foundation.

Physical classroom audio should be captured once and fanned out.

This is an architecture contract, not evidence of an active runtime defect.
Do not launch a remediation phase without fresh Owner/runtime evidence.

## Live

Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> dashboard.

Screen capture uses `ddagrab`.

Current production still uses ingress transcoding.

## QA

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> restricted rules
-> local Vosk
-> local WAV evidence
-> direct `QaAlert`
-> dashboard.

No server STT worker and no QA Candidates product are supported.

## Recording

MediaMTX
-> archive recorder
-> finalized MP4/`.ready`
-> archive registrar
-> MinIO/backend.

Recorder persists `.device-id`.
Registrar uses sidecar identity first and bounded exponential retry backoff.

## Current runtime status rule

No active echo, repeated-voice, buffering or delay issue is established by this
document.

Historical diagnostic observations are considered resolved unless fresh
evidence demonstrates otherwise.

Architecture inspection should be driven by a current feature/change request or
a newly observed regression, not by stale chat history.