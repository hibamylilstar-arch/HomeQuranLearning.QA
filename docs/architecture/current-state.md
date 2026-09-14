# Current Supported Architecture

Last verified: 2026-09-15

## Agent

The Agent owns heartbeat, session awareness, screen capture, classroom audio,
live publishing, recording, local restricted-word QA and evidence upload.

## Shared audio

`ClassroomAudioRuntime` and `ClassroomAudioHub` are the shared foundation.

Physical classroom audio should be captured once and fanned out.

The next phase must prove source/runtime matches this rule and identify any
duplicate capture or blocking/buffering boundary.

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

## Immediate architecture investigation

Prove effective communication mic/render selection, shared consumers,
queue/buffer boundaries, back-pressure, route-change recovery, teacher echo
source and end-to-end latency before changing the proven media transport.