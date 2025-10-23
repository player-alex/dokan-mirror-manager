using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using DokanMirrorManager.Utils;
using DokanNet;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DokanMirrorManager.Services;

/// <summary>
/// Service responsible for mounting and unmounting mirror drives.
/// Handles mount/unmount operations with timeout handling, background processing, and user feedback.
/// </summary>
public class MountService : IMountService
{
    private readonly Dokan _dokan;
    private readonly IMountMonitoringService _mountMonitoringService;
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<MountItem, SemaphoreSlim> _mountLocks = new();
    private Func<Window?> _getWindow = null!;
    private Action<string> _setStatusMessage = null!;
    private Func<Task> _saveConfiguration = null!;

    public MountService(IMountMonitoringService mountMonitoringService)
    {
        _dokan = new Dokan(null!); // Dokan library accepts null for default logger
        _mountMonitoringService = mountMonitoringService;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// Initializes the service with callbacks from the view model.
    /// Must be called before using MountAsync or UnmountAsync.
    /// </summary>
    public void Initialize(Func<Window?> getWindow, Action<string> setStatusMessage, Func<Task> saveConfiguration)
    {
        _getWindow = getWindow ?? throw new ArgumentNullException(nameof(getWindow));
        _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
        _saveConfiguration = saveConfiguration ?? throw new ArgumentNullException(nameof(saveConfiguration));
    }

    public async Task<MountResult> MountAsync(MountItem item, bool isAutoMount)
    {
        if (_getWindow == null || _setStatusMessage == null || _saveConfiguration == null)
        {
            throw new InvalidOperationException("MountService must be initialized with callbacks before use. Call Initialize() first.");
        }

        // Race Condition Prevention - Prevent concurrent mount operations on same item
        var semaphore = _mountLocks.GetOrAdd(item, _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(0)) // Non-blocking check
        {
            // Another mount operation is already in progress for this item
            return new MountResult { Success = false, ErrorMessage = "Mount operation already in progress" };
        }

        try
        {
            if ((item.Status != MountStatus.Unmounted && item.Status != MountStatus.Error) || string.IsNullOrEmpty(item.DestinationLetter))
            {
                return new MountResult { Success = false, ErrorMessage = "Item cannot be mounted" };
            }

            // Check if drive letter is already in use
            if (DriveInfo.GetDrives().Any(d => d.Name.Equals(item.DestinationLetter, StringComparison.OrdinalIgnoreCase)))
            {
                var errorMsg = $"Drive letter {item.DestinationLetter} is already in use.";
                if (!isAutoMount)
                {
                    MessageBox.Show(_getWindow(), errorMsg, "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    _setStatusMessage($"AutoMount failed: {errorMsg}");
                    item.Status = MountStatus.Error;
                    item.ErrorMessage = errorMsg;
                }
                return new MountResult { Success = false, ErrorMessage = errorMsg };
            }

            var driveLetter = item.DestinationLetter;
            item.Status = MountStatus.Mounting;
            _setStatusMessage($"Mounting {item.SourcePath} to {driveLetter}...");

            // Start timer for status updates
            var startTime = DateTime.Now;
            System.Threading.Timer? updateTimer = null;
            updateTimer = new System.Threading.Timer(_ =>
            {
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                _dispatcher.InvokeAsync(() =>
                {
                    _setStatusMessage($"Mounting {driveLetter}... ({elapsed:F0}s)");
                });
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            try
            {
                // Create mount task - only performs the mount, doesn't wait for unmount
                var mountTask = Task.Run(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mount] Starting mount for {item.SourcePath} to {driveLetter}");

                        // Verify source path exists
                        if (!Directory.Exists(item.SourcePath))
                        {
                            throw new DirectoryNotFoundException($"Source path does not exist: {item.SourcePath}");
                        }

                        System.Diagnostics.Debug.WriteLine("[Mount] Creating Mirror instance");
                        var mirror = new Mirror(null, item.SourcePath);

                        // Update UI-bound properties on dispatcher thread
                        _dispatcher.InvokeAsync(() => item.Mirror = mirror);

                        System.Diagnostics.Debug.WriteLine("[Mount] Building DokanInstance");
                        var builder = new DokanInstanceBuilder(_dokan)
                            .ConfigureOptions(options =>
                            {
                                // Set base options
                                var baseOptions = DokanOptions.EnableNotificationAPI;
#if DEBUG
                                baseOptions |= DokanOptions.DebugMode;
#endif
                                // Add WriteProtection if read-only
                                if (item.IsReadOnly)
                                {
                                    baseOptions |= DokanOptions.WriteProtection;
                                }

                                options.Options = baseOptions;
                                options.MountPoint = driveLetter;
                            });

                        System.Diagnostics.Debug.WriteLine("[Mount] Calling Build()");
                        var instance = builder.Build(mirror);

                        // Update UI-bound properties on dispatcher thread
                        _dispatcher.InvokeAsync(() =>
                        {
                            item.DokanInstance = instance;
                            // Update status immediately - mount is now complete
                            item.Status = MountStatus.Mounted;
                        });

                        System.Diagnostics.Debug.WriteLine("[Mount] Build() completed successfully");

                        // Return the instance for background monitoring
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Mount] Exception: {ex.GetType().Name} - {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[Mount] StackTrace: {ex.StackTrace}");

                        // Log to a file as well
                        try
                        {
                            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mount_error.log");
                            File.AppendAllText(logPath, $"[{DateTime.Now}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n");
                        }
                        catch { }

                        // Re-throw to be handled by outer try-catch
                        throw;
                    }
                });

                // Wait with timeout (10 seconds initial timeout)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(mountTask, timeoutTask);

                if (completedTask == mountTask)
                {
                    // Mount completed successfully
                    var instance = await mountTask; // Propagate any exceptions
                    updateTimer?.Dispose();
                    _setStatusMessage($"Mounted {item.SourcePath} to {driveLetter}");
                    await _saveConfiguration();

                    // Start background monitoring with callback
                    var onUnmountDetected = CreateUnmountCallback(item);
                    _ = Task.Run(() => _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, onUnmountDetected));

                    return new MountResult { Success = true };
                }
                else
                {
                    // Timeout occurred
                    updateTimer?.Dispose();
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;

                    // For AutoMount, automatically continue in background without user interaction
                    MessageBoxResult result;
                    if (isAutoMount)
                    {
                        result = MessageBoxResult.No; // Automatically continue in background
                        _setStatusMessage($"AutoMount: {driveLetter} taking longer than expected, continuing in background...");
                    }
                    else
                    {
                        // Ask user for manual mounts
                        result = MessageBox.Show(
                            _getWindow(),
                            $"Mounting {driveLetter} is taking longer than expected ({elapsed:F0} seconds).\n\n" +
                            "This usually happens when:\n" +
                            "  • The source path is on a slow network drive\n" +
                            "  • The system is under heavy load\n" +
                            "  • Dokan driver is initializing\n\n" +
                            "Do you want to continue waiting?",
                            "Mount Timeout",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Warning);
                    }

                    if (result == MessageBoxResult.Yes)
                    {
                        // Continue waiting (extend timeout to 30 more seconds)
                        _setStatusMessage($"Still mounting {driveLetter}...");
                        updateTimer = new System.Threading.Timer(_ =>
                        {
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            _dispatcher.InvokeAsync(() =>
                            {
                                _setStatusMessage($"Mounting {driveLetter}... ({elapsed:F0}s)");
                            });
                        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                        var extendedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                        var extendedCompletedTask = await Task.WhenAny(mountTask, extendedTimeoutTask);

                        updateTimer?.Dispose();

                        if (extendedCompletedTask == mountTask)
                        {
                            var instance = await mountTask;
                            _setStatusMessage($"Mounted {item.SourcePath} to {driveLetter}");
                            await _saveConfiguration();

                            // Start background monitoring
                            var onUnmountDetected = CreateUnmountCallback(item);
                            _ = Task.Run(() => _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, onUnmountDetected));

                            return new MountResult { Success = true };
                        }
                        else
                        {
                            // Still timed out after extended wait - continue in background
                            return await HandleBackgroundMount(item, driveLetter, mountTask, isAutoMount, startTime);
                        }
                    }
                    else
                    {
                        // User chose No or Cancel - continue in background
                        return await HandleBackgroundMount(item, driveLetter, mountTask, isAutoMount, startTime, result == MessageBoxResult.No);
                    }
                }
            }
            catch (Exception ex)
            {
                updateTimer?.Dispose();
                item.Status = MountStatus.Error;
                item.ErrorMessage = ex.Message;
                _setStatusMessage($"Failed to mount: {ex.Message}");

                if (!isAutoMount)
                {
                    MessageBox.Show(
                        _getWindow(),
                        $"Failed to mount {item.SourcePath}:\n\n{ex.Message}",
                        "Mount Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return new MountResult { Success = false, ErrorMessage = ex.Message };
            }
            finally
            {
                updateTimer?.Dispose();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private Task<MountResult> HandleBackgroundMount(MountItem item, string driveLetter, Task<DokanInstance> mountTask, bool isAutoMount, DateTime startTime, bool showContinueMessage = true)
    {
        _setStatusMessage($"Mount of {driveLetter} will continue in background...");

        if (!isAutoMount && showContinueMessage)
        {
            if ((DateTime.Now - startTime).TotalSeconds > 30)
            {
                MessageBox.Show(
                    _getWindow(),
                    $"Mount is still in progress after {(DateTime.Now - startTime).TotalSeconds:F0} seconds.\n\n" +
                    "The mount will continue in the background.\n" +
                    "Please wait for completion.",
                    "Mount Taking Long Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    _getWindow(),
                    "Note: The mount operation cannot be cancelled once started.\n" +
                    "It will continue in the background.\n\n" +
                    "The drive will be mounted when the operation completes.",
                    "Mount Continuing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Continue waiting in background
        _ = Task.Run(async () =>
        {
            try
            {
                var instance = await mountTask;
                await _dispatcher.InvokeAsync(async () =>
                {
                    _setStatusMessage($"Mounted {item.SourcePath} to {driveLetter} (completed in background)");
                    await _saveConfiguration();
                });

                // Start background monitoring
                var onUnmountDetected = CreateUnmountCallback(item);
                await _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, onUnmountDetected);
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    item.Status = MountStatus.Error;
                    item.ErrorMessage = ex.Message;
                    _setStatusMessage($"Background mount failed: {ex.Message}");
                    if (!isAutoMount)
                    {
                        MessageBox.Show(_getWindow(), $"Failed to mount {item.SourcePath}:\n\n{ex.GetType().Name}: {ex.Message}\n\nSee mount_error.log for details",
                            "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        });

        return Task.FromResult(new MountResult { Success = true, ContinuedInBackground = true });
    }

    public async Task<UnmountResult> UnmountAsync(MountItem item)
    {
        if (_getWindow == null || _setStatusMessage == null || _saveConfiguration == null)
        {
            throw new InvalidOperationException("MountService must be initialized with callbacks before use. Call Initialize() first.");
        }

        if (item.Status != MountStatus.Mounted)
        {
            return new UnmountResult { Success = false, ErrorMessage = "Item is not mounted" };
        }

        // Cancel monitoring task
        _mountMonitoringService.CancelMonitoring(item);

        var driveLetter = item.DestinationLetter;
        _setStatusMessage($"Unmounting {driveLetter}...");

        // Start timer for status updates
        var startTime = DateTime.Now;
        System.Threading.Timer? updateTimer = null;
        updateTimer = new System.Threading.Timer(_ =>
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            _dispatcher.InvokeAsync(() =>
            {
                _setStatusMessage($"Unmounting {driveLetter}... ({elapsed:F0}s)");
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        try
        {
            // Create unmount task
            var unmountTask = Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(item.DestinationLetter))
                {
                    _dokan.RemoveMountPoint(item.DestinationLetter);
                }

                if (item.DokanInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            });

            // Wait with timeout (10 seconds initial timeout)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(unmountTask, timeoutTask);

            if (completedTask == unmountTask)
            {
                // Unmount completed successfully
                await unmountTask; // Propagate any exceptions
                updateTimer?.Dispose();

                // Set null on UI thread after unmount completes
                item.DokanInstance = null;
                item.Mirror = null;
                item.Status = MountStatus.Unmounted;
                item.ErrorMessage = string.Empty;
                _setStatusMessage($"Unmounted {driveLetter}");
                await _saveConfiguration();

                return new UnmountResult { Success = true };
            }
            else
            {
                // Timeout occurred - check for processes and ask user
                updateTimer?.Dispose();
                var elapsed = (DateTime.Now - startTime).TotalSeconds;

                // Check for processes using the drive (only when timeout occurs)
                var processCheckMessage = "";
                try
                {
                    var processesUsingDrive = await Task.Run(() =>
                        DriveHandleDetector.GetProcessesUsingDrive(driveLetter));

                    if (processesUsingDrive.Any())
                    {
                        var processNames = string.Join("\n  • ", processesUsingDrive.Take(5));
                        if (processesUsingDrive.Count > 5)
                        {
                            processNames += $"\n  • ... and {processesUsingDrive.Count - 5} more";
                        }
                        processCheckMessage = $"\n\nProcesses using this drive:\n  • {processNames}\n";
                    }
                }
                catch
                {
                    // Ignore errors during process detection
                }

                var result = MessageBox.Show(
                    _getWindow(),
                    $"Unmounting {driveLetter} is taking longer than expected ({elapsed:F0} seconds).\n\n" +
                    "This usually happens when:\n" +
                    "  • Another process is using files on the drive\n" +
                    "  • Windows Explorer is browsing the drive\n" +
                    "  • A background service is accessing files" +
                    processCheckMessage + "\n\n" +
                    "Do you want to continue waiting?",
                    "Unmount Timeout",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Continue waiting (extend timeout to 30 more seconds)
                    _setStatusMessage($"Still unmounting {driveLetter}...");
                    updateTimer = new System.Threading.Timer(_ =>
                    {
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        _dispatcher.InvokeAsync(() =>
                        {
                            _setStatusMessage($"Unmounting {driveLetter}... ({elapsed:F0}s)");
                        });
                    }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                    var extendedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                    var extendedCompletedTask = await Task.WhenAny(unmountTask, extendedTimeoutTask);

                    updateTimer?.Dispose();

                    if (extendedCompletedTask == unmountTask)
                    {
                        await unmountTask;

                        // Set null on UI thread after unmount completes
                        item.DokanInstance = null;
                        item.Mirror = null;
                        item.Status = MountStatus.Unmounted;
                        item.ErrorMessage = string.Empty;
                        _setStatusMessage($"Unmounted {driveLetter}");
                        await _saveConfiguration();

                        return new UnmountResult { Success = true };
                    }
                    else
                    {
                        // Still timed out after extended wait - continue in background
                        return await HandleBackgroundUnmount(item, driveLetter, unmountTask, startTime);
                    }
                }
                else
                {
                    // User chose No or Cancel - continue in background
                    return await HandleBackgroundUnmount(item, driveLetter, unmountTask, startTime, result == MessageBoxResult.No);
                }
            }
        }
        catch (Exception ex)
        {
            updateTimer?.Dispose();
            _setStatusMessage($"Failed to unmount: {ex.Message}");
            item.ErrorMessage = ex.Message;
            MessageBox.Show(
                _getWindow(),
                $"Failed to unmount {driveLetter}:\n\n{ex.Message}",
                "Unmount Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return new UnmountResult { Success = false, ErrorMessage = ex.Message };
        }
        finally
        {
            updateTimer?.Dispose();
        }
    }

    private Task<UnmountResult> HandleBackgroundUnmount(MountItem item, string driveLetter, Task unmountTask, DateTime startTime, bool showContinueMessage = true)
    {
        _setStatusMessage($"Unmount of {driveLetter} will continue in background...");

        if (showContinueMessage)
        {
            if ((DateTime.Now - startTime).TotalSeconds > 30)
            {
                MessageBox.Show(
                    _getWindow(),
                    $"Unmount is still in progress after {(DateTime.Now - startTime).TotalSeconds:F0} seconds.\n\n" +
                    "The unmount will continue in the background.\n" +
                    "Please close any programs using the drive and wait for completion.",
                    "Unmount Taking Long Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    _getWindow(),
                    "Note: The unmount operation cannot be cancelled once started.\n" +
                    "It will continue in the background.\n\n" +
                    "The drive will be unmounted when all file handles are released.",
                    "Unmount Continuing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // Continue waiting in background
        _ = Task.Run(async () =>
        {
            try
            {
                await unmountTask;
                await _dispatcher.InvokeAsync(async () =>
                {
                    // Set null on UI thread after unmount completes
                    item.DokanInstance = null;
                    item.Mirror = null;
                    item.Status = MountStatus.Unmounted;
                    item.ErrorMessage = string.Empty;
                    _setStatusMessage($"Unmounted {driveLetter} (completed in background)");
                    await _saveConfiguration();
                });
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    _setStatusMessage($"Background unmount failed: {ex.Message}");
                    item.ErrorMessage = ex.Message;
                });
            }
        });

        return Task.FromResult(new UnmountResult { Success = true, ContinuedInBackground = true });
    }

    private Action<MountItem> CreateUnmountCallback(MountItem item)
    {
        return (mountItem) =>
        {
            _dispatcher.InvokeAsync(async () =>
            {
                if (mountItem.Status == MountStatus.Mounted)
                {
                    mountItem.Status = MountStatus.Unmounted;
                    mountItem.DokanInstance = null;
                    mountItem.Mirror = null;
                    _setStatusMessage($"{mountItem.DestinationLetter} was unmounted externally");
                    await _saveConfiguration();
                }
            });
        };
    }

    /// <summary>
    /// Initializes a semaphore for the given mount item to prevent concurrent operations.
    /// </summary>
    public void InitializeMountLock(MountItem item)
    {
        _mountLocks.TryAdd(item, new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Removes and disposes the semaphore for the given mount item.
    /// </summary>
    public void RemoveMountLock(MountItem item)
    {
        if (_mountLocks.TryRemove(item, out var semaphore))
        {
            semaphore?.Dispose();
        }
    }

    public void Dispose()
    {
        _dokan?.Dispose();

        // Dispose all semaphores
        foreach (var semaphore in _mountLocks.Values)
        {
            semaphore?.Dispose();
        }
        _mountLocks.Clear();
    }
}
