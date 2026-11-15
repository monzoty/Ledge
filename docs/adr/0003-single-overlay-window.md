# ADR-0003: One fixed-size overlay window for both pill and panel

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

The pill (112 × 28 DIP) expands into a panel (360 × up to 520 DIP) with a
220 ms animation (DESIGN.md §4). The window is borderless, transparent,
topmost and must not take focus on hover. WPF can animate a window's
`Width`/`Height`/`Left`, but each change re-creates the layered-window
surface, which is often jerky and can flicker at non-100 % DPI.

## Options considered

1. **One window that resizes itself** — the smallest footprint on screen; but
   animating window bounds is jerky in WPF, and the position must be
   recalculated every frame.
2. **Two windows (pill + panel)** — each is simple, but focus, hover handoff
   and z-order between two topmost windows are fiddly, and the "grow from the
   pill" animation is hard.
3. **One fixed-size window, content animates inside** — the window is as big
   as the largest expanded state plus its shadow; only XAML elements animate
   (`ScaleTransform`, `Width`, `Opacity`), which is smooth. Fully transparent
   pixels let mouse clicks through, so the unused area doesn't block apps
   underneath.

## Decision

Use **option 3**: a single `PillWindow` at top-centre, sized for the
expanded panel plus its shadow margin, with all animation done in the
window's content.

## Consequences

- Smooth animations and one place for focus and hover logic.
- The transparent area must stay **fully** transparent (alpha 0), or it
  will swallow clicks. Only the deliberate hover margin uses `#01000000`.
- Layered windows render through the CPU, which is fine at this size;
  re-check idle CPU and memory in M6.
- If click-through ever misbehaves (e.g. some apps' drag-and-drop), fall back
  to option 1 for the collapsed state only, or toggle `WS_EX_TRANSPARENT`
  while collapsed.
