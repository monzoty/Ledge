# Design — Ledge

> This file is the **source of truth** for visuals and interaction.
> Claude Code must not change design values without asking the owner.
>
> **Status:** v1 filled in by Claude on 2026-11-16 at the owner's request.
> Every value below is a starting point. Change anything you don't like;
> code reads these values only through `Themes/Tokens.xaml`.

---

## 0. Identity

| | Decision | Why |
|---|---|---|
| Name | **Ledge** | It sits on the edge of your screen, and you put things "on the ledge" to deal with soon. Short, easy to type, works as a namespace (`Ledge`, `Ledge.Core`). |
| One-line tagline | **"Your day, one glance away."** | States the core promise (G2) in five words. |
| Personality | **Calm, minimal, a little warm** | It's always on screen, so it must feel quiet. Warmth comes from one amber accent, not from decoration. |
| Mascot or icon idea | **No mascot.** Icon: a rounded horizontal bar (the pill) with a small amber dot resting on its top edge, like something sitting on a ledge. | A mascot adds copy and animation work for no v0.1 value. A simple shape still reads clearly at 16 px in the tray. |

**Stand-in icon (until M7):** `src/Ledge/Resources/Ledge.ico`, drawn from the
idea above. It's a `PillBackground` rounded bar with a `TextSecondary` outline,
so it stays visible on a dark taskbar, and a 13/32-size `Accent` dot centred on
its top edge. It includes 16/20/24/32/48/256 px sizes.

> Name check: before M7, search the Microsoft Store and GitHub for "Ledge"
> to avoid a clash. Renaming before M0 is trivial; after M0 it touches
> project names.

## 1. Placement on screen

| Option | Pros | Cons |
|---|---|---|
| **A. Top-centre pill** | Familiar "island" feel, always in view | Overlaps browser tabs and window titles |
| B. Above the taskbar (centre) | Natural on Windows 11 | Taskbar can be left-aligned, auto-hidden, or on another edge |
| C. Screen edge / corner tab | Never covers content | Less discoverable |
| D. Hotkey only, no visible pill | Zero clutter | Nothing to glance at |

**Decision:** **A. Top-centre**, floating, on the primary monitor's work area.

**Why:** It's the most glanceable place, and it matches what users of
similar apps expect (NotchOwl, Yata Notch, Isle Notch all live there). To
keep the overlap with browser tabs small:
- the collapsed pill is small (see §2),
- it floats **4 DIP** below the top edge and doesn't stretch across it,
- it hides over fullscreen apps (PRD F1.6).

Option D (no pill) stays possible later as a "minimal mode" setting. That
isn't in v0.1.

**Hover zone:** the pill's own bounds, plus **8 DIP** of extra margin on
the left, right and bottom (none above it, because it touches the screen
edge). Pushing the mouse against the top edge directly above the pill
therefore opens it.

## 2. States

All sizes are in DIPs (WPF units; they scale automatically with the display).

| State | Size (w × h) | What's shown |
|---|---|---|
| Collapsed (idle, tasks open) | **112 × 28** | Small amber dot (**6 DIP** circle, `Accent`, 4 DIP gap to the text) + "**3 today**" (count of open tasks in Today) |
| Collapsed (idle, nothing open) | **112 × 28** | Check icon + "**All clear**" |
| Collapsed (timer running) | **136 × 28** | Ring icon showing progress + "**18:42**" (tabular digits); when paused, a pause icon replaces the ring and the text dims to secondary. See "Pill timer" below the table. |
| Peek (hover) | — | **None in v0.1.** Hover goes straight to Expanded. Simpler, and one fewer state to tune. |
| Expanded | **360 wide**, height fits content, **min 160, max 520** (the list scrolls beyond that) | The pill widens and grows downward into the panel |
| Empty (no tasks) | — | Message in the Today area (§6) |

**Pill timer** (decided 2026-11-17):
- **What the pill shows:** while a session is running or paused, the pill
  shows the timer instead of "n today" / "All clear" (PRD F1.4).
- **Ring:** 12 DIP (the pill icon size), 2 DIP stroke.
  - The track is a full circle in `PillText` at 30 % opacity.
  - On top, an arc in `PillAccent` shows the time **left**. It starts as a
    full circle at 12 o'clock and shrinks clockwise as the session runs, like
    a countdown.
- **Paused:** a 12 DIP pause icon in place of the ring. The icon and the time
  use `PillTextSecondary`.
- **Changing modes:** switching between the timer and the other labels uses
  the §4 cross-fade (150 ms), with the width animating 112 ⇄ 136.

**Expanded layout, top to bottom** (padding 16, gaps 12):

```
╭──────────────────────────────────────╮
│  [+] Add a task…              ⏎      │  ← quick-add box (height 36)
│  ◔ Focus  [15] [25] [50]   ▶ / ❚❚ ■  │  ← timer row (height 32)
│ ──────────────────────────────────── │
│  TODAY · 2                           │  ← section header (caption; counts open tasks)
│  ☐ Email Priya                       │  ← task row (height 32)
│  ☐ Review PR #12                ◔    │     ◔ = linked to running timer
│  ☑ ~~Buy milk~~                      │
│  ▸ INBOX · 2                         │  ← collapsible, collapsed by default
╰──────────────────────────────────────╯
```

**List details** (decided 2026-11-16):

- **Checkbox:** square Fluent glyphs at 16 DIP. Open = outline in
  `TextSecondary`; done = filled square with a check in `Success`.
- **Horizontal alignment:** section headers, checkboxes and empty-state text
  start **12 DIP** in, directly under the quick-add `+` icon. **8 DIP** between
  the checkbox and the title, as between `+` and the placeholder.
- **Vertical spacing:** section header **12 DIP** below the block above it;
  first row **4 DIP** below its header.
- **Section count:** `TODAY · n` counts **open** tasks only, the same number as
  the pill's "{n} today". Done rows aren't counted.
- **Empty state:** `Small` style in `TextSecondary`, 12 DIP in, vertically
  centred in one row height (32).
- **Inbox header:** `▸ INBOX · n`, always visible, even at 0. It has a Fluent
  chevron at 12 DIP in `TextSecondary` with a 4 DIP gap before the caption.
  Clicking anywhere on the header line opens or closes the Inbox, and the
  chevron turns 90° (▸ → ▾) over **150 ms**, `QuadraticEase` EaseOut.
  Collapsed by default; the open/closed state is remembered while the app
  runs, but not saved. The count is **open** Inbox tasks. When the Inbox is
  open but empty, it shows "Inbox is clear.", styled like the Today empty state.
- **Move action (mouse):** while the mouse is over a row, a 16 DIP icon in
  `TextSecondary` appears at the row's right end, 12 DIP from the edge, with a
  12 DIP gap after the title. On Today rows it's an inbox tray (tooltip
  "Move to Inbox"); on Inbox rows it's a calendar (tooltip "Move to Today").
  One click moves the task. The keyboard equivalent is `Ctrl+I` (§5).
- **Delete action (mouse):** a 16 DIP trash icon also appears on hover, to the
  **left** of the move icon with an **8 DIP** gap. It's `TextSecondary`, turns
  `Danger` while the mouse is on it, and has the tooltip "Delete".
- **Undo hint:** after a delete, the line "Task deleted · Ctrl+Z to undo"
  appears at the **bottom of the panel**, below the lists: one row high (32),
  `Small`, `TextSecondary`, 12 DIP in. It stays until you undo, the next
  delete replaces it, or the panel closes (which also clears undo). It appears
  and disappears instantly.
- **Selected row (keyboard):** the 2 DIP `FocusRing` around the row (8 DIP
  corners) **plus** the `Surface` background. It shows only when the row was
  reached with the keyboard.
- **Rename:** the title becomes a text box with the `Surface` background and
  8 DIP corners, in the same font and position as the title, with all text
  selected. Enter saves and Esc cancels. Clicking elsewhere or closing the
  panel **saves**. An empty title keeps the old one.
- **Timer row** (decided 2026-11-17): 32 high, **12 DIP** below quick-add.
  Below it sits a 1 DIP `Border`-colour divider, also 12 DIP below.
  - **Layout:**
    - Stopwatch icon at 16 DIP, `TextSecondary`, 12 DIP in, with 8 DIP to
      what follows (the same spacing as a task row's checkbox).
    - Play / pause / stop icons at 16 DIP, `TextSecondary`, 8 DIP apart, the
      last one 12 DIP from the right edge.
  - **Idle:** "Focus" (`Body`, `TextPrimary`), then 8 DIP later the preset
    chips **15 · 25 · 50**, and ▶ at the right.
    - Chips are 24 high, 8 DIP side padding, 4 DIP apart, `Body`, 8 DIP
      corners, on a `Surface` background (like the theme picker; derived).
    - The selected chip has a 1 DIP `Accent` outline and `TextPrimary`; the
      others `TextSecondary`. 25 is selected at start-up.
    - Click a chip to select it, then ▶ to start (unlinked).
  - **Running:** the remaining time `mm:ss` (`Body`, tabular digits,
    `TextPrimary`), then ❚❚ and ■.
  - **Paused:** the time in `TextSecondary`, then ▶ (resume) and ■.
  - **Replacing a running timer** (PRD F4.7): the row turns into the
    question, `Body` `TextPrimary`, wrapping. Below it, right-aligned and 8 DIP
    apart, sit **Start new** and **Keep current**, styled as chips; "Start new"
    has the `Accent` outline. Enter means Start new, Esc means Keep current
    (Esc doesn't close the panel then).
- **Linked task marker:** a 16 DIP stopwatch in `Accent` at the task row's
  right end, 12 DIP from the edge, while its timer runs or is paused. It's
  hidden while the mouse is over the row, where the delete and move icons
  appear.
- **When a focus session ends** (decided 2026-11-18):
  - **Notification:** a tray notification with the title and body from §6.
    Clicking it opens the panel. There are no buttons; tray notifications
    can't have them.
  - **Ended while asleep:** if the end is noticed more than **1 minute** late,
    the notification uses the "ended" texts from §6 instead.
  - **Panel line:** for a session linked to a task, a line at the bottom of
    the panel (where the undo hint goes) reads "Focus session done", with a
    **Mark task done** chip on the right. It's one row high, `Small`
    `TextSecondary`, 12 DIP in, and stays until the panel closes. If the task
    is already done, there's no line.
  - **Pill pulse:** a **2 DIP** `PillAccent` outline that follows the shell's
    rounded shape (pill or panel). It's invisible except during the pulse:
    twice, opacity 0 → 1 → 0, 600 ms each (§4). There's no pulse when Windows
    animations are off.
- **Data notice** (task file restored or reset): a line at the **bottom of
  the panel**, where the undo hint goes, in `Small` text in `Danger`. It wraps
  to as many lines as it needs, at least one row high (32), 12 DIP in. It shows
  the first time the panel opens after start-up and disappears when the panel
  closes.
- **Growing while open:** when content changes the panel's height, it eases to
  the new height over **150 ms**, `QuadraticEase` EaseOut (same as the pill
  label change in §4).
- **Scrolling** (beyond max height 520): a thin overlay scrollbar, **4 DIP**
  wide, `Border` colour, rounded ends, visible only while the mouse is over
  the list.

## 3. Visual style

The **pill** is always dark in both themes, so it reads as part of the
screen's top edge. The **expanded panel** follows the Windows light/dark setting.

| Token | Light | Dark | Notes |
|---|---|---|---|
| `PillBackground` | `#121214` | `#121214` | Solid, the same in both themes |
| `PillText` | `#F2F2F5` | `#F2F2F5` | |
| `PillTextSecondary` | `#A0A0AB` | `#A0A0AB` | Dimmed pill text (paused timer). The same in both themes because the pill is always dark (added 2026-11-17). |
| `PillAccent` | `#F5A524` | `#F5A524` | The pill's "n today" dot. Always the dark-theme amber, because the pill is always dark (added 2026-11-17). |
| `Background` (panel) | `#F7F7F4` | `#1B1B1F` | **Solid** in v0.1. No acrylic or blur: that's hard in WPF and costs CPU. |
| `Surface` (rows on hover, input) | `#FFFFFF` | `#26262C` | |
| `Border` / divider | `#E4E4DF` | `#33333B` | 1 DIP |
| `TextPrimary` | `#1A1A1E` | `#F2F2F5` | |
| `TextSecondary` | `#5C5C66` | `#A0A0AB` | Both meet WCAG AA on their background |
| `Accent` | `#B45309` | `#F5A524` | Amber. A darker shade on light so text still meets AA contrast. |
| `Success` (done) | `#15803D` | `#4ADE80` | Checkbox fill when done |
| `Danger` | `#B91C1C` | `#F87171` | Delete action, error notices |
| `FocusRing` | `Accent` | `Accent` | 2 DIP outline for keyboard focus |
| Corner radius | Pill **14** (fully round), Panel **16**, Rows/inputs **8** | | |
| Spacing unit | **4** | | Every margin and padding is a multiple of 4 |
| Shadow | Panel: blur 24, offset Y 8, black at 35 % | | Pill: none |

**Typography:** **Segoe UI Variable** (falls back to Segoe UI on Windows 10).

| Style | Size | Weight | Use |
|---|---|---|---|
| `PillText` | 12 | SemiBold, tabular digits | Pill label and timer |
| `Body` | 14 | Regular | Task titles, input |
| `Caption` | 12 | SemiBold, UPPERCASE, `TextSecondary` | Section headers (WPF has no letter-spacing, so the earlier "4 %" was dropped on 2026-11-16) |
| `Small` | 12 | Regular | Secondary info |

**Theme:** follow the Windows app theme automatically (a setting can
override it; see PRD F6).

**Icons:** **Segoe Fluent Icons** on Windows 11, with **Segoe MDL2 Assets** as
the fallback on Windows 10. Both are font icons built into Windows, so no
image files are needed. Size 16 in the panel, 12 in the pill.

## 4. Motion

| Interaction | Animation | Duration | Easing (WPF) |
|---|---|---|---|
| Expand | Pill grows in width, then height, into the panel (anchored top-centre); content fades in from 0 to 1, starting 60 ms later | **220 ms** | `CubicEase`, EaseOut |
| Collapse | Reverse: content fades out first, then the size shrinks | **160 ms** | `CubicEase`, EaseIn |
| Pill label change (count ↔ timer, and "All clear" ↔ "{n} today") | Crossfade, width animates if it changes. A number changing (3 → 2) is instant. | 150 ms | `QuadraticEase`, EaseOut |
| Task completed | Check fills with `Success`, strikethrough appears, row opacity drops to 60 % | **150 ms** | `QuadraticEase`, EaseOut |
| Task deleted | Row height and opacity go to 0 | 120 ms | `QuadraticEase`, EaseIn |
| Timer finished | Pill border pulses `Accent` twice (opacity 0 → 1 → 0) | 2 × 600 ms | `SineEase`, EaseInOut |

If Windows "Show animations" is **off**, every animation becomes an instant
change. There is still a notification when the timer ends.

## 5. Interaction details

- **Global hotkey:** **Win + Alt + L** ("L for Ledge"; configurable in Settings).
  - Why (decided 2026-11-16): on the owner's PC, Ctrl+Alt+Space and
    Win+Alt+T were already owned by other apps. Ctrl+L was considered and
    rejected: as a *global* hotkey it would stop Ctrl+L working in every
    other app (browser address bar, VS Code, Word, terminals). Win+Alt+L is
    free, isn't used by Windows, and doesn't collide with AltGr characters.
  - Fallbacks if it's taken: **Win + Alt + Space**, then **Ctrl + Alt + Shift + L**.
  - Rule for future choices: never pick a shortcut that apps use inside
    their own windows (Ctrl+letter, Ctrl+Shift+letter).
- **Hover open delay:** **150 ms** · **close delay:** **300 ms** after the mouse
  leaves the panel and its hover zone.
- **Clicking the collapsed pill:** does the same as the hotkey. It opens the
  panel *and* activates it, with focus in quick-add. A click is a deliberate
  action, so taking focus is expected; a hover is not. Clicking outside the
  panel then closes it.
- **Hover-opened panel:** doesn't take focus. Clicking inside it activates it.
  Once it has focus, it stays open when the mouse leaves (PRD F2.6).
- **Tray icon:**
  - Left-click toggles the panel, opening it with focus like the hotkey.
  - Right-click opens a menu: **Open**, **Settings** (from M4 step 4c),
    **Quit**. The tooltip is "Ledge".
  - The menu matches the panel: `Background`, a 1 DIP `Border`-colour outline,
    8 DIP corners and 4 DIP inner padding. Items are 32 high, 12 DIP side
    padding, `Body` text in `TextPrimary`, with a `Surface` background and
    8 DIP corners when highlighted.
- **Alt+F4 on the panel** closes the panel like Esc. Only **Quit** exits Ledge (PRD F5.4).
- **Settings window** (decided 2026-11-17; opened from the tray):
  - **Window:** a standard window with the system title bar "Ledge settings",
    made dark through the Windows dark-title-bar API. Fixed **400 DIP** wide,
    height fits the content, **24 DIP** padding, `Background` colour, panel
    fonts. Centred on screen and shown in the taskbar while open. Only one is
    open at a time; opening it again brings it to the front.
  - **No Save button:** every change applies and saves at once.
  - **Layout:** each setting is a label (`Body`, `TextPrimary`) with its control
    **8 DIP** below; **16 DIP** between settings (derived from the spacing unit).
  - **Shortcut box:** 36 high (like quick-add), `Surface`, 8 DIP corners,
    12 DIP side padding, `Body` text such as "Win + Alt + L". Click it to
    record: it shows "Press a shortcut…" and the 2 DIP `FocusRing`. The new
    shortcut applies as soon as it's valid and free. Esc or clicking elsewhere
    cancels.
    - **Allowed:** must include **Win**, or **Ctrl+Alt** together (Shift
      optional), plus one non-modifier key. That rules out Alt+letter,
      Ctrl+letter and Ctrl+Shift+letter, which apps use for their own menus
      and commands.
    - **Problems:** an invalid or already-taken shortcut shows a `Small`
      message in `Danger` under the box, and the old shortcut stays active.
      Below the box sits a `Small` `TextSecondary` hint, 4 DIP under it
      (derived). A problem message replaces the hint in the same spot.
  - **Start with Windows:** a toggle switch, 40×20 with fully rounded ends.
    - Off: `Surface` track, 1 DIP `Border` outline, `TextSecondary` thumb.
    - On: `Accent` track with a `Background`-coloured thumb.
    - The thumb is 12 DIP with 4 DIP inset (derived). It switches instantly.
  - **Theme:** a segmented control, "System | Light | Dark", on a `Surface`
    background with 8 DIP corners. Segments are 32 high with 12 DIP side
    padding. The selected segment has a 1 DIP `Accent` outline and
    `TextPrimary` text; the others use `TextSecondary`.
- **Keyboard shortcuts inside the panel:**

| Key | Action |
|---|---|
| `Enter` (in quick-add) | Add task to Today |
| `↓` from quick-add / `↑` from first row | Move between quick-add and list |
| `↑` / `↓` | Move selection |
| `Space` | Toggle done |
| `Del` | Delete (undo with `Ctrl+Z`) |
| `F2` | Rename |
| `Ctrl+I` | Move between Today ↔ Inbox |
| `Ctrl+Enter` | Start a focus session on the selected task, using the selected preset (25 min unless changed) |
| `Ctrl+N` or `/` | Jump to quick-add |
| `Esc` | Cancel rename → else close panel |

## 6. Copy and tone

Voice: short, plain, friendly. No exclamation marks, no emoji in the UI.

| Where | Text |
|---|---|
| Quick-add placeholder | "Add a task…" |
| Today empty | "Nothing on the ledge. Add a task above." |
| Inbox empty | "Inbox is clear." |
| Move action tooltips | "Move to Inbox" / "Move to Today" |
| Delete action tooltip | "Delete" |
| Tray menu | "Open" · "Settings" · "Quit"; tooltip "Ledge" |
| Settings window title | "Ledge settings" |
| Settings: shortcut | "Shortcut" · hint "Opens Ledge from anywhere. Click, then press the keys." · while recording "Press a shortcut…" |
| Settings: shortcut invalid | "Use Win or Ctrl+Alt together with a key." |
| Settings: shortcut taken | "{hotkey} is already used by another app." |
| Settings: other labels | "Start with Windows" · "Theme" · "System" / "Light" / "Dark" |
| Pill, nothing open | "All clear" |
| Pill, tasks open | "{n} today" |
| Timer finished: title | "Focus session done" |
| Timer finished: body (linked) | "{minutes} min on “{task}”. Time for a short break." |
| Timer finished: body (unlinked) | "{minutes} minutes of focus. Time for a short break." |
| Timer finished: action | "Mark task done" |
| Replace running timer | "Stop the current session and start a new one?" · buttons "Start new" / "Keep current" |
| Timer ended while asleep: title | "Focus session ended" |
| Timer ended while asleep: body | "It ended at {time}, while your PC was asleep." ({time} in the user's short time format) |
| Panel line after a linked session | "Focus session done" · chip "Mark task done" |
| Timer row label | "Focus" |
| Timer, names for screen readers only (not shown) | "Start focus session" · "Pause" · "Resume" · "Stop" · "{n} minutes" (chips) · "Linked to the focus timer" (marker) |
| Hotkey taken | "{hotkey} is already used by another app. Pick a different shortcut in Settings." |
| Data restored from backup | "Your task file couldn't be read, so Ledge restored the last backup." |
| Data reset (file and backup unreadable) | "Your task file and its backup couldn't be read, so Ledge started with an empty list. The damaged files were kept in the Ledge data folder." |
| Undo delete hint | "Task deleted · Ctrl+Z to undo" |

## 7. Inspiration board

Ideas only. No assets, names or layouts are copied.

| Source | What I like | What I'm avoiding |
|---|---|---|
| **NotchOwl** (macOS) | Tasks, focus timer and today in one hover panel; hotkey to open; top-centre placement on Macs without a notch | Notes and calendar in v0.1 (scope) |
| **Yata Notch**, **Nusa**, **Isle Notch** (Windows) | Proof that a top-centre pill feels at home on Windows; Pomodoro plus to-do is the common core | Music, lyrics, water reminders, file shelf: too much for an always-visible tool |
| **Notchify** (Windows) | — | Reviewers say it covers app content, which is why our pill is small and hides over fullscreen apps |
| **Tomte** | Nothing drawn until you reach the edge: a possible future "minimal mode" | — |
| **PowerToys Run** | Hotkey → type → Enter → gone; that's exactly how quick-add should feel | — |
