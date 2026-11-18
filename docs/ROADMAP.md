# Roadmap — Ledge

> One milestone = one feature branch = one pull request.
> Don't start a milestone until the previous one meets its "Done when" list.
> Tick boxes as you go. PRD feature IDs (F1.1 …) are in brackets.

Branch naming: `m1-overlay-window`, `m2-hotkey-and-expand`, …
Commit style: `feat: …`, `fix: …`, `refactor: …`, `test: …`, `docs: …`

**Current milestone: M5**

---

## M0 — Project setup
**Goal:** an empty app that builds, a test project that runs, and a repo on GitHub.

- [x] Pick a name (`DESIGN.md §0`) → **Ledge**
- [x] `git init`, `dotnet new gitignore`
- [x] First commit
- [x] Create the solution: `src/Ledge` (WPF), `src/Ledge.Core` (classlib), `tests/Ledge.Core.Tests` (xUnit)
- [x] `Directory.Build.props`: `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`
- [x] Add `.editorconfig` (file-scoped namespaces, `_camelCase` private fields, naming rules)
- [x] Add a `LICENSE` file (MIT)
- [x] Push to a GitHub repo
- [ ] (Optional) GitHub Actions: build + test on every push (`windows-latest`)

**Done when:** `dotnet build` and `dotnet test` pass; an empty window opens.

**Learn:** solution/project structure, `.csproj` files, the `dotnet` CLI, `Directory.Build.props`.

---

## M1 — The overlay window
**Goal:** a static pill at top-centre that behaves like an overlay. [F1.1–F1.3, F1.5, F1.7, F5.2]

- [x] `Themes/Tokens.xaml` + `Colors.Dark.xaml` from `DESIGN.md §3` (light colours come in M4)
- [x] Borderless, transparent window; a 112 × 28 pill floating 4 DIP below the top edge
- [x] Always on top; not in the taskbar; not in Alt+Tab (`WS_EX_TOOLWINDOW`)
- [x] Hovering or clicking doesn't take focus from the current app yet (`WS_EX_NOACTIVATE`)
- [x] Transparent area around the pill lets clicks through to the app underneath
- [x] `app.manifest` with PerMonitorV2; correct position at 100/125/150/200 %
- [x] Re-positions when screen resolution, scaling or the taskbar changes
- [x] Single-instance check (second launch exits)

**Manual checklist:** not in Alt+Tab · not in the taskbar · stays on top of a
maximised browser · clicking just beside the pill reaches the browser · crisp
at each scaling level · moves correctly after a resolution change.

**Done when:** the manual checklist passes on your laptop and with an external monitor set as primary.

**Learn:** XAML basics, window styles, P/Invoke, DPI/DIPs.

---

## M2 — Hotkey, hover and expand/collapse
**Goal:** the pill opens and closes the way DESIGN.md §4–§5 describes. [F2.1–F2.6]

- [x] Global hotkey Win+Alt+L via `RegisterHotKey` / `WM_HOTKEY`; handle "already in use"
- [x] Hover opens after 150 ms; leaving closes after 300 ms; no flicker
- [x] Hotkey or click → activate the panel, focus a (placeholder) quick-add box, remember the previous window
- [x] Esc, the hotkey again, or a click outside closes it; focus returns to the previous app
- [x] A focused panel stays open when the mouse leaves
- [x] Expand 220 ms / collapse 160 ms animations; instant when "Show animations" is off

**Manual checklist:** open/close 50 times with hover, hotkey and click; type in
Notepad → hotkey → Esc → keep typing in Notepad without clicking.

**Done when:** you can open and close it 50 times without a glitch.

**Learn:** `HwndSource` message hooks, focus and activation, WPF animations and Storyboards.

---

## M3 — Tasks + storage (MVVM + tests)
**Goal:** real tasks that are saved. [F2.7, F3.*, US6]

- [x] Core: `TaskItem`, `TaskDocument`, `TaskBoard`, `TaskQueries`
- [x] Core: `ITaskStore` + `JsonTaskStore` + `AtomicFile` (`File.Replace` with `.bak`, schema version)
- [x] Unit tests: add / complete / delete / undo / move / rename, Today vs Inbox, rollover at midnight, title rules (trim, empty, 200 chars), crash mid-save, corrupt file → backup, both corrupt
- [x] DI via the Generic Host; `TaskListViewModel` with `[ObservableProperty]` / `[RelayCommand]`
- [x] Quick-add, Today list, collapsible Inbox, checkbox, delete with Ctrl+Z undo
- [x] Keyboard: ↑/↓, Space, Del, F2, Ctrl+I, Ctrl+N or `/` (DESIGN.md §5)
- [x] The pill shows "{n} today" / "All clear"
- [x] Empty states and copy from DESIGN.md §6 (plus the restored/reset data notice)
- [ ] (Should) drag or Alt+↑/↓ reorder; can move to v0.2

**Done when:** tasks survive a restart; all Core tests pass.

**Learn:** MVVM, data binding, DI, System.Text.Json, unit testing, `ItemsControl` / `ListBox` templating.

---

## M4 — Lives in the background
**Goal:** it feels like a real utility app. [F5.1, F5.3, F5.4, F6]

- [x] Decide on the tray library → `H.NotifyIcon.Wpf` **2.3.2** (2.4+ only ships .NET 10 and .NET Framework builds)
- [x] Tray icon: Open, Settings, Quit; left-click toggles the panel; survives an explorer.exe restart
- [x] `AppSettings` + `ISettingsStore` (`settings.json`, same atomic writes) + tests
- [x] Settings window: hotkey (validated live), start with Windows, theme
- [x] Start with Windows via the HKCU Run key
- [x] Light theme colours + following the Windows theme live
- [x] Second launch opens the existing panel (named event)
- [x] File logging to `%LOCALAPPDATA%\Ledge\logs` (no task titles)

**Done when:** you can reboot and it's just there, configured.

**Learn:** the registry, `SystemEvents`, resource dictionaries and theme switching.

---

## M5 — Focus timer
**Goal:** a timer you can see without opening anything. [F1.4 timer, F1.6, F4.*]

- [x] Core: `FocusSession` + `IFocusTimer` / `FocusTimer` based on `TimeProvider` timestamps
- [x] Tests with `FakeTimeProvider`: pause/resume, a 10-minute sleep jump, finishes exactly once, replacing a session
- [x] Timer row: presets 15/25/50, start/pause/stop; Ctrl+Enter starts on the selected task
- [x] The pill shows a ring and `mm:ss` (136 × 28); paused state
- [ ] Notification when finished (+ "Mark task done" for a linked task); pill pulse
- [ ] Hide the pill while a fullscreen app is in front (`SHQueryUserNotificationState` + foreground hook)

**Done when:** a 25-minute session is accurate to the second after sleep/wake, and the pill
hides during a fullscreen YouTube video and a PowerPoint slideshow.

**Learn:** `TimeProvider`, `SetWinEventHook`, notifications.

---

## M6 — Polish and daily use (v0.1 release candidate)
- [ ] Use it daily for 1 week; log every annoyance as a GitHub issue
- [ ] Fix the top issues
- [ ] Accessibility pass: keyboard only, focus ring, contrast, Narrator names
- [ ] Performance check: idle CPU ≈ 0 %, memory < 100 MB, startup < 1 s
- [ ] Update `README.md` with screenshots

---

## M7 — Ship v0.1
- [ ] App icon (DESIGN.md §0) in `.ico` with 16/24/32/48/256 px sizes; version info
- [ ] Velopack installer + auto-update from GitHub Releases
- [ ] Check the name for clashes in the Microsoft Store and on GitHub
- [ ] (Optional) Code signing; unsigned apps trigger SmartScreen warnings
- [ ] Tag `v0.1.0`, write release notes

---

## Later (v0.2+): pick based on what you actually miss

| Feature | Approach | Seen in |
|---|---|---|
| Minimal mode (no visible pill; edge or hotkey only) | Setting that hides the pill but keeps the hover strip | Tomte |
| Quick notes / scratchpad | A second tab in the panel; `notes.json` | NotchOwl |
| Today's calendar events | Microsoft Graph + Google Calendar API (OAuth), ICS links | NotchOwl, Yata Notch |
| Full dashboard window | Normal resizable window reusing the same ViewModels | NotchOwl |
| Week view / planning | New view + `DateOnly` queries | |
| Focus history and stats | Persist `FocusSession`s | |
| Pomodoro cycles with breaks | Extend `FocusTimer` with a phase state machine | Nusa, Isle Notch |
| Phone sync | Microsoft To Do via Graph | |
| Voice capture | Windows.Media.SpeechRecognition or whisper.cpp | |
| Screenshots / file shelf | `FileSystemWatcher` on `Pictures\Screenshots`; drag-out | Yata Notch, Nusa |
| AI assistants | MCP server over stdio (Claude, Cursor) | |
| Multi-monitor | A pill per monitor using `GetMonitorInfo` | |
| Native Windows toasts with buttons | Windows App SDK `AppNotificationManager` | |
