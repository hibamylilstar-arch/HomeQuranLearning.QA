# HomeQuranLearning.QA Engineering Rules

This file governs the repository. A more specific nested `AGENTS.md` may add local instructions; the dashboard file under `src/Dashboard/academy-dashboard` adds Next.js guidance only.

## Authoritative project context

Before substantive work, read the minimum relevant context:

1. `AGENTS.md`
2. `docs/PROJECT-STATE.md`
3. relevant decisions/architecture documents
4. `docs/OWNER-CONTROL-PLANE.md` for Owner, authorization, retention, device lifecycle, or remote Agent management work

Chat history is supporting context only. Git, current source/runtime evidence, and `docs/PROJECT-STATE.md` are authoritative.

## Working model

Treat this as a real production software project.

Normal development flow:

`inspect relevant state -> implement real production-quality slice -> targeted verification -> fix regressions -> update PROJECT-STATE.md -> review intended diff/secrets -> atomic commit -> normal push -> continue`

Do not add process steps that do not reduce risk or uncertainty.

Use branch/HEAD/worktree checks before meaningful product work. Inspect runtime, database, Docker, browser, or external services only when the current slice touches them.

Never discard, reset, clean, or stash unexpected human work.

## Production-first rules

- Build real production-capable paths from the start, even while development remains local.
- No fake production endpoints, fake health data, fake production metrics, placeholder production controls, hidden credentials, hard-coded device IDs, IP-based trust, or localhost-only assumptions in production paths.
- Development-only mocks or proof data are allowed only when clearly isolated and labelled development/test-only.
- A UI control that claims to perform a production action must have a real backend contract; otherwise show it as unavailable/planned, not as working.
- Secrets must come from approved configuration/secret stores and must never be committed, printed, embedded in UI/source, or copied into documentation.
- Agents remain outbound-only for normal production communications.
- Device identity must be unique, revocable, auditable, and independent of roaming IP addresses.
- Preserve historical evidence identity and auditability.

## Change isolation and Git safety

Prefer the smallest coherent production-quality change.

`main` is the canonical green branch. Use a feature branch for substantial product work when appropriate.

Never use without separate explicit exceptional approval:

- `git reset --hard`
- `git clean`
- `git push --force`
- `git push --force-with-lease`
- history rewriting

Do not stage unrelated files, backups, generated noise, temporary probes, or secrets.

Known local line-ending/index noise must be proven harmless before excluding it; never blindly restore/reset it.

For a verified low-risk development slice:

1. update `docs/PROJECT-STATE.md` at a meaningful recoverable checkpoint;
2. review intended diff and secret exposure;
3. create one meaningful atomic commit;
4. normal push to the current feature branch;
5. continue the approved roadmap.

A separate release approval is not required for ordinary verified local development commits/pushes.

## Verification policy

- Prefer targeted gates during iteration.
- Run broader build/test/runtime/browser checks when the slice can affect them.
- Never fake green, suppress a real failure, or weaken meaningful assertions just to pass.
- Distinguish product defects from harness/probe defects.
- After enough evidence identifies the failing boundary/root cause, implement the smallest justified fix and verify it.
- Preserve proven media, recording, LiveKit, Teams, attendance, and historical-session mechanisms unless a demonstrated defect and impact analysis justify change.
- Retry-sensitive persistence must be idempotent and restart-safe.
- Temporary runtime/database proof data must use isolated unique IDs and be cleaned up without touching real evidence.

## Current product invariants

- Development and production use the same production-capable architecture. Verified releases may be deployed to the VPS without artificial staging/pilot gates; production mutations still require the explicit high-risk approval defined below.
- `StudentCallConnected` is explicit Teams student-presence evidence.
- `TeacherGreetingSent` and `CallAttempted` are teacher evidence only.
- `CallEnded` ends duration and does not independently prove attendance.
- `LessonShared`  is strong attendance evidence for both teacher and student, but its timestamp is not an arrival-time signal and must not by itself mark either participant Late.
- QA alert time represents recording start plus matched speech offset.
- A recording is QA-processed only after the complete successful QA path.
- The production-wired QA worker currently remains `spikes/SttSpike/qa_worker.py`; do not move it casually.

## Owner Control Plane

The Owner Control Plane is a first-class product track, separate from the Admin/Manager operational dashboard.

Authorization model:

`authenticated user + granular permission + resource scope`

Backend enforcement is mandatory. Hidden navigation is not authorization.

Owner capabilities must evolve through real backend contracts and audited actions. Planned areas include:

- system health and operational overview;
- device enrollment, assignment, revoke/disable, last-seen, and lifecycle history;
- granular permissions/templates/effective access;
- organization assignments with history;
- audit events;
- configurable recording retention;
- secure, auditable remote Agent lifecycle and future signed updater controls.

Do not implement fake Owner controls. If a capability is not wired end-to-end, mark it unavailable/planned until the backend contract exists.

## Runtime, browser, and Teams

Use `.dev-runtime/Runtime.ps1` when appropriate for local API/Agent lifecycle. TeamsHelper on a development/admin machine should run only when Teams testing requires it.

Use bounded readiness checks and avoid indefinite waits.

Automate local dashboard/browser tests when possible. Never print or persist credentials. If MFA/OTP or a human login is required, stop and ask the owner to perform only that login step.

Teams automation is limited to the documented QA test target and approved test plan. Do not inspect unrelated chats, contact unrelated people, impersonate a student, delete content, or alter account/security settings.

## High-risk approval gate

Stop and obtain explicit owner approval before:

- production/VPS deployment or cutover;
- DNS/TLS production changes;
- production database migrations or destructive non-test DB actions;
- deleting real recordings or mutating historical evidence;
- secret rotation;
- auth/RBAC/security policy changes with production impact;
- replacing proven live/recording architecture;
- changing Teams attendance semantics;
- system-wide firewall/security changes;
- driver installation;
- force/history Git operations;
- merge/release to canonical production branch when that changes deployment state.

For these actions use:

`========== HIGH-RISK APPROVAL REQUIRED ==========`

Explain the exact change, impact, rollback, evidence, and requested approval.

## Project state and checkpoints

Update `docs/PROJECT-STATE.md` at meaningful recoverable checkpoints and before a commit that closes a coherent slice.

Keep it factual:

- branch/HEAD;
- current phase/status;
- completed work;
- verification proof;
- known issues/regressions;
- intentionally uncommitted/noise files;
- next production-development slice;
- whether anything was pushed/deployed.

Do not turn project state into a command-by-command diary.

After a meaningful checkpoint, a concise report is enough:

`Completed -> Proof -> Current state -> Next`

Do not require ceremonial `GO`/`APPROVE` gates for ordinary low-risk development.

## Human actions

Ask the owner to perform a local command or physical action only when this environment cannot perform it or when a human action is inherently required.

Commands given to the owner must be exact, copy/paste-safe, and preferably single-line when PowerShell continuation prompts could cause confusion.

For a physical/mobile test, state exactly what must remain running and what single action the owner should perform.
