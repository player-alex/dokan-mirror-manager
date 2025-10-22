using DokanMirrorManager.Models;
using System.Windows;

namespace DokanMirrorManager.Services.Interfaces;

/// <summary>
/// Service responsible for mounting and unmounting mirror drives.
/// Handles the core mount/unmount operations, timeout handling, and background processing.
/// </summary>
public interface IMountService
{
    /// <summary>
    /// Initializes the service with callbacks from the view model.
    /// Must be called before using MountAsync or UnmountAsync.
    /// </summary>
    void Initialize(Func<Window?> getWindow, Action<string> setStatusMessage, Func<Task> saveConfiguration);

    /// <summary>
    /// Mounts a mirror drive asynchronously.
    /// </summary>
    /// <param name="item">The mount item to mount</param>
    /// <param name="isAutoMount">Whether this is an automatic mount operation (affects user prompts)</param>
    /// <returns>Result of the mount operation</returns>
    Task<MountResult> MountAsync(MountItem item, bool isAutoMount);

    /// <summary>
    /// Unmounts a mounted drive asynchronously.
    /// </summary>
    /// <param name="item">The mount item to unmount</param>
    /// <returns>Result of the unmount operation</returns>
    Task<UnmountResult> UnmountAsync(MountItem item);

    /// <summary>
    /// Initializes a semaphore for the given mount item to prevent concurrent operations.
    /// </summary>
    void InitializeMountLock(MountItem item);

    /// <summary>
    /// Removes and disposes the semaphore for the given mount item.
    /// </summary>
    void RemoveMountLock(MountItem item);
}

/// <summary>
/// Result of a mount operation.
/// </summary>
public class MountResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ContinuedInBackground { get; set; }
}

/// <summary>
/// Result of an unmount operation.
/// </summary>
public class UnmountResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ContinuedInBackground { get; set; }
}
