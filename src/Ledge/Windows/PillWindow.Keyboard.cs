using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ledge.ViewModels;

namespace Ledge.Windows;

/// <summary>
/// Keyboard focus inside the panel (DESIGN.md §5): which element has the
/// keyboard, and moving it with ↑/↓, Ctrl+N and "/". Focus is a view concern,
/// so it lives in code-behind; the ACTIONS (Space, Del, F2, Ctrl+I, Ctrl+Z)
/// are KeyBindings in XAML that call ViewModel commands.
/// </summary>
/// <remarks>
/// "Preview" events travel from the window DOWN to the focused element before
/// the normal events bubble back UP, so this handler sees every key first and
/// can choose to handle it (e.Handled = true) or let it continue.
/// https://learn.microsoft.com/dotnet/desktop/wpf/events/routed-events-overview
/// </remarks>
public sealed partial class PillWindow
{
    private void OnPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;

        // Inside the rename box, keys belong to the box (arrows move the caret).
        if (focused is TextBox box && box != QuickAdd.Input)
        {
            return;
        }

        var inQuickAdd = focused == QuickAdd.Input;
        var row = focused as FrameworkElement;
        var rowIndex = row is null ? -1 : FocusableRows().IndexOf(row);

        switch (e.Key)
        {
            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                e.Handled = true;
                QuickAdd.FocusInput();
                break;

            case Key.Down when inQuickAdd:
                e.Handled = true;
                FocusRowAt(0);
                break;

            case Key.Down when rowIndex >= 0:
                e.Handled = true;
                FocusRowAt(rowIndex + 1);
                break;

            case Key.Up when rowIndex == 0:
                e.Handled = true;
                QuickAdd.FocusInput();
                break;

            case Key.Up when rowIndex > 0:
                e.Handled = true;
                FocusRowAt(rowIndex - 1);
                break;

            // Del and Ctrl+I take the selected row out of its list. Move the
            // selection to the row below (or above) first; the key itself still
            // reaches the old row's KeyBinding, because a routed event's path is
            // fixed when the event starts.
            case Key.Delete when rowIndex >= 0:
            case Key.I when rowIndex >= 0 && Keyboard.Modifiers == ModifierKeys.Control:
                SelectNeighbourOf(rowIndex);
                break;
        }
    }

    /// <summary>
    /// "/" jumps to quick-add (DESIGN.md §5). Handled as text, not as a key,
    /// because the "/" key sits in different places on different keyboard layouts.
    /// Ignored while typing in a text box, where "/" is just a character.
    /// </summary>
    private void OnPanelPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text != "/" || Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        e.Handled = true;
        QuickAdd.FocusInput();
    }

    /// <summary>
    /// Every row the keyboard can select, top to bottom: Today, then the Inbox
    /// when it's open. Rows that are fading out (being deleted) are skipped.
    /// </summary>
    private List<FrameworkElement> FocusableRows()
    {
        var rows = new List<FrameworkElement>();
        Collect(ListContent, rows);
        return rows;

        static void Collect(DependencyObject parent, List<FrameworkElement> rows)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // A row is the focusable Border whose DataContext is a row ViewModel
                // (the rename TextBox shares that DataContext, but isn't a Border).
                if (child is Border { Focusable: true, IsVisible: true, DataContext: TaskRowViewModel { IsRemoving: false } } row)
                {
                    rows.Add(row);
                    continue;
                }

                Collect(child, rows);
            }
        }
    }

    /// <summary>Selects the row at <paramref name="index"/>, clamped to the list; nothing if there are no rows.</summary>
    private void FocusRowAt(int index)
    {
        var rows = FocusableRows();
        if (rows.Count == 0)
        {
            return;
        }

        var row = rows[Math.Clamp(index, 0, rows.Count - 1)];
        Keyboard.Focus(row);

        // Scroll it into view if the list is longer than the panel.
        row.BringIntoView();
    }

    /// <summary>The row at <paramref name="index"/> is leaving: select the next one, else the previous, else quick-add.</summary>
    private void SelectNeighbourOf(int index)
    {
        var rows = FocusableRows();
        if (rows.Count <= 1)
        {
            QuickAdd.FocusInput();
            return;
        }

        FocusRowAt(index + 1 < rows.Count ? index + 1 : index - 1);
    }
}
