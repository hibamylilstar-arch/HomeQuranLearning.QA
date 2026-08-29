# Owner Control Plane — Authoritative Product Requirement

## Status and purpose

Status: **desired product policy; not implemented as a complete control plane**.

The product must ultimately include a dedicated Owner Control Plane, separate in purpose from the normal Admin/Manager operational dashboard. It is the academy's highest-level surface for organization-wide access, device lifecycle, Agent deployment/maintenance, recording retention, audit/security visibility, system policy, and global operational control.

Current implementation evidence and gaps are tracked in `docs/PROJECT-STATE.md`. This document states what the product must become; it must never be used to claim these capabilities already exist.

## Authority and authorization model

Owner is the highest authority. Protected backend actions must evaluate:

```text
authenticated user + stable granular permission + resource scope
```

Role names provide templates/defaults, not complete authorization. Frontend visibility is convenience only and cannot replace API enforcement.

Owner must be able to create, disable/re-enable, and organize Admins and Managers; assign Managers to Admins where the chosen model requires it; assign Teachers to Managers; grant/revoke permissions; inspect assigned, inherited/default, and effective access; select resource scope; and see who changed access and when.

Managers are normally teacher-scoped. Historical sessions, recordings, attendance, and evidence keep their original identity when current assignments change.

## Durable permission catalog

Use stable identifiers and evolve the catalog deliberately. Avoid meaningless micro-permissions. Initial categories/candidates are:

- Users: `users.view`, `users.create`, `users.update`, `users.enable_disable`, `users.assign_roles`
- Administration: `admins.view`, `admins.manage`, `managers.view`, `managers.manage`, `managers.assign_to_admin`, `teachers.assign_to_manager`
- Teachers: `teachers.view`, `teachers.create`, `teachers.update`, `teachers.disable`
- Students: `students.view`, `students.create`, `students.update`, `students.disable`
- Courses: `courses.view`, `courses.manage`
- Schedules: `schedules.view`, `schedules.create`, `schedules.update`, `schedules.cancel`
- Sessions: `sessions.view`, `sessions.manage`, `sessions.review`
- Attendance: `attendance.view`, `attendance.review`, `attendance.override`, `attendance.reports`
- Devices: `devices.view`, `devices.manage`, `devices.assign`, `devices.agent.install`, `devices.agent.uninstall`, `devices.agent.update`, `devices.agent.restart`, `devices.agent.repair`, `devices.agent.enable_disable`
- Recordings: `recordings.view`, `recordings.play`, `recordings.download`, `recordings.preserve`, `recordings.extend_retention`, `recordings.delete_when_policy_allows`
- Live: `live.view`, `live.audio`, `live.manage`
- QA: `qa.rules.view`, `qa.rules.manage`, `qa.alerts.view`, `qa.alerts.review`, `qa.evidence.view`
- Reports: `reports.attendance`, `reports.qa`, `reports.device_health`, `reports.recordings`
- System/audit: `audit.view`, `settings.view`, `settings.manage`

Provide Admin and Manager default templates plus Owner customization. The UI must preview effective access before save. Later template edits must not silently grant dangerous new access to existing users without deliberate Owner policy/migration.

## Organization and history

Use durable assignment entities supporting assign, unassign, move, temporary reassignment, and history. The hierarchy may support Owner → Admin → Manager → Teacher, but the schema should remain evidence-driven rather than hard-coding a needlessly rigid tree.

Current assignments control present visibility/operational scope. They must not rewrite historical entity identity.

## Device and Agent lifecycle

The Owner surface must ultimately show DeviceId/name, teacher and organization scope, online/last seen, Agent install/service/version/desired-version/update state, latest command/result, recording/live/audio capability, useful disk health, and important runtime errors.

Owner controls include install, uninstall, update, restart, repair, enable/disable, safe re-registration/recovery, logs/status, and command history.

Prefer, if design review confirms it, a two-layer architecture:

1. a small trusted device-management bootstrap/service for secure identity, commands, signed package verification, Main Agent lifecycle, and status/version reporting;
2. the Main Academy Agent for operational capture/attendance/QA functions.

This permits removal/reinstallation of the Main Agent while retaining legitimate academy device management. Re-evaluate before building if an enterprise managed-device platform is selected.

Management commands must be strongly authenticated, Owner-scoped by default for high-impact actions, auditable, idempotent, retry-safe, version-aware, signed/trusted-package based, and explicit about results. Do not implement security evasion or covert persistence.

## Recording retention

Retention must be configurable rather than permanently hard-coded. Support global defaults for standard, QA-flagged, and preserved recordings; per-recording preserve/unpreserve/extend/new-expiry controls; current expiry and rationale; safe deferred evaluation; and audit history.

Add per-teacher/course/severity/category overrides only when they solve a demonstrated need. A policy change must not silently delete recordings immediately.

The Owner policy screen must show usable storage, recent recording volume,
projected retained GB and safety headroom before a retention change is applied.
Normal, pending-candidate and confirmed-QA retention must be distinguishable.
Storage warning/critical thresholds are Owner-configurable within backend safety
validation. Increasing VPS/object-storage capacity must permit the Owner to
increase retention without a code release. Every policy or threshold change is
backend-authorized, versioned and audited, with a deferred dry-run/impact preview
for any change that could make existing media newly eligible for deletion.

## Owner modules

Plan coherent modules for:

1. Overview and academy/system health
2. Users & Access
3. Organization assignments/hierarchy
4. Permission Center and effective-access preview
5. Device Management and command history
6. Recording Policy and storage impact
7. Security/Audit
8. System Settings
9. API, DB, MinIO, LiveKit, ingress, worker, and Agent fleet health

Prioritize clear workflows over feature count. Sensitive grants must be visually obvious without making harmless edits unnecessarily painful.

## Audit and Owner safety

Durably audit high-impact actions with ActorUserId, ActorRole, ActionType, TargetType/TargetId, request/completion times, result/error summary, and safe before/after metadata. Never log secrets.

Owner-only defaults should cover Agent lifecycle, high-impact access grants, and system/retention policy. Prevent Manager self-escalation. Admin must not create/promote Owner unless Owner explicitly delegates it and product policy permits it. Do not use an authenticated-role shortcut around permission checks.

## Delivery strategy

Do not implement this opportunistically inside unrelated phases. First close immediate authorization defects during stabilization, then design and deliver coherent phases. A likely sequence is:

- **O1 — Authorization & Permission Foundation:** catalog, grants, scopes, backend enforcement/tests, Owner editor foundation
- **O2 — Owner Organization Control:** Admin/Manager management and assignments, hierarchy/effective-access UI, audit
- **O3 — Owner Recording Policy:** retention defaults, preserve/extend, expiry evaluation, dashboard/audit
- **O4 — Device Management Foundation:** command/enrollment model, management-service architecture, signed versions/status
- **O5 — Remote Agent Lifecycle:** install/uninstall/update/restart/repair, results, retry/idempotency, Owner UI/audit, managed-device proof

Actual dependency evidence may justify different sequencing. Permission + resource-scope combinations require backend tests; sensitive Owner workflows require browser proof; device lifecycle begins only after secure enrollment/command architecture is approved.
