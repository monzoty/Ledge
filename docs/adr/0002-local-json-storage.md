# ADR-0002: Store data in local JSON files

- **Status:** Accepted
- **Date:** 2026-11-16

## Context

v0.1 has one user, a few hundred tasks at most, and no sync. Data must survive
crashes and be easy to inspect while developing.

## Options considered

1. **JSON file (System.Text.Json)** — zero dependencies, human-readable,
   trivial to back up. Whole file is rewritten on each save (fine at this size).
2. **SQLite (Microsoft.Data.Sqlite / EF Core)** — scales, queries, partial
   writes. More setup, migrations, harder to eyeball.
3. **LiteDB** — embedded document DB; extra dependency for little gain now.

## Decision

Use **JSON files** in `%LOCALAPPDATA%\Ledge\`, behind the `ITaskStore`
interface, with atomic writes, one `.bak` copy and a `SchemaVersion` field.

## Consequences

- Simple and debuggable; good for learning.
- If task history grows large (insights, years of data) or sync needs
  change-tracking, switch to SQLite by writing a new `ITaskStore`
  implementation — no UI changes needed.
