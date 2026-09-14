# Current Architecture

## Windows Agent

The Agent is responsible for:

- device heartbeat
- session awareness
- screen capture
- classroom audio capture
- live publishing
- recording
- local restricted-word QA detection
- local QA evidence buffering/upload

## Shared Audio

`ClassroomAudioRuntime` and `ClassroomAudioHub` are the shared audio
foundation.

Live monitoring, recording, and QA must not independently compete for the
same physical microphone endpoint.

## Live Monitoring

Supported path:

Agent capture
→ FFmpeg
→ LiveKit Ingress
→ LiveKit
→ dashboard

Screen video uses the proven `ddagrab` path.

## QA

Supported path:

ClassroomAudioHub
→ QaLocalVoskWorker
→ active restricted rules
→ local Vosk phrase detection
→ local pre/post evidence WAV
→ QA alert API
→ QaAlert
→ dashboard

No server-side continuous STT worker is part of the supported design.

## QA Evidence

Evidence is captured locally around a restricted-word match.

Target evidence window:

- about 10 seconds before detection
- about 20 seconds after detection

Evidence is uploaded as WAV and associated directly with the resulting alert.

## Dashboard

Current relevant surfaces include:

- Live
- Recordings
- QA Alerts
- QA Rules
- Sessions
- Schedules
- Teachers
- Students
- Courses
- Devices
- Users
- Attendance reports

There is no QA Candidates dashboard.

## Data Model

Current QA product entities include:

- QaRule
- QaAlert

Retired experimental entities are absent from the current model.

Historical migration files may still reference retired entities.

## Retention

QA alert evidence cleanup is based on the current alert evidence retention
policy.

The retired chunk-retention worker path is not supported.