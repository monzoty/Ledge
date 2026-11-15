# ADR-0001: Use WPF on .NET 9 for the UI

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

The app is mostly one small, borderless, transparent, always-on-top window
that must not steal focus and must react to a global hotkey. It needs deep
Win32 access (window styles, `RegisterHotKey`, monitor info). The developer
uses VS Code + Claude Code and wants to learn real Windows desktop development.

## Options considered

1. **WPF (.NET 9)** — mature, huge amount of docs/answers, easy transparent
   layered windows, simple `HwndSource` hooks for Win32 messages, no extra
   runtime to install. Looks dated by default (fixable with styles or WPF-UI).
2. **WinUI 3 (Windows App SDK)** — modern Fluent look, Mica/Acrylic built in.
   Transparent/borderless overlay windows are harder, packaging is more
   complex, fewer answers online, and tooling works best in Visual Studio.
3. **Tauri / Electron** — web UI; heavier (Electron) or adds Rust (Tauri).
   Not chosen: the goal is to learn C#/.NET.

## Decision

Use **WPF on .NET 9** with CommunityToolkit.Mvvm. Optionally use the WPF-UI
library for Fluent-style controls if the custom design calls for it.

## Consequences

- Easy overlay window and Win32 interop.
- Fluent look must be designed by hand (or via WPF-UI); this fits the plan to
  own the visual design.
- No visual XAML designer in VS Code — use `dotnet watch` hot reload.
- Revisit if WinUI 3 overlay support improves or Windows-only features
  (e.g. Widgets board) need the Windows App SDK — it can be referenced from
  WPF for specific APIs.
