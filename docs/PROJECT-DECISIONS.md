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
