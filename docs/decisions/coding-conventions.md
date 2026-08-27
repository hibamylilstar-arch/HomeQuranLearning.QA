# HomeQuranLearning.QA — Engineering Conventions

## General

Prefer the smallest coherent production-quality change.

Do not modify unrelated proven code merely to make a temporary probe pass.

Every feature should consider failure behavior, retry behavior, idempotency, historical-data safety, RBAC, restart/recovery, observability, cleanup and maintainability.

## Repository safety

Before a substantial phase, record branch, HEAD, worktree, staged state, relevant runtime and relevant database baseline.

Unexpected changed files must be investigated rather than discarded. Do not automatically overwrite or stash unknown human changes.

## Git

`main` is the canonical green branch.

Never use without explicit exceptional owner approval:

```text
git reset --hard
git clean -fd
git push --force
git push --force-with-lease
```

Major/coherent phase commit and push require explicit release approval.

## Database

Use EF Core migrations for persistent schema changes. Never rewrite real historical session identity for convenience. Runtime proof data must use isolated unique IDs and be cleaned after proof. Retry-sensitive persistence must be idempotent.

## Agent

Preserve known-good media and Teams mechanisms unless a demonstrated defect requires change.

Do not add Task Manager hiding, Windows security bypass, antivirus evasion, admin-evasion persistence or credential theft.

## Attendance

Current Teams policy:

- TeacherGreetingSent: teacher evidence
- CallAttempted: teacher evidence
- StudentCallConnected: explicit student presence
- CallEnded: stop/duration evidence
- LessonShared: teacher evidence only

Do not infer student presence from `LessonShared`.

## QA/STT

QA alert timestamps must represent speech time rather than processing time.

Failed QA/STT processing must not falsely mark a recording complete.

Retry paths must avoid duplicate durable evidence.

## Testing

Do not ignore failing tests, redefine a failure as success, or disable an important assertion to obtain green output.

If the test harness is wrong but product code is healthy, fix the harness.

## Documentation

`docs/PROJECT-STATE.md` is the resumable active project checkpoint.

`docs/architecture/current-state.md` describes current implementation.

Architecture ADRs explain strategic decisions.

`docs/OWNER-CONTROL-PLANE.md` is authoritative desired product policy. It must remain clearly distinguished from current implementation evidence.

## Authorization direction

Protected operations must converge on backend enforcement of authenticated user + granular permission + resource scope. Role names and hidden dashboard navigation are insufficient by themselves.

Immediate authorization defects belong in an explicit stabilization phase. The full Owner Control Plane must be implemented later in coherent O1–O5-style phases, not opportunistically inside unrelated work.
