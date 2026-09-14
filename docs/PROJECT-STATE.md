# HomeQuranLearning.QA â€” Project State

## Status

The project now uses one supported QA architecture: local Agent-side Vosk
restricted-word detection with local evidence capture.

Legacy server-side QA/STT experiments have been removed from active source.

## Verified Build/Test State

The cleanup verification completed with:

- full .NET solution build: PASS
- Unit tests: 148 passed, 0 failed
- Integration tests: 4 passed, 0 failed
- Agent tests: 173 passed, 0 failed
- Next.js production dashboard build: PASS
- active legacy QA code references: ZERO
- `git diff --check`: PASS

## Live Monitoring

The established live path remains:

Windows Agent
â†’ screen/audio capture
â†’ FFmpeg
â†’ LiveKit Ingress
â†’ LiveKit room
â†’ dashboard

The live architecture was intentionally not changed during QA cleanup.

A previous owner-machine live buffering issue was traced to an obsolete local
QA worker service competing for resources. Removing that legacy service
restored smooth live viewing.

## Audio

Audio is the highest-priority subsystem.

Shared classroom audio is owned by the Agent and reused through
`ClassroomAudioRuntime` / `ClassroomAudioHub`.

QA must consume shared audio rather than opening another microphone pipeline.

## Recording

The supported recording architecture remains intact.

QA no longer polls recordings for speech-to-text processing and recordings no
longer contain QA-processing state.

## Final QA Flow

1. eligible live class session exists
2. Agent shared classroom audio is available
3. `QaLocalVoskWorker` loads active restricted rules
4. Vosk performs local phrase detection
5. local rolling evidence keeps approximately:
   - 10 seconds before detection
   - 20 seconds after detection
6. Agent uploads WAV evidence
7. API validates device/session/rule/phrase
8. API creates `QaAlert`
9. dashboard exposes evidence and review workflow

## Current QA Alert Workflow

Supported actions:

- Review
- Ignore
- Reopen

Manager visibility is constrained to assigned teachers.

Evidence cleanup is handled through QA alert evidence retention.

## Permanently Removed Components

The active product no longer contains:

- Python server QA worker
- continuous QA audio chunk pipeline
- server Whisper/faster-whisper QA
- QA Candidates
- transcript segment persistence
- semantic/off-topic classifier
- recording QA polling
- direct chunk-backed QA alert service
- retired local STT spike
- worker Docker image/service

Historical migrations remain because EF migration history is immutable.

## Schema Cleanup

Forward migration:

`RemoveLegacyQaExperiments`

Its `Up()` removes:

- `qa_audio_chunks`
- `qa_candidates`
- retired transcript persistence table
- retired chunk-origin alert index
- retired recording QA-processing column
- retired chunk-origin alert column

The migration does not modify the supported live-monitoring, recording,
session, schedule, teacher, student, or course schema.

## Deployment State

Do not apply the new cleanup migration to production until the cleanup commit
has been pushed and deployment is intentionally started.

The legacy VPS QA worker must remain disabled/removed.

## Next Engineering Task

Inspect and simplify the active Windows audio endpoint pipeline so that an
active headset/handfree communication microphone and output are selected
automatically in preference to built-in laptop devices.

Goals:

- reduce buffering
- eliminate teacher echo
- preserve live-feed stability
- keep one shared capture architecture
- avoid extra waiting/buffering stages between audio consumers