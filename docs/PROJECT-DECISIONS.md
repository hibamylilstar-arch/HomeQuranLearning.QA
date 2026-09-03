# HomeQuranLearning.QA — Project Decisions

## Purpose and authority

Durable Owner product decisions that future implementation, tests, governance and project-state records must follow. Newer explicit Owner decisions supersede older conflicting statements.

## ATT-001 — LessonShared attendance semantics

Status: accepted 2026-08-27; supersedes the earlier teacher-only interpretation.

- A valid `LessonShared` event is strong attendance evidence for both teacher and student.
- It proves participation/presence for both participants.
- Its timestamp does not establish when the student joined and must not by itself mark the student Late.
- `StudentCallConnected` remains the stronger explicit student connection and arrival-time signal.
- `CallAttempted` remains teacher evidence only.
- `CallEnded` remains lifecycle/duration evidence only and does not independently prove attendance.

A valid `LessonShared` detector result continues to require all of the following:

- the exact scheduled student's Teams chat;
- an outgoing teacher message;
- lesson-related semantic text;
- an actual image in the same Teams message.

Image alone is insufficient. Keyword-only text without an image is insufficient. A filename or extension alone is not semantic evidence.

## QA-001 — Teacher-speech provenance and human-confirmed findings

Status: accepted 2026-08-28.

- Teacher speech used for QA must come from a separately attributable teacher
  microphone track. System/loopback or mixed audio alone is insufficient.
- Arabic Quran/Qaida recitation is excluded from QA-rule evaluation but remains
  present in the original recording and timeline.
- Urdu/Hindi, English and mixed lesson speech is evaluated using surrounding
  language and intent. An isolated ambiguous token such as `fee`/`fi` is not
  sufficient evidence.
- Teacher communication with a parent during class is prohibited.
- Communication with a student is limited to lesson teaching/correction,
  necessary class control and necessary technical continuity.
- Automation creates a QA candidate. Only an authorized human confirmation can
  turn that candidate into a QA alert/evidence finding.
- Candidate review exposes ten seconds before and ten seconds after the trigger
  and preserves the ability to inspect the complete recording at that offset.
- Missing teacher-audio provenance or coverage is an explicit coverage failure,
  never proof of compliant silence.

The detailed proposed design and implementation gates are in
`docs/architecture/teacher-audio-context-qa.md`.

## RET-001 — Owner-configurable recording retention and capacity

Status: accepted 2026-08-28.

- The current 200 GB estimate and three-day normal / seven-day confirmed-QA
  periods are planning defaults, not permanent product limits.
- The Owner Control Plane must allow authorized configuration of normal,
  pending-candidate and confirmed-QA retention as storage capacity changes.
- Before applying a change, the UI and backend expose projected storage use and
  safety headroom from measured recording volume.
- Retention changes are versioned and audited. A shorter policy must not cause
  immediate silent deletion; it requires a safe deferred impact evaluation.
- Capacity expansion must allow retention to increase without a product code
  change.

## PILOT-001 — Secured real-academy VPS pilot

Status: accepted 2026-08-29; supersedes synthetic-only direct-IP staging.

- The approved VPS pilot uses real academy sessions from a bounded cohort of
  four or five authorized teacher laptops.
- Domain registration remains deferred, but real credentials, Agent traffic,
  recordings and evidence require publicly trusted HTTPS for the VPS IPv4
  address. Plain HTTP and certificate-bypass behavior are prohibited.
- Access is restricted to exact approved public IPv4 `/32` sources during the
  pilot. Only the reverse proxy publishes host application ports.
- Rollout is one laptop first, then one additional laptop at a time after device
  identity, heartbeat, headset provenance, upload, playback, worker, scope and
  recovery proof.
- Authorized humans review candidates and sampled no-candidate windows. Report
  false positives and false negatives; do not claim production accuracy or
  create automatic disciplinary findings from the classifier.
- Pilot evidence is a secondary monitoring copy, not the academy's sole system
  of record.
- Automated retention/deletion of real recordings stays disabled until a
  separate high-risk approval follows a dry-run impact and preservation review.

## PROD-001 — Network-independent academy production

Status: accepted 2026-08-29; supersedes PILOT-001 where the earlier pilot network or cohort restrictions conflict.

- The target system is real academy production, not a permanently bounded synthetic or source-IP pilot.
- Academy-owned Classroom Agent laptops may connect from any Internet provider or public IP.
- A change of teacher-laptop network must not require VPS firewall or source-allowlist changes.
- Classroom Agent API traffic must use trusted HTTPS and application/device authentication rather than source IP as the permanent identity boundary.
- The dashboard must remain reachable from any Internet connection for authenticated Owner/Admin/authorized Manager users.
- Live monitoring uses HTTPS WHIP signalling and encrypted WebRTC media through the VPS.
- The first laptop remains a deployment canary for production verification, not a permanent product-capacity restriction.
- After initial installation, routine Agent upgrades and bug-fix releases must move toward centrally controlled VPS-driven update delivery without requiring manual reinstall on every laptop.
- Per-device revocable credentials supersede the shared fleet credential as the long-term production authentication model.
## AUDIO-001 — Effective communication routes

Status: accepted 2026-09-03; supersedes all earlier USB-only classroom-audio requirements.

- The teacher laptop is the classroom-audio point of observation.
- Teacher speech comes from the microphone/input endpoint effectively used by
  Teams/Zoom or the supported communication application.
- Remote/student speech comes from loopback of the playback/render endpoint
  effectively used by that communication application.
- USB, Bluetooth, wired and internal/Realtek endpoints are equally valid when
  they are the actual effective communication routes.
- The Agent must not additionally capture unrelated microphones or speakers.
- If the application uses Windows Default or Default Communications, resolve
  the effective endpoint behind that selection.
- Route changes during a call must recover automatically.
- The two sources form one canonical classroom conversation for Live,
  Recording and QA.

Detailed authority:
`docs/architecture/classroom-monitoring-product-contract.md`.

## ATT-002 — LessonShared is automatic attendance truth

Status: accepted 2026-09-03; supersedes ATT-001 and earlier multi-evidence attendance semantics where they conflict.

- A valid `LessonShared` event belonging to the correct scheduled
  session/student and valid class window marks both Teacher and Student
  `Present`.
- Generic audio activity, student-audio meters, communication-process
  detection, greeting detection, call attempts and speaker inference do not
  independently determine automatic attendance.
- Existing validation that ties `LessonShared` to the correct scheduled
  student/session must remain intact.
- Operational communication/process signals may temporarily remain for
  diagnostics during migration, but they are not attendance truth.

## QA-002 — Mixed classroom conversation is the QA source

Status: accepted 2026-09-03; supersedes QA-001 only where QA-001 requires a separately attributable verified-USB teacher track.

- QA/STT consumes the approved classroom conversation source.
- The source is teacher effective microphone plus teacher effective
  communication playback.
- Speaker identification and diarization are not core dependencies.
- QA may flag policy-relevant classroom conversation without first proving
  which speaker said it.
- Existing contextual multilingual analysis, evidence context, auditability
  and human-review mechanisms may remain where useful.
- USB provenance must not be a prerequisite for QA coverage.

## REC-001 — Capture once, fan out

Status: accepted 2026-09-03.

- Physical classroom audio is captured once.
- The canonical classroom source fans out independently to Live, Recording and
  QA/STT.
- Recording must migrate away from its duplicate physical system/microphone
  capture chain.
- Recording, upload recovery and QA must not block, stall or restart Live
  Monitoring.

## DEV-001 - Owner-first evidence-driven validation

Status: accepted 2026-09-03.

- The project is in active development/trial; the Owner device is the primary physical/runtime canary for new Agent behavior before teacher-laptop rollout.
- Local development remains the normal place to inspect and change source; the Owner device is used when the requirement depends on real Windows, Teams/Zoom, audio routing, installed-Agent behavior, VPS connectivity or another physical/runtime boundary.
- Engineers must inspect the exact affected source/runtime/path/service before changing it. Do not guess facts that can be directly inspected.
- Debug difficult runtime problems boundary-by-boundary: inspect, isolate, directly probe, prove, then move to the next unproven boundary.
- Make the smallest source fix that addresses the proven root cause and run targeted verification appropriate to that boundary.
- When physical behavior is involved, the change is not behaviorally proven until it works on the Owner device in the real intended scenario.
- A conclusively proven boundary becomes the baseline. Do not re-prove it unless later changes could invalidate that proof or contradictory evidence appears.
- Extra worktrees, staging directories, duplicate installers, probes and verification layers require a concrete engineering reason; they are not default ceremony.
- Teacher laptops are rollout targets after Owner validation, even if they are already registered in the system.
- After Owner confirmation, build from the verified commit, deploy only affected VPS components, publish the Agent release through the existing release store, then use per-device Owner-controlled remote updates.
- An unproven Agent change must not be pushed fleet-wide merely because remote update capability exists.
- This decision governs all AI engineers and development assistants working on this repository.

Sequence: exact inspection -> evidence -> smallest fix -> targeted verification -> Owner physical confirmation -> controlled VPS/release rollout -> teacher laptops.
## DEV-002 - Owner active-class remote update baseline

Status: accepted and physically proven 2026-09-03.

- Owner device `DESKTOP-PUFUU3U` was updated during a real active Microsoft Teams class from `1.0.0-0329c70810cc-b1a-ts1` to `1.0.0-c4cebb5546e8-ownerupdate1`.
- The installed legacy updater was first isolated as the only incompatible boundary: it still referenced `update-readiness.json` and invoked the installer with plain `--silent`.
- Only the updater script boundary was bootstrapped to the verified `c4cebb5` implementation before the physical update test; Agent, FFmpeg, Teams and recording runtime were not changed by that bootstrap.
- The dashboard Owner-controlled `Update Now` request correctly queued release `ownerupdate-c4cebb5546e8-1`.
- Package delivery was directly proven: authenticated manifest enabled the expected release, package endpoint returned HTTP 200 with `356237966` bytes, and body transfer succeeded.
- The managed update completed with `UPDATE_SUCCESS`; scheduled-task result was `0`.
- Microsoft Teams stayed connected throughout the update and the pre-update Teams processes survived.
- Only managed Agent components restarted as intended: Agent service, Teams helper and managed FFmpeg.
- Existing runtime configuration was preserved: Recording remained disabled, Recording output remained `C:\ProgramData\AcademyAgent\Recordings-LiveOnlyTimestampTrial`, and Live Streaming remained enabled.
- After the managed restart, Live video reconnected and both teacher microphone audio and remote/student playback audio were physically confirmed working from a second device.
- This behavior is now the accepted Owner-device baseline. Do not re-prove this boundary unless later code changes could invalidate it or contradictory runtime evidence appears.
- Teacher-laptop rollout may use this proven release path only when the target laptop's installed updater is compatible with managed `--silent --update` semantics. Legacy updater state must not be assumed.

Baseline sequence: dashboard `Update Now` -> authenticated manifest/package -> SHA verification -> managed installer update -> Agent/helper/FFmpeg restart -> Teams survives -> Live reconnects -> classroom audio resumes.
