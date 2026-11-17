namespace Ledge.Services;

/// <summary>Whether Ledge starts when you sign in to Windows (PRD F5.3).</summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
