# HomeQuranLearning.QA

Private internal QA, attendance and monitoring platform for the HomeQuranLearning online Quran academy.

## Current stack

- Windows Agent — C# / .NET 10
- Backend — ASP.NET Core / EF Core / PostgreSQL
- Dashboard — Next.js / React / TypeScript
- Object storage — MinIO / S3-compatible
- Live monitoring — FFmpeg RTMP -> LiveKit Ingress -> LiveKit -> browser
- System audio — NAudio / WASAPI loopback
- QA/STT — Python / faster-whisper
- Infrastructure — Docker Compose / PostgreSQL / Redis / MinIO / LiveKit

## Current checkpoint

`a983841 docs: close 7A-5C classifier checkpoint`

Latest closed phase: `7A-5C — multilingual QA classifier and evaluation`

Current engineering phase: `VPS staging and production preparation` (high-risk approval received; staging access validation in progress).

Next action: validate the approved production Compose configuration and obtain secure VPS/DNS access facts. See `docs/operations/vps-staging-runbook.md`.

## Important documentation

- `docs/PROJECT-STATE.md`
- `docs/PROJECT-DECISIONS.md`
- `docs/CODEX-WORKFLOW.md`
- `docs/OWNER-CONTROL-PLANE.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/architecture/qa-worker-service.md`
- `docs/architecture/teams-helper-lifecycle.md`
- `docs/decisions/ADR-001-system-architecture.md`
- `docs/decisions/coding-conventions.md`

## Development policy

The project is local-first during development. VPS staging is now authorized, but
production activation remains a separate release decision with measured backup,
retention, multi-laptop and rollback proof.

Do not push major-phase changes without a completed validation summary and explicit owner approval.

On a new Codex task or after reboot, use `Continue project`. Codex must reconstruct state from root `AGENTS.md`, `docs/PROJECT-STATE.md`, Git, and runtime evidence.

## Security

Do not commit passwords, API keys, JWT secrets, storage secrets, cookies or authentication tokens.
