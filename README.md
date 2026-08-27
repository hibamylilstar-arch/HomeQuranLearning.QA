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

`415bbec feat(qa): align alerts with transcript timestamps`

Latest closed phase: `S1 - access and attendance stabilization`

Next planned QA phase: `7A-2`; next engineering phase requires post-S1 reassessment.

## Important documentation

- `docs/PROJECT-STATE.md`
- `docs/PROJECT-DECISIONS.md`
- `docs/CODEX-WORKFLOW.md`
- `docs/OWNER-CONTROL-PLANE.md`
- `docs/architecture/current-state.md`
- `docs/architecture/overview.md`
- `docs/architecture/qa-worker-service.md`
- `docs/decisions/ADR-001-system-architecture.md`
- `docs/decisions/coding-conventions.md`

## Development policy

The project is local-first. Production/VPS deployment remains deferred until the planned local product is substantially implemented, tested and stable.

Do not push major-phase changes without a completed validation summary and explicit owner approval.

On a new Codex task or after reboot, use `Continue project`. Codex must reconstruct state from root `AGENTS.md`, `docs/PROJECT-STATE.md`, Git, and runtime evidence.

## Security

Do not commit passwords, API keys, JWT secrets, storage secrets, cookies or authentication tokens.
