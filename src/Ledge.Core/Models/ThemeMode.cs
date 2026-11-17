namespace Ledge.Core.Models;

/// <summary>Which colours the panel uses (PRD F6, DESIGN.md §3). The pill is always dark.</summary>
public enum ThemeMode
{
    /// <summary>Follow the Windows app theme, live (default).</summary>
    System,

    Light,

    Dark,
}
