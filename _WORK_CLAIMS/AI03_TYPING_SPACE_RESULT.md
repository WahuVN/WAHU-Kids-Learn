# AI03 — Typing Space Ship Combat / Boss Handoff

Base: `origin/main @ 6e70f52`
Branch: `ai03-typing-space-combat-0909`

## Ownership delivered
- Pure ship action state: `Idle`, `Charging`, `Firing`, `RescueBeam`, `UnlockBeam`, `BossStrike`.
- `TYPING_CHAR_CORRECT` -> charge pulse, capped safely; wrong char does not remove charge or alter boss HP.
- `TYPING_WORD_COMPLETED` maps by frozen `TypingTarget.kind` binding:
  - `shoot` -> Firing + `TARGET_DESTROYED`
  - `rescue` -> RescueBeam + `TARGET_RESCUED`
  - `unlock` -> UnlockBeam (no invented non-contract core event)
  - `boss` -> BossStrike + `BOSS_HIT`; exactly 3 unique boss-word hits -> one `BOSS_DEFEATED`.
- Duplicate target completion is idempotent/exactly-once, including concurrent callbacks.
- Short action windows are presentation-only. Typing input is never locked by firing/beam/boss cooldown; only `GAME_PAUSED` sets `InputLocked=true`.
- Pause freezes combat clock; resume shifts action deadline by actual paused duration.
- Renderer/VFX hooks: `StateChanged`, `OutputEmitted`; subscriber exception/mutation is isolated and cannot corrupt gameplay or another subscriber.
- Direct frozen event adapter: `HandleCoreEvent(type, targetId, value, text, timestamp)`.
- Thread-safe controller lock; 100 parallel correct-char callbacks and 32 duplicate word-complete callbacks covered by regression.

## Files
- `src/TypingSpaceCombat/TypingSpaceCombatModels.cs`
- `src/TypingSpaceCombat/TypingSpaceBossController.cs`
- `src/TypingSpaceCombat/TypingSpaceCombatController.cs`
- `src/TypingSpaceCombat/WAHU.TypingSpace.Combat.csproj`
- `tests/TypingSpaceCombatRuntimeSmoke/Program.cs`
- `tests/TypingSpaceCombatRuntimeSmoke/WAHU.TypingSpaceCombatRuntimeSmoke.csproj`

## AI10 integration contract
1. When AI02 exposes/spawns a full `TypingTarget`, call `RegisterTarget(target.id, target.kind)` once. The frozen core event payload does not carry `kind`, so do not infer it from `text`.
2. Feed AI01 core events into `HandleCoreEvent(...)` or construct `TypingCombatEvent` 1:1 from `{type,targetId?,value?,text?,timestamp}`.
3. Renderer/VFX reads `CurrentState` and/or subscribes `StateChanged`.
4. Republish `OutputEmitted.Event` to the shared core bus for `TARGET_DESTROYED`, `TARGET_RESCUED`, `BOSS_HIT`, `BOSS_DEFEATED`.
5. Do not route AI03 outputs back into AI03 as inputs; AI03 intentionally consumes only typing/pause events.
6. AI10 may add the two csproj files to the solution/integration build; AI03 intentionally does not edit shared `.sln` or App shell.

## Gates
- Release/x86 build: PASS, 0 warnings, 0 errors.
- Runtime smoke: `TYPING_SPACE_COMBAT_RUNTIME_SMOKE_PASS assertions=115`.
- Final concurrent stress: 50 consecutive runs PASS, 115 assertions/run.
- No map/UI/audio/reward/persistence files modified.
