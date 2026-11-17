# ADR-0005: A bounded synchronous wait for the final save on exit

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

Tasks are saved automatically about 300 ms after the last change
(`TaskAutoSaver`). When the app exits, including when Windows shuts down, a
change made in those last 300 ms may not be on disk yet.

WPF's `Application.OnExit` is synchronous, so it can't `await` the final save.
CLAUDE.md forbids blocking waits (`.Wait()`, `.Result`) because they freeze
the UI.

## Options considered

1. **Wait synchronously in `OnExit`, with a time limit.** It breaks the
   no-blocking rule, but at a moment when no UI is left to freeze.
2. **Never wait.** You accept losing at most the last ~300 ms of changes on
   shutdown.
3. **Cancel the shutdown, `await` the save, then shut down again.** Fully
   async, but on Windows shutdown it shows "Ledge is preventing shutdown", and
   it's complex.

## Decision

Use **option 1**. `App.OnExit` runs `TaskAutoSaver.FlushAsync()` on a
thread-pool thread and waits for it for **at most 2 seconds**. This is the
**only** permitted blocking wait in the codebase.

## Consequences

- Changes made just before Quit or shutdown are saved.
- In the worst case (a stuck disk), exit is delayed by 2 seconds. The save
  itself is atomic (`AtomicFile`), so even a save cut off by the time limit
  can't corrupt `tasks.json`.
- A Quit command with UI (the tray in M4) should still `await FlushAsync()`
  normally before calling `Shutdown()`. Then nothing is left for `OnExit` to do.

## Amendment (2026-11-17, M4 step 4e): the log flush

The file logger queues lines and writes them on a background task.
`FileLoggerProvider.Dispose()` waits **at most 1 second** for the queued
lines. It runs only on exit and after a crash, when no UI is left to freeze.
This is the same kind of exception as above: bounded, and only while
shutting down. The crash handler also uses the save flush above before Ledge
closes.
