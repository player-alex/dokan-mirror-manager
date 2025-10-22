using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Manages the system tray icon and related functionality.
/// </summary>
public interface ITrayIconManager
{
    /// <summary>
    /// Initializes the tray icon with the specified window and actions.
    /// </summary>
    /// <param name="window">The main application window.</param>
    /// <param name="showWindowAction">Action to show the window.</param>
    /// <param name="exitAction">Async function to handle application exit.</param>
    /// <param name="setStatusMessageAction">Optional action to set status message.</param>
    void Initialize(Window window, Action showWindowAction, Func<Task> exitAction, Action<string>? setStatusMessageAction = null);

    /// <summary>
    /// Shows the main application window.
    /// </summary>
    void ShowWindow();

    /// <summary>
    /// Hides the main application window to the system tray.
    /// </summary>
    void HideWindow();

    /// <summary>
    /// Shows a balloon tip notification.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="icon">The icon to display.</param>
    void ShowBalloonTip(string title, string message, BalloonIcon icon);

    /// <summary>
    /// Disposes of the tray icon resources.
    /// </summary>
    void Dispose();
}
