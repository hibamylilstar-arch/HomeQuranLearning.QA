# HomeQuranLearning.QA Working Rules

These rules exist to make engineering faster and more reliable, not to create ceremony.
Use the minimum investigation, verification and process needed for the actual change.

## 1. Authority and continuation

- `docs/architecture/classroom-monitoring-product-contract.md` is the highest authority for intended classroom-monitoring product behavior.
- Current Git source, current runtime evidence and the top CURRENT ACTIVE STATE in docs/PROJECT-STATE.md are authoritative for implementation/runtime state.
- If source, tests, historical docs or earlier decisions conflict with the Product Contract, treat the conflicting implementation as technical debt rather than silently redefining the product.
- Chat history is supporting context only.
- A new AI/session must continue from the latest verified state instead of restarting old investigations.
- Read older historical project-state sections only when they are directly relevant to the current task.
- The user's latest clear decision is the default product direction. Do not repeatedly ask for the same confirmation.
- Do not blindly agree with a technically incorrect decision. Briefly explain the issue and improve the approach.
- Ask a question only when required information cannot be resolved from source/runtime, or a genuinely high-impact irreversible action needs approval.

## 2. User operating model

- The user can copy/paste commands but should not be asked to manually find, replace or edit source files.
- Give complete copy/paste-safe commands with exact paths.
- Clearly separate Windows PowerShell commands from VPS SSH/Bash commands.
- Never mix shell syntaxes.
- Do not ask the user to manually type API keys, passwords, tokens or other secrets when they already exist in approved local/server storage.
- Read required secrets programmatically from existing secure configuration when necessary, use them without printing them, and clear temporary plaintext values.
- Never print or commit secrets.
- Keep an existing VPS SSH/root session open unless the user explicitly asks to disconnect.
- Do not promise background work or future asynchronous completion.

## 3. Fast engineering loop

For a normal small or medium fix:

1. inspect only the exact relevant source/state;
2. identify the failing boundary;
3. make the smallest production-quality fix;
4. run the smallest meaningful targeted verification;
5. if green, continue;
6. update PROJECT-STATE only at a meaningful checkpoint;
7. stage exact intended files, commit and push the feature branch.

Rules:

- Once enough evidence identifies the root cause, stop diagnosing and fix it.
- Do not run broad diagnostics just because they are available.
- Do not repeat a verification that already passed unless subsequent changes could invalidate it.
- Do not run the full solution test suite for a narrowly isolated fix unless the change can realistically affect the wider system.
- Cross-cutting architecture, database, auth, deployment or release changes may justify broader tests.
- Distinguish a command/script/harness error from a product defect quickly.
- If a mutation script partially succeeds and then fails, continue from the actual current state. Do not blindly rerun the whole mutation block.
- After a simple error, fix it and move forward.
- Prefer source-first investigation over speculative runtime probing.
- Do not create ceremonial GO/APPROVE checkpoints for ordinary development.

## 4. Files and documentation

- Do not create extra status files, handoff files, probe files or scripts unless they provide real ongoing value.
- Temporary build/probe material should live outside the tracked repo or be removed after use.
- Do not accumulate obsolete installers, dumps or diagnostic artifacts in tracked source.
- docs/PROJECT-STATE.md is a concise recovery checkpoint, not a command-by-command diary.
- Keep current state in the form: Completed -> Proof -> Current -> Next.
- Historical sections may remain for evidence but must not impose obsolete workflow rules.

## 5. Git workflow

- Preserve unrelated human work.
- Never reset, clean, stash, overwrite or discard unexpected work just to simplify the current task.
- Do not use git add -A.
- Stage exact intended files.
- Normal verified development commits and pushes to the current feature branch do not need separate approval.
- Do not use force push, force-with-lease, hard reset, clean or history rewriting without explicit approval.
- main is the canonical branch, but feature work may remain on a feature branch until a deliberate integration decision.

## 6. Verification policy

- Small isolated code fix: targeted build/test only.
- UI-only fix: relevant lint/build or focused browser check only when needed.
- Agent-only fix: relevant Agent test/build only.
- API-only fix: relevant backend tests/build only.
- Database/schema change: migration plus affected backend verification.
- Production deployment: verify the affected deployed services, not unrelated infrastructure.
- Never fake green or weaken a meaningful assertion to get a pass.

## 7. Production and approval boundary

Explicit user approval is required only for genuinely high-impact actions such as:

- production/VPS deployment or cutover;
- destructive production database or real evidence changes;
- secret rotation;
- production auth/RBAC/security-policy changes;
- firewall or driver changes;
- replacing the proven live/recording transport architecture;
- destructive Git/history operations;
- a main-branch merge/release when it changes production deployment state.

Do not invent approval gates for ordinary fixes, documentation, targeted tests, feature-branch commits or normal pushes.

## 8. Protected HomeQuranLearning invariants

- Managed classroom Agents communicate outbound for normal production operation.
- Durable Agent DeviceId is the machine identity; editable friendly laptop names are user-facing labels; Windows computer names are not durable targeting identity.
- Classroom audio must follow the teacher communication application's effective microphone and playback/render endpoints. USB, Bluetooth, wired and internal/Realtek endpoints are all valid when they are the routes actually used by Teams/Zoom.
- Do not capture unrelated microphones or playback endpoints. If an effective communication route is temporarily unavailable, report the route as unavailable and recover automatically when the communication application exposes a valid route.
- Preserve the proven live/recording media path unless an actual defect requires a scoped change.
- Dashboard live feeds remain muted by default and only one selected feed should be audible at a time.
- Owner-controlled Agent updates target the selected durable device. Audio transport type must not be an installation or update gate.
- Do not reopen already proven updater/live/audio investigations without new evidence of a regression.

## 9. Owner Control Panel

- The correct product name is Owner Control Panel.
- Owner Control Panel is deferred until the final product phase, after the main operational system is otherwise ready.
- Existing older Owner Control Plane documentation is historical/reference material only.
- Do not treat Owner Control Panel as an active dependency, roadmap gate or required current workstream unless the user explicitly starts that final phase.

## 10. Response and execution style

- Be concise and implementation-focused.
- Prefer one reliable command block over many tiny manual steps.
- Do not waste time proving obvious facts repeatedly.
- Surface a discovered blocker once, fix it, and continue.
- If the user proposes a weaker solution, improve it rather than merely accepting it.
- If the user is mistaken, correct the technical point respectfully and proceed with the better implementation when the intent is clear.
- Optimize for: correctness, continuity, minimum manual effort and minimum wasted time.

## Local development runtime

- The user's normal interaction is one original Windows PowerShell terminal only.
- Do not ask the user to open or manage separate API, Dashboard or Agent PowerShell windows.
- API, Dashboard and DEV Agent must run as background processes with logs under the local development runtime.
- Before any task that requires the local application, the AI should include .dev-runtime/LocalDevelopment.ps1 -Action Ensure in its own copy/paste command when needed.
- Ensure is idempotent: use it to start only missing local components instead of restarting healthy components.
- During work, use .dev-runtime/LocalDevelopment.ps1 -Action Status only when runtime state is materially relevant; do not repeatedly check it without reason.
- When local services are no longer needed, the AI should include .dev-runtime/LocalDevelopment.ps1 -Action Stop itself when stopping them provides a benefit.
- Do not make the user remember routine start/stop commands.
- Do not stop a healthy local runtime between consecutive development steps when the next step still needs it.
- Do not restart API, Dashboard, Agent, Docker or other healthy local infrastructure merely as a verification ritual.
- If one local component fails, repair or restart only that affected boundary whenever possible.
- Local DEV Agent identity is separate from the production Owner device identity.
- Local development must target local API/RTMP infrastructure and must not accidentally send Owner development traffic to the VPS.
- Local DEV recordings are disposable and recording remains off by default unless a specific test requires recording.
- Optional same-Wi-Fi testing may use the Owner PC LAN address; another laptop's localhost must never be treated as the Owner PC.
- The production Owner device is a dormant reusable VPS test device, not the normal development device.
- The user should normally only need to copy/paste the complete command block supplied by the AI into the original terminal.

## Classroom communication audio invariant

- The Agent captures the effective microphone/input endpoint and effective playback/render endpoint used by the teacher's Teams/Zoom communication route.
- Device transport and brand are irrelevant: USB, Bluetooth, wired and internal/Realtek are valid when actually used by the communication application.
- Do not capture unrelated active microphones or speakers.
- If Teams/Zoom uses Windows Default or Default Communications, resolve the effective endpoint behind that selection.
- Route changes during a call must recover automatically without reinstall.
- There is no separate student-device endpoint to discover; remote/student speech is the audio arriving on the teacher's communication playback route.
- Read `docs/architecture/classroom-monitoring-product-contract.md` before changing classroom audio, attendance, recording or QA behavior.
## Classroom media priority

- Classroom media priority is audio first. Teacher and student speech must receive the lowest practical latency and continuous delivery; video quality is secondary. Normal live monitoring and Agent recordings target approximately 240p at low frame rate/bitrate to reduce teacher-laptop CPU, network bandwidth and storage/VPS load.

## 11. Owner-first evidence-driven development

HomeQuranLearning.QA is still in active development/trial. The Owner device is the primary physical/runtime canary for Agent behavior before teacher-laptop rollout.

For runtime-affecting work, AI engineers must:

1. Inspect the exact affected source, machine, runtime, path, process, service or data boundary.
2. Never guess facts that can be inspected directly.
3. Reproduce or isolate the actual failing boundary and test that boundary directly.
4. Make the smallest production-quality source fix.
5. Run only targeted verification that can meaningfully validate the change.
6. Validate physical/runtime behavior on the Owner device in the real intended scenario when applicable.
7. Confirm the result behaves exactly as the Owner requested.
8. If it fails, continue from the next unproven boundary; do not reopen already-proven boundaries without contradictory evidence.
9. After conclusive Owner-device proof, treat that behavior as the baseline unless a later change could invalidate it.
10. Only after Owner confirmation should the approved release be deployed and rolled out to teacher laptops.

Mandatory engineering principles:

- Use the evidence-driven sequence: inspect -> isolate -> direct probe -> prove boundary -> next boundary -> root cause -> source fix -> Owner physical validation -> controlled rollout.
- The successful classroom-audio debugging sequence is the model for difficult runtime investigation.
- Do not create extra worktrees, staging directories, probe layers, duplicate packages or verification gates unless they solve a concrete isolation, safety or reproducibility need.
- Do not repeat conclusive verification merely for reassurance.
- Teacher laptops being registered or already used does not turn every development change into a fleet rollout.
- Do not deploy an unproven Agent change across teacher laptops merely because remote update capability exists.
- After Owner confirmation, build from the verified commit, update only VPS services that actually changed, publish through the existing release mechanism, then use per-device Owner-controlled remote updates.
- Inspect exact installed paths, runtime configuration and live service state before acting; do not rely on assumed or historical layouts when the current machine can answer directly.
- Verification exists to establish correctness, not to create ceremony. More verification layers are not automatically better engineering.
- This rule applies to ChatGPT, Codex, Gemini, DeepSeek, Claude and any other AI engineer working on this repository.
