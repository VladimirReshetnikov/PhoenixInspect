using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PhoenixInspect.Desktop.Docking;
using PhoenixInspect.Desktop.ViewModels;

namespace PhoenixInspect.Desktop.Views;

/// <summary>
/// The Immediate tool pane: a C#-colored transcript above a prompt with the shared completion drop-down.
/// </summary>
public partial class ImmediatePaneView : UserControl
{
    private CompletionController? controller;
    private ImmediateViewModel? subscribedPanel;

    /// <summary>Creates the view and configures the read-only transcript editor.</summary>
    public ImmediatePaneView()
    {
        InitializeComponent();
        TranscriptEditor.SyntaxHighlighting = PhoenixCSharpHighlighting.Definition;
        DataContextChanged += (_, _) => Attach();

        // Focus-driven bring-into-view requests from the prompt row would nudge the console sideways by the
        // prompt width; scrolling is managed explicitly instead, so the surface stays pinned to the left edge.
        // The handler sits inside the scroll presenter's route, so it wins before the presenter scrolls.
        ConsoleStack.AddHandler(
            RequestBringIntoViewEvent,
            (_, e) => e.Handled = true,
            handledEventsToo: false);
    }

    private ImmediateTool? Tool => DataContext as ImmediateTool;

    private void Attach()
    {
        if (subscribedPanel is not null)
        {
            subscribedPanel.PropertyChanged -= OnPanelChanged;
        }

        subscribedPanel = Tool?.Panel;
        if (subscribedPanel is not null)
        {
            subscribedPanel.PropertyChanged += OnPanelChanged;
            controller = new CompletionController(CompletionPopup, CompletionList, subscribedPanel.Completion);
            ApplyTranscript();
        }
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ImmediateViewModel.TranscriptText), StringComparison.Ordinal))
        {
            ApplyTranscript();
        }
    }

    private void ApplyTranscript()
    {
        var text = (subscribedPanel?.TranscriptText ?? string.Empty).TrimEnd('\r', '\n');
        TranscriptEditor.Text = text;

        // An empty transcript collapses the editor entirely, so the prompt line starts at the very top —
        // the input is the console's next line, never a detached field.
        TranscriptEditor.IsVisible = text.Length > 0;
        Dispatcher.UIThread.Post(
            () =>
            {
                TranscriptScroll.ScrollToEnd();
                TranscriptScroll.Offset = TranscriptScroll.Offset.WithX(0);
            },
            DispatcherPriority.Loaded);
    }

    private void OnSurfacePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Clicking the empty console surface puts the caret on the prompt line, like a terminal.
        if (e.Source is ScrollViewer or StackPanel)
        {
            InputBox.Focus();
        }
    }

    private void OnSubmitClick(object? sender, RoutedEventArgs e)
    {
        controller?.Close();
        if (Tool is { } tool)
        {
            _ = tool.Panel.SubmitAsync();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (Tool is not { } tool)
        {
            return;
        }

        if (controller?.HandleKey(e) == true)
        {
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _ = tool.Panel.SubmitAsync();
                break;
            case Key.Up when tool.Panel.HistoryPrevious() is { } previous:
                e.Handled = true;
                SetInput(previous);
                break;
            case Key.Down when tool.Panel.HistoryNext() is { } next:
                e.Handled = true;
                SetInput(next);
                break;
            default:
                break;
        }
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box)
        {
            controller?.Update(box);
        }
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => controller?.Close();

    private void SetInput(string text)
    {
        controller?.Close();
        InputBox.Text = text;
        InputBox.CaretIndex = text.Length;
    }
}
