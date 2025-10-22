using DokanMirrorManager.Services.Interfaces;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Controls;

namespace DokanMirrorManager.Services;

/// <summary>
/// Manages the system tray icon and related window operations.
/// </summary>
public class TrayIconManager : ITrayIconManager
{
    private TaskbarIcon? _taskbarIcon;
    private Window? _window;
    private Action? _showWindowAction;
    private Func<Task>? _exitAction;
    private Action<string>? _setStatusMessageAction;
    private bool _isClosingToTray = false;
    private bool _isHiding = false;

    /// <summary>
    /// Initializes the tray icon with the specified window and actions.
    /// </summary>
    public void Initialize(Window window, Action showWindowAction, Func<Task> exitAction, Action<string>? setStatusMessageAction = null)
    {
        _window = window;
        _showWindowAction = showWindowAction;
        _exitAction = exitAction;
        _setStatusMessageAction = setStatusMessageAction;

        // Create tray icon with a default icon
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Dokan Mirror Manager",
            NoLeftClickDelay = true
        };

        // Try to use window icon if available, otherwise use a default system icon
        try
        {
            if (window.Icon != null)
            {
                _taskbarIcon.IconSource = window.Icon;
            }
            else
            {
                // Use default application icon
                _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            _taskbarIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        // Create context menu
        var contextMenu = new ContextMenu();

        var openMenuItem = new MenuItem { Header = "Open" };
        openMenuItem.Click += (s, e) => ShowWindow();
        contextMenu.Items.Add(openMenuItem);

        contextMenu.Items.Add(new Separator());

        var exitMenuItem = new MenuItem { Header = "Exit" };
        exitMenuItem.Click += ExitMenuItem_Click;
        contextMenu.Items.Add(exitMenuItem);

        _taskbarIcon.ContextMenu = contextMenu;
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowWindow();

        // Hook window closing event
        window.Closing += Window_Closing;
    }

    /// <summary>
    /// Handles the window closing event to minimize to tray instead of closing.
    /// </summary>
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Window_Closing] _isClosingToTray={_isClosingToTray}");
        if (!_isClosingToTray)
        {
            e.Cancel = true;
            if (!_isHiding)
            {
                HideWindow();
            }
        }
    }

    /// <summary>
    /// Shows the main application window.
    /// </summary>
    public void ShowWindow()
    {
        if (_window != null)
        {
            _isHiding = false;
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }
    }

    /// <summary>
    /// Hides the main application window to the system tray.
    /// </summary>
    public void HideWindow()
    {
        if (_window != null)
        {
            _isHiding = true;
            _window.Hide();

            // Update status message if action is provided
            _setStatusMessageAction?.Invoke("Minimized to tray");

            // Show single balloon tip notification
            System.Diagnostics.Debug.WriteLine("[HideWindow] Showing balloon tip");
            _taskbarIcon?.ShowBalloonTip("Dokan Mirror Manager",
                                        "Application minimized to tray",
                                        BalloonIcon.Info);

            // Reset flag after a short delay
            Task.Delay(100).ContinueWith(_ => _isHiding = false);
        }
    }

    /// <summary>
    /// Shows a balloon tip notification.
    /// </summary>
    public void ShowBalloonTip(string title, string message, BalloonIcon icon)
    {
        _taskbarIcon?.ShowBalloonTip(title, message, icon);
    }

    /// <summary>
    /// Handles the Exit menu item click event.
    /// </summary>
    private async void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Show window first to provide proper parent for MessageBox
        if (_window != null)
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        // Small delay to ensure window is fully visible
        await Task.Delay(100);

        // Set flag to allow actual closure
        _isClosingToTray = true;

        // Execute the exit action (provided by ShellViewModel)
        if (_exitAction != null)
        {
            await _exitAction();
        }
    }

    /// <summary>
    /// Disposes of the tray icon resources.
    /// </summary>
    public void Dispose()
    {
        _taskbarIcon?.Dispose();
    }
}
