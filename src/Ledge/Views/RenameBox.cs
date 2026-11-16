using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ledge.Views;

/// <summary>
/// Focus behaviour for the inline rename box (DESIGN.md §2 "Rename"), as
/// attached properties so the row template can use it from XAML:
/// <code>
/// &lt;TextBox views:RenameBox.IsEditing="{Binding IsEditing}"
///          views:RenameBox.CommitOnBlur="{Binding CommitRenameCommand}" /&gt;
/// </code>
/// </summary>
/// <remarks>
/// An attached property is a property one class defines but any element can
/// carry (like Grid.Row). Its "changed" callback is where the behaviour lives.
/// https://learn.microsoft.com/dotnet/desktop/wpf/properties/attached-properties-overview
/// This is focus handling only (CLAUDE.md allows that outside ViewModels);
/// what "commit" means is decided by the ViewModel's command.
/// </remarks>
public static class RenameBox
{
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.RegisterAttached(
        "IsEditing", typeof(bool), typeof(RenameBox), new PropertyMetadata(false, OnIsEditingChanged));

    public static readonly DependencyProperty CommitOnBlurProperty = DependencyProperty.RegisterAttached(
        "CommitOnBlur", typeof(ICommand), typeof(RenameBox), new PropertyMetadata(null, OnCommitOnBlurChanged));

    public static bool GetIsEditing(DependencyObject element) => (bool)element.GetValue(IsEditingProperty);

    public static void SetIsEditing(DependencyObject element, bool value) => element.SetValue(IsEditingProperty, value);

    public static ICommand? GetCommitOnBlur(DependencyObject element) => (ICommand?)element.GetValue(CommitOnBlurProperty);

    public static void SetCommitOnBlur(DependencyObject element, ICommand? value) => element.SetValue(CommitOnBlurProperty, value);

    private static void OnIsEditingChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            // The box was hidden a moment ago; wait until it's laid out before focusing.
            box.Dispatcher.BeginInvoke(
                () =>
                {
                    Keyboard.Focus(box);
                    box.SelectAll();
                },
                DispatcherPriority.Loaded);
            return;
        }

        // Editing ended (Enter / Esc). Hiding the box drops keyboard focus; give it
        // back to the row, so ↑/↓ and the shortcuts keep working. Done after the
        // current update settles, because the order in which WPF hides the box and
        // calls us isn't guaranteed. If the user moved focus somewhere on purpose
        // (e.g. clicked quick-add), FocusedElement isn't empty and we leave it.
        if (FindFocusableAncestor(box) is not UIElement row)
        {
            return;
        }

        box.Dispatcher.BeginInvoke(
            () =>
            {
                if (row.IsVisible && Keyboard.FocusedElement is null or Window)
                {
                    Keyboard.Focus(row);
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>
    /// A click inside the panel on something that can't take focus (a row, an
    /// icon) doesn't move keyboard focus, so the box would never "blur". The
    /// window calls this on every mouse press: a click outside the open rename
    /// box moves focus to its row, which commits the rename.
    /// </summary>
    public static void CommitIfClickedOutside()
    {
        if (Keyboard.FocusedElement is not TextBox box || !GetIsEditing(box) || box.IsMouseOver)
        {
            return;
        }

        if (FindFocusableAncestor(box) is { } row)
        {
            Keyboard.Focus(row);
        }
    }

    private static void OnCommitOnBlurChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box)
        {
            return;
        }

        box.LostKeyboardFocus -= OnLostKeyboardFocus;
        if (e.NewValue is not null)
        {
            box.LostKeyboardFocus += OnLostKeyboardFocus;
        }
    }

    /// <summary>Clicking elsewhere or closing the panel saves (DESIGN.md §2).</summary>
    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var command = GetCommitOnBlur((DependencyObject)sender);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private static IInputElement? FindFocusableAncestor(DependencyObject start)
    {
        for (var node = VisualTreeHelper.GetParent(start); node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement { Focusable: true } element)
            {
                return element;
            }
        }

        return null;
    }
}
