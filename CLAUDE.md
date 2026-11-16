# CLAUDE.md — Ledge

A small Windows overlay app: a pill at the edge of the screen that expands
(hover or global hotkey) into a task list and focus timer. C# / .NET 9 / WPF.

## Read first

- `docs/PRD.md` — what we're building and what's out of scope
- `docs/DESIGN.md` — visual & interaction decisions (**owned by me**)
- `docs/ARCHITECTURE.md` — structure, layers, Win32 techniques
- `docs/ROADMAP.md` — current milestone and its checklist
- `docs/adr/` — why past decisions were made

## How I want to work with you

I'm building this to learn real Windows development. So:

1. **Explain before you implement.** For anything new (a Win32 API, a WPF
   concept, a pattern), give me a short explanation and the plan first, and
   wait for my OK.
2. **One milestone at a time.** Only work on the current milestone in
   `docs/ROADMAP.md`. If you notice something for later, tell me — don't build it.
3. **Small steps.** Prefer changes I can review in one sitting. Stop after each
   logical step so I can run it and commit.
4. **Design is mine.** Never invent or change colours, sizes, fonts,
   animations, copy or placement. Use values from `docs/DESIGN.md` /
   `Themes/Tokens.xaml`. If a value is missing (🟡 DECIDE), ask me.
5. **Ask before adding a NuGet package** — say what it does and the alternative.
6. **Don't commit or push** unless I ask. Suggest a commit message instead.
7. **Teach me.** When you use Win32 interop, link the Microsoft docs page in a
   code comment. Point out anything I should understand rather than copy.
8. When unsure about scope or behaviour, check `docs/PRD.md`, then ask.

## Commands

```bash
dotnet build
dotnet test
dotnet watch --project src/Ledge      # run with hot reload
dotnet format                              # fix formatting
```

## Architecture rules

- `src/Ledge.Core` has **no** WPF / Windows references. Models, interfaces
  and logic only. It must be unit-testable.
- `src/Ledge` is the WPF app: Views, ViewModels, UI services, interop.
- **MVVM** with CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`).
  Code-behind is only for purely visual things (animations, focus).
- ViewModels depend on **interfaces**, injected via the Generic Host DI.
- All P/Invoke goes in `Interop/NativeMethods.cs`, one comment per function.
- No hard-coded colours/sizes in XAML — use resources from `Themes/Tokens.xaml`.
- Time comes from an injected `TimeProvider`, never `DateTime.Now` directly.
- File I/O is async; never block the UI thread (`.Result`, `.Wait()` forbidden).
  Only exception: the bounded final save in `App.OnExit` (ADR-0005).
- Saves are atomic (temp file + replace) with a `.bak` copy.

## C# conventions

- Nullable reference types on; warnings are errors.
- File-scoped namespaces; one public type per file; file name = type name.
- `PascalCase` for types/members, `_camelCase` for private fields,
  `Async` suffix on async methods.
- Prefer `record` for immutable models, `sealed` by default.
- Guard clauses over deep nesting. No magic numbers — name them.

## Testing

- Every change to Core comes with xUnit tests (happy path + edge cases).
- Use `FakeTimeProvider` for anything time-based.
- Storage tests use a temp directory, never the real `%LOCALAPPDATA%` folder.
- UI/Win32 behaviour: add the manual check to the milestone checklist instead.

## Definition of done (every step)

- [ ] `dotnet build` passes with no warnings
- [ ] `dotnet test` passes
- [ ] Matches `docs/DESIGN.md`
- [ ] I've run it and checked the behaviour by hand
- [ ] Docs updated if structure or a decision changed (add an ADR for big ones)
