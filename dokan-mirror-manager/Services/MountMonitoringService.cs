using System.Collections.Concurrent;
using System.IO;
using DokanNet;
using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;

namespace DokanMirrorManager.Services;

/// <summary>
/// Service responsible for monitoring mounted file systems and detecting external unmounts.
/// Uses DokanInstance.WaitForFileSystemClosedAsync with polling fallback.
/// </summary>
public class MountMonitoringService : IMountMonitoringService
{
    private readonly ConcurrentDictionary<MountItem, CancellationTokenSource> _monitoringTokens = new();

    /// <inheritdoc />
    public async Task StartMonitoringAsync(
        DokanInstance instance,
        MountItem item,
        string driveLetter,
        Action<MountItem> onUnmountDetected)
    {
        // Cancel any previous monitoring for this item
        if (_monitoringTokens.TryRemove(item, out var oldCts))
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _monitoringTokens.TryAdd(item, cts);

        try
        {
            System.Diagnostics.Debug.WriteLine($"[Monitor] Starting monitoring for {driveLetter}");

            // Try to use DokanInstance's WaitForFileSystemClosedAsync
            bool usePolling = false;
            try
            {
                var waitTask = instance.WaitForFileSystemClosedAsync(uint.MaxValue);
                var completedTask = await Task.WhenAny(waitTask, Task.Delay(-1, cts.Token));

                if (completedTask == waitTask)
                {
                    await waitTask; // Propagate exceptions
                    System.Diagnostics.Debug.WriteLine($"[Monitor] File system closed normally for {driveLetter}");
                }
                else
                {
                    // Cancellation requested
                    System.Diagnostics.Debug.WriteLine($"[Monitor] Monitoring cancelled for {driveLetter}");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                // Monitoring was cancelled (unmount requested or item removed)
                System.Diagnostics.Debug.WriteLine($"[Monitor] Monitoring cancelled for {driveLetter}");
                return;
            }
            catch (Exception ex)
            {
                // WaitForFileSystemClosedAsync failed - switch to polling
                System.Diagnostics.Debug.WriteLine($"[Monitor] WaitForFileSystemClosedAsync failed: {ex.Message}, switching to polling");
                usePolling = true;
            }

            // If WaitForFileSystemClosedAsync failed or returned, use polling as fallback
            if (usePolling)
            {
                System.Diagnostics.Debug.WriteLine($"[Monitor] Starting polling monitor for {driveLetter}");

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(2000, cts.Token);

                        var driveExists = DriveInfo.GetDrives()
                            .Any(d => d.Name.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));

                        if (!driveExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Monitor] Drive {driveLetter} no longer exists (polling detected)");
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PollingMonitor] Error: {ex.Message}");
                        // Continue polling despite errors
                    }
                }
            }

            // Drive was unmounted - invoke callback
            onUnmountDetected(item);
        }
        finally
        {
            _monitoringTokens.TryRemove(item, out _);
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public void CancelMonitoring(MountItem item)
    {
        if (_monitoringTokens.TryRemove(item, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public void CancelAllMonitoring()
    {
        foreach (var kvp in _monitoringTokens)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _monitoringTokens.Clear();
    }
}
