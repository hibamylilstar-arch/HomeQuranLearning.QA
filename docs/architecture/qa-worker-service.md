# QA Worker Service — Current Implementation

## Purpose

The QA worker processes uploaded recordings using local speech-to-text and QA rules.

Current STT engine: `faster-whisper 1.2.1`

## Production wiring

Current production worker source:

`spikes/SttSpike/qa_worker.py`

Docker wiring:

`infrastructure/docker/Dockerfile.worker`

It copies `spikes/SttSpike/requirements.txt` and `spikes/SttSpike/qa_worker.py` and starts `python -u qa_worker.py`.

Do not treat this worker as unused experimental code unless deployment wiring is deliberately migrated and fully proved.

## Worker API

```text
GET  /api/worker/recordings/pending
POST /api/worker/recordings/{recordingId}/mark-qa-processed
GET  /api/worker/qa-rules
POST /api/worker/qa-alerts
POST /api/worker/recordings/{recordingId}/transcript-segments
```

Worker authentication uses `X-Api-Key`.

## Phase 7A-1 behavior

- fetch pending recording
- download MP4
- transcribe with reused faster-whisper model
- normalize/index transcript segments
- evaluate active QA rules
- map match to speech timing
- create alert with exact `QaRuleId`
- mark processed only after QA path succeeds

Failure leaves the recording pending.

Alert time is:

```text
Recording.StartedAtUtc + matched transcript segment offset
```

Cross-segment phrase matching is supported.

Duplicate suppression uses RecordingId + QaRuleId + MatchedPhrase + TimestampUtc.

## Phase 7A-1 proof

Commit: `415bbec`

Self-test markers:

```text
QA_WORKER_TRANSCRIPT_INDEX_OK
QA_WORKER_CROSS_SEGMENT_MATCH_OK
QA_WORKER_RULE_LINK_OK
QA_WORKER_TIMESTAMP_ALIGNMENT_OK
QA_WORKER_SELF_TEST_OK
```

- full solution build: GREEN
- unit tests: 67/67
- integration tests: 2/2
- production API/database proof: GREEN
- `StartedAtUtc` verified
- exact `QaRuleId` verified
- recording-relative timestamp verified
- duplicate retry suppression verified
- successful `QaProcessedAtUtc` verified
- processed recording removed from pending queue
- temporary proof data cleaned

## Phase 7A-2 — durable transcript segments

- Transcript segments are persisted with deterministic `SegmentIndex` and recording-relative start/end seconds.
- Whisper language and probability metadata are retained when present.
- A unique `(RecordingId, SegmentIndex)` constraint plus service-level conflict checks make retries idempotent and reject divergent replays.
- Empty transcript batches are valid for no-speech recordings.
- The worker persists segments before alert evaluation and only marks QA processed after all downstream work succeeds.

The worker remains restart/retry safe and does not write `QaProcessedAtUtc` after partial processing.

Validation completed on 2026-08-28:

- worker self-test: 6/6 markers GREEN
- full unit tests: 81/81 GREEN
- integration tests: 3/3 GREEN
- full solution build: 0 warnings, 0 errors
- local API persistence/retry proof: first request persisted 2, identical retry returned existing 2
- isolated proof rows cleaned; baseline database counts restored
