# Home Quran Learning VPS staging runbook

## Scope

This runbook prepares the Cobanto UK 8 GB / 200 GB VPS for a controlled
staging deployment of the **Home Quran Learning Operations Suite**. The
Admin/Manager entry surface is the **Operations & Quality Console**. The product
owner and developer identity shown in the dashboard is **Abdul Wahid**. This is an
academy-internal system; it must not be exposed as a public SaaS product.

Staging is a separate release checkpoint. It does not copy real recordings or
production credentials from local development.

## Release topology

```text
teacher laptops (temporary outbound HTTP; synthetic staging only)
        |
        v
VPS public IPv4 (temporary Caddy HTTP staging)
        |-- dashboard (same-origin browser UI)
        `-- /api/* -> ASP.NET API

PostgreSQL  |  Redis  |  MinIO  |  QA worker
```

The initial VPS deployment remains a single Ubuntu host with Docker Compose.
LiveKit/live monitoring is not enabled by this staging slice until its own
network and TLS proof is approved. Direct-IP HTTP staging accepts only synthetic
test data because dashboard credentials and Agent API keys are not encrypted in
transit.

## Required owner-supplied infrastructure facts

Provide these through the approved secure access channel, never in chat:

- VPS public IPv4 address and Ubuntu version;
- SSH access available to the Codex host, or an owner-run terminal session;
- the intended owner login email (the password is entered only in the secure
  environment, never committed or pasted into chat).

No password, API key, JWT signing key or cookie belongs in Git or this runbook.

## Deployment order

1. Verify host OS, disk, memory, time sync, firewall and Docker versions.
2. Create a least-privilege deployment directory and a root-owned secrets file.
3. Transfer only the approved Git release checkout; do not copy local `.env` or
   recordings.
4. Generate unique production/staging secrets on the host.
5. Run `docker compose config` with the filled environment file and validate
   every image, volume and exposed port before starting anything.
6. Start PostgreSQL, Redis and MinIO; verify health and create the recordings
   bucket with private access.
7. Start the API and apply only the committed EF migrations.
8. Start the dashboard and QA worker; verify `/health`, authenticated login,
   candidate review authorization and worker-to-API authentication.
9. Expose Caddy on port 80 for bounded IP-based staging and restrict the VPS to
   approved test operators/devices where practical. Domain/TLS is a later
   production gate.
10. Install one staging Agent package configured for the staging HTTP URL;
    prove heartbeat, headset provenance, recording upload, playback and QA
    candidate creation with synthetic/test data.
11. Expand to four or five test laptops and verify independent device identity,
    retry/recovery, storage growth and cleanup.
12. Capture the complete evidence report. Production activation remains a
    separate release approval.

## Storage and retention guardrails

The current planning baseline is 90 recorded-hours/day, three-day normal
retention and seven-day confirmed-QA retention. On a 200 GB disk this is not
safe if every recording remains QA evidence. Before admitting real recordings:

- measure actual daily recording volume;
- reserve OS, database, logs, Docker layers, MinIO multipart and free-space
  headroom;
- configure disk warning/critical thresholds;
- verify pending-candidate and confirmed-QA retention separately;
- test exact cleanup and backup restore with staging-only data.

Retention policy changes are Owner Control Plane work and are not silently
introduced by this deployment.

## Local-to-VPS development flow

- Local `main` remains the canonical integration branch.
- Each VPS deployment uses an immutable release commit/tag.
- Fixes continue locally on `codex/<phase>` branches, pass the normal release
  gates, then receive a new release approval.
- The VPS is updated by pulling the approved release and running the bounded
  migration/compose procedure; never edit source directly on the VPS.
- Rollback means selecting the previous known-good release and restoring only
  the migration/data state proven safe for that release.

## Laptop package identity

The production Agent package will display:

- **Home Quran Learning Classroom Agent**;
- owner/developer: **Abdul Wahid**;
- academy API host: `http://<VPS_PUBLIC_IP>` for synthetic staging only, then
  the approved production HTTPS hostname when domain/TLS is enabled.

Each laptop receives a newly generated device identity. `device.json` must never
be copied between laptops.
