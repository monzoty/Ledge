# MEMORY.md — Ledge project log

> The project's working memory: where we are, and what we learned the hard
> way. Decisions with lasting impact still go in `docs/adr/`. Design values
> go in `docs/DESIGN.md`, and the plan in `docs/ROADMAP.md`. This file
> doesn't repeat them; it records the things you'd otherwise rediscover.
>
> Update it at the end of every step: the status, plus any new gotcha.

---

## Current status

- **Milestone:** M4, "Lives in the background", on branch `m4-background`.
- **M4 is complete and merged into `main`** (2026-11-17). Its hand-run
  checklist is still pending (reboot and it's just there, configured). Fix
  anything it finds on a `fix/...` branch.
- **M5 (focus timer)** is on branch `m5-focus-timer`.
  - **5a** (Core timer) is committed.
  - **5b** (timer row, Ctrl+Enter, linked-task marker, replace question) is
    built, tested and **seen running**, but not yet committed.
  - **Next steps:**
    - 5c: the pill ring and `mm:ss`
    - 5d: the notification
    - 5e: hiding the pill over full-screen apps

## Milestone log

| Milestone | Merged | Notes |
|---|---|---|
| M0 Project setup | ✅ | Solution, Core/App/Tests, warnings as errors, `.editorconfig` |
| M1 Overlay window | ✅ | Tool window, no-activate, click-through, PerMonitorV2, single instance |
| M2 Hotkey + expand | ✅ | Win+Alt+L, hover delays, focus hand-off, morph animation |
| M3 Tasks + storage | ✅ `3e40d8c` | Core model, atomic JSON storage, autosave, MVVM + DI, Today/Inbox, keyboard, delete/undo, rename, pill count. Reorder moved to v0.2. |
| M4 Background app | ✅ | Tray, single-instance signal, settings.json, Settings window, light theme, file logging. Manual check still pending. |
| M5 Focus timer | in progress | 5a `1868964`; 5b built (uncommitted) |

## Gotchas and lessons learned

### Testing
- **`FakeTimeProvider` needs a UTC start time.** Given `23:59 +05:30`, its
  `GetLocalNow()` adds the offset a second time. Use `start.ToUniversalTime()`
  (see `tests/Ledge.Core.Tests/TestClock.cs`).
- **Core tests run in UTC+05:30 on purpose,** so mix-ups between UTC and local
  dates show up near midnight.
- **xUnit hangs on unfinished async work.** A test that starts something that
  waits on fake time (e.g. a delete animation) must advance the clock before
  it ends. To find a hanging test, run each suspect alone with a time limit,
  or use `--blame-hang-timeout 30s`. Delete the dumps it writes to
  `tests/*/TestResults` afterwards.

### Timers
- **Compute, don't count.** Remaining time is
  `Duration − (now − StartedAt − PausedTotal)`, recomputed on every tick, so
  sleep can't make it drift.
- **Line ticks up with the remaining time's whole seconds** (the first tick
  after `remaining % 1 s`), so `mm:ss` changes on the second and zero is hit
  exactly, even after part-second pauses.
- **Ticks can arrive a millisecond before the wall clock says the second has
  passed.** Timers and `GetUtcNow` use different clocks. Without
  `FocusTimer.ClockTolerance` (100 ms), the display lagged a second and the
  session ended a second late. Unit tests with `FakeTimeProvider` can't show
  this; it was found by running the app.
- **xUnit installs its own `SynchronizationContext`,** so a ViewModel that reads
  `SynchronizationContext.Current` posts its updates for later in tests. Pass
  the UI context in explicitly (null in tests).
- **Clear the session inside the lock when finishing,** so two checks racing
  each other (a tick and `CheckNow` on wake) can only finish it once.

### JSON (System.Text.Json)
- **The default encoder escapes `+` as `+`,** along with every non-ASCII
  letter and emoji. Use `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` for
  local files people may read.
- **`JsonStringEnumConverter` still accepts numbers** (`"theme": 7`), even for
  values that don't exist. Validate with `Enum.IsDefined`.
- **A non-`required` string property can still be `null`** if the file says
  `"hotkey": null`. Check for it after loading.

### Building and running
- **A running Ledge locks `bin/`.** Build and test elsewhere with
  `dotnet test --artifacts-path <some temp folder>`, or quit Ledge first
  (tray → Quit).
- **Only one Ledge can run.** A test launch exits at once while another copy is
  running, and keystrokes then go to the copy that's running.
- **To close Ledge from a script,** send it `WM_CLOSE`: that's a graceful exit
  and runs the final save. Killing the process skips the save.
- **NuGet version gaps:** `H.NotifyIcon.Wpf` 2.4.x only ships .NET 10 and .NET
  Framework builds. On net9.0 that gives NU1701, which is an error here, so
  it's pinned to **2.3.2**.

### WPF and Windows
- **`IsActive` is wrong on a `WS_EX_NOACTIVATE` window.** Ask Windows instead
  (`GetForegroundWindow() == hwnd`).
- **A lone Alt key-up opens the other app's menu bar.** After the hotkey, wait
  until the modifier keys are released before handing focus back.
- **Brushes from resources are frozen** and can't be animated. Use your own
  brush, or cross-fade two elements.
- **XAML trims leading and trailing spaces** in string resources. Use
  `xml:space="preserve"`.
- **WPF has no letter-spacing** and no text-transform. Uppercase is done with
  `UpperCaseConverter`.
- **`TextBox` bindings update when focus leaves,** unless you set
  `UpdateSourceTrigger=PropertyChanged`.
- **`Visibility="Hidden"` keeps the element's space;** `Collapsed` gives it up.
  Hover icons use `Hidden` so titles don't re-flow.
- **A `KeyBinding` whose command can't execute lets the key through.** That's
  how a space typed in the rename box doesn't also tick the task.
- **`TaskbarIcon.ForceCreate()` turns on Windows Efficiency Mode by default.**
  Pass `false`.
- **Clicking the tray icon activates the taskbar first,** which closes an open
  panel. A short grace period stops the same click from reopening it.
- **A registered global hotkey swallows its keys everywhere,** including in
  our own Settings window. Suspend it while recording a new shortcut.
- **Win+L locks the PC** and Windows reserves other Win combinations; they
  can't be captured or registered. `TryChange` reports them as "taken".
- **WPF's `KeyConverter` writes the number-row keys as digits** ("1", not
  "D1"), and reads them back correctly.
- **Setting an `[ObservableProperty]` backing field directly** gives the
  MVVMTK0034 warning, which is an error here. To load values without triggering
  the `On…Changed` hooks, use an "initialising" flag.
- **A closed WPF window can't be shown again.** Register it as transient and
  create a new one each time.
- **`StaticResource` is looked up once; `DynamicResource` stays linked** to its
  key. Anything that must change with the theme uses `DynamicResource`.
  Colours that code reads (`FindResource`) must be re-read after a theme
  change.
- **The light `Accent` (#B45309) is for light backgrounds.** The pill is always
  dark, so its dot uses its own `PillAccentBrush`.
- **The title bar is drawn by Windows,** not WPF. Its dark or light look is set
  with `DwmSetWindowAttribute` and must be updated when the theme changes.
- **A click on a non-focusable element doesn't move keyboard focus,** so "click
  elsewhere cancels" needs a window-level `PreviewMouseDown`.

### Logging
- **Host default logging adds a console and the Windows Event Log.** Use
  `ClearProviders()`, then add only what's wanted.
- **The DI container doesn't dispose a logger provider passed to
  `AddProvider`.** Dispose it yourself, last, in `OnExit`.
- **`DispatcherUnhandledException` is usually followed by
  `AppDomain.UnhandledException`** for the same crash. Guard against handling
  it twice.

### Tooling (PowerShell 5.1)
- **`Set-Content -Encoding utf8` adds a BOM,** which fails `dotnet format`
  (CHARSET). Write with `[IO.File]::WriteAllText(path, text, new UTF8Encoding($false))`.
- **`git commit -m @'…'@` can fail silently** when the message contains quotes.
  Write the message to a file and use `git commit -F <file>`, then check
  `git log`.
- **Red "NativeCommandError" output after `git` commands** is just git's
  progress text on stderr; it isn't an error.
- **In scripted UI tests, the first hotkey right after start-up is
  unreliable.** Wait about 3 s and retry once. Never type unless Ledge is
  confirmed to be the foreground window.

## Working agreements (short version)

- One milestone per branch; small steps; explain first, then wait for an OK.
- Commit only when asked. At the end of a milestone: push the branch,
  `git merge --ff-only` into `main`, push `main`. No PRs.
- Missing design values are asked as multiple-choice questions, then recorded
  in DESIGN.md.
- The full rules are in `CLAUDE.md`.
