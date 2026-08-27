# HomeQuranLearning.QA — Architecture Overview

## Purpose

HomeQuranLearning.QA is an internal QA, attendance and monitoring system for an online Quran academy using academy-owned/managed Windows teacher laptops.

Expected scale:

- 20+ teachers
- about 15 concurrent teacher devices/classes during peak periods

This is not a public multi-tenant SaaS platform.

## Architecture principles

1. Teacher devices initiate outbound connectivity.
2. Backend remains a modular monolith: Domain, Application, Infrastructure and API.
3. PostgreSQL stores relational/metadata state.
4. Media lives in S3-compatible object storage rather than PostgreSQL.
5. Browser live monitoring uses LiveKit/WebRTC.
6. Current publishing path is FFmpeg RTMP into LiveKit Ingress.
7. Reliability, retries, historical safety and idempotency are first-class requirements.
8. QA detection uses speech timing/context rather than processing-time assumptions.
9. Production/VPS deployment is deferred until local product completeness and stabilization.

## Current component map

```text
Windows Agent
  -> Backend API / PostgreSQL / MinIO / Redis
  -> FFmpeg RTMP -> LiveKit Ingress -> LiveKit -> Browser Dashboard

Uploaded Recording
  -> qa_worker.py / faster-whisper
  -> timestamped QA matching
  -> QA alerts
```

## Windows Agent

Responsibilities include class/session context, screen capture, system audio, live streaming, MP4 recording, upload/recovery, heartbeat, Teams evidence and attendance evidence delivery.

Current screen/live implementation uses FFmpeg `ddagrab`.

Current system-audio path uses NAudio/WASAPI loopback and local UDP transport into FFmpeg.

## Backend

Technology:

- ASP.NET Core
- .NET 10
- EF Core
- PostgreSQL

Major domains include auth/RBAC, teachers, students, courses, devices, schedules, sessions, session events, attendance, recordings, QA rules and QA alerts.

## Teams attendance

Teams UI evidence uses an academy-controlled helper and secured Named Pipe integration with the main Agent.

`LessonShared` is strong teacher and student presence evidence, but its timestamp is not an arrival-time signal and cannot by itself establish lateness.

`StudentCallConnected` is the explicit Teams student-presence event.

## QA/STT

Current worker:

- Python
- faster-whisper
- `spikes/SttSpike/qa_worker.py`
- `X-Api-Key` worker authentication

Current completed QA work aligns alerts to recording-relative transcript timestamps and exact QA rule IDs.

Durable transcript-segment persistence is the next planned QA slice.

## Deployment

Eventual deployment remains compatible with a Linux VPS, Docker Compose, reverse proxy/TLS and S3-compatible object storage.

Production deployment is not the current phase.

## Detailed state

Read:

- `docs/architecture/current-state.md`
- `docs/PROJECT-STATE.md`
- `docs/decisions/ADR-001-system-architecture.md`
