namespace Ledge.Core.Services;

/// <summary>
/// The title rules from PRD F3.2: trimmed, never empty, at most 200 characters.
/// </summary>
public static class TaskTitle
{
    public const int MaxLength = 200;

    /// <summary>
    /// Returns the cleaned-up title, or null when there is nothing to save
    /// (null, empty or whitespace-only input).
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        // A .NET string is UTF-16: an emoji like 😀 is TWO chars (a "surrogate
        // pair"). Cutting between them would leave half a character, which
        // shows up as a "?" box. So if the cut lands mid-pair, cut one earlier.
        // https://learn.microsoft.com/dotnet/standard/base-types/character-encoding-introduction
        var cut = char.IsHighSurrogate(trimmed[MaxLength - 1]) ? MaxLength - 1 : MaxLength;

        // The cut may expose a space at the end ("… word |cut| next"); trim again.
        return trimmed[..cut].TrimEnd();
    }
}
