# Architecture Overview

HomeQuranLearning.QA is an internal academy monitoring platform consisting of:

- Windows classroom Agent
- ASP.NET Core API
- PostgreSQL
- Redis
- MinIO
- LiveKit + Ingress
- Next.js dashboard

## Main Runtime Flows

### Live

Windows Agent
→ screen/audio capture
→ FFmpeg
→ LiveKit Ingress
→ dashboard

### Recording

Windows Agent
→ encoded recording
→ upload/storage
→ API metadata
→ dashboard playback

### QA

shared classroom audio
→ local Vosk restricted-word detection
→ local evidence capture
→ API QA alert
→ dashboard review/evidence playback

## Design Principle

Audio capture is shared.

QA processing must not introduce a second competing microphone capture or a
server-side continuous speech-recognition pipeline.

## Retired Architecture

The previous experimental Python worker, QA chunks, transcript persistence,
candidate review layer, semantic/off-topic classification, and recording QA
polling are intentionally removed.

They are not fallback paths.