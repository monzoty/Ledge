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
- **Steps 4a and 4b** are built and unit-tested, but not yet checked by hand
  or committed. Commit them as two separate commits.
  - 4a: the tray icon (Open / Quit, left-click toggles), a second launch
    opening the running panel, and Alt+F4 closing only the panel.
  - 4b: `AppSettings` and `settings.json` through a shared `JsonFile<T>`. The
    hotkey is now read from the settings.
- **Next:**
  - 4c: Settings window
  - 4d: light theme
  - 4e: file logging

## Milestone log

| Milestone | Merged | Notes |
|---|---|---|
| M0 Project setup | ✅ | Solution, Core/App/Tests, warnings as errors, `.editorconfig` |
| M1 Overlay window | ✅ | Tool window, no-activate, click-through, PerMonitorV2, single instance |
| M2 Hotkey + expand | ✅ | Win+Alt+L, hover delays, focus hand-off, morph animation |
| M3 Tasks + storage | ✅ `3e40d8c` | Core model, atomic JSON storage, autosave, MVVM + DI, Today/Inbox, keyboard, delete/undo, rename, pill count. Reorder moved to v0.2. |
| M4 Background app | in progress | 4a and 4b built (uncommitted) |

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
