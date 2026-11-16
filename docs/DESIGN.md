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
| Collapsed (idle, tasks open) | **112 × 28** | Small amber dot + "**3 today**" (count of open tasks in Today) |
| Collapsed (idle, nothing open) | **112 × 28** | Check icon + "**All clear**" |
| Collapsed (timer running) | **136 × 28** | Ring icon showing progress + "**18:42**" (tabular digits); when paused, a pause icon replaces the ring and the text dims to secondary |
| Peek (hover) | — | **None in v0.1.** Hover goes straight to Expanded. Simpler, and one fewer state to tune. |
| Expanded | **360 wide**, height fits content, **min 160, max 520** (the list scrolls beyond that) | The pill widens and grows downward into the panel |
| Empty (no tasks) | — | Message in the Today area (§6) |

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
| Pill label change (count ↔ timer) | Crossfade, width animates | 150 ms | `QuadraticEase`, EaseOut |
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
| `Ctrl+Enter` | Start a 25-min focus session on the selected task |
| `Ctrl+N` or `/` | Jump to quick-add |
| `Esc` | Cancel rename → else close panel |

## 6. Copy and tone

Voice: short, plain, friendly. No exclamation marks, no emoji in the UI.

| Where | Text |
|---|---|
| Quick-add placeholder | "Add a task…" |
| Today empty | "Nothing on the ledge. Add a task above." |
| Inbox empty | "Inbox is clear." |
| Pill, nothing open | "All clear" |
| Pill, tasks open | "{n} today" |
| Timer finished: title | "Focus session done" |
| Timer finished: body (linked) | "{minutes} min on “{task}”. Time for a short break." |
| Timer finished: body (unlinked) | "{minutes} minutes of focus. Time for a short break." |
| Timer finished: action | "Mark task done" |
| Replace running timer | "Stop the current session and start a new one?" · buttons "Start new" / "Keep current" |
| Hotkey taken | "{hotkey} is already used by another app. Pick a different shortcut in Settings." |
| Data restored from backup | "Your task file couldn't be read, so Ledge restored the last backup." |
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
