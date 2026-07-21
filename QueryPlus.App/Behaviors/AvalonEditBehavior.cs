using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;

namespace QueryPlus.App.Behaviors;

/// <summary>
/// Attached properties that make AvalonEdit's <see cref="TextEditor"/> usable from MVVM:
/// a two-way bindable <c>BoundText</c> and a <c>HighlightingName</c> to select a built-in
/// syntax definition (e.g. "TSQL"). AvalonEdit's own Text property is not a DP.
/// </summary>
public static class AvalonEditBehavior
{
    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating", typeof(bool), typeof(AvalonEditBehavior), new PropertyMetadata(false));

    public static readonly DependencyProperty BoundTextProperty =
        DependencyProperty.RegisterAttached(
            "BoundText", typeof(string), typeof(AvalonEditBehavior),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundTextChanged));

    public static string GetBoundText(DependencyObject d) => (string)d.GetValue(BoundTextProperty);

    public static void SetBoundText(DependencyObject d, string value) => d.SetValue(BoundTextProperty, value);

    public static readonly DependencyProperty HighlightingNameProperty =
        DependencyProperty.RegisterAttached(
            "HighlightingName", typeof(string), typeof(AvalonEditBehavior),
            new PropertyMetadata(null, OnHighlightingNameChanged));

    public static string? GetHighlightingName(DependencyObject d) => (string?)d.GetValue(HighlightingNameProperty);

    public static void SetHighlightingName(DependencyObject d, string? value) => d.SetValue(HighlightingNameProperty, value);

    private static void OnHighlightingNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor && e.NewValue is string name && !string.IsNullOrEmpty(name))
            editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(name);
    }

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor)
            return;

        EnsureHooked(editor);

        if ((bool)editor.GetValue(IsUpdatingProperty))
            return; // change originated from the editor itself

        var newText = e.NewValue as string ?? string.Empty;
        if (editor.Document != null && editor.Document.Text != newText)
            editor.Document.Text = newText;
    }

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked", typeof(bool), typeof(AvalonEditBehavior), new PropertyMetadata(false));

    private static void EnsureHooked(TextEditor editor)
    {
        if ((bool)editor.GetValue(IsHookedProperty))
            return;
        editor.SetValue(IsHookedProperty, true);
        editor.TextChanged += (_, _) =>
        {
            editor.SetValue(IsUpdatingProperty, true);
            try
            {
                SetBoundText(editor, editor.Document?.Text ?? string.Empty);
            }
            finally
            {
                editor.SetValue(IsUpdatingProperty, false);
            }
        };
    }
}
