<!-- HQL_CURRENT_HANDOFF_BEGIN -->

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

<!-- CURRENT-ACTIVE-STATE:START -->
# CURRENT ACTIVE STATE — 2026-09-02

This section is the default recovery point for a new AI/session.
Older sections below are historical evidence and are not active workflow governance.

## Current source position

- Active development branch: `codex/local-development-mode`.
- Latest verified product source before this governance-only checkpoint: `b98a416dbc51a617a9673d741dc1c9f739a65575`.
- `b98a416` fixes heartbeat AgentVersion reporting by sending the installed deployment version instead of the legacy `0.1.0` fallback.
- Targeted Agent verification at that source: 56 / 56 PASS.
- `main` has intentionally not been advanced by the updater feature yet.

## Automatic updater

- Owner-controlled automatic Agent updater implementation is complete.
- Production API/Dashboard updater support was deployed and proven.
- Owner remote update completed successfully end-to-end.
- Package download, SHA256 validation, silent install and DeviceId preservation all passed.
- Test/proof release manifest was disabled after successful proof.
- Laptop 5 received the one-time updater-enabled USB bootstrap.
- Future teacher-laptop Agent updates are intended to use Dashboard -> selected friendly laptop -> Update Agent.
- Do not reopen updater diagnostics unless new evidence shows a regression.

## Live / recording / classroom audio

- Existing proven live and recording architecture remains protected and unchanged by the updater work.
- Teacher audio remains tied to the verified genuine USB headset path with no internal/Realtek microphone fallback.
- Continuous recording/live operation is normal and does not by itself block an Agent update.
- Actual communication microphone use is the final update installation safety gate.

## Working method

- Continue from current source/runtime rather than restarting old investigations.
- Small fixes use minimal source inspection plus targeted verification only.
- Do not repeat already-passed tests without an affected-code reason.
- User commands must be complete copy/paste blocks; no manual source editing or secret entry.
- PROJECT-STATE should remain concise and checkpoint-oriented.

## Owner Control Panel

- Correct name: Owner Control Panel.
- It is deferred to the final product phase after the main system is otherwise ready.
- Older Owner Control Plane material is historical/reference only and is not an active project gate.

## Next

- Automatic-updater phase is closed.
- Continue with the next requested product feature from this state.
- Do not spend time on Owner Control Panel until the user explicitly starts that final phase.

## Local development isolation

- Owner PC is being converted from a production classroom device into the local development workstation.
- Local Agent uses a separate `C:\ProgramData\AcademyAgent.Dev\device.json` identity and must not reuse the old production Owner DeviceId.
- Local Agent cloud traffic targets only `http://127.0.0.1:5100`.
- Local RTMP publishing targets only `rtmp://127.0.0.1:1935/live`.
- Local recording is disabled by default to avoid unnecessary test recording accumulation.
- Local API, Dashboard and DEV Agent are controlled from one .dev-runtime/LocalDevelopment.ps1 controller. They run as background processes with file logs and no separate visible PowerShell windows. Ensure starts only what is missing; Stop shuts the local runtime down.
- Installed production Agent tasks are disabled when Local Development Mode starts.
- Production Owner device will remain as a reusable dormant test device; after local runtime proof its production Agent, ingress, archive target and trial recordings will be disabled/cleared instead of deleting the Device record.
- Runtime ownership rule: the AI manages local Ensure, Status and Stop as part of its own copy/paste commands. The user normally works only in the original Windows PowerShell terminal and is not responsible for manually managing API, Dashboard or Agent processes.
- USB headset auto-selection fix completed locally: one verified physical USB headset may expose multiple logical Windows render/capture endpoints. Agent prefers Communications -> Multimedia -> Console -> deterministic fallback within that same physical USB device. Realtek/internal fallback remains forbidden; multiple physical USB headsets still fail closed. Laptop 5 physical verification remains pending until the teacher/headset is available; deployment target is the next Agent updater release.
- Permanent classroom audio invariant: USB headset selection is brand/model independent. A teacher may replace one USB headset with another; the Agent must auto-detect the newly available verified physical USB playback+microphone pair and recover without reinstall. No Logitech/vendor hard-coding and no Realtek/internal fallback.
- Audio-first media profile: live video 240p/5 FPS at 200k target and 250k max; recording 240p/5 FPS at max 250k with ultrafast H.264. Live and recording audio use small bounded queues, 20 ms continuity cadence, faster microphone recovery and 48 kHz mono speech output. Single connected verified USB headset remains auto-selected independent of brand.
- Dashboard UX checkpoint: live feed expanded view is mobile responsive and class/standby status stays below the audio control. Recording playback starts independently through byte-range media streaming, transcript UI/loading is removed, and Sessions hard-coded mojibake separators are cleaned.
<!-- CURRENT-ACTIVE-STATE:END -->
# HomeQuranLearning.QA — Project State

<!-- AGENT-AUTO-UPDATE-FOUNDATION-20260902 START -->
## Owner-controlled Agent automatic update — CLOSED / PRODUCTION PROVEN — 2026-09-02

Status: **CLOSED**.

Feature branch: `codex/agent-auto-update`

Latest updater/Agent source commit:
`b98a416dbc51a617a9673d741dc1c9f739a65575`
`fix(agent): report installed version in heartbeats`

Production API/Dashboard updater deployment source:
`98039f1aad8c9a32cf87bf257eef0cffcf99d8e5`

### Final owner-controlled behavior

- Owner selects a laptop from the Dashboard Device page by its editable friendly name.
- Dashboard sends the internal Device record ID; backend resolves the durable Agent DeviceId.
- Windows computer name is not used as update targeting identity.
- Only Owner can queue an Agent update.
- Pending update requests expire after 30 minutes.
- Updater Scheduled Task runs as LocalSystem every 1 minute.
- Continuous recording, always-on live streaming and idle Teams/Zoom process presence do not block an update.
- Actual communication microphone use is the final installation safety gate.
- Agent package download is authenticated over HTTPS and SHA256 verified before silent installation.
- Existing durable Device ID, DPAPI Agent credential, ProgramData and recordings are preserved.

### Production deployment proof

- Production API and Dashboard were deployed from `98039f1`.
- Database migration for pending Agent update requests completed successfully.
- Agent release storage is mounted read-only into the production API.
- LiveKit, LiveKit Ingress, MediaMTX relay, recording archive and Caddy were not recreated.
- Production health, Dashboard and migration checks passed.

### Owner remote-update proof

- Owner durable DeviceId: `82f9b22d-2d5b-46b2-b372-ef864219e383`.
- Owner friendly name: `Abdul Wahid`.
- Proof release: `ownerproof-98039f1aad8c-1`.
- Proof version: `1.0.0-98039f1aad8c-ownerproof1`.
- Proof package SHA256: `861106A1B1B96F86477800A83FAD038B9D62E578828A9B456D20C2F6E1332FEF`.
- Before Dashboard queue request, manifest response was `enabled:false`.
- After Owner queued the selected device, manifest response was `enabled:true` for that durable DeviceId.
- Public package endpoint returned HTTP 200.
- Windows Agent manifest lookup returned the expected release.
- `SAFE_TO_UPDATE=True` and `COMMUNICATION_MIC_IN_USE=False` before install.
- Remote package download: PASS.
- SHA256 verification: PASS.
- Silent install: PASS.
- Durable Device ID preservation: PASS.
- `UPDATE_START`: `2026-09-01T20:51:27.9524877Z`.
- `UPDATE_SUCCESS`: `2026-09-01T20:51:52.1967164Z`.
- Owner installed version changed from `1.0.0-98039f1aad8c-ownerupdate1` to `1.0.0-98039f1aad8c-ownerproof1`.

After successful proof:

- production proof manifest was removed/disabled;
- proof package was preserved;
- no automatic release is currently active.

### Heartbeat AgentVersion correction

The original heartbeat request silently defaulted AgentVersion to `0.1.0`.
Commit `b98a416` removed that fake fallback and wires the installer deployment version into `Cloud.AgentVersion`; HeartbeatWorker now reports that configured installed release version.

Verification:

- Agent targeted tests: **56 / 56 PASS**.
- Full-solution retest was intentionally skipped because the final change was isolated to Agent version reporting.

Owner currently remains on the already-proven `ownerproof1` package; the heartbeat-version correction can reach Owner with the next normal Agent release rather than another dedicated large proof download.

### Laptop 5 bootstrap

- Laptop 5 friendly name: `Laptop 5`.
- Durable DeviceId: `8fa05fc9-c72c-494c-a2d0-ff622e7ead77`.
- One-time USB bootstrap with the updater-enabled latest Agent source was reported complete.
- Future Agent updates can use Dashboard -> Laptop 5 -> Update Agent without another manual bootstrap.

### Branch / release state

- Feature branch is pushed through `b98a416dbc51a617a9673d741dc1c9f739a65575`.
- `main` has intentionally NOT been merged or advanced by this updater phase.
- `main` remains at `2ae57a98992c277acbe7b6d102c1b44ca42a252f` until a separate explicit merge approval.
- Unrelated local `AGENTS.md` work must remain untouched.

The automatic-updater implementation and initial rollout phase are therefore considered complete.
<!-- AGENT-AUTO-UPDATE-FOUNDATION-20260902 END -->

<!-- VERIFIED-CLASS-AUDIO-PROD-20260901 START -->
## USB classroom audio production deployment — 2026-09-01

Release commit: 5a0db194b2dac5a792dc87ef91fd37537427fb6d
Release: fix(agent): stabilize USB class audio lifecycle

Production source before deployment:
2e4f52e9c0fdedde9fecba716fbd081393e72f68

Production VPS deployment completed successfully.

Deployment scope:
- Dashboard image rebuilt and Dashboard container recreated.
- API was not recreated.
- PostgreSQL was not recreated.
- LiveKit was not recreated.
- LiveKit Ingress was not recreated.
- MediaMTX relay was not recreated.
- Recording archive services were not recreated.
- Caddy was not recreated.

Recorded VPS verification:
- DASHBOARD_BUILD=PASS
- DASHBOARD_RUNTIME=PASS
- API_UNTOUCHED=PASS
- DATABASE_UNTOUCHED=PASS
- LIVEKIT_UNTOUCHED=PASS
- INGRESS_UNTOUCHED=PASS
- MEDIAMTX_UNTOUCHED=PASS
- ARCHIVE_UNTOUCHED=PASS
- CADDY_UNTOUCHED=PASS
- API_HEALTH=PASS
- PUBLIC_DASHBOARD=PASS
- LIVE_PORT_1935=PASS
- LIVE_PORT_1936=PASS
- VPS_DASHBOARD_DEPLOY=PASS

Production runtime commit:
5a0db194b2dac5a792dc87ef91fd37537427fb6d

Rollback commit:
2e4f52e9c0fdedde9fecba716fbd081393e72f68

Final Owner hardware verification:
- Student live voice: PASS
- Teacher live voice: PASS
- Live latency approximately 2-3 seconds: PASS
- Stale delayed voice backlog removed: PASS
- Microphone ON during Teams call: PASS
- Microphone OFF after Teams call: PASS
- Recording student voice: PASS
- Recording teacher voice: PASS
- Recording mixed audio: PASS
- Recording stale audio regression: PASS

Verified Windows Agent installer version:
1.0.0-fd59be681511-classaudio4

Verified installer SHA256:
4BDE9012D318D44F713C27A46CDC20AD3A8C6C0275532A3591BFE0426EABED50

Laptop 5 has not yet been upgraded to this final installer.
<!-- VERIFIED-CLASS-AUDIO-PROD-20260901 END -->

<!-- VERIFIED-CLASS-AUDIO-20260901 START -->
## Verified USB classroom audio baseline — 2026-09-01

Branch: `codex/strict-headset-microphone`
Pre-commit HEAD: `fd59be681511ad737ffdd15d883513f8b308cbec`

Owner-machine physically verified installer:

- Version: `1.0.0-fd59be681511-classaudio4`
- Path: `C:\Dev\HomeQuranLearning.QA\publish\owner-test-classaudio4-20260901-224959\Home Quran Learning Setup.exe`
- Size: `356221582` bytes
- SHA256: `4BDE9012D318D44F713C27A46CDC20AD3A8C6C0275532A3591BFE0426EABED50`
- Signature: unsigned

Final classroom-audio behavior verified on Owner hardware:

- student/system/class playback is captured from the render endpoint of the
  single verified physical USB headset pair;
- no Windows Default, Realtek/internal or implicit playback/microphone fallback
  is used for the teacher headset path;
- the same physical USB headset microphone is used for teacher audio;
- Teams/Zoom active render-session detection controls teacher-microphone
  lifecycle only;
- teacher microphone is closed while no communication call is active;
- taskbar microphone indicator turns ON during the Teams call and OFF after
  call end;
- live dashboard receives both student and teacher speech;
- observed live audio latency is approximately 2-3 seconds;
- stale teacher speech no longer drains minutes later;
- live raw-audio buffering is bounded with thread queue 16 and UDP FIFO 512
  instead of the former oversized queue/FIFO;
- recording Track 0 contains mixed student/system plus teacher audio;
- recording Track 1 remains teacher microphone QA audio;
- physical recording playback verified student voice, teacher voice and correct
  mix with no stale/delayed-audio backlog;
- protected ddagrab timing, stream-key replacement and live recovery behavior
  remain preserved;
- Dashboard selected-feed audio control remains listener-only and only one feed
  is intended to be audible at a time.

Physical proof:

- idle mic icon OFF: PASS;
- Teams call mic icon ON: PASS;
- call-ended mic icon OFF: PASS;
- live student voice: PASS;
- live teacher voice: PASS;
- live stale-audio regression: PASS;
- recording student voice: PASS;
- recording teacher voice: PASS;
- recording mixed audio: PASS;
- recording stale-audio regression: PASS.

Laptop 5 has not yet been upgraded to this final installer.

Production deployment approval was explicitly granted on 2026-09-01. The VPS
deployment for this source is dashboard-only because the completed audio
implementation is Windows-Agent code; API, database, MediaMTX relay, LiveKit,
ingress and archive services are intentionally not part of this deployment.
<!-- VERIFIED-CLASS-AUDIO-20260901 END -->
<!-- VERIFIED-LIVE-FIX-6782031 START -->
## Verified live-monitoring recovery baseline — 2026-09-01

Source commit: `6782031` — `fix(live): stabilize publisher recovery and idle audio`

Verified installer:
- Version: `1.0.0-6782031e873a`
- Path: `publish\stable-agent-6782031-20260901-040509\Home Quran Learning Setup.exe`
- Size: `356205198` bytes
- SHA256: `A79779A27E9F512D95E04B5C472239C88AF9FF423054A6B59BD19E4511110CFF`
- Signature: unsigned

Automated verification before packaging:
- Agent tests: 24 / 24 PASS
- Unit tests: 115 / 115 PASS
- Integration tests: 6 / 6 PASS
- Total: 145 / 145 PASS
- Release solution build: PASS

Physical installation verification on development machine:
- device identity preserved
- existing recordings preserved
- exactly one managed Agent task/process
- exactly one TeamsHelper task/process
- installed version confirmed as `1.0.0-6782031e873a`

Laptop 5 (`DESKTOP-71RJV67`) real E2E validation with this exact installer:
- idle live feed: PASS
- dashboard enable/disable control: PASS
- live feed during Teams call: PASS
- live feed after Teams call ended: PASS

Resolved live-path defects:
- stream-key changes now force replacement of a still-running FFmpeg publisher
- idle system-audio periods now receive adaptive silence keepalive without racing real WASAPI audio

This installer supersedes `1ddf417` as the current physically verified Agent baseline for future installs/repairs unless a newer baseline is explicitly verified and recorded.
<!-- VERIFIED-LIVE-FIX-6782031 END -->

<!-- STRICT-TEACHER-HEADSET START -->
## Generic verified USB teacher-microphone source checkpoint — 2026-09-01

Branch: `codex/strict-headset-microphone`

The original exact-approved-headset design in this branch has been superseded
before commit by the generic verified-USB policy.

Current teacher-microphone behavior:

- the Agent never uses Windows Default, Default Communications,
  internal/Realtek Microphone Array or another implicit fallback;
- Core Audio capture endpoints are mapped to Windows PnP identities and USB is
  verified through the real PnP parent/device-bus chain rather than friendly
  name text;
- exactly one verified USB capture microphone is selected automatically;
- zero verified USB microphones produces `Teacher Mic Missing`;
- more than one verified USB microphone is treated as ambiguous and also fails
  closed as `Teacher Mic Missing`;
- recording/live operation continues with teacher-input silence while the
  microphone is missing or ambiguous;
- discovery retries allow a different genuine USB headset to be selected
  automatically when it becomes the single valid endpoint;
- setup no longer requires selection/approval of one exact headset and no
  exact microphone endpoint is persisted in Agent configuration;
- Track 0 remains system/student/class audio plus teacher microphone and Track 1
  remains the isolated verified USB teacher microphone;
- live publishing now mixes the existing protected system-audio path with an
  independent verified USB teacher-microphone UDP input;
- the protected `6782031` ddagrab timing, stream-key replacement and adaptive
  system-audio silence behavior remain preserved;
- Dashboard live listening is coordinated globally: all feeds are muted by
  default and only one browser feed may be audible at a time; disabling or
  switching audio detaches stale browser audio without stopping video.

Verification completed before source commit `80c4dcd`:

- USB endpoint mapping static gate: PASS;
- physical current-machine USB classifier smoke:
  exactly one `Microphone (Logi USB Headset)` verified and internal Realtek
  capture was not accepted;
- Audio/Media generic-USB runtime slice: PASS;
- obsolete exact-headset installer active-source cleanup: PASS;
- Installer/Media/Service Release builds: PASS;
- Agent tests after live teacher-audio implementation: 34 / 34 PASS;
- Agent test build: zero warnings;
- live teacher-audio static protected-path gate: PASS;
- Dashboard single-audible-feed static gate: PASS;
- Dashboard lint: PASS, 0 warnings;
- Dashboard production build: PASS.

The physically verified `6782031` installer remains the deployed Agent baseline.
The generic USB/live/dashboard source above has not yet been packaged,
installed or physically validated as a replacement installer. Laptop 5 must
not be manually patched.
<!-- STRICT-TEACHER-HEADSET END -->

## Purpose and authority

Canonical resumable project checkpoint. Use it with Git and actual repository state after reboot, new Codex chat, interrupted phase or missing chat history.

Chat history is not authoritative. Repository state + this file are the durable project state.

<!-- CURRENT-VERIFIED-CHECKPOINT:START -->

# CURRENT VERIFIED PROJECT CHECKPOINT — 2026-09-01

> **AUTHORITATIVE RECOVERY SECTION**
>
> A new engineering session must read this section before the historical
> checkpoints below. If an older statement later in this file conflicts with
> this section, this section wins. Git and fresh runtime evidence still outrank
> this document when the repository has advanced.

## Exact source and deployment position

- Primary branch: `main`
- Latest verified product/source commit: `2e4f52e`
- Product commit: `feat(schedules): add conflict-safe multi-day management`
- Before this documentation-only checkpoint, local `main` and `origin/main`
  both resolved to `2e4f52e` and the worktree was clean.
- The documentation commit containing this section is newer than the product
  commit; `2e4f52e` remains the product baseline for this checkpoint.
- Full .NET verification at the product baseline: **121 / 121 PASS**, 0 failed,
  0 skipped.
- Dashboard Next.js production build: **PASS**.
- The latest management/scheduling slices changed no Agent source.

Important recent product checkpoints:

- `008aeef` — professional Teacher/Student/Course management actions
- `24b9de4` — safe Teacher/Student/Course edit and archive foundation
- `5c99a70` — simplified people and device dashboard views
- `fee8692` — Manager global operational scope completed
- `92096f0` — old Manager assignment operational scope removed
- `e84563d` — Owner-only private-laptop visibility foundation
- `59e70d9` — Admin user-management boundaries
- `8aee968` — stable Windows Agent physical verification documented
- `1ddf417` — verified Agent/live-publisher source baseline

## Production VPS checkpoint

Owner-supplied VPS deployment output records the following successful
production deployment. This documentation-only turn did not mutate or re-probe
the VPS.

- Production project: `/opt/homequranlearning`
- Production product HEAD: `2e4f52e`
- Production environment file:
  `/opt/homequranlearning/infrastructure/docker/.env.production`
- Base Compose: `infrastructure/docker/docker-compose.prod.yml`
- Relay overlay:
  `infrastructure/docker/docker-compose.relay-production.yml`
- Pre-deployment database backup:
  `/var/backups/homequranlearning/pre-management-2e4f52e-20260831T203759Z.dump`
- Management migration:
  `20260831195915_ManagementEntityActiveFlags`

Recorded deployment proof:

```text
SOURCE_CHECKOUT=PASS
COMPOSE_CONFIG=PASS
DATABASE_BACKUP=PASS
API_DASHBOARD_BUILD=PASS
API_HEALTH=PASS
MANAGEMENT_MIGRATION=PASS
DASHBOARD_LOGIN=PASS
API_DASHBOARD_RUNTIME=PASS
LIVE_SERVICES_UNTOUCHED=PASS
LIVE_PORTS_1935_1936=PASS
VPS_MANAGEMENT_DEPLOY=PASS
PRODUCTION_HEAD=2e4f52e
DATABASE_MIGRATION=PASS
API=PASS
DASHBOARD=PASS
LIVE_RUNTIME_UNTOUCHED=PASS
```

API and Dashboard were rebuilt/recreated. The live relay, LiveKit, ingress,
archive registrar and Caddy services were deliberately preserved. For routine
updates, do not use blanket `docker compose down`; recreate only affected
services. The correct Docker-network API readiness target is
`http://api:8080/health`, not local-development port 5100.

## Protected production live architecture

Current topology:

```text
Windows Classroom Agent
    -> RTMP H264/AAC
    -> VPS MediaMTX relay :1935
       -> independent VPS archive path
       -> LiveKit Ingress :1936
          -> LiveKit
             -> authenticated Dashboard live view
```

Do not regress to the older direct-public-ingress architecture or casually
rewrite the known-good FFmpeg timing, codec, `ddagrab`, relay or browser
playback behavior. Public TCP 1935 remains required for roaming academy
laptops while this architecture is active. Protected live behavior includes
continuous online-device feeds, muted autoplay, independent audio controls,
no reconnect on enlargement/metadata refresh and `No Active Class` when no
session metadata applies.

## Verified Windows Agent baseline

- Authoritative physically verified Agent source: `1ddf417`
- Verification/documentation commit: `8aee968`
- Verified installer:
  `C:\Dev\HomeQuranLearning.QA\publish\stable-agent-1ddf417-20260831-160734\Home Quran Learning Setup.exe`
- Installer size: `362107534 bytes`
- Installer SHA256:
  `7E9AD671DDD43DAFD7E78D308067B920F6E34300C6FE93F635204130A518C99D`
- Windows-visible identity: `Home Quran Learning`
- Publisher/company: `Abdul Wahid`

Permanent runtime layout:

```text
C:\Program Files\Home Quran Learning\Classroom Agent\app\agent\Academy.Agent.Service.exe
C:\Program Files\Home Quran Learning\Classroom Agent\app\teams-helper\Academy.Agent.TeamsHelper.exe
C:\ProgramData\AcademyAgent
C:\ProgramData\AcademyAgent\Recordings
C:\ProgramData\AcademyAgent\Logs\ClassroomAgent.log
```

Physical proof recorded at `8aee968` includes two consecutive Install/Repair
cycles, stable Agent and TeamsHelper paths, exactly one managed process/task for
each, legacy version-directory cleanup, preserved device identity/recordings,
verified branding/icon, heartbeat PASS, RTMP live publishing PASS and growing
local recording PASS. Future laptop fixes must be implemented centrally in
source/installer. Never patch individual teacher laptops or install an older
package merely because it remains in `publish`.

A future Agent may supersede this baseline only after recording its source
commit, installer path/size/hash, physical repair proof, heartbeat, live,
recording and durable-data preservation. Agent updates must preserve
`C:\ProgramData\AcademyAgent` unless a separately approved migration says
otherwise.

## Recording and attendance invariants

Local `AudioLayoutVersion1` recordings use:

- Track 0: `Academy Class Mixed Audio`
- Track 1: `Academy Teacher Microphone QA v1`

Do not represent server RTMP/archive mixed audio as an isolated teacher
microphone. Normal VPS archive handling should remux/stream-copy incoming H264
rather than transcode video, and session association uses Device plus absolute
timestamps/overlap rather than segment-number assumptions.

Preserve Teams evidence semantics:

- `TeacherGreetingSent` and `CallAttempted` are teacher evidence only.
- `StudentCallConnected` is explicit student presence.
- `CallEnded` supplies stop/duration evidence.
- `LessonShared` is strong teacher and student evidence, but its timestamp is
  not an arrival-time signal.
- Late threshold: 3 minutes.
- Pre-class teacher-ready window: 5 minutes.

## Current authorization model

Roles are Owner, Admin and Manager.

- Owner is the highest authority.
- Manager now has global access to normal academy operations: Teachers,
  Students, Courses, Schedules, Sessions, Live, Recordings, Attendance and
  appropriate QA operational views.
- Do not reintroduce `/assignments`, `/manager-assignments` or assigned-teacher
  filtering as an operational requirement.
- Historical assignment tables/services may remain dormant until deliberate
  cleanup.
- Global Manager operational scope does not grant Owner/Admin user-management
  authority.
- Admin account-management boundaries implemented at `59e70d9` remain in
  force; Owner records are not ordinary Admin-manageable users.

An older statement in `docs/OWNER-CONTROL-PLANE.md` says Managers are normally
teacher-scoped. The newer explicit Owner decision and implemented source above
supersede that statement for normal academy operations; the Owner Control Plane
document requires later reconciliation rather than reintroducing the old
restriction.

## Owner-only private laptop

The private/trial laptop must remain visible to Owner and invisible to Admin
and Manager across current and historical device-linked data. Commit `e84563d`
is only the foundation. Global server-side coverage is **not yet proven** for
Schedules, Sessions, Recordings, Attendance/reporting and linked QA evidence.
Prefer a centralized resource/device visibility service and do not call this
requirement complete until Owner/Admin/Manager E2E tests prove it.

## Management and scheduling status

Teacher, Student and Course management now provides professional Edit and
archive/Delete actions with `IsActive` lifecycle fields. Existing records
default active through migration
`20260831195915_ManagementEntityActiveFlags`. Historical evidence must not be
hard-deleted. Teacher/Student screens do not expose phone/email management
fields, and Student/Teacher class relationships derive from schedules rather
than a permanent UI assignment.

Remaining archive correctness gap: archiving a Teacher, Student or Course must
safely expire/deactivate active and future schedules referencing it while
preserving historical schedule/session/evidence rows.

Schedule management at `2e4f52e` includes multi-day creation, batch validation,
one persistence transaction, current Teacher-class preview, exact duplicate
checks, Teacher/Student/device overlap checks, adjacent-class allowance,
cross-midnight weekly logic, history-safe Edit/archive, friendly laptop names,
responsive controls and deterministic 12-hour display (`hh:mm AM/PM`, no
seconds). This source is deployed to the production VPS per the proof above.

Operational UI should prefer `recordingDisplayName`, falling back to
`deviceName`. Schedule management complies; Sessions, Live metadata and reports
still require consistency verification.

## Current unresolved work, in priority order

1. Complete server-side Owner-only private-laptop visibility across every
   device-linked resource and prove it with Owner/Admin/Manager E2E tests.
2. On Teacher/Student/Course archive, safely deactivate active/future schedules
   without deleting history.
3. Complete Sessions Edit, Cancel and history-safe archive/Delete; verify
   Manager permissions, friendly laptop names and private-device isolation.
4. Finish mobile Schedule UI/browser polish and real-device verification.
5. Verify friendly laptop naming across Sessions, Live and reporting.
6. Fix stale online/offline device status behavior.
7. Implement strict approved-headset microphone behavior: never silently fall
   back to the internal mic, surface missing headset state and safely auto-bind
   when it returns.
8. Run full Owner/Admin/Manager browser E2E regression.
9. Resume remaining QA roadmap only after management, privacy, session and
   device correctness are stable.

QA remains secondary for now. Preserve mixed-audio eligibility, hard-rule
independence, off-topic detection, Arabic/Quran exclusions, ASR hallucination
safeguards and evidence windows. Never infer isolated teacher audio from the
server mixed stream.

## Security, workflow and recovery

Never document or print Agent API keys, RTMP stream keys, LiveKit secrets,
database passwords, archive credentials, signed storage URLs or production
environment values. The installer SHA256 above is not a credential.

Normal workflow remains:

```text
inspect exact source/runtime
-> implement smallest production-grade slice
-> targeted and regression verification
-> update this checkpoint
-> review diff/secrets
-> atomic commit and normal push
-> separately approved production mutation/deployment
-> runtime/E2E proof
```

At the start of a new session:

1. Read `AGENTS.md` and this authoritative section.
2. Run `git status --short` and `git log -5 --oneline --decorate`.
3. If HEAD is newer, inspect it and update this section; never reset backward.
4. Do not install an Agent older than the verified baseline above.
5. Preserve MediaMTX `:1935` -> LiveKit Ingress `:1936` and ProgramData.
6. Do not reintroduce Manager assigned-teacher operational restrictions.
7. Do not claim Owner-only isolation complete without E2E evidence.
8. Continue from the unresolved-work list, not an older roadmap below.

## Checkpoint summary

```text
LATEST_PRODUCT_SOURCE=2e4f52e
PRODUCTION_PRODUCT_SOURCE=2e4f52e
PRODUCTION_DEPLOYMENT=PASS
DATABASE_MIGRATION=PASS
API=PASS
DASHBOARD=PASS
LIVE_RUNTIME_UNTOUCHED=PASS
DOTNET_TESTS=121/121 PASS
DASHBOARD_BUILD=PASS
VERIFIED_AGENT_BASELINE=1ddf417
VERIFIED_AGENT_DOC_COMMIT=8aee968
VERIFIED_INSTALLER_SIZE_BYTES=362107534
VERIFIED_INSTALLER_SHA256=7E9AD671DDD43DAFD7E78D308067B920F6E34300C6FE93F635204130A518C99D
```

Update this section whenever product source, production deployment, verified
Agent, live topology, security/role policy or major roadmap completion changes.

<!-- CURRENT-VERIFIED-CHECKPOINT:END -->

---

## Canonical checkpoint

- Branch: `codex/agent-windows-branding`
- Base HEAD: `926fe1c2fcd0`
- Status: `AGENT_WINDOWS_BRANDING_VERIFIED`
- Development mode: production-targeted local development; this branding/update-path slice is not deployed or installed on academy laptops yet.
- Current focused slice:
  - Windows file metadata and the Windows-service display name identify the process exactly as `Home Quran Learning`, published by Abdul Wahid;
  - the Agent and setup executables embed the approved Home Quran Learning logo icon generated deterministically from the repository branding asset;
  - installer updates use one stable managed application path instead of a new commit-named executable path per release;
  - after the replacement Agent is running, obsolete legacy version directories are removed while ProgramData device identity, recordings and evidence remain untouched.
- Verification:
  - Agent Release build GREEN with 0 warnings/errors;
  - installer Release build GREEN with 0 warnings/errors;
  - built Agent metadata reports the branded file description/product/company and exposes a valid embedded icon;
  - built installer metadata reports the branded setup description/product/company and exposes a valid embedded icon;
  - logo-to-ICO generation is deterministic and contains 16, 24, 32, 48, 64, 128 and 256 pixel assets;
  - final full solution Release build GREEN with 0 warnings/errors;
  - unit tests 108/108 GREEN and integration tests 6/6 GREEN;
  - intended path references were checked repository-wide; no runtime or script consumer still depends on a commit-named `versions` path.
- Scope protection: dashboard, VPS configuration/deployment, database, recordings, LiveKit, Teams attendance semantics and QA processing were not changed.
- Existing Windows microphone-history rows may remain visible until Windows expires its history. Installing a newly built approved setup switches the live process to the branded stable path and prevents future per-release identity rows.
- Next step: commit and push this isolated slice, then build/install the next approved Classroom Agent setup when the Owner chooses to update a laptop.

## Current runtime snapshot

Verified 2026-08-29:

- `.dev-runtime/Runtime.ps1`: API OFF, Agent OFF, FFmpeg 0
- Docker: PostgreSQL, Redis, MinIO, LiveKit, LiveKit Ingress and ingress-manager running
- Windows service `AcademyQaWorker`: Running / Automatic

The `AcademyQaWorker` service remains Running/Automatic and is wired to the repository worker source. Its current source passes all self-test markers and a live read-only API probe, but Windows denied service-control and child-process termination, so the long-running service process itself could not be refreshed. Keep this operational caveat visible after release.

## Proven architecture

Backend:
- ASP.NET Core
- .NET 10
- EF Core
- PostgreSQL

Infrastructure:
- PostgreSQL
- Redis
- MinIO
- LiveKit
- LiveKit Ingress
- ingress-manager
- Docker Compose

Dashboard:
- Next.js
- React
- TypeScript

Agent:
- Academy.Agent.Service
- Academy.Agent.Cloud
- Academy.Agent.Audio
- Academy.Agent.Capture
- Academy.Agent.Media
- Academy.Agent.Teams
- Academy.Agent.TeamsHelper

Live path:

```text
Agent -> FFmpeg RTMP -> LiveKit Ingress -> LiveKit -> browser dashboard
```

Known-good screen timing:

```text
ddagrab
framerate=10
dup_frames=1
hwdownload
format=bgra
setpts=N/(10*TB)
```

Audio:

```text
NAudio/WASAPI loopback -> UDP -> FFmpeg AAC -> LiveKit
```

Recording uses direct H264 MP4 rather than huge raw BGRA temp files.

## Completed foundations

- JWT + HTTP-only cookie auth
- Owner/Admin/Manager roles and partial Manager teacher filtering
- teachers/students/courses/devices
- schedules/sessions
- historical session safety
- session events
- live screen/audio
- dashboard audio control
- recording/upload/recovery
- attendance reducer/review/daily report
- QA rules/alerts foundation

These foundations do not mean authorization, durable service startup, manual-session workflow, or the Owner Control Plane is complete.

## Teams attendance — completed

```text
Academy.Agent.Service
<-> secured Named Pipe
<-> Academy.Agent.TeamsHelper
-> Teams UI Automation
```

Event types:

```text
TeacherGreetingSent  = 19
CallAttempted        = 20
StudentCallConnected = 21
CallEnded            = 22
LessonShared         = 23
```

Source: `TeamsUIAutomation`

Current semantics:

- TeacherGreetingSent = teacher evidence, not student presence
- CallAttempted = teacher evidence, not student presence
- StudentCallConnected = explicit student presence
- CallEnded = stop/duration evidence
- LessonShared = strong teacher and student participation/presence evidence
- LessonShared timestamp is not a student arrival timestamp and must not by itself cause Late

Late threshold: 3 minutes.
Pre-class teacher-ready window: 5 minutes.

LessonShared requires exact scheduled student chat + outgoing lesson-related text + actual image in same message.

Known reducer test example:

```text
StudentCallConnected +2m
CallEnded +12m
=> Present
=> ActiveSeconds 600
```

Targeted attendance reducer suite after Teams integration: `27/27 green`.

## Phase 7A-1 — closed GREEN

Commit: `415bbec`

Implemented:

- pending recording exposes StartedAtUtc
- alert request exposes QaRuleId
- Whisper model reuse
- normalized transcript indexing
- cross-segment phrase matching
- recording-relative timestamps
- exact rule linkage
- duplicate retry suppression
- mark processed only after success
- failure remains pending

Worker self-test:

```text
QA_WORKER_TRANSCRIPT_INDEX_OK
QA_WORKER_CROSS_SEGMENT_MATCH_OK
QA_WORKER_RULE_LINK_OK
QA_WORKER_TIMESTAMP_ALIGNMENT_OK
QA_WORKER_SELF_TEST_OK
```

Gates:

- full solution build GREEN
- unit 67/67
- integration 2/2
- production runtime/API/DB proof GREEN

Proven:
- StartedAtUtc reaches worker
- exact QaRuleId persists
- recording-relative alert timestamp persists
- duplicate alert retry suppressed
- QaProcessedAtUtc persists only after explicit success
- processed recording disappears from pending queue
- temporary proof data cleaned

Historical proof counts:

```text
Recordings 148
QA Rules 1
QA Alerts 5
Sessions 20
Session Events 163
```

Not permanent invariants.

QA worker remains production-wired at `spikes/SttSpike/qa_worker.py`.

## Known non-blocking issue

`.dev-runtime/Runtime.ps1 StartApi` may briefly report API OFF immediately after launch while HTTP readiness later succeeds. Use explicit bounded readiness checks.

## S1 - closed

Implemented:

- Owner/Admin role policy on user, people, assignment, QA-rule, QA-alert write, device-management and manual-session routes.
- Manager recording playback restricted to recordings belonging to assigned teachers.
- Manager live-session visibility and LiveKit tokens restricted to active sessions belonging to assigned teachers.
- Manager denied LiveKit server credentials and publish-capable participant tokens; Owner/Admin token workflows remain available.
- Unknown roles fail closed for dashboard recordings, alerts and devices.
- Corrected Owner policy recorded durably: `LessonShared` proves both teacher and student presence but is not an arrival/late timestamp.

Proof:

- full solution build: GREEN
- focused attendance/access tests: 35/35
- unit tests: 75/75
- integration tests: 2/2
- Agent tests: 1/1
- local API Owner/Admin/Manager policy and assigned/unassigned resource proof: GREEN
- temporary runtime proof cleanup: GREEN, zero remaining rows
- QA worker current-source self-test and read-only API probe: GREEN
- exact service-process refresh: not achieved because host service/process control returned Access Denied

S1 implementation release files:

- `AGENTS.md`
- `README.md`
- `docs/PROJECT-DECISIONS.md`
- `docs/PROJECT-STATE.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/decisions/coding-conventions.md`
- `src/Backend/Academy.Api/Program.cs`
- `src/Backend/Academy.Application/Services/DashboardQueryService.cs`
- `tests/Academy.UnitTests/DashboardResourceAccessTests.cs`

## S1.1 - closed

Goal:

- Replace the HTTP 500 returned for an authorized playback request against a historical `Deleted` recording with the established unavailable-recording client response, without changing authorization or healthy uploaded playback.

Root cause:

- `RecordingService.GetPlaybackUrlAsync` represented every non-`Uploaded` status with a generic `InvalidOperationException`.
- The `/api/admin/recordings/{recordingId}/playback-url` endpoint did not map that known domain condition, so ASP.NET Core surfaced it as HTTP 500 after authorization had correctly succeeded.

Released:

- A typed `RecordingUnavailableException` carries the current recording status.
- `RecordingService` throws that exception before requesting a presigned URL for any non-`Uploaded` recording.
- The playback endpoint maps only that known exception to HTTP 400 with the existing `Recording file is not available.` response.
- A regression test proves `Deleted` status raises the typed unavailable condition and never calls storage.

Proof:

- targeted deleted-recording regression: 1/1 GREEN
- full unit tests: 76/76 GREEN
- integration tests: 2/2 GREEN
- Agent tests: 1/1 GREEN
- full solution build: GREEN, 0 warnings, 0 errors
- authenticated runtime HTTP proof: isolated `Deleted` recording returned 400; existing `Uploaded` control returned 200
- cleanup: isolated proof row count zero; recordings baseline restored to 148
- runtime after proof: API OFF, Agent OFF, FFmpeg 0

Protected behavior:

- Existing authorization and manager resource-scope checks still execute before playback resolution.
- Uploaded recording presigned-URL behavior is unchanged.
- Download endpoint behavior, recording lifecycle, storage, LiveKit, Teams, attendance and QA processing semantics are unchanged.

## S1.2 - closed

Goal:

- Make `Academy.Agent.TeamsHelper` start once and recover reliably in the active teacher's interactive Windows session, with observable health and reversible setup, while preserving the proven Teams evidence pipeline.

Baseline:

- The helper currently runs only when invoked manually with `--monitor`; no durable launcher, installer or startup registration exists.
- `Academy.Agent.Service` is Windows-service capable, but the local product runtime currently starts it as a normal process and no Academy Agent Windows service is installed.
- Teams UI Automation requires the active interactive desktop. The named-pipe server deliberately validates the expected helper process name and active-console session, so the service/session-0 process must not directly host UI Automation.
- Active user is `DESKTOP-PUFUU3U\SAMSUNG` in session 5. Initial TeamsHelper and Academy Agent process counts are zero, and no Academy/TeamsHelper scheduled task exists.

Approved boundaries:

- Prefer a least-privilege per-user interactive launcher with idempotent install/status/remove tooling and bounded restart behavior.
- Preserve helper process-name and active-session pipe validation.
- Preserve `TeacherGreetingSent`, `CallAttempted`, `StudentCallConnected`, `CallEnded` and `LessonShared` detection, dedupe and attendance semantics.
- Do not add production deployment, remote lifecycle, Owner Control Plane, manual-session or `7A-2` scope.

S1.2 implementation checkpoint (2026-08-28):

- Added a per-session single-instance mutex, bounded file logging with `.1` rotation, and atomic throttled health snapshots under the current user's SID-scoped ProgramData runtime directory.
- Added `setup-teams-helper.ps1` with idempotent staged install/rollback, limited interactive logon task registration, bounded restart settings, exact executable-path process checks, status output and uninstall preservation of health/log evidence.
- `Academy.Agent.TeamsHelper --lifecycle-probe` is GREEN with `TEAMS_HELPER_SINGLE_INSTANCE_OK`, `TEAMS_HELPER_HEALTH_OK`, `TEAMS_HELPER_LOG_ROTATION_OK` and `TEAMS_HELPER_LIFECYCLE_PROBE_OK`.
- Existing helper probes are GREEN: UIA, detector policy and state-machine probes.
- Direct Agent lifecycle VSTest is GREEN: 3/3. Existing unit and integration VSTest binaries are GREEN: 76/76 and 2/2.
- PowerShell parser and `git diff --check` are GREEN.
- Elevated ProgramData install/task/runtime proof remains pending. The elevated command runner currently rejects requests with a usage-limit error before execution; no indirect or destructive workaround was attempted.

S1.2 runtime proof completed (2026-08-28):

- Fresh Release publish was verified to resolve runtime state to the SID-scoped ProgramData path. The previously installed binary still resolved to AppData; republishing and reinstalling corrected that mismatch.
- The installer was corrected to stop the exact managed helper before clearing `.previous`, preventing DLL-lock cleanup failures.
- Final installed task action is the ProgramData executable with `--monitor`, current user `SAMSUNG`, Interactive/Limited principal, logon trigger, one-minute repeating recovery trigger, `RestartOnFailure` count 10/interval 1 minute, battery-safe settings and `IgnoreNew` duplicate policy.
- Runtime proof: task registered/running, exactly one helper in active session 5, fresh heartbeat and `LIFECYCLE_HEALTHY=YES`.
- Agent outage/recovery proof: helper transitioned `Idle -> WaitingForAgent -> Idle` while Agent was stopped and restarted; API remained OFF and FFmpeg remained 0.
- Helper recovery proof: exact managed helper PID termination was followed by a new session-5 helper and fresh heartbeat through the repeating recovery trigger.
- Intentional Stop/Start proof: Stop disabled the task and removed the process; Start re-enabled it and restored `Idle`/healthy state.
- Clean full solution build is GREEN with 0 warnings and 0 errors after stopping the runtime Agent process. Agent lifecycle tests 3/3, unit tests 76/76 and integration tests 2/2 are GREEN. All helper probes remain GREEN.
- Temporary publish output and earlier AppData test copies are not part of the repository or active task. They remain for explicit cleanup because the current command policy rejected recursive deletion outside the repository; no production data was touched.

Dashboard implementation checkpoint (2026-08-28):

- Added a read-only, role-scoped `/api/admin/sessions/{sessionId}/events` endpoint. It reuses the existing session access policy and returns purpose-limited event fields without exposing idempotency keys.
- Added dashboard session filters for teacher, student, course/device search, status, student attendance and date, plus a raw session evidence timeline with explicit attendance/SOP separation.
- Fixed the dashboard authentication lifecycle: login now reads the canonical `/auth/me` profile after the HttpOnly cookie is set, updates the shared AuthProvider state and keeps logout cleanup in a `finally` path. Unauthenticated dashboard routes redirect to `/login`.
- Pinned the dashboard development script to Next Webpack mode because the local Windows Turbopack runtime reproduced API route 404s; Webpack returned the expected 401/200 responses.
- Backend role-scope tests cover assigned and unassigned Manager session-event access. The event DTO intentionally excludes `IdempotencyKey`.

Dashboard proof completed (2026-08-28):

- Dashboard lint: 0 errors, 0 warnings.
- Dashboard production build: GREEN; auth, proxy and sessions routes generated.
- Full solution build: GREEN, 0 warnings, 0 errors.
- Full unit tests: 78/78 GREEN; integration tests: 2/2 GREEN.
- Authenticated local login flow: login 200, `/api/auth/me` 200, Owner profile active, HttpOnly cookie present.
- Authenticated Next proxy flow: sessions 200 with 20 records; selected completed session events 200 with 6 events.
- Direct API authorization: unauthenticated events request 401; missing/inaccessible event request 404.
- Browser proof: unauthenticated `/sessions` redirected to `/login`; approved local Owner login reached the authenticated dashboard; Sessions showed all 20 records; the selected completed session rendered six ordered raw evidence events; a no-match search produced the expected 0-of-20 empty state; Clear filters restored 20-of-20; logout returned to `/login`.
- Temporary runtime cleanup: API, dashboard dev servers and FFmpeg are OFF; Docker infrastructure and the Windows QA worker were not changed.

Next recoverable action:

- Discuss the next phase and wait for the Owner's `GO`. If VPS staging execution
  is selected, collect secure VPS access facts and begin the bounded direct-IP
  synthetic staging runbook without using real academy data.

## Current engineering gate - post-S1 reassessment

S1 closed the immediate role and resource-scope defects. Remaining stabilization and future authorization observations are:

1. Full granular permission-catalog enforcement remains future Owner Control Plane phase O1 work; S1 intentionally applies existing role and assignment boundaries only.
2. The running QA worker service process could not be refreshed under the current host permissions, although current source/runtime probes are green.
3. The historical `Deleted`-recording HTTP 500 follow-up was resolved and released in S1.1. Runtime proof returned Deleted 400, Uploaded 200, and S1 authorization ordering remains unchanged.
4. TeamsHelper durable launcher/installer/startup was implemented and runtime-proven in S1.2; the remaining cleanup warning is limited to old temporary publish/test copies outside the repository.
5. Manual-session workflow is incomplete.
6. Agent configuration contains environment-specific FFmpeg/device assumptions and recording is disabled by default.
7. Production Compose omits some live/reverse-proxy components and needs deliberate deployment design later.

7A-2 is released at `f4617e0`, 7A-3 at `a2b8aae`, 7A-4 at `ee42315`, 7A-5A at `a67ff8a`, 7A-5B at `b7e6deb` and 7A-5C at `a983841`. 7A-5A implements two-track teacher-audio provenance with fail-closed worker validation. VPS direct-IP staging preparation is in progress; Owner Control Plane and APK remain deferred.

## QA phase — 7A-2 (closed)

Durable timestamped transcript segment persistence.

Implemented contract:

- RecordingId
- deterministic SegmentIndex
- relative StartSeconds/EndSeconds
- Text
- language
- meaningful Whisper metadata
- retry/idempotency
- batch/transaction behavior

Correctness requirement: if transcript persistence succeeds but later alert processing fails, retry must not duplicate transcript segments.

Worker order is now:

```text
download
-> transcribe
-> persist segments
-> evaluate/persist alerts
-> mark QA processed only after all success
```

Evidence clips remain a later phase.

## Owner/engineering workflow decisions

Treat the repository as a real production software project.

Normal low-risk development flow:

`inspect relevant state -> implement production-quality slice -> targeted verification -> fix regressions -> update PROJECT-STATE.md -> review intended diff/secrets -> atomic commit -> normal push -> continue`

Rules:

- No fake production endpoints, fake health/metrics, placeholder production controls, hidden credentials, hard-coded device IDs, IP-based trust, or localhost-only production assumptions.
- Development-only proof data or mocks must be clearly isolated and labelled.
- Do not require ceremonial `APPROVE` or `GO` gates for ordinary verified low-risk local development commits/pushes.
- Explicit approval remains mandatory for production/VPS deployment, production migrations/destructive DB actions, secret rotation, production-impacting auth/RBAC/security changes, historical evidence mutation, proven live/recording architecture replacement, Teams attendance semantic changes, system-wide firewall/security changes, driver installation, force/history Git operations, and deployment-changing canonical release/merge actions.
- Keep `PROJECT-STATE.md` factual and recoverable, not a command diary.
- Ask the owner to run local commands only when this environment cannot execute them; commands must be exact and copy/paste-safe, preferably single-line when PowerShell continuation could confuse.
- Human-only actions remain physical/mobile tests, MFA/OTP/login steps, and genuine business decisions.

## Owner Control Plane — required, not implemented

`docs/OWNER-CONTROL-PLANE.md` is authoritative desired product policy.

The product must gain a separate Owner control surface with backend-enforced role + granular permission + resource scope; templates/custom grants and effective-access preview; durable Admin↔Manager and Manager↔Teacher assignments/history; configurable recording retention; durable audit; system health; and secure centralized device/Main-Agent lifecycle.

Likely future tracks are O1 authorization/permission foundation, O2 organization control, O3 retention, O4 device-management foundation, and O5 remote Agent lifecycle. Stabilization first closes immediate authorization defects. None of O1–O5 is currently implemented as a complete phase.

## Codex governance status

Project-wide governance was committed and pushed at `32202b3b4202515a684373bfbf6500e8a4e7eef7`. Status is `CLOSED`.

Root `AGENTS.md` is the repository-wide governance file. The existing `src/Dashboard/academy-dashboard/AGENTS.md` adds only Next.js/dashboard-specific guidance.

Project-local `.codex/config.toml` requests `workspace-write`, `on-request`, and the supported native Windows elevated sandbox. Project rules allow routine read-only Git inspection, prompt on commit/push, and forbid destructive Git patterns. These project-local layers load only when the repository is trusted.

Committed bootstrap files:

- `.codex/config.toml`
- `.codex/rules/default.rules`
- `AGENTS.md`
- `README.md`
- `docs/PROJECT-STATE.md`
- `docs/CODEX-WORKFLOW.md`
- `docs/OWNER-CONTROL-PLANE.md`
- `docs/architecture/ADR-001-system-architecture.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/architecture/qa-worker-service.md`
- `docs/decisions/coding-conventions.md`

No product source, migration, or test file belongs in this bootstrap.

## Production deployment

Development is production-targeted. Actual VPS/production deployment remains a separate high-risk action.

Historical VPS pilot/deployment work is documented above. The current `codex/final-classroom-agent-installer` branch and its uncommitted changes are not deployed by this checkpoint.

Before any next VPS mutation, verify the exact remote commit/config/runtime state and obtain explicit high-risk approval for the bounded production action.

## Resume checklist

1. Read `AGENTS.md` and this file.
2. Inspect branch, HEAD, worktree/staged state, and origin when available.
3. Read only the decisions/architecture documents relevant to the current slice.
4. Inspect runtime/database/Docker/browser state only if the current slice touches them.
5. Resume from the latest verified checkpoint; do not redo closed work.
6. Never discard/reset/clean/stash unexpected human work.
7. Continue ordinary verified low-risk development without ceremonial release gates.
8. Stop for explicit approval only when the next action is high-risk under `AGENTS.md`.

### Permanent Windows Agent physical verification — DESKTOP-PUFUU3U

Verified at: 2026-08-31 16:31:19 +05:00

- Release source HEAD: `1ddf417`.
- Owner-approved physical update laptop: `DESKTOP-PUFUU3U`.
- Two consecutive Install/Repair cycles completed successfully.
- Agent remained at `C:\Program Files\Home Quran Learning\Classroom Agent\app\agent\Academy.Agent.Service.exe` after both repairs.
- TeamsHelper remained under the permanent `app\teams-helper` path.
- Exactly one managed Agent process/task and one managed TeamsHelper process/task were verified.
- Legacy version-named runtime directories were removed.
- Durable `C:\ProgramData\AcademyAgent` device identity and existing recordings were preserved.
- FileDescription/ProductName verified as `Home Quran Learning` and CompanyName as `Abdul Wahid`.
- Embedded Home Quran Learning executable icon verified.
- Successful heartbeat, RTMP live publishing and growing local recording verified after both repairs.
- Existing Windows microphone privacy-history records were not modified.

<!-- OWNER-CONTROLLED-AGENT-UPDATE-20260902 -->
### Owner-controlled Agent updates
- Owner selects the editable laptop name shown on Device page, for example Laptop 5.
- Dashboard submits the selected Device database record Id; backend resolves the permanent Agent DeviceId internally.
- Windows computer name is not used for update targeting.
- Only Owner can queue an update.
- Queued update requests expire after 30 minutes.
- Agent checks for updates every 1 minute.
- Continuous recording and always-on live monitoring do not block Owner-controlled maintenance.
- Verified communication microphone usage remains the final accidental-call safety gate.
