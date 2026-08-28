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
