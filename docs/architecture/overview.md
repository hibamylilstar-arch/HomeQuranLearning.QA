# HomeQuranLearning QA — Architecture Overview

## Purpose

An internal QA and monitoring system for the HomeQuranLearning academy, covering
20+ teacher laptops (academy-owned, mixed remote/local). This is **not** a
commercial SaaS product — it is built for a single organization's own devices
and processes.

## Core capabilities

* Windows agent on each teacher laptop: screen capture, audio capture
(microphone + system/communication audio via WASAPI loopback), local
recording with reconnect-safe buffering.
* Cloud backend: device registration/auth, session and recording metadata,
RBAC (Owner / Admin / Manager with per-teacher assignment), background
workers for speech-to-text and QA rule evaluation.
* Web dashboard (mobile-friendly): live monitoring tiles, live screen/audio
playback, recording search/playback, QA alerts with timestamped evidence.
* AI QA pipeline: STT → normalization → tiered rule matching (keyword →
phrase/context → optional AI classification) → alert → evidence clip.

## Key architectural principles

1. **Outbound-only agent connections.** Teacher laptops initiate secure
outbound connections to the cloud. No inbound connections to laptops, no
dependency on static IPs or LAN topology — teachers can be on any ISP.
2. **Modular monolith, not microservices.** At \~20 devices, a single
well-structured ASP.NET Core backend (Domain / Application /
Infrastructure / Api layering) plus a dedicated media server is simpler
to build, test, and operate than a distributed microservices setup.
Kubernetes is explicitly out of scope for now.
3. **Recordings live in object storage, not the database.** PostgreSQL
stores metadata only (device, teacher, class, timestamps, storage key,
status). Media files go to S3-compatible object storage.
4. **Low-latency live monitoring uses WebRTC**, not polling/HTTP streaming,
via a dedicated SFU/media server (e.g. LiveKit).
5. **Resilience is a first-class requirement.** Agents must buffer locally
and reconnect/resume upload when connectivity drops — the system must not
assume the network is always available.
6. **Tiered QA matching to reduce false positives.** Keyword-only matching
is insufficient (e.g. "WhatsApp" alone vs. "WhatsApp number de dein" vs.
"WhatsApp use nahi karna" should not carry equal severity).

## High-level component map

```
qa.homequranlearning.com (dashboard, Next.js/React/TypeScript)
        |
api.qa.homequranlearning.com (ASP.NET Core API)
        |
   +----+----+----------------+
   |         |                |
PostgreSQL  Redis        Background Workers (STT / QA rules)
                                |
media.qa.homequranlearning.com (WebRTC SFU)
        |
   Windows Agents (C# / .NET, on teacher laptops)
```

## Technology stack

|Layer|Technology|
|-|-|
|Agent|C#, .NET 10, Windows.Graphics.Capture, WASAPI, Media Foundation/FFmpeg|
|Backend|ASP.NET Core, .NET 10, EF Core|
|Database|PostgreSQL|
|Cache/events|Redis|
|Frontend|Next.js, React, TypeScript|
|Live media|WebRTC, LiveKit (or equivalent SFU)|
|STT|faster-whisper / Whisper (managed STT to be evaluated later if needed)|
|Infra|Ubuntu VPS, Docker Compose, Caddy/Nginx, S3-compatible object storage|

## Delivery approach

Built as a vertical slice first (one laptop → agent → local capture → local
recording → backend → database → dashboard playback → STT → QA alert), then
scaled from 1 → 3 → 10 → 20+ devices, moving the same architecture to the
cloud once the slice is proven locally.

See `docs/decisions/ADR-001-system-architecture.md` for the formal decision
record behind these choices.



