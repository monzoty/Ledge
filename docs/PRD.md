# Product Requirements Document — Ledge

> **Status:** v1.1 (complete — ready to build) · **Owner:** project owner · **Last updated:** 2026-11-16
>
> This PRD defines **what** v0.1 does and **why**. Visual and interaction
> values (sizes, colours, motion, copy, placement, hotkey) live in
> `docs/DESIGN.md` and are owned by the project owner. Where this document
> needs one of those values, it links to `DESIGN.md` instead of setting it.
>
> Markers used below:
> - **Decided**: a product decision for v0.1. Change it here if you disagree.
> - **→ DESIGN.md**: the value is a design decision that belongs in `DESIGN.md`.
>   A *recommendation* may be given, but it is not binding.

---

## 1. Summary

Ledge is a small Windows companion that sits at the edge of the screen as
a tiny pill. Hovering over it or pressing a global hotkey expands it into a
panel with today's tasks, a quick-add box and a focus timer. The panel opens
over whatever you're working in and collapses again without stealing focus.
You never have to open, find or switch to an app window.

v0.1 is a **single-user, offline, single-monitor** tool. Its job is to be the
fastest way to capture a thought and the easiest way to see what you're
working on right now.

## 2. Problem

Draft: rewrite this in your own words.

> When I'm working, my task list lives in a window that's buried behind
> everything else. Opening it to jot something down breaks my focus, so I
> either lose the thought or lose my flow. I also have no constant reminder
> of what I'm meant to be doing right now or how long I've been doing it.

Underlying causes:

| # | Pain | Effect |
|---|------|--------|
| P1 | Task apps are normal windows that get buried | The list is out of sight, so it's out of mind |
| P2 | Capturing a task means switch → find → click → type → switch back | Capture is too slow, so thoughts get lost or interrupt focus |
| P3 | Timers and "current task" are hidden while working | Easy to drift; no sense of elapsed time |
| P4 | Full task apps have many features | Friction and decision fatigue for small, daily to-dos |

## 3. Target users

**Primary: "the context-switcher".** Someone on a Windows laptop or desktop
who juggles many small tasks and loses focus easily: students, developers,
makers. They live in a few full-screen-ish apps (IDE, browser, docs) and want
their to-dos *near* their work, not *in another window*.

**Secondary: "the keyboard-first power user".** Someone who already uses a
launcher (PowerToys Run, Flow Launcher) or hotkeys heavily, and wants
capture to work the same way: one shortcut, type, Enter, gone.

**Not targeted in v0.1:** teams and shared lists, people who plan weeks
ahead in detail (they need a calendar or week view), and mouse-only or
touch-first tablet use.

### Primary scenario

> It's 10:40. I'm debugging in VS Code and remember I need to email Priya. I
> press the hotkey, type "email Priya", press Enter, and I'm back in my editor
> with the cursor where it was. At 11:00 I glance at the pill, see 2 tasks
> left, hover to read them, start a 25-minute timer on the first one, and keep
> working. The remaining time sits in the pill until a notification tells me
> I'm done.

## 4. Goals (v0.1)

| # | Goal | Measured by |
|---|------|-------------|
| G1 | Capture a task in **under 3 seconds** from anywhere | Hotkey → type → Enter, no mouse; focus returns to the previous app |
| G2 | See today's tasks **without switching windows** | Hover or hotkey shows the panel over the current app |
| G3 | **Never get in the way** | Never takes focus while collapsed; never shows over a fullscreen app; never in taskbar or Alt+Tab |
| G4 | **Data stays on the device** and is never lost | All data in local files; no network calls; atomic saves with backup |
| G5 | Make **focus time visible** | A running timer is readable in the collapsed pill |

## 5. Non-goals (v0.1)

These are deliberately **out** of v0.1. See `ROADMAP.md → Later` for when
they might come back.

- Calendar integration (Outlook, Google, ICS)
- Week view, future-date planning, due dates, reminders, recurring tasks
- Sub-tasks, projects, tags, priorities, notes on tasks
- Sync to any service or device (e.g. Microsoft To Do), and backup to cloud
- Voice input, screenshots shelf, AI planning, MCP server
- Accounts, licensing, payments, telemetry
- Multiple monitors (**primary monitor only**)
- Pomodoro cycles with automatic breaks, timer statistics or history views
- Localisation (v0.1 is English only, but strings are kept in one place)
- Import/export (the JSON file is human-readable as a stop-gap)

## 6. v0.1 scope: features

Priority: **Must** = v0.1 can't ship without it. **Should** = planned for
v0.1, can slip to v0.2. **Could** = only if it's cheap.

### F1. The pill (collapsed state): Must

| ID | Requirement |
|----|-------------|
| F1.1 | A small pill at a fixed position on the **primary monitor**. **Top-centre**, floating just below the top edge (DESIGN.md §1). |
| F1.2 | Always on top of normal windows; not in the taskbar; not in Alt+Tab. |
| F1.3 | **Hovering** over the pill never takes keyboard focus from the active app. **Clicking** it is a deliberate action and behaves like the hotkey (F2.5). |
| F1.4 | **Decided: what it shows at a glance.** When a timer is running, the remaining time (`mm:ss`). Otherwise, the number of open tasks for today. When there are none, an idle state. How each one looks → DESIGN.md §2. |
| F1.5 | Re-positions itself when screen resolution, scaling or the taskbar changes. |
| F1.6 | Hides while a fullscreen app (game, video, presentation) is in front, and reappears when it leaves. *(Built in M5.)* |
| F1.7 | Crisp and correctly placed at 100 %, 125 %, 150 % and 200 % scaling. |

### F2. Expanded panel: Must

| ID | Requirement |
|----|-------------|
| F2.1 | Opens on **hover**, after a delay so accidental passes don't open it. Delay → DESIGN.md §5 (*recommendation: about 150 ms*). |
| F2.2 | Opens on the **global hotkey**. Key combination → DESIGN.md §5. The hotkey **toggles** the panel: pressed again, it closes. |
| F2.3 | Closes when the mouse leaves (after a delay, only if the panel doesn't have focus), on **Esc**, or on a click outside the panel (when it has focus, so the window is deactivated). |
| F2.4 | **Hover-open** never takes keyboard focus. Focus moves into the panel only when the user clicks inside it. |
| F2.5 | **Hotkey-open** activates the panel with the cursor in the quick-add box. On close, focus returns to the window that had it before. |
| F2.6 | A hover-opened panel does not close on mouse leave while it has keyboard focus (so you don't lose a half-typed task). |
| F2.7 | Contents, top to bottom (**Decided**): quick-add box → timer controls → **Today** list → collapsible **Inbox** section. Layout → DESIGN.md §2. |

### F3. Tasks: Must (except where noted)

**Decided: the task model for v0.1**

- A task has a title, a done flag and an optional **planned date**
  (see `ARCHITECTURE.md §4`).
- **Today** shows tasks planned for today or earlier that are not done,
  plus tasks completed today.
- **Inbox** shows tasks with no planned date.
- Tasks from earlier days that aren't done **roll over** into Today
  automatically. Nothing silently disappears.
- "Today" is the local calendar date from the injected `TimeProvider`. The
  day changes at local midnight, including while the app is running.

| ID | Requirement | Priority |
|----|-------------|----------|
| F3.1 | **Quick-add:** type a title and press Enter. The task is added to **Today**, at the bottom of the list. The box clears and keeps focus, so you can add several in a row. | Must |
| F3.2 | Titles are trimmed. Empty or whitespace-only input does nothing. Max **200 characters**; longer input is cut off at the limit. | Must |
| F3.3 | **Complete / un-complete** with the checkbox, or **Space** on the selected task. Done tasks stay visible, struck through, until the day changes. | Must |
| F3.4 | **Delete** with **Del** or a delete action. No confirmation dialog. **Ctrl+Z** undoes the most recent delete while the panel is open. | Must (undo: Should) |
| F3.5 | **Move between Today and Inbox** with one action per task (e.g. a context-menu item or shortcut). Shortcut → DESIGN.md §5. | Must |
| F3.6 | **Rename** a task inline (F2 or double-click). Enter saves, Esc cancels. | Should |
| F3.7 | **Keyboard navigation:** ↑/↓ moves the selection; Tab moves between the quick-add box, timer and lists. | Must |
| F3.8 | **Reorder** by drag-and-drop, or Alt+↑/↓. | Should (may slip to v0.2) |
| F3.9 | Every change saves automatically. There is no Save button. | Must |
| F3.10 | Empty states for "no tasks today" and "Inbox empty". Copy → DESIGN.md §6. | Must |

### F4. Focus timer: Must

**Decided: timer behaviour for v0.1**

- One timer at a time.
- **Presets: 15, 25 and 50 minutes; default 25.** No custom duration in
  v0.1 (Could: a custom duration from 1 to 120 minutes).
- A timer can be linked to a task. Starting it from a task's action links it
  to that task; starting it from the timer controls leaves it unlinked.
- The timer is calculated from its start time and duration, not from
  counting ticks. It stays accurate across sleep and hibernate.
- In v0.1 the timer is **not** kept across an app restart. If the app quits,
  the timer is cancelled.

| ID | Requirement |
|----|-------------|
| F4.1 | Start with a preset (default 25 min), optionally linked to a task. |
| F4.2 | The remaining time shows in the collapsed pill and in the panel, updated every second. |
| F4.3 | **Pause / resume** and **stop**. Stop asks nothing and records nothing. |
| F4.4 | When the timer ends, a Windows notification appears even if the panel is closed or another app is fullscreen. Notification text → DESIGN.md §6. |
| F4.5 | If the timer ended while the PC was asleep, the notification shows on wake, saying that it ended. |
| F4.6 | When a linked timer ends, the notification offers **"Mark task done"** as a Should. |
| F4.7 | Starting a new timer while one is running replaces it, after a confirmation. |

### F5. Background app basics: Must

| ID | Requirement |
|----|-------------|
| F5.1 | Tray icon with **Open**, **Settings** and **Quit**. Left-click toggles the panel. |
| F5.2 | **Single instance:** launching a second copy opens the existing panel and then exits. |
| F5.3 | **Start with Windows** option. **Decided:** off by default; the user turns it on in Settings. |
| F5.4 | The app runs without an open window and doesn't exit when the panel closes. Only **Quit** exits. |

### F6. Settings: Must (minimal)

A small settings window, opened from the tray. v0.1 settings:

| Setting | Default | Notes |
|---|---|---|
| Global hotkey | → DESIGN.md §5 | Validated: if another app already uses it, say so and keep the previous one |
| Start with Windows | Off | HKCU Run key |
| Theme | Follow Windows | Light / Dark / Follow Windows (if DESIGN.md §3 includes both themes) |

Settings are stored separately from tasks (`settings.json`).

## 7. User stories and acceptance criteria

Each **[manual]** item goes in the matching milestone's manual checklist in
`ROADMAP.md`. Everything else is covered by unit tests where possible.

**US1: Quick capture (G1)**
> As a user, I press the hotkey, type "email Priya", press Enter, and keep working.

- [ ] Hotkey works while any other app has focus. [manual]
- [ ] The quick-add box has focus straight after the hotkey. [manual]
- [ ] Enter adds the task to Today and clears the box; empty input adds nothing.
- [ ] Esc or the hotkey closes the panel, and focus returns to the previous app. [manual]
- [ ] The whole flow takes under 3 s with no mouse. [manual]

**US2: Glance (G2, G3)**
> As a user, I glance at the pill to see how many tasks are left, and hover to read them.

- [ ] The pill shows today's open-task count when no timer is running.
- [ ] The panel opens after the hover delay and doesn't flicker on quick passes. [manual]
- [ ] The panel closes after the mouse leaves (after the delay). [manual]
- [ ] Hovering doesn't take keyboard focus from the current app. [manual]

**US3: Complete and tidy**
> As a user, I tick off what I've done and clear out what no longer matters.

- [ ] The checkbox or Space marks a task done, and it's saved immediately.
- [ ] Done tasks stay visible, struck through, until the next day, then leave Today.
- [ ] Unfinished tasks from yesterday appear in Today.
- [ ] Del removes a task; Ctrl+Z restores it while the panel is open.
- [ ] Tasks move between Today and Inbox with one action.
- [ ] All of this survives an app restart and a PC restart.

**US4: Focus (G5)**
> As a user, I start a 25-minute timer on a task and can see it ticking without opening anything.

- [ ] Starting a timer shows the remaining time in the pill.
- [ ] Pause, resume and stop work from the panel.
- [ ] A notification appears when the timer ends, even with the panel closed.
- [ ] After 10 minutes of sleep mid-session, the remaining time is correct to the second. [manual + FakeTimeProvider test]

**US5: Out of the way (G3)**
> As a user, I never have to fight the app.

- [ ] The pill isn't in the taskbar or Alt+Tab. [manual]
- [ ] The pill hides while a fullscreen app is in front. [manual]
- [ ] The pill is crisp and well placed at 100/125/150/200 % scaling. [manual]
- [ ] A second launch doesn't create a second pill. [manual]

**US6: Trust my data (G4)**
> As a user, I never lose tasks, even if the app or PC crashes.

- [ ] A crash in the middle of a save leaves either the old file or the new one, never a corrupt one. [unit test]
- [ ] A corrupt `tasks.json` loads from `.bak`, the user is told, and nothing is silently wiped. [unit test]
- [ ] The app makes no network connections. [manual: check with Resource Monitor]

## 8. Quality requirements

| Area | Requirement |
|------|-------------|
| Startup | Pill visible **< 1 s** after the process starts (warm start) |
| Responsiveness | Hotkey → panel shown with focus in the box **< 150 ms** (excluding any animation set in DESIGN.md) |
| Memory | **< 100 MB** working set when idle |
| CPU | **≈ 0 %** when idle and no timer is running. No polling loops; a running timer ticks at most once per second |
| Data safety | Atomic writes (temp file, then replace), one `.bak` copy, `SchemaVersion`; saves debounced to about 300 ms |
| Reliability | Survives sleep/wake, resolution and scaling changes, explorer.exe restarts (tray icon comes back) |
| Accessibility | Every feature usable by keyboard alone; visible focus indicator; text contrast meets WCAG AA; respects the Windows "Show animations" setting; controls have UI Automation names (works with Narrator) |
| Privacy | No telemetry, no network calls; data only in `%LOCALAPPDATA%\Ledge\` |
| Platform | Windows 10 1903+ and Windows 11, x64; .NET 9 |
| Logging | Local log files only, no task titles in logs |

## 9. Error handling (user-visible)

| Situation | Behaviour |
|---|---|
| Hotkey already registered by another app | Message on startup or in Settings; the app still runs (hover and tray still work); the user picks another hotkey |
| `tasks.json` corrupt or unreadable | Load `.bak`, show a one-time notice, keep the corrupt file as `tasks.corrupt-<timestamp>.json` |
| Both main file and backup unreadable | Start with an empty list, keep both files untouched, tell the user where they are |
| Save fails (disk full, permissions) | Keep the data in memory, show a non-blocking warning, retry on the next change |
| Second instance launched | Existing instance opens its panel; the new one exits quietly |

## 10. Success metrics (personal project)

| Metric | Target |
|---|---|
| Daily use | Used **every workday for 2 weeks** without going back to the old method |
| Capture habit | **≥ 5 tasks captured per workday** on average, mostly via the hotkey |
| Data safety | **Zero** data-loss bugs during the 2 weeks |
| Annoyance | **No open "it got in my way" issues** (focus stealing, covering content) at the end of M6 |
| Focus | At least **one focus session per workday** on average |
| Learning (meta) | Can explain every Win32 technique in `ARCHITECTURE.md §6` without notes |

Measured by hand, by looking at `tasks.json` and the GitHub issues. There is
no telemetry.

## 11. Release plan

| Milestone (ROADMAP.md) | PRD features delivered |
|---|---|
| M0: Project setup | none (foundation) |
| M1: Overlay window | F1.1–F1.3, F1.5, F1.7, F5.2 |
| M2: Hotkey and expand | F2.1–F2.6 |
| M3: Tasks and storage | F2.7, F3.*, US6 |
| M4: Background | F5.1, F5.3, F5.4, F6, logging |
| M5: Focus timer | F1.4 (timer part), F1.6, F4.* |
| M6: Polish | §8 quality pass, empty states, accessibility |
| M7: Ship v0.1 | Installer, auto-update, release notes |

**v0.1 is "done"** when every Must in §6 is built, every acceptance check in
§7 passes, and §8 is met on the developer's laptop plus one external monitor
(as the primary display).

## 12. Assumptions and risks

| Risk / assumption | Mitigation |
|---|---|
| A top-of-screen pill may overlap browser tabs or window title bars | Placement is in DESIGN.md §1; keep the collapsed pill small; fullscreen hiding |
| `WS_EX_NOACTIVATE` plus typing in WPF can be awkward (focus handoff) | Prototype it early in M2; activate explicitly only on hotkey or click |
| Global hotkeys often clash with other apps | Validate on registration; make it configurable in M4 |
| Unsigned installer triggers SmartScreen warnings | Accept for personal use; decide on code signing in M7 |
| Scope creep (calendar, sync, AI) | Non-goals in §5; ideas go to ROADMAP "Later", not into v0.1 |

## 13. Open questions

Resolved (2026-11-16). Design answers are in `DESIGN.md`:

- ✅ Name: **Ledge**, no mascot (DESIGN.md §0).
- ✅ Placement: **top-centre**, floating 4 DIP below the top edge (DESIGN.md §1).
- ✅ Hotkey: **Win+Alt+L**, configurable (DESIGN.md §5). Changed from Ctrl+Alt+Space on 2026-11-16 because another app on the owner's PC already uses that.
- ✅ Copy for empty states, placeholder and notifications (DESIGN.md §6).
- ✅ Distribution and license: **free and open source under MIT**, released on
  GitHub (installer arrives in M7).
- ✅ Completed tasks are **kept in the file** and hidden from the UI after
  their day. Revisit if `tasks.json` grows past about 1 MB (ADR-0002).

Still open (doesn't block v0.1): whether to add a "minimal mode" with no
visible pill (hotkey and edge only) in v0.2.

## 14. Glossary

| Term | Meaning |
|---|---|
| Pill | The collapsed, always-visible overlay shape |
| Panel | The expanded view with quick-add, timer and lists |
| Today | Open tasks planned for today or earlier, plus tasks completed today |
| Inbox | Tasks with no planned date |
| Focus session | One run of the focus timer, optionally linked to a task |
| DIP | Device-independent pixel (1/96 inch); WPF's unit for sizes |

## 15. Inspiration (idea only)

The concept is inspired by macOS "notch" apps. This project borrows the
**idea** only. The name, visual design, mascot, copy and code are original.

## Revision history

| Date | Change |
|---|---|
| 2026-11-15 | Draft v1: filled in all product decisions; design values point to DESIGN.md |
| 2026-11-16 | v1.1: name set to Ledge; open questions resolved; clicking the pill now activates the panel (F1.3, F2.3) |
