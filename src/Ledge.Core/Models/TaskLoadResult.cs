namespace Ledge.Core.Models;

/// <summary>The loaded tasks plus how they were loaded.</summary>
public sealed record TaskLoadResult(TaskDocument Document, TaskLoadOutcome Outcome);
