<!-- HQL_CURRENT_HANDOFF_BEGIN -->

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
