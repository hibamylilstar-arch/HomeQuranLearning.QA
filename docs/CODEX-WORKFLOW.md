# Codex Workflow for the Owner

The repository stores durable project context, so a laptop reboot or new Codex task does not require the old chat. Codex reconstructs state from `AGENTS.md`, `docs/PROJECT-STATE.md`, Git, current source, and runtime/database evidence.

## The four normal owner messages

### `Continue project`

Read governance and project state, inspect the real repository/runtime, and resume from the last verified checkpoint. Completed phases must not be repeated just because chat history is unavailable.

### `DONE`

The requested human test/login step is complete. Codex resumes the dependent automated proof from the prepared state.

### `APPROVE`

Approve only the exact commit and normal push described in the latest `RELEASE APPROVAL REQUIRED` summary. Codex reverifies the changed files and gates, stages only those files, commits, pushes without force, and verifies origin/clean state.

### `GO`

Start the detailed next-phase plan discussed after the prior phase was pushed and closed. `GO` is not release approval for unrelated changes.

## Status and approval banners

- `CHECKPOINT GREEN X/Y`: a meaningful automated milestone passed. No response is needed when `Human action: NONE`; Codex continues.
- `HUMAN TEST REQUIRED`: only a real person/device can complete the next proof. Follow the short numbered steps and reply `DONE`.
- `HUMAN LOGIN REQUIRED`: login, MFA, or OTP must be completed directly in the browser. Never paste credentials or OTPs into chat.
- `RELEASE APPROVAL REQUIRED`: the coherent phase is prepared and validated but is not committed or pushed. Review/discuss it, request changes, or reply `APPROVE`.
- `HIGH-RISK APPROVAL REQUIRED`: a sensitive action needs separate, explicit authorization; it is never implied by `GO` or routine approval.
- `PHASE CLOSED`: the approved commit was pushed and branch/origin/clean state were verified.
- `NEXT PHASE DISCUSSION`: proposed scope, exclusions, proofs, human-test points, risks, and alternatives. Discuss it, then reply `GO` only when ready.

## What Codex normally handles

Codex performs routine source inspection, PowerShell, builds/tests, Docker/PostgreSQL work, API/Agent lifecycle, browser checks, approved Teams test automation, temporary proof data, cleanup, Git diagnostics, and failure analysis.

The owner normally intervenes only for physical/mobile/voice actions, MFA/OTP, genuine product choices, high-risk actions, production deployment, and release approval.

## Credentials and Teams

Codex may reuse an already authenticated approved local development browser. Credentials, cookies, API keys, and tokens must never be written into repository governance, source, logs, or chat.

Teams automation is limited to the explicitly documented test target and approved test plan. When a student/mobile user must answer, speak, listen, or act physically, Codex stops instead of simulating success.
