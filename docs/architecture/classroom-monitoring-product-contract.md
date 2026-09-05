# Classroom Monitoring Product Contract

## Status and authority

Status: AUTHORITATIVE OWNER PRODUCT CONTRACT

Accepted: 2026-09-03

This document defines the intended behavior of HomeQuranLearning.QA.

When implementation, tests, older architecture documents, historical project
decisions or previous AI assumptions conflict with this contract, this contract
wins unless the Owner explicitly changes it later.

Historical documents may remain for evidence, but they must not silently
override this contract.

---

## 1. Core viewpoint

The Classroom Agent monitors a class from the teacher laptop's actual
communication perspective.

For classroom audio, only two effective routes matter:

1. what the teacher is saying into the communication application;
2. what the teacher is hearing from the communication application.

There is no separate student-device or student-microphone endpoint for the
Agent to discover.

---

## 2. Live Monitoring audio source

For an active Teams, Zoom or supported communication class, Live Monitoring
uses exactly two effective audio inputs.

### 2.1 Teacher microphone

Capture the microphone/input endpoint that the teacher's communication
application is actually using.

This represents what the teacher is saying into the class.

### 2.2 Teacher playback

Capture by WASAPI loopback the playback/render endpoint that the teacher's
communication application is actually using.

This represents what the teacher is hearing from the remote student or other
remote class participant.

### 2.3 Mixed classroom conversation

The two sources are combined into one continuous classroom conversation:

Teacher effective microphone
+
Teacher effective communication playback
->
canonical classroom audio
->
Live Feed

The Agent does not need speaker identification to provide Live Monitoring.

---

## 3. Effective-route rule

Device transport or manufacturer is irrelevant.

Valid communication routes may include:

- USB headset;
- Bluetooth headset;
- 3.5 mm wired headset;
- Realtek/internal laptop microphone and speakers;
- another valid Windows audio endpoint.

The rule is not "prefer USB" and it is not "capture every Windows default
device."

The rule is:

Capture the effective microphone and playback endpoints being used by the
teacher's communication application.

If Teams/Zoom explicitly selects a microphone or speaker, capture those
effective endpoints.

If Teams/Zoom uses Windows Default or Default Communications, resolve the
Windows endpoints to which those choices currently map and capture those
effective endpoints.

If the teacher changes the microphone or speaker during a call, the Agent must
detect the route change and move capture to the new effective endpoints without
requiring reinstall.

---

## 4. Isolation from unrelated audio devices

The Agent must not simultaneously capture every available microphone or
speaker.

Example:

If Teams is using a USB headset, capture:

- the USB headset microphone used by Teams;
- the USB headset playback used by Teams.

Do not additionally capture:

- the laptop Realtek microphone;
- laptop speakers;
- an unrelated Bluetooth device;
- another inactive audio endpoint.

Likewise, if the home development machine uses a Bluetooth headset, the Agent
must capture the Bluetooth communication routes rather than rejecting them
because they are not USB.

This isolation prevents unrelated room audio and unrelated device audio from
entering the classroom Live Feed.

---

## 5. Audio capture architecture

Physical classroom audio is captured once.

The intended architecture is:

effective communication playback --\
                                      +--> canonical continuous audio --> sinks
effective teacher microphone -------/

The canonical classroom audio timeline is shared by independent consumers.

Consumers include:

- Live Monitoring;
- Recording;
- QA/STT.

A consumer must not create a second physical capture chain when the shared
classroom audio source is available.

Live Monitoring is the highest-priority sink and must not be blocked by
Recording, QA or upload/recovery work.

---

## 6. Live Monitoring non-goals

Live Monitoring does not require:

- USB physical-device verification;
- a particular headset brand or model;
- student endpoint discovery;
- student microphone discovery;
- teacher-vs-student speaker classification;
- speaker diarization;
- attendance inference;
- capturing all active Windows audio devices.

The Live Feed only needs to reproduce the classroom conversation available from
the teacher laptop's actual communication routes.

---

## 7. QA source and semantics

QA/STT consumes the same classroom conversation source used by the monitoring
architecture.

QA must not require a separately verified USB teacher track as a prerequisite
for analyzing classroom conversation.

The core QA question is whether policy-relevant conversation occurred in the
class.

Speaker identification or diarization may be added later as optional analytics,
but it is not a core dependency for detection.

Existing candidate review, context review, auditability and human review
mechanisms may continue unless separately changed by the Owner.

Arabic Quran/Qaida recitation handling and contextual multilingual analysis may
continue where useful, but they must operate on the approved classroom-audio
source rather than requiring USB teacher provenance.

---

## 8. Attendance source of truth

Attendance is independent from QA classification and speaker identification.

The attendance system works from the scheduled Session boundary.

### 8.1 Session activity window

Teacher/student activity evidence belongs only to the immutable scheduled
session window:

`ScheduledStartUtc -> ScheduledEndUtc`

At ScheduledEndUtc:

- the finished session's teacher-microphone activity stops accumulating;
- the finished session's communication-render activity stops accumulating;
- those activity totals are frozen for that session;
- the next scheduled session may start immediately even when the same Teams
  call remains connected.

A continuing Teams call does not merge two scheduled student sessions.

The same communication call may therefore span consecutive student sessions
while attendance evidence remains partitioned by scheduled session time.

### 8.2 Audio meaning

Attendance may use the already-approved shared classroom audio architecture.

For attendance evidence:

- meaningful activity on the effective teacher communication microphone is
  teacher-side participation evidence;
- meaningful activity on the effective communication playback/render route is
  remote/student-side participation evidence.

The system does not claim biometric speaker identity.

It does not need to prove the human identity of a voice.

It uses the scheduled class context plus the effective communication routes.

Raw noise level, hiss or an always-open endpoint must not by itself become
attendance evidence.

Attendance must use meaningful speech/activity evidence rather than simple
device-open state or raw audio presence.

No duplicate physical audio capture chain may be created for attendance.

The removed StudentAudioEvidenceWorker must not be restored.

### 8.3 LessonShared

LessonShared remains the strongest direct attendance evidence.

A valid LessonShared event associated with the correct session means:

- Teacher = Present
- Student = Present
- attendance may be automatically resolved.

Lesson delivery time is not arrival time and must never create Late status.

### 8.4 Lesson grace

The teacher SOP is to share the completed student's lesson at the session
boundary.

Normal expected behavior is within about five minutes.

The system provides a maximum lesson grace period of:

**10 minutes after ScheduledEndUtc**

This grace period is ONLY for LessonShared evidence.

It is not an extension of the class activity/audio window.

During that ten-minute grace:

- the completed session remains open only for lesson evidence;
- its mic/render activity remains frozen;
- the next scheduled session may run normally;
- the same Teams call may remain connected.

The system therefore maintains:

1. one operational Current Session;
2. at most the immediately previous completed session as a Lesson Grace Target,
   until its ten-minute deadline expires or its lesson is accepted.

There is no four-hour delayed reconciliation design.

There is no multi-hour historical lesson scan.

### 8.5 Lesson association

The lesson is associated with the scheduled session/student.

Teacher lesson messages normally include the student's saved name, for example:

`Ahmed Lesson - Para No ...`

The lesson detector may use the saved session student name as confirmation when
matching lesson evidence to the previous Lesson Grace Target.

Safe normalization may ignore:

- letter case;
- surrounding punctuation;
- harmless spacing differences.

The system must not use aggressive fuzzy guessing that could silently assign a
lesson to the wrong student.

If the student-name evidence is ambiguous or does not safely match, do not
guess.

### 8.6 Consecutive students / siblings

No sibling flag, family identifier, Teams Chat Name field or combined-student
session is required.

Each scheduled student keeps an independent Session.

Example:

- Student A: 06:00-06:30
- Student B: 06:30-07:00

At 06:30:

- Student A audio evidence freezes;
- Student A becomes the Lesson Grace Target until 06:40;
- Student B becomes Current Session immediately;
- the Teams call may continue without disconnecting.

If Student A's valid lesson is shared during the grace period, it belongs to
Student A's previous session while Student B's class continues.

At Student B's scheduled end, the same process applies to Student B.

### 8.7 Grace expiry attendance finalization

At the ten-minute lesson deadline:

If valid LessonShared exists:

- Teacher = Present
- Student = Present
- attendance = AutoResolved.

If LessonShared does not exist:

- `Lesson Shared = No` is visible as an SOP fact;
- attendance is finalized from the frozen scheduled-session activity evidence.

Teacher-side meaningful microphone activity:

- sufficient evidence -> Teacher Present;
- insufficient/uncertain evidence -> Teacher NeedsReview.

Remote/student-side meaningful communication-render activity:

- sufficient evidence -> Student Present;
- insufficient/uncertain evidence -> Student NeedsReview.

The automatic engine must not create Absent solely because evidence is missing.

The automatic engine must not create Late from lesson timing.

When both attendance sides are confidently Present from session activity,
attendance may be AutoResolved even when LessonShared is No.

When either side remains uncertain, attendance remains Pending/NeedsReview for
human review.

### 8.8 Separation of facts

The system must keep these concepts distinct:

- attendance result;
- session activity evidence;
- Lesson Shared Yes/No;
- lesson grace deadline;
- human review status.

A teacher may have genuine Present attendance while still failing the
LessonShared SOP.

That SOP failure must not falsely convert genuine attendance into Absent.


---

## 9. Recording

Recording consumes the shared canonical classroom conversation.

Recording must not own a duplicate physical system-output capture and duplicate
teacher-microphone capture once migration to the shared source is complete.

Recording lifecycle, storage, upload and recovery are independent sinks and
must not stall or restart Live Monitoring.

---

## 10. Sessions

Sessions and schedules identify:

- correct teacher;
- correct student;
- course;
- expected class window;
- lesson/activity ownership.

Session logic must not dictate which physical audio transport is allowed.

Live audio route selection follows the teacher's effective communication
endpoints, not attendance state.

---

## 11. Route observability

The Agent must expose enough non-secret diagnostics to prove which sources are
actually being captured.

At minimum, runtime logs/health should identify:

- effective communication application/process;
- selected render endpoint ID/name;
- selected capture endpoint ID/name;
- route change;
- capture available/unavailable state.

Logs must make it possible to prove whether Bluetooth, USB, wired or internal
audio was selected without guessing.

---

## 12. Implementation preservation rules

Preserve proven mechanisms unless they directly conflict with this contract.

Keep where valid:

- durable DeviceId semantics;
- shared classroom audio hub;
- canonical 48 kHz / 20 ms audio timeline;
- bounded nonblocking subscribers;
- LiveKit/RTMP live transport unless separately changed;
- screen capture path;
- session identity/history;
- recording authorization/storage contracts;
- candidate review/audit mechanisms;
- historical evidence readability.

Replace or remove mechanisms whose purpose is based on the old USB-only,
speaker-attribution or multi-evidence attendance assumptions.

---

## 13. Development order

Implementation order after this contract is accepted:

1. make communication audio routing transport-agnostic;
2. prove actual effective microphone + render endpoint capture;
3. prove Bluetooth development-machine behavior;
4. verify Live audio continuity and latency;
5. migrate Recording to shared classroom audio;
6. simplify attendance to LessonShared truth;
7. align QA with mixed classroom conversation;
8. only then continue broader production hardening.

Latency tuning must not be used to hide an incorrect audio-source selection.