namespace Ledge.Core.Models;

/// <summary>How <see cref="TaskLoadResult.Document"/> was obtained, so the UI can tell the user (DESIGN.md §6).</summary>
public enum TaskLoadOutcome
{
    /// <summary>tasks.json was read normally.</summary>
    Loaded,

    /// <summary>No saved tasks yet (first start): an empty document.</summary>
    FirstRun,

    /// <summary>tasks.json was unusable; the previous version (tasks.json.bak) was loaded.</summary>
    RestoredFromBackup,

    /// <summary>Both files were unusable; started empty. The broken files were kept, renamed.</summary>
    ResetAfterCorruption,
}
