# Teacher Audio Provenance and Context-Aware Multilingual QA

> **SUPERSEDED BY CLASSROOM MONITORING PRODUCT CONTRACT — 2026-09-03**
>
> This document is retained as historical implementation evidence.
> Its verified-USB teacher-only audio provenance requirement is no longer the
> authoritative product model.
>
> Current authority:
> `docs/architecture/classroom-monitoring-product-contract.md`
>
> QA now consumes the approved classroom conversation source: the teacher's
> effective communication microphone plus the teacher's effective communication
> playback. Speaker attribution is not a core QA dependency.

## Status

7A-5A is released at `a67ff8a` under the approved high-risk scope and 7A-5B
candidate persistence is closed. 7A-5C is released at `411bbac` with a deterministic,
versioned lexical baseline and reproducible synthetic evaluation. It remains
fail-closed: Arabic-recitation windows and isolated ambiguous tokens produce
no candidate, and only candidate review can create a final alert.

## Problem proven from current source

The current recording path uses `WasapiLoopbackCapture`, which captures the
default Windows render endpoint. That is system/student playback audio, not a
separately attributable teacher microphone signal. The resulting transcript
therefore cannot safely be treated as teacher speech.

The current QA worker also performs normalized literal phrase matching with
`transcript.find(phrase)`. It has no speaker provenance, local language
classification, recitation exclusion, intent classification or human
confirmation stage. Creating a final teacher alert directly from this signal
can misattribute student speech and produce context-free false alerts.

## Owner policy

- A teacher must not communicate with a parent during a class.
- Communication with the student is limited to Quran/Qaida teaching, lesson
  correction, necessary class control and necessary technical continuity.
- Arabic Quran/Qaida recitation is excluded from QA-rule evaluation. It remains
  in the original recording and timeline; it is not deleted or hidden.
- Ambiguous words such as English `fee` and Arabic `fi` are judged using the
  surrounding language, transcript and intent rather than an isolated token.
- Automated analysis creates a review candidate, not a final allegation.
- A reviewer receives ten seconds before and ten seconds after the suspicious
  speech and can open the complete recording at the same timestamp.
- Only a human-confirmed candidate becomes a QA alert/evidence finding.

## Selected recording architecture

Use one MP4 with two audio tracks:

1. A primary mixed track containing system audio and teacher microphone for
   ordinary dashboard playback.
2. A discrete teacher-microphone track used exclusively as the QA speech
   source.

The existing screen capture, segment lifecycle, one-file upload contract,
pending-manifest recovery, storage key and normal playback remain intact. The
worker must select the discrete teacher track explicitly; it must never fall
back to the mixed/system track and label it teacher speech.

The teacher microphone source is the single active capture endpoint that is
verified as genuine USB through Windows Core Audio/PnP ancestry. There is no
Windows default or default-communications fallback because either one can
select the laptop internal microphone. Friendly-name text alone is not accepted
as USB proof.

When exactly one verified USB microphone is present, the Agent automatically
selects it and records its runtime endpoint provenance. If none is present, or
multiple verified USB microphones make selection ambiguous, the Agent records
silence on the teacher input, marks the interval `TeacherMicMissing`, continues
the mixed/system recording safely, and retries discovery. A replacement genuine
USB headset may be selected automatically when it becomes the single valid
endpoint; no per-headset installer approval is required.
### Alternatives rejected

- Replacing the existing audio track with microphone-only audio would remove
  student/class context from normal review and damage proven playback.
- Treating the current loopback transcript as teacher speech is incorrect.
- A separate audio sidecar provides isolation but adds a second upload,
  idempotency, recovery, authorization and retention lifecycle for every
  recording. The two-track MP4 achieves provenance while retaining the proven
  one-object lifecycle.
- Three audio tracks (mixed, teacher and system) add storage without a current
  product need. The original system context already exists in the mixed track.

## Fail-closed QA coverage

QA processing is eligible only when all of these facts are true:

- recording metadata declares the expected audio layout/version;
- the discrete teacher track is present and decodable;
- the microphone endpoint was available for the evaluated interval;
- the QA worker explicitly extracted the declared teacher track;
- transcription and all candidate persistence completed successfully.

Missing microphone, track loss, undecodable audio or unknown layout produces a
durable `QaCoverageUnavailable`/coverage-gap record and leaves the recording
unprocessed for retry or review. Silence, missing audio and system-only audio
must never be interpreted as proof that the teacher complied.

The Agent must publish health including selected microphone, audio layout,
capture state, last teacher-audio packet and coverage gaps. No raw credentials
or secret device data belong in this telemetry.

## Recording metadata additions

The recording submission and pending manifest gain versioned, retry-safe
metadata:

- `AudioLayoutVersion`
- `TeacherAudioTrackIndex`
- `TeacherAudioSourceKind`
- `TeacherAudioEndpointId` (stable identifier, treated as operational metadata)
- `TeacherAudioEndpointName`
- `TeacherAudioCoverageStartedAtUtc`
- zero or more bounded coverage-gap intervals

The backend validates internally consistent combinations. Historical recordings
without this metadata remain playable and retain their existing evidence, but
are marked `TeacherAudioProvenanceUnavailable` and are not reinterpreted as
teacher-speech evidence.

## Candidate data model

Introduce a `QaCandidate`; do not overload the current `QaAlert` status.
Required fields include:

- recording, rule/policy version, session and historical teacher identity;
- source track and audio-layout version;
- trigger start/end offsets relative to the recording;
- context start/end offsets, clamped to the recording duration;
- transcript and local language-family decision;
- candidate intent/category and confidence components;
- deterministic analysis/idempotency key;
- `Pending`, `Confirmed` or `Dismissed` status;
- reviewer, review timestamp and structured review reason.

Candidate uniqueness is based on recording, policy/model version, source track
and bounded trigger interval. Retry must return the existing identical result
and reject a divergent replay.

A confirmed candidate creates or links one idempotent `QaAlert`. Dismissal never
creates an alert. Review actions are authorized, auditable and race-safe.

## Multilingual analysis pipeline

Process only the teacher microphone track:

```text
validate provenance
-> extract declared teacher track
-> voice activity detection
-> overlapping timestamped speech windows
-> multilingual transcription
-> local language-family / recitation classification
-> allowed-vs-suspicious intent classification
-> candidate persistence
-> mark QA processed only after the complete successful path
```

Per-window language families are:

- `ArabicRecitation`
- `UrduHindiEnglishInstruction`
- `Mixed`
- `Uncertain`

`ArabicRecitation` bypasses alert-rule evaluation but stays visible on the
timeline. Mixed or uncertain windows do not receive an automatic clean result.
Surrounding windows, ASR confidence and lesson vocabulary are evaluated before
creating a candidate. Isolated homophones are insufficient evidence.

Initially allowed intent covers lesson reading, correction, repetition,
pronunciation, pace/volume requests, understanding checks, lesson navigation,
class focus and necessary technical continuity. Candidate categories include
parent interaction, personal/off-lesson conversation, contact sharing,
financial/private arrangements and other policy-relevant conversation.

Detection is high-recall, but a low-confidence result is never a final alert.
Production metrics must separately measure candidate rate, confirmation rate,
dismissal rate, coverage gaps, processing failures and sampled miss rate.

## Evidence context and dashboard

For a trigger interval `[start, end]`:

```text
contextStart = max(0, start - 10 seconds)
contextEnd   = min(recordingDuration, end + 10 seconds)
```

The dashboard candidate queue must:

- clearly label the item `Candidate — human review required`;
- show teacher/session identity, category, confidence, transcript, language and
  coverage state;
- play the bounded context by seeking within the existing full recording;
- offer `Open full recording` at the trigger timestamp;
- support authorized Confirm and Dismiss actions with an audit reason;
- never display a pending candidate as a confirmed teacher violation.

No duplicate evidence clip is stored in this phase. Offsets into the existing
recording provide the context and avoid multiplying object storage. Exportable
clips can be a later separately approved phase.

## Configurable retention and current 200 GB capacity baseline

At the current 700 kbps video ceiling, two 64 kbps AAC audio tracks produce a
conservative 828 kbps total ceiling. At 90 recorded-hours/day this is about
33.53 decimal GB/day:

| Seven-day evidence fraction | Estimated retained storage |
| ---: | ---: |
| 0% | 100.6 GB |
| 10% | 114.0 GB |
| 20% | 127.4 GB |
| 30% | 140.8 GB |
| 50% | 167.7 GB |
| 100% | 234.7 GB |

This excludes database, operating-system, container, temporary, multipart and
free-space headroom. These values are a planning baseline for the current VPS,
not hard-coded product limits. Therefore:

- normal recordings currently use the three-day default;
- pending candidates do not automatically turn into seven-day QA alerts;
- candidate review has a target SLA of 24 hours and must complete before normal
  deletion eligibility;
- confirmed candidates currently use the seven-day default;
- a pending candidate may temporarily hold its source only until the earlier of
  review completion or a bounded three-day deadline; expiry is visible and
  audited, never silently treated as a clean result;
- the Owner Control Plane configures normal, pending-candidate and confirmed-QA
  retention and storage warning/critical thresholds through backend-authorized,
  audited policy versions;
- the Owner sees measured daily volume, projected retained GB and safety
  headroom before applying a change;
- increasing storage capacity permits retention to increase without a code
  release;
- a shorter policy uses deferred impact evaluation and never silently deletes
  already-stored media immediately;
- when a configured critical threshold is reached, nonessential proof-data
  admission is stopped while metadata and coverage records are preserved;
- deployment is blocked unless measured candidate/confirmation fractions fit
  the VPS headroom budget.

Retention-policy implementation remains independently high risk and must not be
silently bundled into audio capture work.

## Implementation slices and gates

### 7A-5A — audio provenance (closed at `a67ff8a`)

- Add microphone endpoint selection and capture with hot-plug recovery.
- Produce primary mixed audio plus discrete teacher track in the same MP4.
- Version and persist provenance/coverage metadata through pending recovery,
  submission, backend and database.
- Make the worker reject unproven teacher audio.
- Preserve one-object upload/playback and current historical behavior.

The implementation uses a modern NAudio microphone recorder with bounded
endpoint recovery, a two-audio-track MP4, a post-stop timeline finalizer that
keeps both audio streams aligned to the video, versioned backend persistence,
and fail-closed PyAV extraction of the declared teacher track. Existing
layout-0 recordings remain legacy and are not reinterpreted as teacher speech.

Required proof: unit and integration tests, deterministic FFmpeg argument tests,
restart/idempotency tests, no-headset fail-closed proof, headset physical proof,
`ffprobe` track/layout proof, system/student-only negative-attribution proof,
dashboard playback proof, upload/recovery proof and exact cleanup.

### 7A-5B — candidate foundation

- Add candidate persistence, deterministic idempotency, authorized review APIs,
  audit fields and Confirm/Dismiss lifecycle.
- Create an alert only from Confirm.
- Add candidate queue, context seek and full-recording seek.

Required proof: authorization/resource-scope tests, retry/race tests, candidate
does-not-equal-alert tests, browser review proof and retention-isolation tests.

### 7A-5C — multilingual classifier and evaluation

- Add windowed VAD/transcription, language/recitation and intent decisions with
  versioned outputs. The current baseline consumes timestamped Whisper speech
  segments, applies Unicode/script and surrounding-vocabulary decisions, and
  posts only to `/api/worker/qa-candidates`.
- Establish an approved synthetic/test corpus for Arabic Quran/Qaida,
  Urdu/Hindi, English and mixed speech, including `fee`/`fi` ambiguity.
- Tune thresholds from measured precision/recall; never claim zero false
  positives or zero missed evidence.
- Add random audit sampling of non-candidate windows to estimate missed events.

The checked-in baseline corpus and evaluator are under
`spikes/SttSpike/qa_classifier_eval.py`. Its metrics are synthetic-policy
coverage only and are not a production accuracy claim.

Required proof: reproducible evaluation report, Arabic-recitation exclusion,
parent/off-lesson positive cases, allowed-lesson negative cases, mixed/uncertain
handling, context offsets, restart/retry behavior and full QA processing order.

Each slice receives its own release gate. VPS deployment remains deferred.

## Protected mechanisms

The work must not change Teams attendance semantics, LiveKit publishing,
screen-capture timing, device identity, session linking, historical evidence,
recording upload keys, playback authorization or the production worker path
without separate evidence and approval. Existing recordings remain readable.
