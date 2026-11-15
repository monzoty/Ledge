# Ledge

> Your day, one glance away.

A tiny Windows companion that lives at the edge of your screen. Hover or press
a hotkey to capture tasks, see today's list and run a focus timer — without
opening a window.

**Status:** planning · v0.1 in progress

## Docs

| File | What it's for |
|---|---|
| [docs/PRD.md](docs/PRD.md) | What we're building, for whom, and what v0.1 includes |
| [docs/DESIGN.md](docs/DESIGN.md) | Visual & interaction decisions (owned by the project owner) |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Tech stack, structure, Windows techniques |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Milestones M0–M7 with checklists |
| [docs/adr/](docs/adr/) | Architecture Decision Records |
| [CLAUDE.md](CLAUDE.md) | Rules for Claude Code in this repo |

## Requirements

- Windows 10 (1903+) or Windows 11
- .NET 9 SDK
- VS Code + C# Dev Kit extension

## Getting started

```bash
dotnet build
dotnet test
dotnet watch --project src/Ledge
```

## Default shortcut

**Ctrl + Alt + Space** opens the panel (configurable in Settings).

## Next step

Docs are complete. Start **M0** in [docs/ROADMAP.md](docs/ROADMAP.md).

## License

MIT (a `LICENSE` file is added in M0).
