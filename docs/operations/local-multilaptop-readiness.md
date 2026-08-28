# Local Multi-Laptop Readiness — Phase 7A-4

## Purpose

Prove that multiple academy-owned teacher endpoints can register, heartbeat, submit recordings, and recover independently before any VPS work.

## Automated proof completed — 2026-08-28

- Three unique device identities registered through `/api/agent/heartbeat`.
- Two heartbeats per device updated the same device identity instead of creating duplicates.
- One recording was submitted per device through `/api/agent/recordings`.
- Replaying the same device/file submission returned the original recording ID for all three devices.
- Device-specific recording storage keys remained isolated.
- All proof recordings, heartbeats and devices were removed by exact ID.
- Database returned to the pre-proof baseline: 20 sessions, 163 session events, 2 retained recordings, 0 QA alerts, 0 transcript segments and 3 devices.

Regression gates:

- Agent tests: 3/3 GREEN.
- Unit tests: 81/81 GREEN.
- Integration tests: 3/3 GREEN.
- Full solution build: 0 warnings, 0 errors.
- Runtime after proof: API OFF, Agent OFF, FFmpeg 0.

The reproducible package builder is `scripts/Prepare-LocalAgentTestPackage.ps1`.
It creates a self-contained Agent and TeamsHelper bundle with LAN/test-only
configuration; see `docs/operations/local-agent-test-package.md`.

## Physical acceptance completed — 2026-08-28

The controlled second-laptop test used `DESKTOP-RAAFV2I` over the local LAN:

- A fresh durable device identity registered independently and emitted heartbeats.
- Wi-Fi loss produced a bounded connection failure; reconnect and Agent restart recovered without changing the device identity.
- Eight physical-test recordings uploaded under the correct device-specific storage prefix.
- The authenticated dashboard showed the device, all recordings and working video playback without browser console errors.
- A fresh compressed-package run produced a 50.4-second playable H.264/AAC sample at 1366x768 and 5 FPS. The file was 3,075,494 bytes and 488 kbps; PowerShell text remained readable.
- A follow-up system-audio sample was 48.2 playable seconds and 3,994,379 bytes. It contained AAC at 64.8 kbps, 32 kHz, mono with a -22.1 dB mean level. Whisper detected the speech, produced eight timestamped Urdu transcript segments and marked the recording QA-processed.
- The Unicode transcript exposed a Windows-service stdout encoding defect: the worker repeatedly failed with a `charmap` error while printing non-Latin text. The worker now configures stdout/stderr as UTF-8 and its service launcher sets UTF-8 mode. A forced-cp1252 self-test and the same physical recording both passed after the fix.
- All eight test objects, recording rows, the test device and its heartbeats were removed by exact identity after proof.
- Database cleanup restored 20 sessions, 163 session events, 2 retained recordings, 0 QA alerts, 0 transcript segments and 3 devices.

Teams UI/headphone routing was not re-tested in this storage-focused pass. The already-proven TeamsHelper lifecycle was not changed. Because capture follows the Windows default render endpoint, a real teacher Teams + headset test remains mandatory before VPS acceptance. VPS and production deployment remain deferred.

## Recording storage profile

Physical samples from the earlier profile measured roughly 1.6–2.8 Mbps and could not fit the planned load. The bounded profile is now:

- native 1366x768 capture at 5 FPS;
- H.264 `veryfast`, CRF 32;
- 700 kbps video maximum rate with a 1,400 kbps buffer;
- AAC 64 kbps, 32 kHz, mono;
- 3-day normal retention and 7-day QA-evidence retention.

Six representative recordings transcoded to this profile used 18.84 MiB instead of 95.89 MB, approximately a 79% reduction. The physical Agent sample measured 488 kbps with silence. Capacity planning must use the more conservative 764 kbps video-plus-audio ceiling, not the silent sample.

At 90 recorded-hours per day, the conservative ceiling is approximately 30.9 GB (28.8 GiB) per day. Steady retained recording storage is:

```text
daily storage x (3 + 4 x QA fraction)
```

Approximate conservative scenarios:

| QA-retained share | Retained recording storage |
| ---: | ---: |
| 0% | 92.8 GB / 86.5 GiB |
| 10% | 105.2 GB / 98.0 GiB |
| 20% | 117.6 GB / 109.5 GiB |
| 30% | 130.0 GB / 121.0 GiB |
| 50% | 154.7 GB / 144.1 GiB |
| 100% | 216.6 GB / 201.7 GiB |

A nominal 200 GB VPS is therefore viable only with operational headroom and QA-retention monitoring. It is not safe if every recording remains QA evidence for seven days. Before VPS staging, reserve space for the OS, database, logs and object-storage overhead, add disk alerts, and confirm the expected QA-retained percentage.
