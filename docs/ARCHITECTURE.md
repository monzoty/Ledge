# Architecture — Ledge

> How the app is put together. Update this when a structural decision changes,
> and record *why* in `docs/adr/`.
>
> Last updated: 2026-11-16

---

## 1. Tech stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | **.NET 9** (`net9.0-windows` for the app, `net9.0` for Core) | Nullable enabled, warnings as errors, file-scoped namespaces |
| UI | **WPF** | See ADR-0001 |
| MVVM | **CommunityToolkit.Mvvm** | `[ObservableProperty]`, `[RelayCommand]` source generators |
| DI and startup | **Microsoft.Extensions.Hosting** | Generic Host inside the WPF `App` |
| Storage | **System.Text.Json** → local files | See ADR-0002 (built into .NET, no package) |
| Time | **`TimeProvider`** (built into .NET 8+) | Injected, so "today", day rollover and timers are testable |
| Logging | **Microsoft.Extensions.Logging** + a small hand-written file logger | No Serilog in v0.1, to keep dependencies low |
| Tests | **xUnit** with its built-in `Assert` | FluentAssertions v8+ needs a paid licence for commercial use, so it's not used |
| Fake time | **Microsoft.Extensions.TimeProvider.Testing** (`FakeTimeProvider`) | Test project only |
| Tray icon | **H.NotifyIcon.Wpf** (recommended) or the WinForms `NotifyIcon` | Decide in M4. The WinForms one needs no package, but `UseWindowsForms` causes type-name clashes in WPF |
| Timer-finished notification | Tray balloon / toast via the tray icon library | Full Windows App SDK toasts are deferred to v0.2 (they need package identity or extra setup) |
| Installer and updates | **Velopack** | Milestone 7 |

### Package plan (ask before adding each one)

| Package | Project | Milestone | Why / alternative |
|---|---|---|---|
| `CommunityToolkit.Mvvm` | Ledge | M3 | Removes INotifyPropertyChanged boilerplate. Alternative: write it by hand. |
| `Microsoft.Extensions.Hosting` | Ledge | M3 | DI, configuration, logging, lifetime. Alternative: a hand-built `ServiceCollection`. |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` | Core.Tests | M0 | Test framework (comes with the `dotnet new xunit` template) |
| `Microsoft.Extensions.TimeProvider.Testing` | Core.Tests | M3 | `FakeTimeProvider`. Alternative: a hand-written fake. |
| `H.NotifyIcon.Wpf` | Ledge | M4 | Tray icon with WPF menus. Alternative: WinForms `NotifyIcon`. |
| `Velopack` | Ledge | M7 | Installer and auto-update |

## 2. Solution layout

```
Ledge.sln
├─ Directory.Build.props         ← shared: Nullable, TreatWarningsAsErrors, LangVersion
├─ .editorconfig
├─ src/
│  ├─ Ledge/                     ← WPF app (UI layer), net9.0-windows
│  │  ├─ App.xaml(.cs)           ← host + DI setup, single-instance check
│  │  ├─ app.manifest            ← PerMonitorV2 DPI awareness
│  │  ├─ Windows/
│  │  │  ├─ PillWindow.xaml(.cs) ← the overlay window (view only)
│  │  │  └─ SettingsWindow.xaml(.cs)
│  │  ├─ ViewModels/             ← PillViewModel, TaskListViewModel, TimerViewModel, SettingsViewModel
│  │  ├─ Views/                  ← UserControls inside the panel (QuickAdd, TaskRow, TimerBar)
│  │  ├─ Themes/
│  │  │  ├─ Tokens.xaml          ← sizes, radii, spacing, fonts, durations (from DESIGN.md)
│  │  │  ├─ Colors.Light.xaml    ← colour tokens, light theme
│  │  │  └─ Colors.Dark.xaml     ← colour tokens, dark theme
│  │  ├─ Interop/
│  │  │  ├─ NativeMethods.cs     ← every P/Invoke lives here, commented and linked to docs
│  │  │  └─ WindowStyles.cs      ← helpers: no-activate, tool window, topmost
│  │  └─ Services/               ← HotkeyService, ScreenService, TrayService, ThemeService,
│  │                               FullscreenWatcher, StartupService, NotificationService
│  │
│  └─ Ledge.Core/                ← plain .NET (net9.0), NO WPF or Windows references
│     ├─ Models/                 ← TaskItem, TaskDocument, FocusSession, AppSettings
│     ├─ Abstractions/           ← ITaskStore, ISettingsStore, IFocusTimer
│     └─ Services/               ← TaskBoard, TaskQueries, TaskTitle, TaskAutoSaver,
│                                  JsonTaskStore, JsonSettingsStore, FocusTimer, AtomicFile
│
└─ tests/
   └─ Ledge.Core.Tests/          ← xUnit tests for Core
```

**Rule:** `Core` must compile and be testable without WPF. If something needs
a window handle, a screen, the registry or a `Key` enum, it belongs in the app
project.

## 3. Layers and data flow

```
 Win32 / OS  ──►  UI Services (Hotkey, Screen, Tray, Fullscreen, Theme)
                         │ events
                         ▼
 Views (XAML)  ◄──bind──  ViewModels  ──calls──►  Core Services  ──►  JSON files
```

- Views contain **no logic** beyond purely visual code-behind (animations,
  focus handling).
- ViewModels never touch Win32 directly; they talk to services through interfaces.
- Core services expose `async` methods for I/O and are otherwise plain,
  synchronous logic.

### Who owns task state

`TaskBoard` (Core) is the **single in-memory owner** of the `TaskDocument`.
It has operations (`Add`, `Toggle`, `Delete`, `UndoDelete`, `MoveToInbox`,
`MoveToToday`, `Rename`, later `Reorder`). Each one replaces `Document` with a
new immutable copy and returns `true` if something actually changed. Only then
does it raise `Changed`. `TaskAutoSaver` (Core) listens to `Changed` and calls
`ITaskStore.SaveAsync` once nothing has changed for 300 ms. Its `FlushAsync()`
saves immediately; `App.OnExit` uses it with a 2 s limit (ADR-0005).
Undo keeps **one** slot: the most recently deleted task. Added or moved tasks get
`SortOrder = max + 1`, so they land at the bottom of their list.
`TaskQueries` turns a document plus "today" into the **Today** and **Inbox**
lists, using the rules in PRD §6 F3. ViewModels subscribe to `Changed` and
rebuild their observable lists.

## 4. Data model (v0.1)

```csharp
public sealed record TaskItem
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }       // trimmed, 1–200 chars
    public DateOnly? PlannedFor { get; init; }        // null = Inbox
    public bool IsDone { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public int SortOrder { get; init; }
}

public sealed record TaskDocument                     // tasks.json
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<TaskItem> Tasks { get; init; } = [];
}

public sealed record FocusSession                     // in memory only in v0.1
{
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public TimeSpan PausedTotal { get; init; }
    public DateTimeOffset? PausedAt { get; init; }
    public Guid? TaskId { get; init; }
}

public sealed record AppSettings                      // settings.json
{
    public int SchemaVersion { get; init; } = 1;
    public string Hotkey { get; init; } = "Win+Alt+L"; // DESIGN.md §5; parsed in the app layer
    public bool StartWithWindows { get; init; }
    public ThemeMode Theme { get; init; } = ThemeMode.System; // saved as "system" / "light" / "dark"
    // M5 adds: public int DefaultFocusMinutes { get; init; } = 25;
}
```

A missing property in `settings.json` takes its default. If the hotkey text
can't be parsed, the app falls back to the default hotkey.

**Today rule:** `PlannedFor != null` and (`!IsDone && PlannedFor <= today`
**or** `IsDone && CompletedAt.LocalDate == today`).
**Inbox rule:** `PlannedFor == null` and (`!IsDone` **or** `CompletedAt.LocalDate == today`).
A done task stays in the list it was in, struck through, until the day changes,
so it never shows in both lists.
When a task's day passes, nothing is rewritten in the file; rollover is just
a query.

## 5. Storage

- Location: `%LOCALAPPDATA%\Ledge\tasks.json` and `settings.json`.
- Both go through one generic class, `JsonFile<T>` (Core, internal), which
  handles serialising, the atomic save and the load/recovery rules below.
  `JsonTaskStore` and `JsonSettingsStore` only add their own "is this document
  usable?" check. If settings can't be read, Ledge uses the defaults; it doesn't
  show a notice.
- The JSON is written readably: camelCase names, enums as text, and `+`,
  accented letters and emoji left unescaped (`UnsafeRelaxedJsonEscaping`; the
  "unsafe" only matters inside HTML).
- **Atomic writes** (`AtomicFile` helper): write to `tasks.json.tmp`, flush
  it, then `File.Replace(tmp, tasks.json, tasks.json.bak)`. That one call
  swaps the files *and* keeps the previous version as `.bak`. On the very
  first save (no target file yet), use `File.Move`.
- `SchemaVersion` lets future versions migrate old files.
- Saves happen on every change, debounced to about 300 ms, and are flushed
  on Quit and on Windows shutdown (`SessionEnding`).
- Load order: `tasks.json` → if unusable, `tasks.json.bak` → if that's also
  unusable, start empty. `LoadAsync` returns a `TaskLoadOutcome` (`Loaded`,
  `FirstRun`, `RestoredFromBackup` or `ResetAfterCorruption`) so the UI can tell the user.
- "Unusable" means broken JSON, missing required fields, a `null` task list, or a
  `schemaVersion` outside 1…current. A file written by a **newer** Ledge is
  therefore set aside, never loaded and overwritten. Errors like "access denied"
  are thrown, not treated as corruption.
- Bad files are never deleted. They're renamed to `tasks.corrupt-<yyyyMMdd-HHmmss>.json`
  or `tasks.bak.corrupt-<…>.json`, with `-2`, `-3`, … added if the name is taken.
- `AtomicFile` flushes the temp file to disk (`Flush(true)`) before swapping it in.
  Core I/O uses `ConfigureAwait(false)`, so its work never runs on the UI thread.
  Only one save runs at a time (`SemaphoreSlim`).

## 6. The overlay window: key techniques

These are the Windows-specific parts. Learn each one as you build it.

**Decided (ADR-0003):** a **single window**, `PillWindow`, at a fixed size big
enough for the expanded panel and its shadow, at top-centre. The pill and the
panel are drawn *inside* it, and only the content animates, never the window
itself. Fully transparent pixels let clicks through, so the empty area doesn't
block apps underneath.

| Need | Technique |
|---|---|
| Borderless + transparent | `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"` |
| Always on top | `Topmost="True"`; re-assert with `SetWindowPos(HWND_TOPMOST)` when the panel opens |
| Not in taskbar | `ShowInTaskbar="False"` |
| Not in Alt+Tab | Extended style `WS_EX_TOOLWINDOW` |
| Don't steal focus on hover | Extended style `WS_EX_NOACTIVATE`. On hotkey or click, remove it and call `Activate()`; put it back when the panel collapses. |
| Return focus | Remember `GetForegroundWindow()` before activating; call `SetForegroundWindow(previous)` on close |
| Click outside closes | The window's `Deactivated` event (only while activated) |
| Hover hit-zone | Fully transparent pixels let clicks through, so the invisible hover margin (DESIGN.md §1) uses a near-transparent brush (`#01000000`) |
| Hover open/close delays | `DispatcherTimer`s started on `MouseEnter`/`MouseLeave` |
| Global hotkey | `RegisterHotKey` + listen for `WM_HOTKEY` (0x0312) via `HwndSource.AddHook`; `UnregisterHotKey` on exit; a `false` return means the hotkey is taken |
| Sharp on every monitor | `app.manifest` → `<dpiAwareness>PerMonitorV2</dpiAwareness>`; convert pixels ↔ DIPs when positioning |
| Position | All in **physical pixels**: primary work area from `MonitorFromPoint` + `GetMonitorInfo`, window size from `GetWindowRect`, move with `SetWindowPos`. (`SystemParameters.WorkArea` was dropped: under PerMonitorV2 it converts with the start-up DPI and goes wrong after a scaling change.) Re-position on `SystemEvents.DisplaySettingsChanged`, `SystemParameters.StaticPropertyChanged` ("WorkArea", i.e. taskbar) and `Window.DpiChanged` |
| Hide over fullscreen apps | `SHQueryUserNotificationState` (`QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_PRESENTATION_MODE`), checked on `EVENT_SYSTEM_FOREGROUND` via `SetWinEventHook`, not by polling |
| Theme | `ThemeService`: the Theme setting, or for "System" `AppsUseLightTheme` (HKCU `…\Themes\Personalize`). It follows Windows live via `SystemEvents.UserPreferenceChanged` (category General, which Windows raises for `WM_SETTINGCHANGE` "ImmersiveColorSet") and follows Settings via `SettingsService.Changed`. It swaps `Colors.Dark.xaml` ⇄ `Colors.Light.xaml` in the app's merged dictionaries. Panel colours use `{DynamicResource}` so they repaint; pill colours are the same in both files. Code-drawn things (the shell's animated brush, the Settings title bar) listen to `ThemeChanged`. |
| Animations off | `SystemParameters.ClientAreaAnimation` (and `SPI_GETCLIENTAREAANIMATION`) |
| Single instance | Named `Mutex` (`Local\Ledge-SingleInstance`) + a named `EventWaitHandle` to tell the first copy to "open your panel" |
| Start with Windows | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `Ledge` (unpackaged) |
| Tray icon survives explorer restart | Re-add on the `TaskbarCreated` registered message (handled by H.NotifyIcon) |
| Notifications | Tray balloon / toast via the tray icon (v0.1) |

All P/Invoke signatures go in `Interop/NativeMethods.cs` with a comment
linking to the Microsoft docs page for that function. Prefer
`[LibraryImport]` (source-generated) over `[DllImport]`.

## 7. Threading and time

- The UI thread owns all WPF objects. Use `async`/`await`; never `.Result` or `.Wait()`.
- **Focus timer:** `FocusTimer` (Core) stores *timestamps*
  (`FocusSession`), and remaining time = `Duration − (now − StartedAt − PausedTotal)`.
  The UI ticks once per second with `TimeProvider.CreateTimer`, only while a
  session is running. Sleep and wake therefore can't make it drift (ADR-0004).
- **Day rollover:** `LocalDayWatcher` (Core) keeps one `TimeProvider` timer set
  for the next local midnight and raises `DayChanged` when the date changes.
  `DayRolloverService` (app) also calls `Check()` on wake from sleep
  (`SystemEvents.PowerModeChanged`) and on clock changes (`SystemEvents.TimeChanged`),
  then refreshes the lists on the UI thread. Opening the panel also refreshes them.
- File I/O is async and runs off the UI thread; results come back on the
  Dispatcher.

## 8. Error handling and logging

- Corrupt `tasks.json` → load `.bak`, tell the user (DESIGN.md §6), never silently wipe data.
- Hotkey already taken → show a message; the app keeps working via hover and tray.
- Save failure → keep the data in memory, warn the user, retry on the next change.
- Unhandled exceptions → `DispatcherUnhandledException` and
  `AppDomain.UnhandledException` log the error and flush any pending save.
- Logs go to `%LOCALAPPDATA%\Ledge\logs\ledge-<date>.log`, keeping the last
  7 files. **Task titles are never logged.**

## 9. Testing strategy

| Layer | How |
|---|---|
| Core models and services | xUnit unit tests (required for every Core change) |
| `TaskBoard` / `TaskQueries` | Today/Inbox rules, rollover across midnight, undo, title validation |
| Storage | Tests in a temp folder: round-trip, crash mid-save (leftover `.tmp`), corrupt file → `.bak`, both corrupt |
| Timer logic | `FakeTimeProvider`: pause/resume, a 10-minute "sleep" jump, finish exactly once |
| ViewModels | Unit tests with fake services (from M3; may need a test project for the app) |
| Window / Win32 | Manual checklist in each milestone (DPI, focus, Alt+Tab, fullscreen) |

## 10. Future seams (don't build yet)

Interfaces that make later features drop-in:

- `ICalendarService` → Microsoft Graph / Google Calendar / ICS
- `ITaskSync` → Microsoft To Do via Graph
- `ISpeechInput` → Windows.Media.SpeechRecognition or whisper.cpp
- `IAgentServer` → MCP server over stdio
- `ITaskStore` (SQLite) → if history or sync outgrow JSON (ADR-0002)
