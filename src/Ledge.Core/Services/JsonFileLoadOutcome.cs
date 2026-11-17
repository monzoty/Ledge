namespace Ledge.Core.Services;

/// <summary>How <see cref="JsonFile{T}.LoadAsync"/> got its result.</summary>
internal enum JsonFileLoadOutcome
{
    /// <summary>The main file was read normally.</summary>
    Loaded,

    /// <summary>Neither the file nor a backup exists yet (first start).</summary>
    Missing,

    /// <summary>The main file was unusable; the .bak (previous version) was loaded.</summary>
    RestoredFromBackup,

    /// <summary>Both were unusable; nothing loaded. The broken files were kept, renamed.</summary>
    ResetAfterCorruption,
}
