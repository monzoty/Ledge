# ADR-0004: Focus timer is computed from timestamps and lives in memory

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

The focus timer must show accurate remaining time in the pill, survive the
PC sleeping, and be unit-testable. PRD F4 also says that v0.1 doesn't keep a
running timer across an app restart.

## Options considered

1. **Count ticks** (subtract 1 s on each `DispatcherTimer` tick) — simple,
   but it drifts, and it's wrong after sleep because no ticks happen while
   the PC is asleep.
2. **Timestamps** (`StartedAt`, `Duration`, `PausedTotal`; remaining time
   = `Duration − (now − StartedAt − PausedTotal)`) — always correct, sleep
   included; ticks are only for redrawing.
3. **Timestamps + save the session to disk** — survives restarts, but adds
   file format, migration and "what if it ended while the app was closed"
   cases.

## Decision

Use **option 2**: `FocusTimer` in Core reads `now` from the injected
`TimeProvider`. Sessions are in memory only in v0.1.

## Consequences

- Fully testable with `FakeTimeProvider`, including 10-minute "sleep" jumps.
- When the app or PC wakes and the session has already ended, the timer
  fires its "finished" event once (PRD F4.5).
- If the app quits during a session, the session is lost. Saving it to disk
  (option 3) is the natural next step if focus history is added in v0.2.
