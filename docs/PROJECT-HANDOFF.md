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

Do not prioritize media redesign over the current audio mission.

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

## Next engineering mission

Inspect the active Windows Agent audio pipeline end-to-end before changing it.

Prove:

1. physical microphone capture owner
2. playback/loopback capture owner
3. Live/Recording/QA/session/attendance consumers
4. any duplicate physical capture
5. queue/ring-buffer/UDP/resample/FFmpeg waiting stages
6. whether a consumer can block/back-pressure capture
7. effective communication microphone selection
8. effective render/output selection
9. route-change recovery for wired/USB/Bluetooth
10. exact source of teacher echo/repeated voice
11. latency/buffering sources

Target behavior:

- follow the actual communication headset/handfree mic and output
- support wired, USB, Bluetooth and internal endpoints
- resolve Windows Default / Default Communications correctly
- recover automatically after route changes
- eliminate teacher echo
- reduce buffering/delay
- keep one physical capture architecture
- preserve proven Live stability

Do not change LiveKit/media transport during this Agent audio investigation
unless direct evidence points there.

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