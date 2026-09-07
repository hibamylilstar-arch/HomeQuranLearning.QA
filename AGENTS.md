<!-- HQL_CURRENT_HANDOFF_BEGIN -->

<!-- HQL_QA_PHASE2_RUNTIME_OFF_20260908_BEGIN -->

## CURRENT QA RUNTIME OVERRIDE — PHASE 2 OFF — 2026-09-08

This block supersedes older QA status below that says Phase 2 OFF is not yet
implemented or deployed.

Latest functional source and VPS runtime checkpoint:

`64b511a8157125724bf63b673b9e1eb72bef314e`

Commit:

`fix: disable QA phase 2 worker paths`

Runtime-certified state:

- Restricted Rule QA = ON
- Off-topic Conversation QA = OFF
- QA Candidate creation = OFF
- Phase-2 source code = preserved
- Phase-2 database schema/migrations = preserved
- historical Candidates = preserved

Deployment proof:

- only `academy-qa-worker` rebuilt/recreated
- API unchanged
- Dashboard unchanged
- Agent unchanged
- Live unchanged
- Recording unchanged
- database unchanged
- Caddy preserved
- protected container IDs unchanged

Runtime worker startup proved:

- `Restricted Rule QA: ENABLED`
- `Off-topic QA: DISABLED`
- `QA Candidates: DISABLED`

No further Phase-2 feature development is in current scope.

Current engineering priority:

`QA Phase 1 Restricted Word Detection -> Restricted Alert -> direct 10s-before / 20s-after audio evidence`

Current Phase-1 blocker remains restricted-word recognition accuracy on real
canonical classroom audio.

Do not reopen Off-topic semantic QA or Candidate workflow unless the Owner
explicitly starts Phase 2 after Phase 1 is complete.

<!-- HQL_QA_PHASE2_RUNTIME_OFF_20260908_END -->

<!-- HQL_QA_PHASE1_ACTIVE_20260908_BEGIN -->

# CURRENT ACTIVE STATE — QA PHASE 1 RESTRICTED-WORD ALERTS — 2026-09-08

> **NEW AI / DEVELOPER: READ THIS BLOCK FIRST.**
>
> This block supersedes older QA next-step instructions below it.
>
> Do not restart old QA architecture work.
> Do not reopen Off-topic Conversation work.
> Do not assume QA Candidates are the current priority.
> Do not treat old pre-session garbage Candidates as proof that the latest
> session-scoped worker is still broken.
>
> Current Owner decision:
>
> **Finish QA Phase 1 first: Restricted Words -> verified Alert -> direct audio evidence.**
>
> **Phase 2 Off-topic / semantic QA / Candidates is PENDING and must be feature-level OFF.**

## 1. Repository / functional runtime checkpoint

Repository:

`C:\Dev\HomeQuranLearning.QA`

GitHub:

`hibamylilstar-arch/HomeQuranLearning.QA`

Branch:

`codex/local-development-mode`

Latest functional source and deployed application checkpoint before this
documentation-only update:

`746593ad537cd033b280de452c3de1c8702ac0a2`

Commit:

`fix: scope QA analysis to scheduled sessions`

Parent functional checkpoint:

`15aa97ba2f52f3fcc3cd6d8b164776cab4a9ea68`

`15aa97` made server archive recordings canonical-audio ready.

Documentation commits after `746593a` do NOT mean the production application
code changed. Always distinguish docs HEAD from the latest functional/deployed
application commit.

## 2. VPS runtime / protected infrastructure

VPS:

`158.220.90.195`

Application root:

`/opt/homequranlearning`

Production compose files:

- `infrastructure/docker/docker-compose.prod.yml`
- `infrastructure/docker/docker-compose.relay-production.yml`

Production env:

`infrastructure/docker/.env.production`

Protected custom VPS file:

`infrastructure/docker/Caddyfile`

Required SHA256:

`280dfe2cf855e4be0029c36fe992ae4505dd393f50b25717aa25761447338ac1`

Never overwrite, reset or replace the custom Caddyfile.

Deployment of `746593a` was surgical.

Changed runtime components:

- academy-api
- academy-qa-worker

Explicitly untouched:

- Dashboard
- Owner Agent
- LiveKit
- LiveKit Ingress
- ingress-manager
- MediaMTX relay
- archive recorder
- archive registrar
- Caddy
- PostgreSQL schema

No migration was required by `746593a`.

Production runtime proof after deployment showed no-session recordings were
skipped without transcription.

## 3. Owner device / Agent baseline

Owner durable DeviceId:

`82f9b22d-2d5b-46b2-b372-ef864219e383`

Physical Windows device:

`DESKTOP-PUFUU3U`

Friendly Laptop Name:

`Abdul Wahid`

Installed Agent version:

`1.0.0-c3b10ffd04a3-namegate1`

Installed Agent source commit:

`c3b10ffd04a39addc75c9358dc3eda906a42f667`

Important:

- Owner Agent remains VPS-connected.
- Live streaming remains enabled.
- Owner local Agent recording remains `Recording.Enabled=False`.
- Do NOT enable local Agent recording for current QA work.
- Do NOT restart/update Owner Agent unless source evidence proves an Agent
  change is actually required.
- Do NOT update other academy laptops during Owner QA development.

## 4. Canonical classroom audio contract

Physical classroom audio is captured once through the effective communication
routes:

- effective teacher microphone
- effective communication render/playback

These are combined into one canonical classroom mixed audio timeline.

Canonical audio is consumed by:

- Live
- server Recording
- QA/STT

QA canonical identity:

- AudioLayoutVersion = 1
- ClassroomAudioTrackIndex = 0
- title = `Academy Class Mixed Audio`

Do not create a separate teacher-only QA capture chain.

Do not add speaker identification.

USB, Bluetooth, wired and internal devices are valid only when they are the
effective Teams/Zoom communication endpoints.

User has listened to the relevant real recording and reports that the voice is
clear. Do not assume capture/microphone failure without new contradictory
evidence.

## 5. Schedule / Session authority

Existing Schedule -> Session lifecycle is authoritative.

Product model:

**Recording = continuous evidence**

**Session = class boundary and provenance authority**

**QA = session-scoped analysis**

Always-on server recording may begin before a class and continue after it.

QA must determine class scope from:

- DeviceId
- recording timestamps
- eligible Session timestamps

Eligible session statuses:

- Live
- Completed

Current `746593a` behavior:

- no session overlap -> QA skip without STT
- one valid overlap -> analyze exact Session window
- exact SessionId is sent/persisted
- provenance is validated against exact Session/device/time
- friendly LaptopName uses RecordingDisplayName fallback DeviceName
- physical DeviceName remains technical provenance
- overlapping QA windows are rejected as ambiguous

Do NOT return to:

`Recording.SessionId != null`

as the QA class-boundary rule.

## 6. Real Owner English QA canary

Exact Session:

`1c06347c-8f29-4364-b56c-49a60631380e`

Runtime result:

- Status = Completed
- Teacher = Abdul Wahid
- Student = Student Test 1
- Course = Qaida
- Laptop = Abdul Wahid
- ScheduledStart = 2026-09-07 20:59:00 UTC
- ScheduledEnd = 2026-09-07 21:10:00 UTC

Exact overlapping recording:

`5fe08e46-c409-4cec-9ee9-f8da635de052`

File:

`server-1788814361.mp4`

Recording:

- Start = 2026-09-07 20:52:41 UTC
- End = 2026-09-07 21:07:42.090042 UTC
- AudioLayoutVersion = 1

QA Session window inside this recording:

`+379.000s -> +901.090s`

Active Restricted Rule:

`Whatsapp`

Severity:

`High`

Active:

`true`

Worker proved:

- exact scheduled Session window selected
- language detected `en`
- session transcript generated
- Rule matches = 0
- `TRANSCRIPT_WHATSAPP_MATCHES=0`
- exact-session Alert count = 0
- exact-session Candidate count = 0
- recording marked QA processed

Therefore:

**The English canary did NOT prove an Alert persistence/backend bug.**

The restricted phrase was not recognized by STT, so the Alert engine never
received a confirmed `Whatsapp` match.

## 7. Old Candidate row is stale evidence

Old dashboard Candidate example:

`server-1788811103.mp4`

Observed old row:

- transcript = `you`
- ASR confidence around 0.18
- Unknown teacher
- OffTopicConversation Candidate

Do NOT use this row as evidence that the current exact-session canary still
creates garbage Candidates.

The decisive new Owner session produced:

`SESSION_CANDIDATE_COUNT=0`

The `746593a` low-confidence/session-scope protections behaved correctly in
that canary.

## 8. STT benchmark evidence already completed

Do not repeat these benchmarks without a reason.

### Whole exact Session audio

Direct audio:

- mean volume approximately `-36.5 dB`
- max volume approximately `-5.5 dB`

Normalized audio:

- mean volume approximately `-29.6 dB`
- max volume approximately `-3.0 dB`

Current production default model:

`base`

Whole-session `base` result was poor/hallucinated.

Examples included:

- `You`
- repeated generic phrases
- repeated `Okay`

Normalized audio materially improved semantic recovery.

It recovered content similar to:

- market
- planning
- continue

But:

`WhatsApp = NOT RECOGNIZED`

### Short 100-second known-speech region

Probe relative Session start:

`145 seconds`

Probe duration:

`100 seconds`

Normalized short audio:

- mean approximately `-25.5 dB`
- max approximately `-3.0 dB`

`base`, no VAD:

- partially recovered semantics
- still missed WhatsApp

Default VAD:

- removed almost all useful speech

More-sensitive VAD:

- still performed poorly

Conclusion:

Aggressive current VAD is not appropriate for this sample.

### Stronger model benchmark on same exact audio

`small`:

- poor/hallucinated recognition
- only `continue` among expected controlled vocabulary
- WhatsApp = NO

`medium`:

- worse on this sample
- transcript essentially `Thank you very much.`
- WhatsApp = NO

The Hugging Face warning:

`You are sending unauthenticated requests to the HF Hub`

was NOT a QA/STT-quality failure.

It only means model downloads were unauthenticated and may receive lower rate
limits.

The models loaded successfully.

## 9. Current root-cause conclusion

Current blocker is:

**Restricted-word speech recognition quality / detection strategy**

It is NOT currently proven to be:

- QaAlert persistence
- QA database schema
- Session scoping
- canonical track identity
- Owner provenance
- missing active Whatsapp rule
- HF authentication
- simple lack of RAM
- simple lack of CPU

Do not blindly increase Whisper model size again.

Do not patch Alert storage before restricted-word detection actually produces a
reliable match.

For Phase 1, a finite restricted-vocabulary / keyword-oriented detector may be
more appropriate than requiring perfect free-form transcription of an entire
class.

## 10. Owner product decision — QA PHASE 1 ACTIVE

Owner decision dated 2026-09-08:

Finish Phase 1 first.

Required flow:

`Scheduled Session`
`-> canonical classroom audio`
`-> restricted-word detection`
`-> reliable confirmation`
`-> QA Alert`
`-> direct audio evidence/player`

Only Restricted Rule QA is the current active QA product scope.

### Phase-1 QA Alert should show

- exact restricted word/phrase
- Teacher
- Student
- Course
- friendly Laptop Name
- date/time
- Open / Reviewed status
- direct `Play Audio`
- audio evidence around the trigger
- optional transcript for technical/debug context only

Primary human-review evidence:

**audio**

not free-form transcript.

### Commercial audio evidence target

- 10 seconds before restricted word
- 20 seconds after restricted word
- clamp safely to Session/recording boundaries

## 11. Phase-1 acceptance criteria

Phase 1 is NOT complete until all of these pass on a real scheduled Owner class:

1. restricted word clearly spoken
2. detector reliably recognizes it
3. exactly one Restricted Rule Alert created
4. correct exact SessionId
5. correct Teacher
6. correct Student
7. correct Course
8. friendly Laptop = `Abdul Wahid`
9. direct audio evidence plays from QA Alerts
10. evidence target approximately 10 sec before + 20 sec after
11. no Session -> no QA finding
12. garbage/low-confidence audio -> no Alert
13. Phase-2 Off-topic/Candidate generation produces no new findings while OFF

## 12. QA PHASE 2 — PENDING / OFF

Phase 2 includes:

- Off-topic Conversation semantic classification
- AllowedLesson
- Uncertain
- OffTopic
- Off-topic second-pass classification
- QA Candidate generation
- Candidate review workflow

Owner has explicitly placed Phase 2 on hold.

Do NOT delete or rollback:

- code
- database schema
- migrations
- repositories/services
- historical Candidate rows

Preserve them for future reactivation.

Desired feature state:

- Restricted QA = ON
- Off-topic QA = OFF
- QA Candidates = OFF

### IMPORTANT IMPLEMENTATION STATUS

The product decision is final, but the Phase-2 feature-level OFF patch has
**NOT YET BEEN IMPLEMENTED OR DEPLOYED** at this checkpoint.

Current deployed worker still contains existing Off-topic/Candidate code.

Future AI must NOT falsely report:

`PHASE_2_DISABLED=PASS`

until source tests and runtime prove the actual gate.

## 13. Exact NEXT engineering task

Continue Phase 1 only.

Sequence:

1. Inspect the current `746593a` worker orchestration.
2. Implement a small explicit feature-level gate:
   - Restricted path ON
   - Off-topic path OFF
   - Candidate creation OFF
3. Preserve all Phase-2 source/schema/history.
4. Add targeted tests proving:
   - Restricted processing still executes
   - Off-topic processing does not execute
   - Candidate creation does not execute
   - no-session skip remains working
5. No Agent change for this gate.
6. No Live change.
7. No recording-capture change.
8. No migration unless source inspection proves one is genuinely required.
9. Then continue restricted-word recognition work on the existing Owner real
   recording before requesting unnecessary new real classes.
10. Prefer finite-vocabulary restricted-word/phrase detection strategies over
    whole-class semantic transcription where technically stronger.
11. Once recognition is reliable, prove actual Restricted Rule Alert creation.
12. Then implement/verify direct inline 10-before / 20-after audio playback on
    the QA Alerts page.
13. Close Phase 1 only after the full real Owner acceptance criteria pass.

Do not resume Phase 2 unless Owner explicitly starts it after Phase 1 closure.

## 14. Candidate rollback rule

Do NOT perform a destructive QA Candidate rollback.

Keep:

- Candidate schema
- Candidate migrations
- Candidate code
- Candidate history
- future reactivation capability

Feature-level inactivity is sufficient for Phase 1.

Do not rewrite/delete old Candidate production history merely to make the
dashboard visually clean unless Owner explicitly requests cleanup.

## 15. QA evidence/provenance already built

Commercial QA evidence schema already supports:

- RecordingId
- SessionId
- DeviceId
- TeacherId
- StudentId
- CourseId
- friendly LaptopName
- physical ActualDeviceName
- TeacherName
- StudentName
- CourseName
- DetectionReason
- MatchedPhrase
- Transcript
- TriggerStartSeconds
- TriggerEndSeconds
- EvidenceStartSeconds
- EvidenceEndSeconds
- review metadata

Applied QA migrations:

1. `20260907101637_AddQaCommercialEvidenceProvenance`
2. `20260907104318_AddQaCandidateMatchedPhrase`

Do not regenerate or rewrite them.

## 16. Archive-recorder observation

During the English Owner canary audit, recorder logs contained repeated:

`Independent archive reader disconnected exit=1; retrying`

However:

- the Owner recording was registered
- canonical layout 1 was present
- exact Session overlap was found
- QA processed the Session window

Therefore the disconnect/retry behavior is NOT proven as the reason for the
current restricted-word miss.

Track it separately as a reliability observation.

Do not derail Phase 1 into a broad recording/relay redesign without direct
evidence.

If surgical Owner archive-reader rotation is required:

- identify exactly one Owner reader
- do not print its stream key
- do not restart the whole recorder stack unnecessarily

## 17. Protected project invariants

Never:

- enable Owner local Agent recording for current QA work
- print `.env.production`
- print LiveKit stream keys
- print Agent secrets
- print MinIO credentials
- print API keys
- overwrite/reset Caddy
- use `docker compose down` for routine scoped work
- use `--remove-orphans` casually
- restart LiveKit/Ingress/MediaMTX without affected-boundary evidence
- update other academy Agents before Owner canary proof
- invent a second Session/class-active mechanism
- add speaker-ID as a prerequisite
- backfill layout-0 recordings to layout-1 without media verification
- use destructive Git operations without explicit approval

Always:

- preserve continuous server recording
- use Schedule-driven Session authority
- show friendly LaptopName in user-facing QA evidence
- preserve ActualDeviceName only as technical provenance
- keep Live/Recording independent from STT waiting

## 18. Existing system baseline to preserve

Already-established product/runtime areas include:

- ASP.NET Core .NET 10 backend
- PostgreSQL
- Redis
- MinIO
- Next.js / TypeScript dashboard
- Windows .NET Agent
- FFmpeg
- NAudio / WASAPI
- LiveKit
- LiveKit Ingress
- MediaMTX
- server recording + registrar
- Live Monitoring
- schedules
- automatic Sessions
- attendance
- teachers/students/courses
- RBAC
- Manager visibility
- Activity Log
- Laptop Name
- Usual Teachers
- Agent auto-update
- canonical mixed classroom audio

Accepted Live path:

`Windows H264/AAC RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> Dashboard WebRTC`

Accepted approximate monitoring latency:

- audio 1–2 sec
- video 4–5 sec

Do not reopen accepted Live/viewer architecture absent an observed regression.

## 19. Scale direction

Live and Recording must NOT wait for QA/STT.

Future scale should use:

- independent media pipeline
- queued QA jobs
- bounded Session/chunk processing
- one or more QA workers as capacity requires

Larger CPU/RAM increases throughput but does not automatically fix recognition
accuracy.

Current blocker is Phase-1 detection accuracy, not premature fleet-scale
optimization.

## 20. Owner Control Panel

Correct product name:

`Owner Control Panel`

It remains deferred to the final product phase.

Do not treat Owner Control Panel as a current dependency or roadmap blocker.

## 21. Engineering rules for every future AI

- Inspect real source/runtime before guessing.
- Preserve proven architecture.
- User is product authority.
- Improve implementation quality without inventing product restrictions.
- Do not repeatedly ask questions already answered by source/runtime/user.
- Distinguish harness/script mistakes from product bugs.
- Stop diagnosing once the failing boundary is sufficiently proven.
- Make the smallest production-quality change.
- Run targeted verification appropriate to the change.
- Do not fake green.
- Preserve unrelated work.
- Use exact Git staging.
- Runtime-affecting Agent work must prove on Owner before teacher-laptop rollout.
- Backend/dashboard-only changes do not require an Agent release.
- Owner is the first canary, not a permanent device capability restriction.

Core principle:

`User intent first. Inspect real state. Preserve proven architecture. Make the smallest correct change. Prove it.`

<!-- HQL_QA_PHASE1_ACTIVE_20260908_END -->

<!-- HQL_QA3_PART3C_SOURCE_CLOSED_20260907_BEGIN -->
## QA-3 Part 3C final source regression gate - SOURCE CLOSED - 2026-09-07

Latest source checkpoint:

`f3d5bca827d4bf11a0f74cfc590556fbad2a247b`

Commit:

`test: lock QA commercial evidence behavior`

Parent checkpoint:

`291cd37d7975bc0560a829cbbc39435e2a820c56`

### QA-3 Part 3C source verification

Dedicated regression coverage now locks the QA-3 commercial evidence contract.

Direct Alert coverage verifies:

- Restricted Rule exact MatchedPhrase
- Off-topic Conversation MatchedPhrase = null
- commercial evidence nominally -10 / +20
- recording-boundary clamping
- backend-authoritative observed timestamp
- canonical track 0 / layout 1 enforcement
- stable Alert retry idempotency
- idempotency collision rejection
- backend-resolved provenance
- friendly LaptopName
- physical ActualDeviceName
- Teacher / Student / Course provenance

Candidate coverage verifies:

- legacy classifier Context remains nominal -10 / +10
- commercial Evidence remains nominal -10 / +20
- evidence boundary clamping
- Restricted Candidate exact MatchedPhrase snapshot
- Off-topic Candidate MatchedPhrase = null
- confirmed Restricted Candidate does not use Transcript as MatchedPhrase
- confirmed Off-topic Candidate creates Alert with null MatchedPhrase
- canonical track/layout enforcement

Dashboard coverage verifies:

- Alert commercial evidence projection
- Candidate commercial evidence projection
- LaptopName / ActualDeviceName projection
- Teacher / Student / Course projection
- existing Manager visibility behavior
- Owner-only trial device isolation

### Worker / API final source gate

Verified without additional production-source changes:

- worker Alert endpoint accepts full CreateQaAlertRequest
- old blanket MatchedPhrase requirement remains removed
- worker Candidate endpoint accepts enriched Candidate request
- Restricted direct Alert sends exact phrase
- Off-topic direct Alert sends null phrase
- Restricted Candidate sends exact phrase
- Off-topic Candidate sends null phrase
- shared deterministic analysis idempotency key remains in use
- Restricted processing remains first
- Off-topic processing remains second
- mark_processed remains after persistence work
- classifier verification padding remains unchanged

### Migration readiness

Pending QA migrations remain ordered:

1. `20260907101637_AddQaCommercialEvidenceProvenance`
2. `20260907104318_AddQaCandidateMatchedPhrase`

Source/model audit passed.

The migrations have NOT yet been deliberately applied as part of the QA-3
runtime canary phase.

No `dotnet ef` migration command was run during the final source gate.

### Final regression evidence

QA-3 Part 3C final source gate passed:

- solution build
- 159 Unit tests, 0 failures
- 6 Integration tests, 0 failures
- Python compile
- Off-topic classifier self-test
- QA worker self-test
- worker commercial Alert payload self-test
- worker commercial Candidate payload self-test
- exact three-test-file commit
- remote push verification

### Important status

QA-3 is SOURCE CLOSED but NOT yet runtime closed.

No QA-3 runtime canary has been accepted yet.

Database migration status has not yet been queried/applied in the controlled
runtime environment.

No production-wide deployment has been performed.

### Next exact phase

`QA-3 Runtime Canary Preparation`

Sequence:

1. inspect current local/runtime PostgreSQL container and migration state
2. inspect backend / worker runtime configuration
3. identify the exact Owner-laptop recording/device path for the canary
4. deliberately apply only the required pending migrations
5. start/restart only the required local QA backend/worker components
6. verify backend health before touching the Owner-laptop test
7. run controlled real Restricted Rule canary
8. run controlled real Off-topic Conversation canary
9. verify Dashboard Alert/Candidate metadata
10. verify exact recording timestamp/deep-link
11. verify audible evidence
12. verify duplicate retry/idempotency behavior
13. close QA-3 only after runtime proof

Owner-laptop real QA canary is the next functional milestone.

Production/VPS-wide rollout remains blocked until the controlled canary passes.
<!-- HQL_QA3_PART3C_SOURCE_CLOSED_20260907_END -->

<!-- HQL_QA3_PART3B_CLOSED_20260907_BEGIN -->
## QA-3 Part 3B API + Worker commercial evidence wiring - CLOSED - 2026-09-07

Latest functional source checkpoint:

`df6e426b17b8e7a04e12efe45245fbc281a6d647`

Commit:

`qa: wire worker commercial evidence payloads`

Previous functional source checkpoint:

`4984eb550ced524c66f2d017b0e8dfbbb56cb64e`

### Worker Alert API

`POST /api/worker/qa-alerts` now passes the full
`CreateQaAlertRequest` into the enriched QaAlertService path.

The worker endpoint no longer imposes the old blanket rule:

`MatchedPhrase is required`

Validation now belongs to the enriched backend service contract.

Backend semantics remain:

Restricted Rule:
- DetectionReason = `Restricted Rule`
- QaRuleId required
- exact MatchedPhrase required

Off-topic Conversation:
- DetectionReason = `Off-topic Conversation`
- QaRuleId = null
- MatchedPhrase = null

The worker Alert endpoint maps:
- argument validation failures to HTTP 400
- invalid/conflicting recording/evidence state to HTTP 409

### Worker direct Alert payload

Direct Alert payloads now include:

- RecordingId
- QaRuleId
- nullable MatchedPhrase
- DetectionReason
- Transcript
- PolicyVersion
- AnalysisVersion
- SourceTrackIndex
- AudioLayoutVersion
- TriggerStartSeconds
- TriggerEndSeconds
- TimestampUtc
- AnalysisIdempotencyKey

Restricted Rule direct Alert:
- exact verified restricted phrase is sent as MatchedPhrase
- verified second-pass trigger interval is sent
- second-pass verification transcript is preferred as evidence transcript
- original context transcript is the fallback

Off-topic direct Alert:
- MatchedPhrase is null
- DetectionReason is `Off-topic Conversation`
- verified second-pass conversation interval is sent
- verified transcript is preferred, with primary conversation text fallback

### Stable idempotency

Alert payloads reuse the existing shared
`analysis_idempotency_key(...)` implementation.

The key is deterministically derived from:

- recording
- rule identity when applicable
- policy version
- analysis version
- canonical source track
- trigger start/end interval

No new ad-hoc Alert key format was introduced.

### Worker Candidate payload

Candidate payloads now additionally send:

- DetectionReason
- nullable MatchedPhrase

Restricted Rule Candidate:
- exact detected rule phrase is snapshotted
- DetectionReason = `Restricted Rule`

Off-topic Candidate:
- MatchedPhrase = null
- DetectionReason = `Off-topic Conversation`

`IntentCategory` is still sent temporarily only for backend compatibility.

It is not the long-term user-facing QA reason model.

### Preserved QA orchestration

QA-2A / QA-2B processing order remains unchanged:

1. Restricted Rule detection and second-pass verification
2. confirmed Restricted Rule intervals retained
3. Off-topic conversation analysis
4. overlapping duplicate direct Off-topic alert suppression
5. human-review Candidate when required
6. recording marked QA processed only after persistence work completes

Restricted Rule remains higher priority for overlapping incidents.

### Verification windows unchanged

Part 3B did NOT change classifier/verification audio windows.

Restricted/default verification still uses the existing nominal 10-second
padding.

Off-topic second-pass verification still uses the existing 3-second padding.

These classifier verification windows remain separate from the commercial
persisted evidence contract:

- 10 seconds before
- 20 seconds after
- clamped to recording boundaries

Do not change verification WAV padding merely to imitate persisted commercial
evidence windows.

### Verification

QA-3 Part 3B passed:

- exact two-file source scope
- git diff check
- Python compile
- Off-topic classifier self-test
- QA worker self-test
- worker commercial Alert payload self-tests
- worker commercial Candidate payload self-tests
- Off-topic null MatchedPhrase self-test
- Restricted two-pass Alert orchestration test
- unverified Restricted Candidate orchestration test
- full solution build
- full Unit tests
- full Integration tests
- exact commit
- remote push verification

### Explicitly unchanged

Part 3B did NOT:

- apply a database migration
- deploy to VPS
- deploy the QA worker
- update the Owner Agent
- modify Agent audio capture
- modify canonical classroom audio
- modify Live
- modify Recording generation
- modify Attendance
- modify classifier vocabulary/policy
- modify verification window padding

### Next exact phase

`QA-3 Part 3C - final regression, API behavioral coverage, migration readiness and real-canary gate`

Part 3C must finish source-level closure before real runtime testing.

Required final coverage includes:

- direct Restricted Alert exact MatchedPhrase
- direct Off-topic Alert nullable MatchedPhrase
- Candidate Restricted exact MatchedPhrase
- Candidate Off-topic nullable MatchedPhrase
- confirmed Candidate -> Alert semantics
- Alert and Candidate commercial evidence -10/+20 with boundary clamping
- legacy Candidate context remains -10/+10
- authoritative backend provenance snapshots
- LaptopName / ActualDeviceName semantics
- Student / Course provenance
- stable direct Alert idempotency
- duplicate retry behavior
- canonical track 0 / layout 1 rejection regression
- manager visibility / RBAC regression
- worker processing-order regression
- API worker endpoint behavioral regression
- full Unit / Integration / Python regression

After source-level Part 3C passes:

1. deliberately apply the pending QA migrations to the controlled local/runtime
   database required for the canary
2. deploy/restart only the required QA backend/worker components
3. run Owner-laptop real QA canary
4. verify real Alert/Candidate evidence in Dashboard
5. verify recording deep-link and audible evidence
6. close QA-3 only after runtime canary proof

No production-wide rollout should occur before the controlled Owner-laptop
canary passes.

QA-3 Part 3B is SOURCE CLOSED.
<!-- HQL_QA3_PART3B_CLOSED_20260907_END -->

<!-- HQL_QA3_PART3A_CLOSED_20260907_BEGIN -->
## QA-3 Part 3A backend commercial evidence wiring - CLOSED - 2026-09-07

Latest functional source checkpoint:

`4984eb550ced524c66f2d017b0e8dfbbb56cb64e`

Commit:

`qa: wire commercial evidence backend`

Parent handoff checkpoint:

`d16f9156ec74082e277400ceeab9b76a68aa1532`

### Part 3A implemented

Backend QA evidence/provenance wiring is now source-complete for this phase.

Recording repository now has a dedicated QA provenance load path rather than
globally changing ordinary Recording GetById semantics.

QA provenance loading includes the authoritative graph needed by QA:

- Recording -> Device
- Recording -> Teacher
- Recording -> Session
- Session -> Teacher
- Session -> Student
- Session -> Course
- Session -> Device

QaAlertRepository and QaCandidateRepository now expose the loaded QA graph
needed for enriched projections and legacy fallbacks.

QaAlertRepository also supports lookup by AnalysisIdempotencyKey.

### Candidate immutable matched-phrase snapshot

QA-3 Part 3A found an important schema gap:

a Restricted Rule Candidate could later be human-confirmed, but the Candidate
did not retain the exact restricted phrase detected at analysis time.

A new nullable Candidate MatchedPhrase snapshot was therefore added.

Semantics:

- Restricted Rule Candidate -> exact matched restricted phrase
- Off-topic Conversation Candidate -> null

This prevents Candidate confirmation from reconstructing historical evidence
from a later-edited QaRule phrase.

Follow-up additive migration:

`20260907104318_AddQaCandidateMatchedPhrase`

Migration Up() audit:

- exactly one AddColumn operation
- target: qa_candidates.MatchedPhrase
- nullable
- no destructive Up() operation

The migration has NOT been applied to a database.

The already-closed QA-3 Part 2 migration was NOT rewritten.

### QaCandidateService

New Candidate creation now:

- requires the canonical classroom audio source:
  - SourceTrackIndex = 0
  - AudioLayoutVersion = 1
- validates the clean user-facing DetectionReason
- accepts only:
  - `Restricted Rule`
  - `Off-topic Conversation`
- requires QaRuleId + MatchedPhrase for Restricted Rule
- requires QaRuleId null + MatchedPhrase null for Off-topic Conversation
- persists detection-time MatchedPhrase snapshot
- resolves provenance from backend Recording / Device / Session data
- preserves legacy classifier ContextStartSeconds / ContextEndSeconds as
  nominal -10 / +10
- persists commercial EvidenceStartSeconds / EvidenceEndSeconds as
  nominal -10 / +20, clamped to recording boundaries
- exposes ObservedAtUtc / ObservedOffsetSeconds through DTO projection
- preserves Candidate analysis idempotency behavior

Candidate confirmation no longer abuses the full Transcript as MatchedPhrase.

Confirmed Candidate -> Alert now preserves:

- actual Candidate Transcript as transcript evidence
- Candidate MatchedPhrase snapshot for Restricted Rule
- null MatchedPhrase for Off-topic Conversation
- policy / analysis versions
- canonical source track/layout
- trigger offsets
- evidence offsets
- detection-time provenance snapshots

### QaAlertService

A new enriched CreateQaAlertRequest service path now supports:

- DetectionReason
- nullable MatchedPhrase
- Transcript
- PolicyVersion / AnalysisVersion
- SourceTrackIndex / AudioLayoutVersion
- TriggerStartSeconds / TriggerEndSeconds
- EvidenceStartSeconds / EvidenceEndSeconds
- AnalysisIdempotencyKey
- backend-resolved provenance snapshots

Commercial Alert evidence is nominally:

- 10 seconds before trigger
- 20 seconds after trigger
- clamped to recording boundaries

Off-topic direct Alert semantics are now supported by the backend:

- DetectionReason = `Off-topic Conversation`
- QaRuleId = null
- MatchedPhrase = null

Restricted Rule semantics:

- DetectionReason = `Restricted Rule`
- QaRuleId required
- exact MatchedPhrase required

Blank optional Alert AnalysisIdempotencyKey is normalized to null rather than
persisting an empty unique key.

The old/manual CreateAlertAsync overload is retained temporarily for API
compatibility until Part 3B rewires Program.cs.

### QA provenance naming

QA snapshots use existing dashboard/device naming semantics:

LaptopName:

`RecordingDisplayName` when nonblank, otherwise `DeviceName`

ActualDeviceName:

the physical Windows `DeviceName`

Worker payloads are not trusted to supply provenance names or entity IDs.

### DashboardQueryService

QA Alert and Candidate projections now expose enriched commercial evidence
metadata including:

- detection reason
- matched phrase snapshot
- transcript
- policy / analysis versions
- canonical track/layout
- trigger offsets
- evidence offsets
- observed time / recording offset
- device/session/teacher/student/course IDs
- laptop / physical device name
- teacher/student/course names
- review metadata

Legacy rows can still use the loaded Recording / Session graph as a
presentation fallback.

Existing QA visibility/RBAC filtering was preserved.

### Important substitution note

Do not invent new teacher/device substitution semantics in Part 3B.

The current QA snapshot follows existing Recording / Session provenance
semantics used by the recording pipeline.

Session.ActualTeacherId / ActualDeviceId are separate substitution fields and
are not independently resolved by this Part 3A implementation.

If effective-substitution identity needs to become a QA requirement later,
inspect the authoritative Session substitution workflow first and implement it
as a deliberate follow-up rather than guessing in the worker/API layer.

### Verification

QA-3 Part 3A passed:

- git diff --check
- solution build
- full Unit test suite
- full Integration test suite
- migration audit
- exact 17-file source scope
- exact source commit
- remote push verification

Source commit:

`4984eb550ced524c66f2d017b0e8dfbbb56cb64e`

### Explicitly unchanged

Part 3A did NOT change:

- spikes/SttSpike/qa_worker.py
- worker direct-alert payload behavior
- worker Candidate payload behavior
- Program.cs QA worker endpoints
- Agent
- canonical audio capture
- Live pipeline
- Recording generation
- attendance pipeline
- VPS runtime

No database migration was applied.

No VPS deployment was performed.

### Next exact phase

`QA-3 Part 3B - API + Worker commercial evidence wiring`

Part 3B must:

1. Rewire POST /api/worker/qa-alerts to pass the full CreateQaAlertRequest to
   the enriched QaAlertService path.
2. Remove the old API requirement that every Alert must have MatchedPhrase.
3. Keep Restricted Rule MatchedPhrase exact.
4. Send Off-topic direct Alerts with MatchedPhrase = null.
5. Enrich worker direct Alert payloads with:
   - DetectionReason
   - Transcript
   - PolicyVersion
   - AnalysisVersion
   - SourceTrackIndex
   - AudioLayoutVersion
   - TriggerStartSeconds
   - TriggerEndSeconds
   - stable AnalysisIdempotencyKey
6. Enrich Candidate payloads with:
   - DetectionReason
   - MatchedPhrase for Restricted Rule
   - null MatchedPhrase for Off-topic Conversation
7. Preserve QA-2A restricted-rule-first processing.
8. Preserve QA-2B two-pass OffTopic confirmation behavior.
9. Preserve mark-processed ordering.
10. Do not alter verification WAV padding merely because persisted commercial
    evidence is -10/+20; verification windows and commercial evidence windows
    are separate concerns.
11. Add worker/API regression tests before Part 3 is considered closed.

QA-3 Part 3A is SOURCE CLOSED.
<!-- HQL_QA3_PART3A_CLOSED_20260907_END -->

<!-- HQL_QA3_PART2_CLOSED_20260907_BEGIN -->
## QA-3 Part 2 commercial evidence/provenance schema - CLOSED - 2026-09-07

Latest functional source checkpoint:

`859b206af8aa0326e8abc628d10285f96cdb6aa7`

Commit:

`qa: add commercial evidence provenance schema`

Previous QA milestones:

- QA-2A restricted-rule hardening closed at `b6553e67949cd3a97326490390246a3dd0cbb931`
- QA-2B off-topic classifier + worker integration closed at `cc064452ae50db53ee6be298fa221150a6708981`
- QA-3 Part 2 schema/evidence/provenance foundation closed at `859b206af8aa0326e8abc628d10285f96cdb6aa7`

### QA-3 Part 2 implemented

QaAlert now supports commercial QA evidence metadata including:

- DetectionReason
- Transcript
- PolicyVersion / AnalysisVersion
- SourceTrackIndex / AudioLayoutVersion
- TriggerStartSeconds / TriggerEndSeconds
- EvidenceStartSeconds / EvidenceEndSeconds
- AnalysisIdempotencyKey
- Device / Session / Teacher / Student / Course provenance snapshots
- LaptopName / ActualDeviceName
- TeacherName / StudentName / CourseName
- ReviewedByUserId / ReviewedAtUtc / ReviewNote / ReviewVersion

QaCandidate now additionally supports:

- DetectionReason
- EvidenceStartSeconds / EvidenceEndSeconds
- Device / Session / Teacher / Student / Course provenance snapshots
- LaptopName / ActualDeviceName
- TeacherName / StudentName / CourseName

`IntentCategory` is intentionally retained temporarily for backward compatibility.
It is compatibility debt, not the long-term user-facing QA category model.

### Product-facing detection reasons

The clean user-facing reasons are:

- `Restricted Rule`
- `Off-topic Conversation`

`MatchedPhrase` is nullable.

For Restricted Rule findings it represents the configured/verified restricted phrase.

For Off-topic Conversation findings it must not be populated with the whole transcript merely to satisfy the old schema.

### Evidence contract

Target evidence context remains nominally:

- 10 seconds before the trigger
- 20 seconds after the trigger/event
- clamped to recording boundaries

Exact recording playback deep-link already exists through:

`/recordings/{recordingId}/player?start=<seconds>`

### Migration

Generated migration:

`20260907101637_AddQaCommercialEvidenceProvenance`

Migration audit passed:

- qa_alerts: 25 additive columns
- qa_candidates: 13 additive columns
- MatchedPhrase nullable alteration only
- QaAlert AnalysisIdempotencyKey unique index
- no destructive Up() operation
- no table drop
- no column drop
- no rename
- no delete-data operation

The migration has NOT been applied to a database yet.

### Verification

QA-3 Part 2 verification passed:

- solution build PASS
- 148 Unit tests PASS
- 6 Integration tests PASS
- git diff check PASS
- migration operation audit PASS

### Explicitly unchanged in QA-3 Part 2

- QA worker behavior
- QaAlertService
- QaCandidateService
- DashboardQueryService
- QA repositories
- Dashboard/UI
- Agent
- Live monitoring
- canonical classroom audio
- Recording pipeline
- attendance pipeline
- VPS runtime

No VPS deployment was performed.

No database update was performed.

### Canonical QA/audio product contract remains unchanged

Physical classroom audio is captured once and the canonical classroom mixed audio
is the source consumed by Live, Recording and QA/STT.

Do not introduce a separate teacher-only QA capture prerequisite.

No speaker identification is required.

The product question is whether policy-relevant/off-topic conversation occurred
during the class; owner/admin/manager can review the evidence.

### Next exact phase

`QA-3 Part 3 - service / repository / worker wiring`

Part 3 must:

- populate the new schema from authoritative backend Recording/Session provenance
- enrich Alert and Candidate repository loading/projections
- use the same laptop naming semantics as existing dashboard/session presentation
- wire Restricted Rule alerts with exact matched phrase
- wire Off-topic alerts with nullable MatchedPhrase
- persist transcript and evidence offsets
- change new Candidate evidence target from legacy -10/+10 to -10/+20
- fix Candidate confirmation so Transcript is not abused as MatchedPhrase
- preserve QA-2A restricted-rule priority
- preserve QA-2B two-pass off-topic behavior
- preserve manager visibility/RBAC
- preserve canonical classroom audio track 0/layout 1
- add regression tests before any deployment

Do NOT redesign QA-1, QA-2A or QA-2B when starting Part 3.
Read this handoff, the product contract and current source first.

QA-3 Part 2 is SOURCE CLOSED.
<!-- HQL_QA3_PART2_CLOSED_20260907_END -->

<!-- HQL_ATTENDANCE_FAST_FINISH_20260906_BEGIN -->
## Attendance / Sessions fast-finish source milestone - 2026-09-06

The first real schedule-driven canary proved:
- weekly Schedule -> automatic Live Session creation;
- automatic ScheduledEnd completion;
- teacher audio participation evidence;
- remote/student audio participation evidence;
- LessonShared evidence inside the ten-minute post-class grace;
- grace finalization to Teacher Present + Student Present + AutoResolved.

This source milestone closes the remaining canary gaps:
- manual dashboard Session creation is removed from the product workflow and
  the manual POST /api/admin/sessions endpoint is retired;
- Sessions is history/evidence/review, while Schedules is the operational
  source of truth;
- valid LessonShared evidence changes Lesson Shared to Yes immediately and
  auto-resolves both attendance sides immediately;
- the ten-minute grace now waits only when LessonShared is still missing;
- explicit teacher/student participation evidence provides an ActiveSeconds
  fallback instead of leaving proven classes at Active 0m;
- the first observed Teams Connected snapshot emits call-attempt and
  call-connected evidence instead of silently becoming baseline only;
- lesson evidence supports both same-message image+lesson text and the real
  academy image -> nearby para/page/line/lesson-text sequence;
- lesson-grace scanning binds to the exact active student chat instead of
  requiring the student's name inside the lesson message;
- manual attendance review opens in a modal instead of jumping to the bottom
  of the Sessions page.

The scheduler, schedule recurrence, canonical audio capture, Live transport,
Recording pipeline, QA pipeline and database schema are unchanged.

Runtime recertification is still required after deployment and Owner Agent
update.
<!-- HQL_ATTENDANCE_FAST_FINISH_20260906_END -->


<!-- HQL_LEGACY_SESSION_INFINITY_RUNTIME_VERIFIED_20260906_BEGIN -->
# LEGACY SESSION INFINITY FIX - RUNTIME VERIFIED - 2026-09-06

The Sessions workspace was refreshed after deployment of the legacy session
window compatibility fix.

Runtime UI proof:

- Sessions page loads without the previous HTTP 500 failure
- 22 of 22 historical sessions are visible
- legacy manually-created Scheduled rows no longer crash the projection
- Completed historical sessions remain visible
- Lesson Shared presentation is rendered
- Teacher participation presentation is rendered
- Student participation presentation is rendered
- Attendance and Review presentation is rendered

No historical Session database rows were rewritten for this compatibility fix.

Manual Session creation remains owner-controlled. Do not automatically create
test Sessions; the owner will create one manually when a real attendance
canary is required.

The next product step is the real scheduled-class attendance canary using the
owner laptop and real communication headset/Teams audio.

<!-- HQL_LEGACY_SESSION_INFINITY_RUNTIME_VERIFIED_20260906_END -->

<!-- HQL_LEGACY_SESSION_INFINITY_FIX_20260906_BEGIN -->
# LEGACY SESSION INFINITY COMPATIBILITY FIX - 2026-09-06

Runtime proof after the C2C DashboardQueryService deployment found historical
manually-created Session rows whose ScheduledStartUtc and ScheduledEndUtc were
PostgreSQL `-infinity`.

The attendance presentation path attempted date arithmetic directly on those
sentinel values and `/api/admin/sessions` failed with
ArgumentOutOfRangeException.

The fix does not rewrite historical database rows.

A shared SessionWindowResolver now:

- uses the stored scheduled window when it is finite
- falls back to StartedAtUtc / EndedAtUtc for legacy infinity sentinel rows
- clamps invalid end-before-start fallback windows
- performs grace/pre-window minute arithmetic without DateTime overflow

Both DashboardQueryService and SessionService presentation paths use the same
resolved window.

Manual CreateSessionAsync now initializes ScheduledStartUtc and
ScheduledEndUtc explicitly, preventing new manually-created sessions from
being persisted with PostgreSQL infinity values. When EndedAtUtc is omitted,
ScheduledEndUtc initially equals StartedAtUtc rather than inventing a class
duration.

Regression coverage includes:

- the DashboardQueryService legacy infinity failure
- SessionService legacy presentation
- manual session creation without EndedAtUtc
- full solution regression

No historical Session row was changed.
No database migration was added.
No Agent source was changed.
No audio, Live, QA, Recording, scheduler, or AttendanceReducer source was
changed.

Real scheduled-class attendance canary remains pending. Test Sessions remain
owner-created manually when required.

<!-- HQL_LEGACY_SESSION_INFINITY_FIX_20260906_END -->

<!-- HQL_C2C_DASHBOARD_SESSION_PROJECTION_FIX_20260906_BEGIN -->
# C2C DASHBOARD SESSION PROJECTION FIX - 2026-09-06

Pre-class runtime review found that `/api/admin/sessions` uses
`DashboardQueryService.GetVisibleSessionsAsync`.

C2C presentation calculations already existed in SessionService, but the
independent DashboardQueryService SessionDto projection did not expose the new
attendance presentation fields.

DashboardQueryService now projects:

- ScheduledStartUtc
- ScheduledEndUtc
- LessonGraceEndsAtUtc
- LessonSharedStatus
- TeacherParticipationEvidence
- StudentParticipationEvidence
- AttendanceReviewAllowed

Rules remain:

- Lesson Shared stays Pending through the full 10-minute grace
- after grace valid LessonShared = Yes, otherwise No
- teacher participation requires TeacherAudioParticipationObserved inside the scheduled window
- student participation requires RemoteAudioParticipationObserved inside the scheduled window
- attendance review remains unavailable until Completed + grace expiry

The actual DashboardQueryService route now has regression coverage.

The first targeted test run stopped only because the new test referenced
SessionDto without importing Academy.Application.Contracts. The interrupted
state was preserved; no reset was used. The missing test namespace was added
and validation resumed from the failed point.

No Agent source changed.

No database schema changed.

No QA, Live, Recording, or audio-capture pipeline changed.

Owner-laptop canary Agent remains:

`1.0.0-f0c2ae52958c-attendance1`

Real scheduled-class attendance canary must wait until this backend-only fix is
deployed and the Sessions API presentation payload is runtime verified.

<!-- HQL_C2C_DASHBOARD_SESSION_PROJECTION_FIX_20260906_END -->

<!-- HQL_ATTENDANCE_CANARY_PUBLISHED_20260905_BEGIN -->
# ATTENDANCE AGENT CANARY PUBLISHED BUT NOT QUEUED - 2026-09-05

Runtime application source remains:

`f0c2ae52958c92a19e27b9ea2f6fa443665e7ad7`

Published immutable Agent release:

- version `1.0.0-f0c2ae52958c-attendance1`
- releaseId `attendance-f0c2ae52958c-canary1`
- installer SHA256 `7E65DE2AE775C8C80EC16D45533044D3D6E487B838B9492DFD3024D1182A3D17`
- manifest target list count 1

Package was published before the manifest.

The previous manifest was preserved in manifest history.

The API container sees both the manifest and package through the existing
read-only Agent release mount.

## Important eligibility contract

The manifest TargetDeviceIds list is not the runtime authorization gate.

The backend serves an enabled update only when the managed Device has:

`PendingAgentUpdateVersion == published manifest Version`

That pending version is set by the Owner-only Agent update request endpoint.

Immediately after publication the canary device was verified as:

`enabled = false`

Therefore publication alone did not authorize or install the Agent update.

The owner laptop remained on:

`1.0.0-b043352365aa-resume1`

## Runtime safety

- API/dashboard were not restarted by release publication
- no other container was restarted
- VPS application source was not changed
- custom Caddy was not changed
- database was not changed
- Agent was not updated yet

## Next

Explicitly queue the published release for the owner canary device only.

Then run/observe the updater and verify:

- exact new Agent version
- installer/update success
- Recording.Enabled remains false
- LiveStreaming remains enabled
- heartbeat and class-window recover
- no second device receives an update
- real scheduled-class attendance/audio calibration before certification

<!-- HQL_ATTENDANCE_CANARY_PUBLISHED_20260905_END -->

<!-- HQL_ATTENDANCE_RUNTIME_BACKEND_C2_20260905_BEGIN -->
# ATTENDANCE BACKEND/DASHBOARD RUNTIME DEPLOYED - 2026-09-05

Runtime source commit:

`f0c2ae52958c92a19e27b9ea2f6fa443665e7ad7`

## VPS deployment

Only these application containers were rebuilt/recreated:

- academy-api
- academy-dashboard

Production API public health returned HTTP 200.

Dashboard public endpoint returned a valid HTTP response.

The existing Compose project and both production Compose files were retained.

## Explicitly preserved

- PostgreSQL not restarted
- Redis not restarted
- MinIO not restarted
- QA worker not restarted
- LiveKit not restarted
- LiveKit Ingress not restarted
- ingress-manager not restarted
- Caddy not restarted
- MediaMTX relay not restarted
- recording archive services not restarted

Custom VPS Caddyfile remained byte-identical with SHA256:

`280DFE2CF855E4BE0029C36FE992AE4505DD393F50B25717AA25761447338AC1`

No database migration was introduced.

## Agent canary

Immutable Agent canary is built but NOT yet published:

- version `1.0.0-f0c2ae52958c-attendance1`
- release `attendance-f0c2ae52958c-canary1`
- SHA256 `7E65DE2AE775C8C80EC16D45533044D3D6E487B838B9492DFD3024D1182A3D17`
- target count 1

Current installed owner-laptop Agent has not yet been updated.

## Next

Publish the one-device canary manifest/package, allow only the owner device to
update, then verify updater success and real attendance audio/session behavior.

Audio-attendance remains not runtime-certified until the real headset/classroom
canary passes.

<!-- HQL_ATTENDANCE_RUNTIME_BACKEND_C2_20260905_END -->

<!-- HQL_ATTENDANCE_PRESENTATION_C2C_20260905_BEGIN -->
# ATTENDANCE PRESENTATION - C2C SOURCE PROVEN - 2026-09-05

Previous attendance finalization commit:

`bed49a0d4b63ced199104785979b52a3381881dc`

## Session presentation

Sessions expose:

- LessonSharedStatus
- TeacherParticipationEvidence
- StudentParticipationEvidence
- LessonGraceEndsAtUtc
- AttendanceReviewAllowed

Lesson Shared main status:

- Pending while the ten-minute lesson grace is open
- Yes after grace when valid LessonShared exists
- No after grace when no valid LessonShared exists

LessonShared evidence arriving during grace does not switch the main status to
Yes early.

## Participation UI

User-facing labels are:

- Teacher Participation
- Student Participation

Technical audio route names, PCM labels, detector details, and worker internals
are not shown in normal Sessions UI.

Only explicit in-session TeacherAudioParticipationObserved and
RemoteAudioParticipationObserved events drive these presentation fields.

Historical StudentAudioDetected is not authoritative.

## Manual review

Completed sessions still inside grace show Grace pending rather than Review.

Backend C2B review timing remains authoritative.

## Runtime

Source/build/test proof only.

No Agent release built.
No Agent deployment.
No VPS deployment.
No database migration.
No QA change.
No Live change.
No Recording change.

## Next

Controlled runtime release and canary preparation.

Real hardware audio-attendance calibration remains required before runtime
certification.

<!-- HQL_ATTENDANCE_PRESENTATION_C2C_20260905_END -->

<!-- HQL_ATTENDANCE_FINALIZATION_C2B_20260905_BEGIN -->
# 10-MINUTE ATTENDANCE FINALIZATION - C2B SOURCE PROVEN - 2026-09-05

Previous shared-audio evidence source commit:

`837edfd4405f6e2afcc00da0ebd35983a7cec2ca`

## Lifecycle

ScheduledEndUtc is the hard end of previous-session audio/activity attribution.

At ScheduledEndUtc:

- Session may become Completed.
- Attendance remains Unknown/Pending.
- LessonShared grace continues separately for exactly ten minutes.
- The next scheduled session may start normally.
- No previous-session microphone/render activity continues into grace.

Automatic attendance is not finalized before:

`ScheduledEndUtc + 10 minutes`

## Grace expiry

Valid LessonShared inside the accepted window:

- Teacher = Present
- Student = Present
- Review = AutoResolved

Without valid LessonShared:

- TeacherAudioParticipationObserved => Teacher Present
- no teacher proof => Teacher NeedsReview
- RemoteAudioParticipationObserved => Student Present
- no remote proof => Student NeedsReview
- both audio proofs => both Present + AutoResolved
- missing/partial proof => Review remains Pending

There is no automatic Absent path.

There is no automatic Late path.

Historical StudentAudioDetected remains non-authoritative.

## Evidence windows

Teacher/remote audio participation is authoritative only from:

`ScheduledStartUtc -> ScheduledEndUtc`

LessonShared is accepted only from:

`ScheduledStartUtc - 5 minutes`

through:

`ScheduledEndUtc + 10 minutes`

A later lesson does not automatically resolve attendance.

## Durable finalization

Backend-only event:

`AttendanceFinalizationCompleted`

is written after the ten-minute grace evaluation.

Its session-scoped idempotency key prevents the scheduler from repeatedly
finalizing the same Pending human-review session every minute.

Agents cannot submit this backend-only marker.

## Human review

AttendanceReviewStatus.Reviewed remains authoritative.

Automated evidence does not overwrite a manually reviewed attendance decision.

Manual review is blocked until the ten-minute lesson grace has expired.

## Runtime

Source/build/test proof only.

No new Agent release built.

No Agent deployment.

No VPS deployment.

No schema migration.

Dashboard unchanged in C2B.

## Next

C2C:

- expose Lesson Shared = Pending / Yes / No in Session DTO/dashboard;
- expose teacher-side and remote-side participation evidence cleanly;
- make review UI reflect the ten-minute pending state;
- prepare controlled C1 + C2A + C2B runtime canary.

<!-- HQL_ATTENDANCE_FINALIZATION_C2B_20260905_END -->

<!-- HQL_SHARED_AUDIO_ATTENDANCE_EVIDENCE_20260905_BEGIN -->
# SHARED AUDIO ATTENDANCE EVIDENCE - C2A SOURCE PROVEN - 2026-09-05

Previous lesson-routing source commit:

`e932a954bb5e9191ecbe5647e02ee84ceb0d5a00`

## Implemented

Attendance now consumes the existing canonical ClassroomAudioHub through its
own bounded/non-blocking subscription.

No second physical audio capture chain was created.

Routes:

- TeacherPcm = effective teacher communication microphone
- SystemPcm = effective communication render/playback

The existing shared ClassroomAudioRuntime is reused.

Audio attribution is limited strictly to the scheduled Current Session.

At ScheduledEndUtc audio attribution for that session stops.

The separate 10-minute LessonShared grace receives no previous-session audio.

## Evidence

Maximum logical positive audio evidence per session:

- TeacherAudioParticipationObserved
- RemoteAudioParticipationObserved

The old StudentAudioEvidenceWorker remains removed.

The new worker remembers already-proven sides for the same SessionId during the
Agent process so it does not unnecessarily reprocess the same class after both
positive proofs are obtained.

Backend idempotency remains authoritative across retries/restarts.

## Detector

Defaults:

- float canonical PCM
- 20ms frames
- MinimumMeaningfulSeconds = 5
- AbsoluteFloorDbfs = -55
- NoiseMarginDb = 9
- NoiseWindowSeconds = 2
- adaptive background noise floor

During C2A testing a configuration bug was caught before commit:

threshold calculation used two duplicate uninitialized private fields instead
of the constructor-populated public configuration properties.

The duplicate private fields were removed.

Threshold calculation now uses:

- AbsoluteFloorDbfs
- NoiseMarginDb

A dedicated regression test verifies the configured threshold is really used.

Synthetic tests cover:

- silence rejection
- steady hiss/noise rejection
- steady tone rejection
- speech-like burst acceptance
- speech-like activity above stable hiss/noise

Real academy hardware calibration is still required before runtime
certification.

## Backend boundary

Teacher/remote participation events are accepted only inside:

ScheduledStartUtc -> ScheduledEndUtc

No post-end audio is attributed to LessonGrace.

## Attendance semantics

C2A produces positive evidence only.

AttendanceReducer has NOT yet been changed to decide attendance from these
events.

That change belongs to C2B.

## Runtime

- source/build/tests only
- Agent release not built
- Agent not deployed
- VPS not deployed

## Next

C2B ten-minute attendance finalization:

- LessonShared remains strongest proof
- keep attendance pending through lesson grace
- after grace expires with no lesson:
  - teacher audio proof => Teacher Present
  - missing teacher proof => Teacher NeedsReview
  - remote audio proof => Student Present
  - missing remote proof => Student NeedsReview
  - both proven => AutoResolved possible with Lesson Shared No
- no automatic Absent solely from missing evidence
- no Late from lesson timing
- manually Reviewed results must not be automatically overwritten

<!-- HQL_SHARED_AUDIO_ATTENDANCE_EVIDENCE_20260905_END -->


<!-- HQL_TEN_MINUTE_LESSON_GRACE_SOURCE_20260905_BEGIN -->
# 10-MINUTE LESSON GRACE ROUTING - SOURCE PROVEN - 2026-09-05

Authoritative attendance/lesson contract commit:

`c7c4c41306d4eee963788fec5eb387f66404bb46`

## Implemented

- operational Current Session remains independent from lesson grace;
- backend class window exposes one `LessonGrace` session;
- LessonGrace is the immediately previous Pending/unresolved session;
- maximum LessonShared grace = 10 minutes after ScheduledEndUtc;
- current/next scheduled operation is preserved;
- current session can start while the previous lesson grace remains open;
- same Teams call may continue across the scheduled session boundary;
- completed-session activity attribution ends exactly at ScheduledEndUtc;
- the previous session receives no post-end mic/render/call/activity attribution;
- Teams IPC exposes Current + LessonGrace targets;
- normal Teams evidence remains Current-only;
- LessonShared may resolve Current or LessonGrace;
- helper uses a separate lesson-only state machine for the grace target;
- local message text is used only for safe student-name matching and is not
  persisted as attendance evidence;
- during overlap, current and previous lesson routing uses saved student names;
- case/punctuation/spacing normalization is allowed;
- fuzzy typo guessing is not used;
- a single lesson MessageId matching two different sessions is suppressed as
  ambiguous rather than assigning it to either session;
- online backend LessonGrace is supported;
- cached Current/Next timestamps can preserve the previous 10-minute lesson
  target during short backend connectivity loss;
- no sibling/family/Teams-chat metadata was added;
- no database schema change;
- no canonical audio source change;
- no Live/Recording/QA change;
- StudentAudioEvidenceWorker remains removed.

## Runtime state

Source/build/test proof only.

No new Agent immutable release has been built.

No Agent deployment has occurred.

No VPS deployment has occurred.

## Next

Phase C2:

Consume the existing shared canonical ClassroomAudioHub non-blockingly to derive
scheduled-session teacher-side and remote-side meaningful speech/activity
evidence.

Do not create another WASAPI/physical capture owner.

Do not restore StudentAudioEvidenceWorker.

Audio attribution must stop at ScheduledEndUtc even while LessonShared grace
continues separately.

<!-- HQL_TEN_MINUTE_LESSON_GRACE_SOURCE_20260905_END -->


<!-- HQL_ATTENDANCE_FINAL_10MIN_CONTRACT_20260905_BEGIN -->
# ATTENDANCE + LESSON SOP - FINAL OWNER CONTRACT - 2026-09-05

This checkpoint supersedes the earlier assumption that delayed lessons may need
one-hour or multi-hour reconciliation.

## Final operational rule

Each student keeps an independent scheduled Session.

The Teams call may continue across consecutive student sessions.

At scheduled session end:

- finished session mic/render evidence freezes;
- next scheduled session may start immediately;
- finished session remains only as a Lesson Grace Target.

Lesson grace:

- expected teacher SOP: about 5 minutes;
- hard maximum grace: 10 minutes;
- grace is ONLY for LessonShared;
- previous-session audio does not continue during grace.

State model:

- Current Session
- immediately previous Lesson Grace Target only

No four-hour reconciliation.

No sibling metadata.

No family ID.

No Teams Chat Name field.

No artificial combined sibling student/session.

Lesson association may use the saved session student name appearing in the
teacher's outgoing lesson text.

Safe normalization is allowed; ambiguous fuzzy matching must not guess.

## Attendance

LessonShared for the correct session:

- Teacher Present
- Student Present
- AutoResolved

If lesson grace expires without LessonShared:

- Lesson Shared = No
- use frozen scheduled-session activity evidence

Teacher-side meaningful effective microphone speech/activity can prove teacher
participation.

Meaningful effective communication render/playback speech/activity can prove
remote/student participation.

This is communication-route participation evidence, not biometric speaker
identity.

Raw hiss/noise/device-open state is not enough.

No automatic Absent solely from missing evidence.

No automatic Late from lesson timestamp.

If activity proves both sides Present, attendance may AutoResolve while Lesson
Shared remains No.

If either side is uncertain, that side remains NeedsReview.

## Preservation

Do not restore StudentAudioEvidenceWorker.

Do not create a second physical audio capture chain.

Attendance activity must consume the existing canonical/shared classroom audio
architecture.

Live Monitoring remains the highest-priority audio consumer.

<!-- HQL_ATTENDANCE_FINAL_10MIN_CONTRACT_20260905_END -->


<!-- HQL_ATTENDANCE_SIMPLIFICATION_20260905_BEGIN -->
# ATTENDANCE SIMPLIFICATION - SOURCE COMPLETE / RUNTIME PENDING - 2026-09-05

## Product attendance authority

Attendance now follows the Owner-approved contract:

- `LessonShared` is the only automatic attendance authority.
- Valid `LessonShared` for a session means:
  - Teacher = `Present`
  - Student = `Present`
  - review status = `AutoResolved`
- lesson timestamp is NOT teacher/student arrival time.
- automatic `Late` is not produced.
- automatic `Absent` is not produced.
- completed session without valid lesson evidence:
  - Teacher = `NeedsReview`
  - Student = `NeedsReview`
  - review status = `Pending`
- live session without lesson remains `Unknown / Pending`.
- audio, call state, greeting, generic activity and communication-process
  evidence are NOT attendance truth.

Attendance source commit:

`c51363b5a64d07083e5b2391f7aff46746cf23af`

Commit:

`feat: make lesson sharing attendance authority`

Verification at that commit:

- targeted AttendanceReducer tests = 30 PASS
- full solution tests = 131 PASS
- QA changed = NO
- Live changed = NO
- Recording changed = NO
- database schema changed = NO

## Student-audio attendance worker retired

The dedicated Agent `StudentAudioEvidenceWorker` has been removed.

Removed behavior:

- 250 ms process AudioSession meter polling
- five-second `StudentAudioDetected` emission
- hosted-worker registration
- Agent activity signal type
- ClassObserver mapping for new StudentAudioDetected events

Historical backend `StudentAudioDetected` enum/data compatibility is intentionally
retained so old session evidence remains readable.

Historical StudentAudioDetected events do NOT resolve attendance.

Agent cleanup commit:

`ca2cd772f6bdff91588786604a7ee4948a637c65`

Commit:

`refactor: retire student audio attendance worker`

Verification:

- Agent build = PASS
- AttendanceReducer regression = 30 PASS
- full solution = 131 PASS
- QA changed = NO
- Live changed = NO
- Recording changed = NO
- database schema changed = NO

## Deployment state

These attendance changes are SOURCE COMPLETE only.

They are NOT runtime-certified and have NOT been deployed.

- VPS attendance/API deployment = pending
- new Agent immutable release = not built
- Owner/teacher Agent rollout = not started

Do not publish a new Agent release merely for the worker removal.

Finish delayed LessonShared reconciliation first, test the combined behavior,
then create one immutable Agent release/canary.

## Remaining attendance blocker

Delayed lesson reconciliation is not complete.

Required workflow:

- lesson may be sent substantially after class, including about one hour later
- recent unresolved sessions must remain eligible for lesson reconciliation
- back-to-back sessions must not cause the previous lesson target to disappear
- multiple children/family workflows must never be silently assigned to the
  wrong session
- ambiguity must remain `NeedsReview` rather than guessing
- no audio/speaker attribution may be reintroduced for attendance

Current single-target Teams observation and short evidence-window behavior must
be replaced with a bounded delayed-lesson reconciliation design.

<!-- HQL_ATTENDANCE_SIMPLIFICATION_20260905_END -->


<!-- HQL_LIVE_VIEWER_RUNTIME_CERTIFIED_20260905_BEGIN -->
# LIVE MONITORING VIEWER ? RUNTIME CERTIFIED COMPLETE ? 2026-09-05

> This checkpoint supersedes older Live Monitoring viewer notes where adaptive stream, manual subscription management, feed-quality monitoring, automatic metadata refresh, or native mobile browser fullscreen were still active.
>
> Do not reopen this viewer architecture unless an observed regression or explicit Owner request requires it.

## Runtime-certified application commit

`fd5695033d11af9e959d55352fab3089dae9f7a5`

Commit:

`fix: polish live monitoring mobile ux`

Stable-behavior restoration commit:

`2a8f5cf98083b81f70a7bfdf17c9853c6a2e902d`

VPS deployment model:

- detached HEAD at exact certified commit
- dashboard-only rebuild/recreate for dashboard-only changes
- approved custom VPS Caddyfile must remain preserved

Approved custom Caddy SHA256:

`280dfe2cf855e4be0029c36fe992ae4505dd393f50b25717aa25761447338ac1`

## Final LiveKit viewer contract

The dashboard viewer uses LiveKit default behavior:

- `new Room()`
- `await room.connect(url, token)`
- LiveKit owns built-in reconnect
- `RoomEvent.Reconnecting` / `RoomEvent.Reconnected` are UI status only

Removed and intentionally NOT part of the viewer:

- `adaptiveStream`
- `autoSubscribe: false`
- manual `setSubscribed(...)`
- custom subscription recovery
- publisher watchdog/self-healing loops
- connection-quality badge
- adaptive-video overlay
- automatic 30-second metadata refresh

Manual `Retry feed` remains a user-controlled action.

## Metadata contract

Metadata loads initially.

After initial load, metadata refresh is manual only.

`Refresh now`:

- is clickable
- shows `Refreshing...`
- refreshes Teacher / Student / Course / Session metadata
- does NOT intentionally recreate or interrupt the LiveKit feed

Expanded view also exposes a manual `Refresh` button.

## Mobile fullscreen contract

Desktop retains browser native fullscreen.

Mobile uses dashboard-owned immersive fullscreen instead of Android Chrome native fullscreen.

This avoids browser-generated IP/instruction overlays and keeps the experience inside the SaaS UI.

Runtime-certified mobile behavior:

- immersive fullscreen opens = PASS
- white browser IP/instruction message = NO
- fullscreen exit = PASS
- portrait layout = PASS
- landscape layout = PASS

## Runtime acceptance proof

Desktop:

- video = PASS
- audio = PASS
- Listen / Mute = PASS
- fullscreen = PASS

Viewer behavior:

- feed-quality badge removed = PASS
- adaptive-video UI removed = PASS
- Refresh now clickable = PASS
- Refreshing text visible = PASS
- Refresh interrupts video = NO
- live stays connected = PASS
- unexpected Waiting for classroom = NO
- video freeze / black = NO

Mobile:

- video = PASS
- audio = PASS
- immersive fullscreen = PASS
- fullscreen exit = PASS
- portrait layout = PASS
- landscape layout = PASS
- expanded Refresh = PASS
- expanded Close = PASS

Audio/video synchronization is acceptable for the current use case.

## Remaining audio issue ? NEXT PRIORITY

The viewer itself is now stable and runtime-certified.

Remaining audio defect:

`AUDIO_HISS=YES`

Observed characteristics:

- constant `shhhhh` noise floor
- teacher/student voices remain clear
- no echo/repeat
- no video freeze/black
- audio/video context is acceptably synchronized
- hiss is heard through both desktop and mobile monitoring

Therefore do NOT reopen the dashboard viewer for this hiss unless source-isolation evidence points back to it.

Next engineering task:

isolate the hiss source through the existing audio pipeline, source-by-source, beginning on the Owner laptop so teacher classes are not disturbed.

Priority remains:

`Audio reliability / clarity > Live feed > QA > Recording`

<!-- HQL_LIVE_VIEWER_RUNTIME_CERTIFIED_20260905_END -->

<!-- HQL_RUNTIME_CERTIFIED_20260905_BEGIN -->
# LATEST RUNTIME-CERTIFIED CHECKPOINT — 2026-09-05

> **READ THIS BLOCK BEFORE OLDER HANDOFF TEXT BELOW.**
>
> Older sections are retained as historical context. Any older statement saying Usual Teachers or Activity Log is not deployed, migrated, or runtime-tested is superseded by this checkpoint.
>
> Do not reopen completed work without an observed regression or explicit Owner request.

## Repository / deployed application state

Repository:

`C:\Dev\HomeQuranLearning.QA`

GitHub:

`hibamylilstar-arch/HomeQuranLearning.QA`

Branch:

`codex/local-development-mode`

Current deployed application commit:

`6f0b1654500b1ecbd2b97e857b4bdcacd854b38b`

Commit message:

`fix: clarify audit delete and assignment targets`

The VPS application is deployed at this commit.

Approved custom VPS Caddy SHA256:

`280dfe2cf855e4be0029c36fe992ae4505dd393f50b25717aa25761447338ac1`

The VPS Caddyfile is intentionally modified and MUST be preserved.

---

## Activity Log — RUNTIME CERTIFIED COMPLETE

Dashboard:

`Access -> Activity Log`

Main implementation commit:

`def5c0df27e68e9fe772a4a4e767456875318962`

Proxy 204 fix:

`e8013ecd6414ef6e1479ebf62fe5ac61d057838d`

Delete semantics + friendly assignment target fix:

`6f0b1654500b1ecbd2b97e857b4bdcacd854b38b`

Supporting stale test correction:

`367f7fe1565e39ed6eb1daf6b9eb1f767e6344b1`

Migration:

`20260904235031_AddActivityAuditLog`

VPS migration:

`APPLIED`

Table:

`audit_log_entries`

Activity Log is append-only from application behavior. Normal dashboard/API users cannot edit or clear audit history.

### Capture scope

Meaningful authenticated human dashboard mutations from:

- Owner
- Admin
- Manager

are audited.

Background/system noise remains intentionally excluded, including:

- GET/page views
- Agent heartbeat/polling
- workers
- scheduler ticks
- LiveKit/media internals
- recording worker internals
- QA background workers
- upload/ingest plumbing

### Runtime capture

The deployed path has been proven end-to-end:

`Dashboard -> Next.js proxy -> authenticated API -> EF audit interceptor -> PostgreSQL`

Activity Log create/update/delete capture is operational.

---

## Delete semantics — CURRENT PRODUCT RULE

The dashboard presents the user action as:

`Delete`

Backend may preserve historical referential integrity using soft-delete/inactive state.

For NEW dashboard deletes, Activity Log must display:

`Deleted`

NOT:

`Archived`

Runtime-certified Admin proof:

- Teacher DELETE HTTP 204 = PASS
- Student DELETE HTTP 204 = PASS
- Course DELETE HTTP 204 = PASS

Audit proof:

- Teacher `Deleted` = 1
- Teacher `Archived` = 0
- Student `Deleted` = 1
- Student `Archived` = 0
- Course `Deleted` = 1
- Course `Archived` = 0

Old historical `Archived` audit rows are immutable and MUST NOT be rewritten.

Delete confirmation UI remains enabled.

Successful mutations retain automatic dashboard refresh.

The previous false 500 was caused by the generic Next.js proxy attempting to put a JSON body on an upstream `204 No Content` response.

Generic proxy handling for:

- 204
- 205
- 304

must continue to return bodyless responses.

---

## Human-readable assignment audit targets

Manager -> Teacher audit target:

`Manager: <Manager Name> -> Teacher: <Teacher Name>`

Runtime proof:

`MANAGER_TEACHER_NAMES_IN_LOG=PASS`

Usual Teacher -> Laptop target:

`Teacher: <Teacher Name> -> Laptop: <Laptop Name>`

Both Assigned and Unassigned paths were runtime-certified.

Proof:

`TEACHER_LAPTOP_NAMES_IN_LOG=PASS`

Do not regress these targets to GUID-only display.

---

## Activity Log role visibility — RUNTIME CERTIFIED

### Owner

Owner sees:

- Owner
- Admin
- Manager

Runtime proof:

`OWNER_SEES_OWNER_ADMIN_MANAGER=PASS`

Owner receives technical audit metadata.

Proof:

`OWNER_TECHNICAL_DETAILS=PASS`

### Admin

Admin sees:

- Admin
- Manager

Admin MUST NOT see Owner actions.

Runtime proof:

`ADMIN_SEES_MANAGER=PASS`

`ADMIN_SEES_OWNER=NO`

Exact probe:

`ADMIN_OWNER_MATCHES=0`

Admin does not receive Owner-only technical metadata.

`ADMIN_TECHNICAL_DETAILS_HIDDEN=PASS`

### Manager

Manager sees:

- Manager
- Admin

Manager MUST NOT see Owner actions.

Runtime proof:

`MANAGER_SEES_ADMIN=PASS`

`MANAGER_SEES_OWNER=NO`

Exact probe:

`MANAGER_OWNER_MATCHES=0`

Manager does not receive Owner-only technical metadata.

`MANAGER_TECHNICAL_DETAILS_HIDDEN=PASS`

The backend repository filter is authoritative. This must never be reduced to UI-only hiding.

Final security proof:

`ACTIVITY_LOG_ROLE_SECURITY_RUNTIME_CERTIFIED=PASS`

`ACTIVITY_LOG_FINAL_SECURITY_GATE=PASS`

---

## Activity Log visible wording

The visible Activity Log accountability description does NOT mention Owner.

Visible wording is centered on:

`Admin · Manager`

This is UI wording only.

Owner backend visibility remains:

Owner + Admin + Manager.

---

## Activity Log performance / safety

Current design intentionally uses:

- server-side filtering
- `AsNoTracking`
- max page size 100
- `pageSize + 1` HasMore query
- no CountAsync
- no background polling
- manual Refresh
- human mutation audit only

Audit must never expose:

- passwords
- password hashes
- JWTs
- API keys
- stream keys
- storage secrets
- LiveKit secrets

Password reset may log the action `Password Reset`, but never the secret value/hash.

---

## Laptop Name + Usual Teachers — DEPLOYED / CERTIFIED

Older handoff statements saying this feature is not deployed are obsolete.

Feature commit:

`a4f5871fd24f7313d430b24653354f08c35f20af`

PUT proxy fix:

`5cb83d0257c9b9c80393904fd2b133718e966f87`

Owner Managed badge removal:

`7c880bef0168dd3d3d1cd9cd949849774f65da2f`

Migration:

`20260904212848_AddDeviceTeacherAssignments`

VPS migration:

`APPLIED`

Runtime certification:

`USUAL_TEACHERS_FEATURE_RUNTIME_CERTIFIED=COMPLETE`

Final rules:

- Laptop Name = friendly asset identity
- Windows DeviceName remains separate technical identity
- Laptop can have multiple Usual Teachers
- Teacher can be usual on multiple laptops
- Usual Teachers are informational only
- actual Schedule/Session teacher remains authoritative
- Usual Teachers create no blocking/warning/attendance restriction
- Laptop Name edit = Owner-only
- Usual Teachers management = Owner + Admin
- Manager cannot manage Usual Teachers
- Admin cannot guess and mutate an Owner-hidden device

Do not reopen absent a real regression.

---

## Live/media accepted baseline

Do not reopen absent regression.

- `LIVE_AUDIO_LATENCY=ACCEPTABLE`
- `LIVE_VIDEO_LATENCY=ACCEPTABLE`
- `AUDIO_VIDEO_CONTEXT_SYNC=PASS`

Accepted approximate latency:

- audio ~1–2 sec
- video ~4–5 sec

Accepted path:

`Windows H264/AAC RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit -> Dashboard WebRTC`

Known-good capture:

- ddagrab video
- NAudio/WASAPI loopback audio
- UDP audio transport to FFmpeg

Audio reliability remains the highest monitoring priority.

---

## Current immutable Agent release

Version:

`1.0.0-b043352365aa-resume1`

Release ID:

`resume-b043352365aa-1`

SHA256:

`872DDB40281A73DADE36FCA336C5A10EC1D70B994771B7605723CD82DE7CC5E1`

Do not rebuild/overwrite without Agent source changes.

---

## Current feature state

Activity Log:

`RUNTIME_CERTIFIED=COMPLETE`

Activity Log security:

`RUNTIME_CERTIFIED=COMPLETE`

Usual Teachers:

`RUNTIME_CERTIFIED=COMPLETE`

Synthetic runtime test fixtures were cleaned after certification.

Custom Caddy configuration remained preserved throughout.

After the next meaningful proven product milestone, update BOTH:

- `AGENTS.md`
- `docs/PROJECT-STATE.md`

using the same inspect -> change -> test -> commit/push -> runtime-proof discipline.

<!-- HQL_RUNTIME_CERTIFIED_20260905_END -->
# CURRENT HANDOFF CHECKPOINT — 2026-09-05

> **NEW AI / DEVELOPER: READ THIS SECTION FIRST.**
>
> This is the authoritative continuation checkpoint from the previous ChatGPT engineering session.
> Do not restart architecture discovery, do not undo validated work, and do not invent new product restrictions.
> First inspect `git status`, `git log -1`, `AGENTS.md`, and `docs/PROJECT-STATE.md`, then continue from the exact state below.

## 1. Collaboration / execution contract

- User is the product owner.
- Assistant acts as senior developer.
- User normally executes exact PowerShell commands and returns output.
- Give one bounded step at a time unless user explicitly asks for all steps.
- Commands must be ready to paste with full paths.
- Default local shell is Windows PowerShell 5.1 unless explicitly invoking `pwsh`.
- Do not ask user to manually edit source files.
- Inspect -> smallest change -> build/test -> diff -> commit/push -> runtime proof.
- Do not claim PASS without evidence.
- On unexpected output, stop and diagnose instead of stacking workarounds.
- Do not silently invent restrictions, rollout gates, expiry rules, battery bans, Owner-only behavior, or architecture layers.
- Product behavior is Owner-decided. AI improves implementation quality but does not override product intent.
- Routine integrity protections such as validation, hashes, atomic writes, corruption prevention, and secret protection are automatic.
- Never expose Agent API keys, signing secrets, stream keys, private keys, production passwords, or other credentials.

## 2. Repository / branch

Local repository:

`C:\Dev\HomeQuranLearning.QA`

GitHub repository:

`hibamylilstar-arch/HomeQuranLearning.QA`

Working branch:

`codex/local-development-mode`

Baseline before the current Usual Teachers feature:

`a8645363b127905c7702ea7a7477ce93e5c9b65e`

That baseline is also the currently proven/deployed VPS application baseline before the new Usual Teachers work.

This one-shot handoff command is intended to commit and push the completed Usual Teachers feature plus this documentation. After it runs, inspect:

`git log -1 --oneline`

and:

`git status --short`

The expected commit message is:

`feat: add laptop usual teacher assignments`

If that commit exists and the worktree is clean, the feature is saved in GitHub but is still NOT deployed to VPS yet.

## 3. Current feature completed locally: Laptop Name + Usual Teachers

The user identified a product-model problem: admins were putting teacher names into Laptop Name because they needed to remember who normally uses each academy laptop.

Final product model:

### Laptop identity

Laptop Name is the stable friendly asset identity, examples:

- Laptop 5
- Laptop 7
- Laptop 8

The actual Windows computer name such as `DESKTOP-71RJV67` remains a separate technical identity for troubleshooting.

### Usual Teachers

A laptop can have multiple usual teachers, and a teacher may use multiple laptops.

Therefore this is implemented as a proper many-to-many relationship:

`Device <-> Teacher`

through:

`DeviceTeacherAssignment`

Usual Teachers are informational only.

They MUST NOT:

- automatically change the actual class teacher
- block substitute teachers
- create assignment warnings/gates
- control attendance
- rewrite historical Session teacher data
- restrict Schedule/Session creation

The Teacher selected on the actual Schedule/Session remains authoritative.

## 4. Backend implementation completed and audited

New entity:

`src/Backend/Academy.Domain/Entities/DeviceTeacherAssignment.cs`

New application/infrastructure components include:

- `IDeviceTeacherAssignmentRepository`
- `DeviceTeacherAssignmentRepository`
- `DeviceTeacherAssignmentService`
- `DeviceTeacherInfoDto`
- `SetDeviceTeachersRequest`

Database table:

`device_teacher_assignments`

Important database semantics:

- DeviceId FK
- TeacherId FK
- unique `(DeviceId, TeacherId)` index
- cascade cleanup on device/teacher removal
- duplicate Teacher IDs are de-duplicated
- every selected Teacher ID is validated
- empty selection is supported to clear all Usual Teachers

EF migration:

`20260904212848_AddDeviceTeacherAssignments`

The migration was generated successfully and audited.

Validated:

- additive-only migration
- creates `device_teacher_assignments`
- no destructive Up operations
- unique DeviceId/TeacherId index
- snapshot updated
- generated SQL contains CREATE TABLE and CREATE UNIQUE INDEX

IMPORTANT:

The migration has NOT been manually applied to the local database or VPS as part of this session yet.

Local PostgreSQL at `localhost:5433` was unavailable during one migration-list check, but EF still successfully discovered:

`20260904212848_AddDeviceTeacherAssignments`

Do not start redesigning the migration because of that old local connection warning.

## 5. API behavior

New endpoint:

`PUT /api/admin/devices/{deviceId}/usual-teachers`

Authorization:

Owner + Admin.

It also checks device visibility through the existing dashboard device visibility boundary before allowing modification, so an Admin cannot guess the ID of an Owner-hidden device and modify it directly.

Validated:

`USUAL_TEACHERS_OWNER_ADMIN=PASS`

`ADMIN_HIDDEN_DEVICE_GUARD=PASS`

Laptop Name endpoint:

`PATCH /api/admin/devices/{deviceId}/recording-display-name`

During final audit, an existing mismatch was discovered: backend allowed Owner/Admin while the intended UI/business rule was Owner-only.

That was corrected.

Final rule:

**Laptop Name backend = Owner-only.**

Validated:

`LAPTOP_NAME_OWNER_ONLY=PASS`

Do not change this back without explicit Owner decision.

## 6. Dashboard implementation completed

Dashboard TypeScript contracts now expose:

`DeviceTeacherInfo`

Device:

`usualTeachers: DeviceTeacherInfo[]`

Session:

`laptopName: string`

`usualTeachers: DeviceTeacherInfo[]`

### Devices page

Devices page now separates:

- Actual Device
- Laptop Name
- Usual Teachers
- Status
- Agent
- Last Seen
- Owner Agent update action

Laptop Name edit:

Owner only.

Usual Teachers management:

Owner + Admin.

Usual Teachers editor uses existing Teacher records by ID, not free text.

Multiple teachers can be selected.

Selected teachers display as chips/tags.

Teacher renames therefore automatically flow through by ID.

### Schedules

Schedules continue to select laptops independently from teachers.

Laptop selector uses friendly Laptop Name.

Usual Teachers are shown as subtle informational context:

`Usually: Umar, Huzaifa, Anees`

No validation or restriction is attached to this text.

Existing weekly recurrence behavior remains unchanged:

Schedules remain active weekly until edited/deleted.

### Sessions

Session creation changed from technical `Device` wording to:

`Laptop`

Selector displays friendly Laptop Name instead of `DESKTOP-...`.

Session DTO preserves:

- real technical `DeviceName`
- friendly `LaptopName`
- current informational `UsualTeachers`

Recorded Sessions table now displays Laptop information.

Search includes:

- teacher
- student
- course
- Laptop Name
- technical Device Name
- Usual Teacher names

Mobile Sessions table was shifted correctly from 9 to 10 columns:

1. Teacher
2. Student
3. Course
4. Laptop
5. Started
6. Session
7. Teacher Attendance
8. Student Attendance
9. Review
10. Actions

Validated:

`SESSION_MOBILE_LAPTOP_LABEL=PASS`

`SESSION_MOBILE_REVIEW_LABEL=PASS`

`SESSION_MOBILE_ACTION_COLUMN_10=PASS`

## 7. Validation already completed — do not repeat without reason

Full feature audits passed.

Backend:

- `BACKEND_BUILD=PASS`
- many-to-many model PASS
- duplicate mapping guard PASS
- teacher existence validation PASS
- clear-all assignments PASS
- friendly Laptop projection PASS
- Usual Teachers projection PASS
- Owner-only Laptop Name backend PASS
- Owner/Admin Usual Teachers PASS
- hidden-device backend guard PASS

Dashboard:

- `DASHBOARD_LINT_ZERO_WARNINGS=PASS`
- `DASHBOARD_BUILD=PASS`
- Devices Usual Teachers UI PASS
- Schedule Laptop info PASS
- Session friendly Laptop UI PASS
- Session Usual Teachers info PASS
- Session mobile alignment PASS
- API TypeScript contract PASS

Git:

- exact feature file scope PASS
- `git diff --check` PASS

Migration:

- additive-only PASS
- unique mapping PASS
- snapshot PASS
- SQL generation PASS

There is one known unrelated/pre-existing backend compiler warning:

`CS8321 TryGetSessionIdFromRoomName is declared but never used`

Do not derail this feature to clean that warning unless requested.

## 8. Files belonging to the completed feature

Backend:

- `src/Backend/Academy.Api/Program.cs`
- `src/Backend/Academy.Application/Abstractions/IDeviceTeacherAssignmentRepository.cs`
- `src/Backend/Academy.Application/Contracts/DeviceListItem.cs`
- `src/Backend/Academy.Application/Contracts/DeviceTeacherInfoDto.cs`
- `src/Backend/Academy.Application/Contracts/SessionDto.cs`
- `src/Backend/Academy.Application/Contracts/SetDeviceTeachersRequest.cs`
- `src/Backend/Academy.Application/Services/DashboardQueryService.cs`
- `src/Backend/Academy.Application/Services/DeviceTeacherAssignmentService.cs`
- `src/Backend/Academy.Domain/Entities/DeviceTeacherAssignment.cs`
- `src/Backend/Academy.Infrastructure/DependencyInjection/InfrastructureServiceRegistration.cs`
- `src/Backend/Academy.Infrastructure/Migrations/20260904212848_AddDeviceTeacherAssignments.cs`
- `src/Backend/Academy.Infrastructure/Migrations/20260904212848_AddDeviceTeacherAssignments.Designer.cs`
- `src/Backend/Academy.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `src/Backend/Academy.Infrastructure/Persistence/AppDbContext.cs`
- `src/Backend/Academy.Infrastructure/Repositories/DeviceTeacherAssignmentRepository.cs`

Dashboard:

- `src/Dashboard/academy-dashboard/src/app/devices/page.tsx`
- `src/Dashboard/academy-dashboard/src/app/globals.css`
- `src/Dashboard/academy-dashboard/src/app/schedules/page.tsx`
- `src/Dashboard/academy-dashboard/src/app/sessions/page.tsx`
- `src/Dashboard/academy-dashboard/src/lib/api.ts`
- `src/Dashboard/academy-dashboard/src/types/index.ts`

No Agent source, recording subsystem, attendance subsystem, or Docker infrastructure was intentionally changed for this feature.

## 9. VPS / deployment state

VPS:

`158.220.90.195`

Application root:

`/opt/homequranlearning`

Production compose files MUST be used together:

`infrastructure/docker/docker-compose.prod.yml`

and:

`infrastructure/docker/docker-compose.relay-production.yml`

Use the existing production env file.

Important custom Caddy file must remain unchanged.

Expected tracked custom Caddy SHA:

`280dfe2cf855e4be0029c36fe992ae4505dd393f50b25717aa25761447338ac1`

Do not recreate/restart unrelated containers.

Current deployed application baseline before this feature:

`a8645363b127905c7702ea7a7477ce93e5c9b65e`

The Usual Teachers feature is NOT considered VPS deployed merely because this handoff command commits/pushes it.

## 10. Exact NEXT engineering task

After opening the new AI/chat:

1. Read `AGENTS.md`.
2. Read `docs/PROJECT-STATE.md`.
3. Run/read:
   - `git status --short`
   - `git log -1 --oneline`
   - local/remote branch SHA
4. If commit `feat: add laptop usual teacher assignments` exists and worktree is clean, do NOT rewrite the feature.
5. Perform a targeted VPS deployment of ONLY:
   - API
   - Dashboard
6. Use BOTH production compose files plus production env.
7. Preserve custom Caddy.
8. Do not restart LiveKit, Ingress, MediaMTX, recording workers, MinIO, PostgreSQL, Redis, Agent infrastructure, or other unrelated services unless actual evidence requires it.
9. API startup should apply the new EF migration through the normal existing application migration mechanism.
10. Prove migration/table availability after deployment.
11. Runtime-check:
    - API health
    - Dashboard login
    - `/devices`
    - `/schedules`
    - `/sessions`
    - `/icon.png`
12. Verify unauthenticated Usual Teachers endpoint is protected.
13. Prefer an authenticated Owner/Admin UI test for saving multiple Usual Teachers when safe credentials/session are available.
14. Verify Laptop Name remains Owner-only.
15. Confirm API/dashboard restart counts and that non-target container IDs remained untouched.

Do not jump to unrelated roadmap work until this feature is deployed/runtime-certified or the Owner explicitly changes priority.

## 11. Existing proven runtime architecture — preserve

Stack:

- ASP.NET Core .NET 10
- PostgreSQL
- Redis
- MinIO
- Next.js / TypeScript
- Windows .NET Agent
- FFmpeg
- NAudio / WASAPI
- LiveKit
- LiveKit Ingress
- MediaMTX

Live path:

Windows Agent H264/AAC RTMP
-> MediaMTX
-> LiveKit Ingress
-> LiveKit
-> Dashboard WebRTC

Known-good live monitoring baseline is already validated.

Audio roughly 1–2 seconds.

Video roughly 4–5 seconds.

Teacher cursor/student-reading context alignment accepted.

Locked status:

`LIVE_AUDIO_LATENCY=ACCEPTABLE`

`LIVE_VIDEO_LATENCY=ACCEPTABLE`

`AUDIO_VIDEO_CONTEXT_SYNC=PASS`

Do not optimize this path again without a demonstrated regression.

Zoom dashboard-side stutter investigation is paused. Actual Zoom call audio on Laptop5 was normal; issue was dashboard monitor-side. Do not restart that investigation unless Owner returns to it.

## 12. Agent release / rollout safety

Immutable generic Agent release remains:

Version:

`1.0.0-b043352365aa-resume1`

Release ID:

`resume-b043352365aa-1`

SHA256:

`872DDB40281A73DADE36FCA336C5A10EC1D70B994771B7605723CD82DE7CC5E1`

Do not overwrite or rebuild this release unless Agent source changes and Owner explicitly proceeds with a new release.

Owner durable device ID:

`82f9b22d-2d5b-46b2-b372-ef864219e383`

Known teacher durable IDs:

Laptop8:

`67e170d4-47b3-42d7-8833-61a0d9886154`

Qaisar:

`cf30f945-2048-4cc2-84bc-91907aa5904b`

Laptop5:

`8fa05fc9-c72c-494c-a2d0-ff622e7ead77`

Do not start a broad Agent rollout during the Usual Teachers deployment.

## 13. Recording infrastructure state

Recording infrastructure was repaired and is currently considered healthy.

Permanent fixes already exist for:

- archive registrar API key injection
- direct Agent uploads over old 30 MB Kestrel default cap
- 128 MiB per-request upload allowance

Do not redeploy/re-debug recording infrastructure as part of Usual Teachers work.

Owner local Agent config previously proved:

`Recording.Enabled=False`

Owner current recording pipeline is therefore server/archive side.

A separate future task remains:

**Owner VPS old recording cleanup to free storage.**

Do not mix that destructive cleanup into the Usual Teachers deployment.

When that cleanup is eventually resumed, restrict it to the exact Owner durable-device prefixes and preserve DB deletion semantics.

## 14. Existing dashboard baseline before this feature

Prior deployed dashboard baseline already included:

- commercial responsive layout
- branded modal/dialog feedback
- mobile Teacher/Student/Course action fixes
- Schedule edit/delete mobile cards
- Session Evidence/Review clickable fixes
- QA Rule Delete only
- round transparent academy favicon
- Owner recording delete UI
- weekly Schedule semantics

User visually confirmed the prior dashboard state as correct.

Do not regress those behaviors.

## 15. Production philosophy

Do not move to a final public-production posture until the local product concept is sufficiently complete and stable according to Owner priorities.

The VPS is currently used as the shared staging/pilot/release backend.

Targeted deployments are allowed as part of validating completed features.

Never interpret “not final production yet” as a reason to avoid necessary VPS validation.

## 16. If the one-shot commit/push below did not complete

The source and these handoff documents are still the authoritative local state.

Do NOT reset, checkout, clean, or discard the worktree.

Inspect:

`git status --short`

If feature files are still modified/untracked, preserve them.

Re-run build/audit only if needed, then commit the existing feature instead of reimplementing it.

If GitHub push failed only because of network/authentication, do not redo source changes; push the existing local commit when connectivity is available.

<!-- HQL_CURRENT_HANDOFF_END -->

# HomeQuranLearning.QA Working Rules

These rules exist to make engineering faster and more reliable, not to create ceremony.
Use the minimum investigation, verification and process needed for the actual change.

## 1. Authority and continuation

- `docs/architecture/classroom-monitoring-product-contract.md` is the highest authority for intended classroom-monitoring product behavior.
- Current Git source, current runtime evidence and the top CURRENT ACTIVE STATE in docs/PROJECT-STATE.md are authoritative for implementation/runtime state.
- If source, tests, historical docs or earlier decisions conflict with the Product Contract, treat the conflicting implementation as technical debt rather than silently redefining the product.
- Chat history is supporting context only.
- A new AI/session must continue from the latest verified state instead of restarting old investigations.
- Read older historical project-state sections only when they are directly relevant to the current task.
- The user's latest clear decision is the default product direction. Do not repeatedly ask for the same confirmation.
- Do not blindly agree with a technically incorrect decision. Briefly explain the issue and improve the approach.
- Ask a question only when required information cannot be resolved from source/runtime, or a genuinely high-impact irreversible action needs approval.

## 2. User operating model

- The user can copy/paste commands but should not be asked to manually find, replace or edit source files.
- Give complete copy/paste-safe commands with exact paths.
- Clearly separate Windows PowerShell commands from VPS SSH/Bash commands.
- Never mix shell syntaxes.
- Do not ask the user to manually type API keys, passwords, tokens or other secrets when they already exist in approved local/server storage.
- Read required secrets programmatically from existing secure configuration when necessary, use them without printing them, and clear temporary plaintext values.
- Never print or commit secrets.
- Keep an existing VPS SSH/root session open unless the user explicitly asks to disconnect.
- Do not promise background work or future asynchronous completion.

## 3. Fast engineering loop

For a normal small or medium fix:

1. inspect only the exact relevant source/state;
2. identify the failing boundary;
3. make the smallest production-quality fix;
4. run the smallest meaningful targeted verification;
5. if green, continue;
6. update PROJECT-STATE only at a meaningful checkpoint;
7. stage exact intended files, commit and push the feature branch.

Rules:

- Once enough evidence identifies the root cause, stop diagnosing and fix it.
- Do not run broad diagnostics just because they are available.
- Do not repeat a verification that already passed unless subsequent changes could invalidate it.
- Do not run the full solution test suite for a narrowly isolated fix unless the change can realistically affect the wider system.
- Cross-cutting architecture, database, auth, deployment or release changes may justify broader tests.
- Distinguish a command/script/harness error from a product defect quickly.
- If a mutation script partially succeeds and then fails, continue from the actual current state. Do not blindly rerun the whole mutation block.
- After a simple error, fix it and move forward.
- Prefer source-first investigation over speculative runtime probing.
- Do not create ceremonial GO/APPROVE checkpoints for ordinary development.

## 4. Files and documentation

- Do not create extra status files, handoff files, probe files or scripts unless they provide real ongoing value.
- Temporary build/probe material should live outside the tracked repo or be removed after use.
- Do not accumulate obsolete installers, dumps or diagnostic artifacts in tracked source.
- docs/PROJECT-STATE.md is a concise recovery checkpoint, not a command-by-command diary.
- Keep current state in the form: Completed -> Proof -> Current -> Next.
- Historical sections may remain for evidence but must not impose obsolete workflow rules.

## 5. Git workflow

- Preserve unrelated human work.
- Never reset, clean, stash, overwrite or discard unexpected work just to simplify the current task.
- Do not use git add -A.
- Stage exact intended files.
- Normal verified development commits and pushes to the current feature branch do not need separate approval.
- Do not use force push, force-with-lease, hard reset, clean or history rewriting without explicit approval.
- main is the canonical branch, but feature work may remain on a feature branch until a deliberate integration decision.

## 6. Verification policy

- Small isolated code fix: targeted build/test only.
- UI-only fix: relevant lint/build or focused browser check only when needed.
- Agent-only fix: relevant Agent test/build only.
- API-only fix: relevant backend tests/build only.
- Database/schema change: migration plus affected backend verification.
- Production deployment: verify the affected deployed services, not unrelated infrastructure.
- Never fake green or weaken a meaningful assertion to get a pass.

## 7. Production and approval boundary

Explicit user approval is required only for genuinely high-impact actions such as:

- production/VPS deployment or cutover;
- destructive production database or real evidence changes;
- secret rotation;
- production auth/RBAC/security-policy changes;
- firewall or driver changes;
- replacing the proven live/recording transport architecture;
- destructive Git/history operations;
- a main-branch merge/release when it changes production deployment state.

Do not invent approval gates for ordinary fixes, documentation, targeted tests, feature-branch commits or normal pushes.

## 8. Protected HomeQuranLearning invariants

- Managed classroom Agents communicate outbound for normal production operation.
- Durable Agent DeviceId is the machine identity; editable friendly laptop names are user-facing labels; Windows computer names are not durable targeting identity.
- Classroom audio must follow the teacher communication application's effective microphone and playback/render endpoints. USB, Bluetooth, wired and internal/Realtek endpoints are all valid when they are the routes actually used by Teams/Zoom.
- Do not capture unrelated microphones or playback endpoints. If an effective communication route is temporarily unavailable, report the route as unavailable and recover automatically when the communication application exposes a valid route.
- Preserve the proven live/recording media path unless an actual defect requires a scoped change.
- Dashboard live feeds remain muted by default and only one selected feed should be audible at a time.
- Owner-controlled Agent updates target the selected durable device. Audio transport type must not be an installation or update gate.
- Do not reopen already proven updater/live/audio investigations without new evidence of a regression.

## 9. Owner Control Panel

- The correct product name is Owner Control Panel.
- Owner Control Panel is deferred until the final product phase, after the main operational system is otherwise ready.
- Existing older Owner Control Plane documentation is historical/reference material only.
- Do not treat Owner Control Panel as an active dependency, roadmap gate or required current workstream unless the user explicitly starts that final phase.

## 10. Response and execution style

- Be concise and implementation-focused.
- Prefer one reliable command block over many tiny manual steps.
- Do not waste time proving obvious facts repeatedly.
- Surface a discovered blocker once, fix it, and continue.
- If the user proposes a weaker solution, improve it rather than merely accepting it.
- If the user is mistaken, correct the technical point respectfully and proceed with the better implementation when the intent is clear.
- Optimize for: correctness, continuity, minimum manual effort and minimum wasted time.

## Local development runtime

- The user's normal interaction is one original Windows PowerShell terminal only.
- Do not ask the user to open or manage separate API, Dashboard or Agent PowerShell windows.
- API, Dashboard and DEV Agent must run as background processes with logs under the local development runtime.
- Before any task that requires the local application, the AI should include .dev-runtime/LocalDevelopment.ps1 -Action Ensure in its own copy/paste command when needed.
- Ensure is idempotent: use it to start only missing local components instead of restarting healthy components.
- During work, use .dev-runtime/LocalDevelopment.ps1 -Action Status only when runtime state is materially relevant; do not repeatedly check it without reason.
- When local services are no longer needed, the AI should include .dev-runtime/LocalDevelopment.ps1 -Action Stop itself when stopping them provides a benefit.
- Do not make the user remember routine start/stop commands.
- Do not stop a healthy local runtime between consecutive development steps when the next step still needs it.
- Do not restart API, Dashboard, Agent, Docker or other healthy local infrastructure merely as a verification ritual.
- If one local component fails, repair or restart only that affected boundary whenever possible.
- Local DEV Agent identity is separate from the production Owner device identity.
- Local development must target local API/RTMP infrastructure and must not accidentally send Owner development traffic to the VPS.
- Local DEV recordings are disposable and recording remains off by default unless a specific test requires recording.
- Optional same-Wi-Fi testing may use the Owner PC LAN address; another laptop's localhost must never be treated as the Owner PC.
- The production Owner device is the primary reusable VPS-connected physical/runtime canary when real installed-Agent, Windows, Teams/Zoom, audio-routing or remote-update behavior must be validated; it is not the normal source-development runtime.
- The user should normally only need to copy/paste the complete command block supplied by the AI into the original terminal.

## Classroom communication audio invariant

- The Agent captures the effective microphone/input endpoint and effective playback/render endpoint used by the teacher's Teams/Zoom communication route.
- Device transport and brand are irrelevant: USB, Bluetooth, wired and internal/Realtek are valid when actually used by the communication application.
- Do not capture unrelated active microphones or speakers.
- If Teams/Zoom uses Windows Default or Default Communications, resolve the effective endpoint behind that selection.
- Route changes during a call must recover automatically without reinstall.
- There is no separate student-device endpoint to discover; remote/student speech is the audio arriving on the teacher's communication playback route.
- Read `docs/architecture/classroom-monitoring-product-contract.md` before changing classroom audio, attendance, recording or QA behavior.
## Classroom media priority

- Classroom media priority is audio first. Teacher and student speech must receive the lowest practical latency and continuous delivery; video quality is secondary. Normal live monitoring and Agent recordings target approximately 240p at low frame rate/bitrate to reduce teacher-laptop CPU, network bandwidth and storage/VPS load.

## 11. Owner-first evidence-driven development

HomeQuranLearning.QA is still in active development/trial. The Owner device is the primary physical/runtime canary for Agent behavior before teacher-laptop rollout.

For runtime-affecting work, AI engineers must:

1. Inspect the exact affected source, machine, runtime, path, process, service or data boundary.
2. Never guess facts that can be inspected directly.
3. Reproduce or isolate the actual failing boundary and test that boundary directly.
4. Make the smallest production-quality source fix.
5. Run only targeted verification that can meaningfully validate the change.
6. Validate physical/runtime behavior on the Owner device in the real intended scenario when applicable.
7. Confirm the result behaves exactly as the Owner requested.
8. If it fails, continue from the next unproven boundary; do not reopen already-proven boundaries without contradictory evidence.
9. After conclusive Owner-device proof, treat that behavior as the baseline unless a later change could invalidate it.
10. Only after Owner confirmation should the approved release be deployed and rolled out to teacher laptops.

Mandatory engineering principles:

- Use the evidence-driven sequence: inspect -> isolate -> direct probe -> prove boundary -> next boundary -> root cause -> source fix -> Owner physical validation -> controlled rollout.
- The successful classroom-audio debugging sequence is the model for difficult runtime investigation.
- Do not create extra worktrees, staging directories, probe layers, duplicate packages or verification gates unless they solve a concrete isolation, safety or reproducibility need.
- Do not repeat conclusive verification merely for reassurance.
- Teacher laptops being registered or already used does not turn every development change into a fleet rollout.
- Do not deploy an unproven Agent change across teacher laptops merely because remote update capability exists.
- After Owner confirmation, build from the verified commit, update only VPS services that actually changed, publish through the existing release mechanism, then use per-device Owner-controlled remote updates.
- Inspect exact installed paths, runtime configuration and live service state before acting; do not rely on assumed or historical layouts when the current machine can answer directly.
- Verification exists to establish correctness, not to create ceremony. More verification layers are not automatically better engineering.
- This rule applies to ChatGPT, Codex, Gemini, DeepSeek, Claude and any other AI engineer working on this repository.
## 12. Verified Agent release and selective rollout

The normal Agent delivery model is Owner-canary first, then reuse of the exact verified release on selected academy laptops.

Mandatory rules:

- An Agent source change that affects installed runtime behavior gets one new versioned reusable Agent release after source verification.
- Validate that new Agent version on the production Owner device first when physical/runtime proof is required.
- After Owner physical confirmation, the exact verified installer/package bytes, version and SHA become the approved release artifact for that change.
- Do not rebuild a separate installer for Laptop 5, Laptop 8, Qaisar Laptop or any other teacher laptop when deploying the same verified Agent version.
- The Agent installer/package must remain generic and reusable across authorized academy laptops. Do not hard-code Owner-only capability restrictions into the installer.
- Manifest `targetDeviceIds` or equivalent targeting is rollout metadata only. It controls which devices may receive a release; it must not make the installer itself device-bound.
- Expanding rollout to another laptop should reuse the same verified package and hash and change only the necessary per-device rollout/update metadata.
- The Owner/Admin-controlled dashboard `Update Now` path is the normal post-canary delivery mechanism for already-compatible installed Agents.
- The Owner chooses which laptops receive an approved release and when. Do not force an automatic fleet-wide rollout merely because a release exists.
- A teacher laptop should not require a manual installer run for every routine bug fix or feature update.
- Manual installer/bootstrap is an exception for a fresh installation, an incompatible legacy updater, a broken updater, or an explicitly diagnosed recovery boundary.
- A future Agent code change requires a new versioned release, but once that version is Owner-verified, the same release is reused for every selected target laptop.
- Backend/dashboard-only changes that do not modify Agent binaries do not require an Agent release.
- Do not introduce temporary Owner-only packages, per-laptop rebuilds, artificial staging barriers or duplicate release artifacts unless a concrete technical incompatibility requires them.
- Preserve release immutability: a release ID/version must not silently point to different package bytes. Use a new version/release for changed Agent code.
- Future AI engineers must preserve this model: source fix -> targeted verification -> Owner canary -> immutable reusable release -> selective dashboard rollout.

In short: Owner verification is the quality gate, not a permanent deployment restriction. Once an Agent version is proven on Owner, that exact verified release is the normal artifact used to update any other selected compatible academy laptop.
## 13. Product-intent-first AI engineering

The Owner decides product behavior. AI engineers improve the implementation of that intent; they must not silently invent product restrictions, availability limits or workflow gates.

Mandatory rules:

- The Owner/user decides product behavior, operating policy, rollout behavior and acceptable product constraints.
- Do not independently add restrictions such as battery-only execution limits, arbitrary request-expiry windows, device-only capability restrictions, artificial rollout gates or similar behavior unless the Owner explicitly requested them or a concrete unavoidable technical/security requirement proves they are necessary.
- When an optional restriction or guardrail could be useful, explain its benefit, downside and engineering recommendation before making it part of product behavior. Let the Owner decide.
- Improve raw product ideas into robust engineering. For example, "continue when Internet returns" should be translated into durable intent, resumable transfer, integrity verification, retry/reboot recovery and idempotent completion where appropriate.
- Routine technical integrity does not require separate product approval. Engineers should automatically preserve safeguards such as package/hash verification, secret protection, managed-path boundaries, corruption prevention, atomic state changes, validation, bounded error handling and auditability when they do not redefine requested product behavior.
- Do not create extra architecture layers, temporary workarounds, artificial gates, probe packages, per-device binaries, duplicate staging paths or redundant validation merely because they appear safer. They require a concrete evidence-based engineering purpose.
- Owner-first remains a canary-validation model, not Owner-only architecture or capability restriction.
- Do not merely agree with a weak technical implementation. Preserve the Owner's intent, explain meaningful trade-offs and propose the stronger implementation.
- If a genuinely non-optional security, platform, legal or technical constraint conflicts with requested behavior, explain the exact constraint and use the smallest restriction that satisfies it rather than silently changing the product.
- Prefer automation, recovery and reduced manual work when they preserve the Owner's intended control.
- This rule applies to ChatGPT, Codex, Gemini, DeepSeek, Claude and every future AI engineer working on this repository.

Core principle:

`User intent first. AI improves implementation; AI does not invent product restrictions.`


<!-- HQL_LESSON_EVIDENCE_IMAGE_OR_TEXT_20260906_BEGIN -->
## Final lesson evidence contract - 2026-09-06

LessonShared is OR-based, not pairing-based.

Primary evidence:
- an outgoing lesson page/image in the correct scheduled student's
  Teams chat during the class window or ten-minute lesson grace
  immediately counts as LessonShared;
- no lesson text is required with the image;
- filename and extension are not attendance semantics.

Fallback evidence:
- if no image/page is sent, recognized outgoing lesson wording can
  independently count as LessonShared;
- accepted academy terms include Para/Parah/Sipara, Juz, Surah,
  Ayah/Verse, Line, Page, Lesson, Sabaq/Sabak, Qaida/Qaidah,
  Nazra, Ruku/Rukoo, Tajweed, Hifz, Manzil, Sabaqi/Sabqi,
  Revision and Makhraj/Makharij;
- generic ok/done/good messages and emoji alone do not count.

Correct scheduled-student chat ownership is mandatory.

LessonShared immediately resolves Teacher Present + Student Present
+ AutoResolved. Grace waits only while LessonShared is absent.

Call lifecycle scanning is independent of Teams chat-document
visibility because call controls can live in another Teams WebView.

Backend attendance logic, dashboard, scheduler, canonical audio,
Live transport, Recording, QA and database schema are unchanged.

Runtime certification remains required on the Owner Agent.
<!-- HQL_LESSON_EVIDENCE_IMAGE_OR_TEXT_20260906_END -->


<!-- HQL_LESSON_IMAGE_TEAMS_OR_SIGNAL_20260906_BEGIN -->
## Teams lesson-image detection - 2026-09-06

For the correct scheduled student's outgoing Teams message during the
class/lesson-grace evidence window, either Teams media signal is
sufficient to prove a shared lesson page:

- `attachments-<messageId>` attachment container; OR
- a descendant UI Automation `Image` control.

The two signals are not required together.

Once either signal is present, the message is treated as lesson-image
evidence. Image filename, extension and OCR/content recognition are not
attendance semantics.

Recognized lesson text remains an independent fallback when no image
is shared.

Backend attendance, scheduler, canonical audio, Live, Recording,
dashboard, QA and database schema are unchanged.
<!-- HQL_LESSON_IMAGE_TEAMS_OR_SIGNAL_20260906_END -->


<!-- HQL_TEAMS_MESSAGE_CARD_MEDIA_20260906_BEGIN -->
## Teams lesson media DOM/UIA shape - 2026-09-06

Runtime canary proved that valid Teams Quran/Qaida page media may not
be exposed as a descendant of `message-body-<id>`.

Final detection therefore accepts either:

- exact `attachments-<messageId>` anywhere inside the already-bound
  scheduled student's Teams chat tree; or
- an Image UIA element inside the outgoing message body or its
  surrounding outgoing `ChatMyMessage` card.

No OCR is used. Image content, filename and extension are irrelevant.
Backend attendance/grace/audio/dashboard behavior is unchanged.

Runtime re-certification pending.
<!-- HQL_TEAMS_MESSAGE_CARD_MEDIA_20260906_END -->

## HQL_TEAMS_STUDENT_NAME_GATE_REMOVED_20260907

- Temporary attendance stabilization decision: the Windows Teams detector no longer requires Academy Student.FullName to match the visible Microsoft Teams chat/account name.
- The currently scheduled laptop/session remains the attendance ownership boundary.
- Active Teams document selection is structural rather than student-name based.
- Connected-call detection uses Teams calling controls plus microphone control rather than the scheduled student's display name.
- LessonShared semantics are unchanged: an outgoing lesson/page image alone OR recognized lesson text remains sufficient.
- Backend, database, dashboard, audio pipeline, live streaming, QA and recording are unchanged.
- Cross-device Laptop 5 -> Laptop 7 portable attendance and explicit TeamsChatIdentity scheduling are deferred to a later phase.
- This milestone is not runtime-certified until the new Agent build is installed on a classroom laptop.

## HQL_OWNER_NAMEGATE_AUTOUPDATE_CANARY_20260907

- Owner automatic Agent update transport is runtime-proven on device 82f9b22d-2d5b-46b2-b372-ef864219e383 (Abdul Wahid / DESKTOP-PUFUU3U).
- Agent source commit: c3b10ffd04a39addc75c9358dc3eda906a42f667.
- Installed Agent version: 1.0.0-c3b10ffd04a3-namegate1.
- Dashboard Update Now successfully queued the Owner device and AgentAutoUpdate completed with UPDATE_SUCCESS.
- Installer completed with INSTALL_SUCCESS.
- Runtime architecture clarification: Classroom Agent is launched by Scheduled Task HomeQuranLearning.ClassroomAgent, not by a Windows Service.
- Teams attendance helper is launched by Scheduled Task AcademyAgent.TeamsHelper.
- Both Academy.Agent.Service.exe and Academy.Agent.TeamsHelper.exe remained running through the post-update stability check.
- Recording.Enabled=False and LiveStreaming.Enabled=True after update.
- Student-name to Teams-name attendance gate is removed on the Owner device.
- Actual lesson/image attendance behavior still requires one scheduled-class runtime canary.
- Other academy laptops were not updated in this canary.
- Cross-device attendance remains deferred.

## HQL_GLOBAL_APPROVED_AGENT_RELEASE_20260907

Owner-approved Agent release policy:

- Per-device Agent release allowlists are not part of the product.
- TargetDeviceIds has been removed from the Agent release generator and manifest model.
- The Owner laptop is used only as the first canary/test device.
- After the Owner canary passes, that Agent release is approved for all managed academy laptops.
- Existing academy laptops may use Update Now to install the approved release.
- New academy laptops may use the approved installer.
- PendingAgentUpdateVersion only records an explicit Update Now request for a laptop; it is not a release allowlist.
- No new whitelist, device eligibility restriction, rollout restriction, or similar product limitation may be introduced without explicit Owner approval first.

Runtime state:

- Approved Agent version: 1.0.0-c3b10ffd04a3-namegate1.
- Owner automatic updater passed.
- Student-name to Teams-name gate removal is installed and runtime-proven.
- Real scheduled-class lesson image detection passed.
- LessonShared became Yes.
- Teacher attendance resolved Present.
- Student attendance resolved Present.
- Attendance resolved AutoResolved.
- Call attempt, connected and ended lifecycle evidence was observed.
- This Agent version is approved for academy-wide use.
