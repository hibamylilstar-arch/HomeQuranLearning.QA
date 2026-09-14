# AI / Codex Workflow for the Owner

Repository docs are durable project memory.

Any new ChatGPT, Codex, Gemini, DeepSeek, Claude or other engineering assistant
must read:

1. `AGENTS.md`
2. `docs/PROJECT-HANDOFF.md`
3. `docs/PROJECT-STATE.md`
4. `docs/architecture/current-state.md`
5. `docs/PROJECT-DECISIONS.md`
6. latest Git history
7. exact affected source/runtime

Do not repeat completed phases because old chat history is unavailable.

For difficult runtime issues:

inspect -> isolate -> direct probe -> prove -> smallest fix -> targeted verification.

For Agent changes:

source change -> targeted verification -> Owner physical canary -> immutable
reusable release -> selective teacher rollout.

Never put secrets/tokens/stream keys in docs or chat.

Never use top-level `set -e` or `exit 1` in the Owner's interactive VPS root
shell.

Current continuation is the Windows Agent audio pipeline investigation in
`docs/PROJECT-HANDOFF.md`.