# HomeQuranLearning.QA Working Rules

These rules exist to make engineering faster and more reliable, not to create ceremony.
Use the minimum investigation, verification and process needed for the actual change.

## 1. Authority and continuation

- Current Git source, current runtime evidence and the top CURRENT ACTIVE STATE in docs/PROJECT-STATE.md are authoritative.
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
- Teacher audio must use the verified genuine USB headset/microphone path and must not fall back to internal/Realtek microphone capture.
- Missing or ambiguous verified USB teacher microphone fails closed for teacher-input capture without unnecessarily stopping permitted recording/live video.
- Preserve the proven live/recording media path unless an actual defect requires a scoped change.
- Dashboard live feeds remain muted by default and only one selected feed should be audible at a time.
- Owner-controlled Agent updates target the selected durable device and the communication microphone is the final install safety gate.
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

## Classroom USB headset invariant

- Classroom USB audio is brand/model independent: Logitech, HP, Jabra, generic and other standard USB headsets must be detected from verified physical USB ancestry, never from manufacturer/name hard-coding. When a teacher disconnects one USB headset and connects another verified headset with playback + microphone endpoints, Agent audio must automatically recover onto the new headset without reinstalling the Agent. Realtek/internal audio fallback remains forbidden.
