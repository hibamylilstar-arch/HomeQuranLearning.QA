# ADR-001: System Architecture

* Status: Accepted
* Date: 2026-08-20

## Context

HomeQuranLearning needs an internal QA/monitoring system covering \~20+
teacher laptops (academy-owned, mixed remote/local networks, varying ISPs).
A similar system already exists (built by a third party) and serves as a
functional reference, but the academy wants a more professional, polished,
and purpose-built version under its own domain
(`homequranlearning.com` / `qa.homequranlearning.com`), not a raw cloud IP.

The system is for internal use on academy-controlled devices only â€” it is
not a commercial product and does not need to support arbitrary third-party
tenants.

## Decision

1. **Three-tier system**: Windows Agent (teacher laptops) â†’ Cloud Backend â†’
Web Dashboard, connected via outbound-only agent connections so no
static IP or LAN access is required on the teacher side.
2. **Modular monolith backend** (ASP.NET Core, layered as Domain /
Application / Infrastructure / Api) rather than microservices or
Kubernetes. At the current scale (\~20 devices), a monolith is simpler to
build, deploy, and reason about, with a dedicated media server split out
as the one separate service that genuinely needs to be.
3. **PostgreSQL for relational data; object storage for media.** Recordings
are never stored directly in the database â€” only metadata is. This keeps
the database small and fast and avoids VPS disk exhaustion.
4. **WebRTC (via an SFU such as LiveKit) for live monitoring**, rather than
plain HTTP/MJPEG streaming, to get acceptable latency for live
screen/audio viewing across varying teacher-side network conditions.
5. **Tiered QA detection** (keyword â†’ phrase/context â†’ optional AI
classification) rather than flat keyword matching, to control false
positive rates on sensitive alerts (e.g. contact-sharing detection).
6. **Docker Compose on a single Ubuntu VPS** as the initial deployment
target, not Kubernetes â€” reflecting current scale and operational
capacity. Reverse proxy (Caddy or Nginx) terminates TLS and routes to
the dashboard, API, and media server subdomains.
7. **Delivery is phased and vertical-slice-first**: prove the full
pipeline (capture â†’ record â†’ store â†’ play back â†’ transcribe â†’ alert) on
one laptop before scaling out to 3, 10, then 20+ devices, and before
moving from local dev to the cloud VPS.

## Consequences

* Simpler operations and lower infrastructure cost at current scale, at the
cost of needing a deliberate future decision point if the academy grows
well beyond \~20 devices (at which point the monolith/microservices
trade-off should be revisited).
* Object storage is a hard dependency from early on (Phase 1/2), so it must
be provisioned before recording upload is built, even in local
development.
* Because agents connect outbound-only, the backend must support
device-initiated WebSocket/WebRTC signaling rather than server-initiated
connections to agents.
* Tiered QA detection requires more upfront design (rule schema, severity
levels) than a flat keyword list, but avoids alert fatigue from false
positives.

## Alternatives considered

* **Kubernetes / microservices from day one** â€” rejected as unnecessary
operational overhead at \~20-device scale.
* **Storing recordings in PostgreSQL** â€” rejected due to database bloat and
backup/performance implications at scale.
* **Plain HTTP/MJPEG live streaming** â€” rejected due to latency unsuitable
for real-time monitoring.
* **Keyword-only QA matching** â€” rejected due to high false-positive risk
on short/common trigger words.




