# HomeQuranLearning.QA - Canonical Engineering Handoff

Last verified: 2026-09-15

Read this first in any new AI session. Source/runtime evidence wins over prose.

## Current checkpoint

- Branch: `codex/qa-direct-audio`
- Local repo: `C:\Dev\HomeQuranLearning.QA`
- VPS repo: `/opt/homequranlearning`
- Latest deployed application-code SHA:
  `e4590085ed99aa3de3ad4bf80769f79771615ca2`
- VPS tracked `infrastructure/docker/Caddyfile` has intentional local
  production changes. Do not reset/stash/overwrite it casually.

## Priority

1. Audio reliability
2. Live Monitoring
3. QA
4. Recording

## Current live path

Windows Agent -> FFmpeg -> RTMP -> MediaMTX -> LiveKit Ingress -> LiveKit
-> dashboard.

Screen capture uses the proven `ddagrab` path.

Current production transport is RTMP/Ingress. WHIP/no-transcode is a future
optimization, not the deployed path.

## Current audio rule

Physical classroom audio should be captured once and fanned out.

Shared foundation:

- `ClassroomAudioRuntime`
- `ClassroomAudioHub`

Canonical classroom conversation:

- teacher effective communication microphone
- effective communication playback/render route

QA must not open a second physical microphone pipeline.

## Current QA architecture

Supported path:

`ClassroomAudioHub`
-> `QaLocalVoskWorker`
-> restricted rules
-> local Vosk detection
-> local WAV evidence
-> direct restricted-alert API
-> `QaAlert`
-> dashboard

Evidence is approximately 10 seconds before + 20 seconds after detection.

Current actions: Review / Ignore / Reopen.

Manager visibility remains limited to assigned teachers.

Retired and must not be restored:

- server Python QA worker
- faster-whisper server QA
- continuous QA audio chunks
- QA Candidates
- transcript persistence
- semantic/off-topic classifier experiments
- recording QA polling
- chunk-backed alerts
- retired local STT spike

`RemoveLegacyQaExperiments` is already deployed to production.
Do not repeat that migration deployment.

## Recording reliability - closed incident

A registrar retry storm was caused by stale `.ready` markers whose historical
stream keys no longer mapped to current device keys.

Four historical orphan archives were recovered and registered.

Permanent production fix in `e4590085...`:

- archive target includes stable `DeviceId`
- recorder writes `.device-id` per stream directory
- registrar resolves sidecar identity before current stream-key lookup
- registrar uses bounded exponential retry backoff

Verified after deployment:

- recovered historical archives: 4/4
- target orphan markers remaining: 0
- current `.ready` marker count: 0
- active `.device-id` sidecars present
- registrar returned to effectively idle CPU

Do not remove this identity/backoff behavior.

## VPS capacity finding

Current VPS: 4 vCPU, about 7.8 GiB RAM.

Major scaling cost is RTMP -> LiveKit Ingress transcoding CPU.

A custom one-layer ingress profile was tested and did not materially reduce
CPU. Do not repeat it as a capacity fix without new evidence.

For about 15 simultaneous classes:

- short term: more VPS vCPU
- later: proven no-transcode/WHIP publishing

This capacity note is not an instruction to begin media redesign unless the
Owner selects that work.

## Owner canary

Owner canary: `DESKTOP-PUFUU3U`.

For Agent changes:

inspect exact source/runtime
-> smallest evidence-based fix
-> targeted verification
-> Owner physical canary
-> immutable reusable Agent release
-> selective teacher rollout

Do not update teacher laptops with unproven Agent changes.

## Current issue / task state

No active teacher echo, repeated-voice, buffering or delay defect is currently
established.

Earlier chat observations were diagnostic/observational only. They must not be
treated as an unresolved product bug. If such symptoms existed previously, the
Owner considers them fixed/resolved unless fresh evidence shows otherwise.

Future AI engineers must not automatically begin an audio investigation from
historical chat context.

Start audio troubleshooting only when:

1. the Owner reports a current reproducible symptom; or
2. fresh runtime evidence demonstrates a regression.

The existing audio product contract still requires the Agent to follow the
effective communication microphone/render route, support valid wired/USB/
Bluetooth/internal endpoints, share canonical classroom audio where designed,
and preserve Live stability. Those are architecture requirements, not a claim
that the current runtime is failing them.

There is no standing engineering task implied by this handoff. Continue from
the Owner's current request and the latest verified project state.

## Closed boundaries - do not re-prove without new evidence

- obsolete local `AcademyQaWorker` caused an earlier Owner CPU/live issue and
  is retired
- `ddagrab` video path is a proven baseline
- RTMP LiveKit Ingress transcode is the major VPS media CPU cost
- one-layer ingress tuning did not solve that cost
- four archive orphans are recovered
- registrar retry storm is understood and permanently mitigated
- QA cleanup migration is already deployed
- server QA/candidate/transcript architecture is removed

## VPS shell safety

Never use top-level `set -e` or `exit 1` in the Owner's interactive root SSH
shell.

Use risky strict-mode work inside a subshell and return to the parent shell.

## Verification history

QA cleanup baseline previously passed:

- full .NET solution build
- Unit: 148 passed
- Integration: 4 passed
- Agent: 173 passed
- Next.js production build
- active legacy QA references: zero
- `git diff --check`

For `e4590085...` specifically:

- backend Release build: PASS
- Python syntax: PASS
- focused `ServerArchiveTargetTests`: 1/1 PASS
- controlled VPS deployment: PASS

Do not claim the full suite was rerun after `e4590085`.

## New-session read order

1. `AGENTS.md`
2. `docs/PROJECT-HANDOFF.md`
3. `docs/PROJECT-STATE.md`
4. `docs/architecture/current-state.md`
5. `docs/PROJECT-DECISIONS.md`
6. latest Git history
7. exact affected source/runtime