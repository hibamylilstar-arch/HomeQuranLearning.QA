# TeamsHelper lifecycle

## Purpose

`Academy.Agent.TeamsHelper` reads the signed-in teacher's Microsoft Teams UI through Windows UI Automation and sends evidence to `Academy.Agent.Service` over the secured `AcademyAgent.TeamsEvidence.v1` named pipe.

The main Agent may run as a Windows service, but UI Automation must remain in the active interactive Windows session. The pipe server therefore continues to validate both the exact helper process name and the active-console session. The service must not host or impersonate the interactive UI helper.

## Local lifecycle architecture

S1.2 uses one least-privilege Scheduled Task for the current Windows user:

- task name: `AcademyAgent.TeamsHelper`
- trigger: that user logging on
- recovery trigger: a one-minute repeating trigger for up to ten years; this
  covers Windows builds that do not re-fire `RestartOnFailure` after an
  externally terminated interactive process
- logon type: interactive
- run level: limited
- action: installed `Academy.Agent.TeamsHelper.exe --monitor`
- multiple-instance policy: ignore a second start
- failure policy: restart after one minute, up to ten attempts
- power policy: remain available on battery

The helper also takes a per-session named mutex. This protects manual and non-task launches from creating a second monitor in the same interactive session.

## Files

Installed binaries:

```text
%PROGRAMDATA%\AcademyAgent\Bin\TeamsHelper
```

Runtime state, intentionally separate from replaceable binaries:

```text
%PROGRAMDATA%\AcademyAgent\Users\<Windows SID>\TeamsHelper\Logs\TeamsHelper.log
%PROGRAMDATA%\AcademyAgent\Users\<Windows SID>\TeamsHelper\Logs\TeamsHelper.log.1
%PROGRAMDATA%\AcademyAgent\Users\<Windows SID>\TeamsHelper\State\health.json
```

The installer grants the selected Windows SID Modify access only to that SID-scoped runtime directory. Replaceable binaries remain separate. The log rotates at 2 MiB and retains one previous file. The health snapshot is atomically replaced and records process/session identity, start time, heartbeat time, state and the last non-secret error.

Healthy lifecycle states are:

- `Starting`
- `WaitingForAgent`
- `Idle`
- `Monitoring`

`WaitingForAgent` is expected while the main Agent process or named-pipe server is offline. `Degraded` and `Failed` require investigation.

## Publish and install locally

Run from the repository root in PowerShell:

```powershell
dotnet publish .\src\Agent\Academy.Agent.TeamsHelper\Academy.Agent.TeamsHelper.csproj -c Release -r win-x64 --self-contained false -o .\publish\teams-helper
& .\setup-teams-helper.ps1 -Action Install
```

The setup command must run from an elevated PowerShell session belonging to the signed-in Windows user whose Teams desktop will be observed, because it installs shared binaries below `%PROGRAMDATA%` and prepares the user's SID-scoped runtime ACL. The registered Scheduled Task itself remains limited, stores no password and does not weaken the named-pipe checks.

The installer is idempotent. It stages and validates the published executable, stops only the registered task and exact managed executable, swaps the managed binary directory, registers the task, starts it, and restores the previous binary directory if installation fails.

## Operations

```powershell
& .\setup-teams-helper.ps1 -Action Status
& .\setup-teams-helper.ps1 -Action Start
& .\setup-teams-helper.ps1 -Action Stop
& .\setup-teams-helper.ps1 -Action Uninstall
```

`Status` reports task registration/state/result, exact managed process count, helper and active session IDs, heartbeat state/age and a combined `LIFECYCLE_HEALTHY` result.

Uninstall removes only the per-user scheduled task and the verified managed binary directory. It deliberately preserves logs and health state for diagnosis.

## Verification gates

Before release:

1. Parse `setup-teams-helper.ps1` with the PowerShell parser and require zero errors.
2. Run Agent lifecycle unit tests.
3. Run helper lifecycle, UIA, detector-policy and evidence-state-machine probes.
4. Publish and install from the local release output.
5. Prove exactly one helper in the active session with a fresh heartbeat.
6. Prove `WaitingForAgent` while Agent is stopped and `Idle` after Agent starts.
7. Prove helper recovery after terminating the managed helper process.
8. Prove helper reconnection after Agent stop/start without duplicate evidence.
9. Run full solution build and existing unit/integration gates.
10. Remove temporary publish/proof artifacts where policy permits and return API/Agent/FFmpeg to the documented baseline. If an external temporary path cannot be safely removed by the active automation context, record it as a cleanup warning rather than deleting an unverified target.

Actual sign-out/logon or reboot proof is a human checkpoint because Codex must not terminate the owner's desktop session automatically.

## Preserved semantics

This lifecycle does not change Teams target binding, evidence detection, idempotency keys, delivery journal behavior or attendance reduction. `TeacherGreetingSent`, `CallAttempted`, `StudentCallConnected`, `CallEnded` and `LessonShared` retain their documented meanings.

Production deployment, remote Agent lifecycle, Owner Control Plane, manual sessions and QA transcript persistence remain separate phases.
