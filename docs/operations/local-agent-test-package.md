# Local Agent test package

The repository does not currently ship a production installer for the full
Windows Agent. For the controlled 7A-4 physical test, prepare a self-contained
test package from the approved branch:

```powershell
Set-Location C:\Dev\HomeQuranLearning.QA
& .\scripts\Prepare-LocalAgentTestPackage.ps1 `
    -ApiBaseUrl "http://<API-HOST-LAN-IP>:5100"
```

The script publishes both `Academy.Agent.Service` and `Academy.Agent.TeamsHelper`,
writes a test-only `appsettings.json`, disables live streaming, enables short
recording, copies the TeamsHelper setup script, and creates a SHA256-reported
ZIP beside the generated folder. The default outputs are
`publish\local-agent-test` and `publish\local-agent-test.zip`; both are ignored
by Git.

## Host laptop

Start the API on a LAN-reachable address for the test only. The existing local
development launcher binds to `localhost`, so other laptops cannot use it.
Do not expose the API to the public Internet. If Windows Firewall prompts for
the temporary private-network rule, accept only the scoped local test rule.

From the repository on the API host, the bounded local-test command is:

```powershell
Set-Location C:\Dev\HomeQuranLearning.QA
dotnet run `
    --project .\src\Backend\Academy.Api\Academy.Api.csproj `
    --no-build `
    --urls http://0.0.0.0:5100
```

Use the host laptop's private IPv4 address (for example,
`http://192.168.x.x:5100`) when generating the package. Do not use
`localhost` or a public/VPS address for this acceptance test.

## Each test laptop

Copy the complete generated folder, verify that FFmpeg is installed and matches
the configured path, then run:

```powershell
Set-Location C:\Path\To\local-agent-test\agent
& .\Academy.Agent.Service.exe
```

If Teams evidence is part of the approved test, open an elevated PowerShell and
run:

```powershell
& .\setup-teams-helper.ps1 -SourceDirectory .\teams-helper -Action Install
```

The Agent creates a unique identity in
`C:\ProgramData\AcademyAgent\device.json`. Do not copy that file from another
laptop. Verify the new device heartbeat before starting a short test recording.

This package is for local acceptance only. It is not a VPS/production
deployment mechanism, and it must not be used with real student recordings or
unrelated Teams chats.
