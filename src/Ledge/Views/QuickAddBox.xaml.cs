using System.Windows.Controls;
using System.Windows.Input;

namespace Ledge.Views;

/// <summary>
/// The "Add a task…" box at the top of the panel. Visual only until M3.
/// </summary>
public sealed partial class QuickAddBox : UserControl
{
    public QuickAddBox()
    {
        InitializeComponent();
    }

    /// <summary>Puts the text cursor in the box (window must already be active).</summary>
    public void FocusInput()
    {
        Keyboard.Focus(Input);

        // Keep any half-typed text, but place the caret at the end of it.
        Input.CaretIndex = Input.Text.Length;
    }
}
