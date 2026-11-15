using System.Reflection;

namespace Ledge.Core.Tests.Architecture;

/// <summary>
/// Guards the rule "Ledge.Core has no WPF / Windows references" (CLAUDE.md).
/// If someone adds a WPF reference to Core, this test fails.
/// </summary>
public sealed class CoreDependencyTests
{
    private static readonly string[] ForbiddenAssemblies =
    [
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Windows.Forms",
    ];

    [Fact]
    public void Core_DoesNotReferenceWpfOrWinForms()
    {
        var core = Assembly.Load("Ledge.Core");

        var referenced = core.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        Assert.DoesNotContain(referenced, name => ForbiddenAssemblies.Contains(name));
    }
}
