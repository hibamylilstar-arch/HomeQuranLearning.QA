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

`f4426c1 fix(dashboard): refine login role copy`

Latest closed phase: `7A-5C — multilingual QA classifier and evaluation`

Latest released phase: `VPS direct-IP staging preparation and commercial branding` (`20f2fdb`, closure at `f4426c1`).

Latest closed phase: secured real-data VPS pilot preparation (`b61c1d2`). It adds public-IP HTTPS, exact source allowlisting, hardened package/config validation and responsive login proof. No remote deployment or old-VPS-folder deletion has occurred; the next phase starts with read-only VPS inventory as defined in `docs/operations/vps-staging-runbook.md`.

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

Local approved branches remain the engineering source. A bounded real-academy
VPS pilot is authorized, but release, deployment, retention, expansion and final
production activation remain evidence-based gates with measured backup, storage,
accuracy and rollback proof.

Do not push major-phase changes without a completed validation summary and explicit owner approval.

On a new Codex task or after reboot, use `Continue project`. Codex must reconstruct state from root `AGENTS.md`, `docs/PROJECT-STATE.md`, Git, and runtime evidence.

## Security

Do not commit passwords, API keys, JWT secrets, storage secrets, cookies or authentication tokens.
