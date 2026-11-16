namespace Ledge.ViewModels;

/// <summary>
/// Animation timings the ViewModel needs to wait for. ViewModels can't read
/// XAML resources, so App builds this from Tokens.xaml (zero when Windows
/// animations are off) and the DI container hands it over.
/// </summary>
/// <param name="RowRemoveDuration">The delete animation (DESIGN.md §4: 120 ms).</param>
public sealed record TaskListMotion(TimeSpan RowRemoveDuration);
