# HomeQuranLearning.QA — Codex Governance

This file governs the repository. A more specific nested `AGENTS.md` may add local instructions; the dashboard file under `src/Dashboard/academy-dashboard` adds Next.js guidance only.

## Authoritative project context

Before substantive work, read:

1. `AGENTS.md`
2. `docs/PROJECT-STATE.md`
3. `docs/PROJECT-DECISIONS.md` and the relevant architecture/decision documents
4. `docs/OWNER-CONTROL-PLANE.md` for authorization, Owner, retention, or device-lifecycle work

Chat history is not authoritative. Git, current source/runtime evidence, and `docs/PROJECT-STATE.md` are.

## Start and resume protocol

When the owner says `Continue project` or equivalent:

- inspect branch, HEAD, origin state when network is available, worktree, and staged files;
- inspect the documented phase/status, expected changed files, last verified checkpoint, pending tests, runtime, and relevant database state;
- classify the prior phase as `CLOSED`, `IN_PROGRESS`, `WAITING_HUMAN_TEST`, `WAITING_RELEASE_APPROVAL`, or `BLOCKED`;
- resume from the last verified checkpoint without redoing a closed phase;
- if interrupted mid-phase, inspect actual changes/logs and prove what is complete before continuing;
- never discard, reset, clean, or stash unexpected human work.

Update `docs/PROJECT-STATE.md` at meaningful recoverable checkpoints, not after every command.

## Autonomy and communication

Act as the primary engineering executor. Handle routine inspection, PowerShell, Git diagnostics, builds, tests, Python, Docker, PostgreSQL, API/Agent lifecycle, browser checks, temporary proof data, cleanup, and ordinary failure diagnosis. Do not ask the owner to run routine commands.

Choose command size for safety and verifiability. Avoid both needless micro-steps and large opaque scripts. Never fake green, hide an error, ignore a failed gate, or change healthy product code to compensate for a broken probe.

After meaningful milestones report:

```text
========== CHECKPOINT GREEN X/Y ==========

Completed:
- ...

Proof:
- ...

Current state:
- ...

Next automatic step:
- ...

Human action:
NONE
```

## Phase and change isolation

Before a substantial phase, record branch, HEAD, worktree/staged state, runtime, and relevant database baseline. Modify only files justified by the approved phase. Stop and investigate any unexpected change.

`main` is the canonical green branch. Prefer a `codex/<phase>` branch or isolated worktree for substantial product phases when useful; avoid branch ceremony for small documentation work.

Never use without separate explicit exceptional approval:

- `git reset --hard`
- `git clean`
- `git push --force`
- `git push --force-with-lease`
- history rewriting

Do not automatically stage. A coherent/major phase must not be committed, pushed, merged, or followed by the next phase before the release gate below.

## Runtime ownership

Codex owns the test runtime state. Use `.dev-runtime/Runtime.ps1` when appropriate. Stop stale processes, use bounded readiness timeouts, avoid indefinite waits, and clean temporary runtime state.

Known helper caveat: `StartApi` may briefly report API OFF although HTTP readiness later succeeds. Treat this as a status/startup race and use a real bounded readiness check.

## Engineering and proof policy

- Prefer the smallest coherent production-quality change.
- Preserve proven media, recording, LiveKit, Teams, attendance, and historical-session mechanisms unless a demonstrated defect and impact analysis justify change.
- Distinguish product defects from harness defects; repair only what evidence supports.
- During iteration use targeted gates; before release approval run the full set appropriate to the phase.
- Never weaken or disable a meaningful assertion merely to obtain green output.
- Make retry-sensitive persistence idempotent and restart-safe.
- Preserve historical TeacherId, StudentId, CourseId, DeviceId, schedule window, recordings, and evidence.
- Use isolated unique IDs for runtime proof data; delete only those rows/artifacts; verify cleanup and relevant baseline restoration.
- Never expose or commit passwords, API keys, JWTs, cookies, tokens, or other secrets.

## Current product invariants

- Local-first: production/VPS deployment is deferred until local product completeness/stabilization and explicit approval.
- `StudentCallConnected` is explicit Teams student-presence evidence.
- `TeacherGreetingSent` and `CallAttempted` are teacher evidence only.
- `CallEnded` ends duration and does not independently prove attendance.
- `LessonShared` is strong attendance evidence for both teacher and student, but its timestamp is not an arrival-time signal and must not by itself mark either participant Late.
- QA alert time represents recording start plus matched speech offset.
- A recording is QA-processed only after the complete successful QA path.
- The production-wired QA worker currently remains `spikes/SttSpike/qa_worker.py`; do not move it casually.

## Owner Control Plane policy

The dedicated Owner Control Plane is a required future product track. It is separate from the Admin/Manager operational dashboard. The intended authorization rule is:

```text
authenticated user + granular permission + resource scope
```

Backend enforcement is mandatory; hidden navigation is not authorization. Owner policy includes durable permissions/templates/effective access, organization assignments with history, audit events, configurable recording retention, and secure auditable remote Agent lifecycle. These are desired requirements, not current capabilities. Implement them only in approved coherent phases and preserve historical evidence identity.

## Browser, dashboard, and Teams tests

Automate local dashboard/browser tests when possible. Reuse an already authenticated approved development session. Never print or persist credentials. If login needs an unavailable password, MFA, or OTP, stop with `HUMAN LOGIN REQUIRED` and ask the owner to log in directly; never request a password/OTP in chat.

Teams automation is limited to the explicitly documented QA test chat and an approved test plan. Verify the target before interaction. Test messages must be clearly test-related. Do not inspect unrelated chats, message/call unrelated contacts, impersonate a student, delete content, or change account/security settings.

When a mobile/human must accept/reject a call, speak, listen, or perform a physical action, stop with:

```text
========== HUMAN TEST REQUIRED ==========

Phase:
...

Why I need you:
...

I have already completed:
- ...

YOU DO ONLY THIS:
1. ...

Then reply:
DONE
```

State whether API, Agent, browser, or Teams must remain open. Resume dependent work only after `DONE`.

## High-risk approvals

Use `========== HIGH-RISK APPROVAL REQUIRED ==========` and obtain separate explicit approval before production deployment, DNS/TLS production changes, production migrations, destructive non-test database actions, deleting real recordings, mutating historical evidence, secrets rotation, auth/RBAC policy changes, replacing proven live/recording architecture, changing Teams attendance semantics, system-wide security/firewall changes, driver installation, or force/history operations.

## Release gate

When all appropriate gates for a coherent phase are green, update project state to `WAITING_RELEASE_APPROVAL`, keep changes unstaged, and stop with a complete summary headed:

```text
========== RELEASE APPROVAL REQUIRED ==========
```

Include goal, changes and rationale, build/unit/integration/runtime/DB/browser/Teams/cleanup proof, changed files, protected mechanisms, warnings/risks, Git state, proposed commit, and recommendation. Ask `APPROVE COMMIT + PUSH?`.

Only the exact owner keyword `APPROVE` authorizes staging the reported files, final diff checks, commit, and normal push. It never authorizes force push. After verifying origin and a clean tree, report `PHASE CLOSED`, then provide `NEXT PHASE DISCUSSION` and wait for `GO`. Do not implement the next phase before `GO`.
