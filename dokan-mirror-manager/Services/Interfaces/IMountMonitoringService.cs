using DokanNet;
using DokanMirrorManager.Models;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Service responsible for monitoring mounted file systems and detecting external unmounts.
/// </summary>
public interface IMountMonitoringService
{
    /// <summary>
    /// Starts monitoring a mounted file system to detect when it's unmounted externally.
    /// </summary>
    /// <param name="instance">The DokanInstance to monitor.</param>
    /// <param name="item">The MountItem associated with this mount.</param>
    /// <param name="driveLetter">The drive letter being monitored (e.g., "Z:\\").</param>
    /// <param name="onUnmountDetected">Callback invoked when an external unmount is detected.</param>
    /// <returns>A task representing the asynchronous monitoring operation.</returns>
    Task StartMonitoringAsync(
        DokanInstance instance,
        MountItem item,
        string driveLetter,
        Action<MountItem> onUnmountDetected);

    /// <summary>
    /// Cancels monitoring for a specific mount item.
    /// </summary>
    /// <param name="item">The MountItem to stop monitoring.</param>
    void CancelMonitoring(MountItem item);

    /// <summary>
    /// Cancels all active monitoring operations.
    /// </summary>
    void CancelAllMonitoring();
}
