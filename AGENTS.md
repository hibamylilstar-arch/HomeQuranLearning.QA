# HomeQuranLearning.QA â€” Current Engineering State

This file describes the current supported production architecture.
Historical QA experiments are intentionally not part of the active design.

## Repository

- Branch: `codex/qa-direct-audio`
- Backend: ASP.NET Core / .NET 10
- Dashboard: Next.js + TypeScript
- Database: PostgreSQL
- Cache: Redis
- Object storage: MinIO
- Live transport: LiveKit + Ingress
- Windows Agent: .NET Windows service

## Priority Order

1. Audio reliability
2. Live monitoring
3. QA detection
4. Recording

Changes must not regress a higher-priority subsystem while modifying a
lower-priority subsystem.

## Current Audio Architecture

The Windows Agent owns classroom audio capture.

Shared audio is exposed through:

- `ClassroomAudioRuntime`
- `ClassroomAudioHub`

Consumers reuse this shared audio path rather than opening independent
microphone capture pipelines.

Live monitoring and recording must remain independent enough that QA work
cannot stall the live feed.

## Current Live Monitoring Architecture

The supported live path uses:

- Windows screen capture
- FFmpeg `ddagrab`
- NAudio / Windows audio capture
- local UDP audio transport into FFmpeg
- LiveKit Ingress
- dashboard LiveKit playback

The known-good live path must not be replaced by experimental QA/STT
processing.

## Current Recording Architecture

Recording uses the established direct encoded recording path.

Do not reintroduce large raw BGRA intermediate recording files.

Recording playback and historical recordings remain independent from QA
detection.

## Current QA Architecture

The only supported restricted-word QA detection path is local Agent-side
Vosk processing through `QaLocalVoskWorker`.

`QaLocalVoskWorker`:

- runs only when enabled
- consumes shared classroom audio
- does not open a second microphone capture
- processes only eligible class sessions
- downloads active restricted QA rules from the API
- performs local phrase detection
- maintains local rolling evidence
- captures approximately 10 seconds before and 20 seconds after a match
- uploads canonical WAV evidence
- creates the final QA alert through the local restricted-alert API path

There is no supported continuous server-side speech-to-text QA pipeline.

## Removed QA Architecture

The following are permanently retired and must not be reintroduced:

- server Python QA worker
- continuous QA audio chunk upload
- server Whisper/faster-whisper QA processing
- transcript segment persistence
- QA Candidates
- semantic/off-topic QA classification
- recording polling for QA processing
- legacy recording QA-processing marker
- direct chunk-backed QA alerts
- legacy chunk-origin alert field
- retired local STT spike experiments

Historical EF migration files may still contain these names because migration
history must remain intact.

## QA Alerts

Current QA alerts support:

- restricted High-severity rules
- direct local WAV evidence
- teacher/student/course/session/device context
- dashboard evidence playback
- Review
- Ignore
- Reopen
- optimistic review versioning
- manager visibility restricted to assigned teachers
- evidence retention cleanup

QA evidence retention is approximately 7 days unless intentionally changed.

## Database Rule

Do not modify or delete historical EF migrations that have already formed part
of schema history.

Obsolete current schema is removed only through forward migrations.

The cleanup migration is:

`RemoveLegacyQaExperiments`

It removes only the retired QA experiment schema.

## Deployment Rule

Production deployment must not proceed with uncommitted experimental code.

Before production deployment:

1. solution build must pass
2. unit tests must pass
3. integration tests must pass
4. Agent tests must pass
5. dashboard production build must pass
6. active legacy QA reference check must be zero
7. generated migration must be reviewed before applying it

## Development Rule

Do not restore old experimental QA paths as fallbacks.

If a current feature fails, repair the supported architecture directly instead
of reviving retired worker/chunk/transcript/candidate pipelines.

## Current Next Technical Focus

After the QA cleanup is committed, the next audio work is:

- headset/handfree endpoint auto-selection
- prioritize active communication headset microphone/output
- avoid built-in microphone/speaker when a valid active headset is present
- remove teacher voice echo
- reduce audio buffering/delay
- preserve the proven live-monitoring path while making these changes