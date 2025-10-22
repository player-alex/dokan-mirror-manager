using Caliburn.Micro;
using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using DokanMirrorManager.Utils;
using DokanNet;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DokanMirrorManager.ViewModels;

public class ShellViewModel : Screen
{
    private readonly IWindowManager _windowManager;
    private readonly IConfigurationService _configurationService;
    private readonly IDriveLetterManager _driveLetterManager;
    private readonly ITrayIconManager _trayIconManager;
    private readonly IMountMonitoringService _mountMonitoringService;
    private readonly Dokan _dokan;
    private string _statusMessage = string.Empty;
    private MountItem? _selectedItem;
    private const int WM_SHOWWINDOW_CUSTOM = 0x8001;
    private HwndSource? _hwndSource;

    // Critical Fix #1: Race Condition - Add locking mechanism for concurrent mount operations
    private readonly ConcurrentDictionary<MountItem, SemaphoreSlim> _mountLocks = new();
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<MountItem> MountItems { get; } = [];

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            NotifyOfPropertyChange(() => StatusMessage);
        }
    }

    public MountItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            // Unsubscribe from old item
            if (_selectedItem != null)
            {
                _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
            }

            _selectedItem = value;

            // Subscribe to new item
            if (_selectedItem != null)
            {
                _selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
            }

            NotifyOfPropertyChange(() => SelectedItem);
            NotifyOfPropertyChange(() => CanRemoveMount);
        }
    }

    public bool CanRemoveMount => SelectedItem != null && SelectedItem.Status != MountStatus.Mounted && SelectedItem.Status != MountStatus.Mounting;

    public List<string> AvailableDriveLetters => _driveLetterManager.GetAvailableDriveLetters(MountItems, null);

    public ShellViewModel(IWindowManager windowManager, IConfigurationService configurationService, IDriveLetterManager driveLetterManager, ITrayIconManager trayIconManager, IMountMonitoringService mountMonitoringService)
    {
        _windowManager = windowManager;
        _configurationService = configurationService;
        _driveLetterManager = driveLetterManager;
        _trayIconManager = trayIconManager;
        _mountMonitoringService = mountMonitoringService;
        _dokan = new Dokan(null!); // Dokan library accepts null for default logger
        DisplayName = "Dokan Mirror Manager";

        // Critical Fix #4: Capture dispatcher early to avoid null reference
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    protected override async void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        StatusMessage = "Ready";

        if (view is Window window)
        {
            _trayIconManager.Initialize(window, ShowWindow, ExitApplicationAsync, msg => StatusMessage = msg);
        }
        SetupWindowMessageHook(view);

        // Load configuration and auto-mount asynchronously to avoid blocking UI thread
        await LoadConfigurationAsync();
    }

    private void SetupWindowMessageHook(object view)
    {
        if (view is Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SHOWWINDOW_CUSTOM)
        {
            // Another instance is trying to start - show this window
            _trayIconManager.ShowWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ShowWindow()
    {
        _trayIconManager.ShowWindow();
    }

    private async Task ExitApplicationAsync()
    {
        var mountedItems = MountItems.Where(m => m.Status == MountStatus.Mounted).ToList();

        if (mountedItems.Any())
        {
            var result = MessageBox.Show(
                GetView() as Window,
                $"There are {mountedItems.Count} mounted drive(s). Do you want to exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return;
            }

            // Unmount all drives before exiting
            foreach (var item in mountedItems)
            {
                await Unmount(item);
            }
        }

        _trayIconManager?.Dispose();
        _dokan?.Dispose();
        Application.Current.Shutdown();
    }

    public void AddMount()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Source Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var sourcePath = dialog.FolderName;

            if (!Directory.Exists(sourcePath))
            {
                MessageBox.Show("Selected directory does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var mountItem = new MountItem
            {
                SourcePath = sourcePath,
                DestinationLetter = string.Empty,
                IsReadOnly = true,
                Status = MountStatus.Unmounted
            };

            mountItem.PropertyChanged += MountItem_PropertyChanged;
            MountItems.Add(mountItem);

            // Critical Fix #1: Initialize semaphore for this mount item
            _mountLocks.TryAdd(mountItem, new SemaphoreSlim(1, 1));

            _driveLetterManager.UpdateAllDriveLetters(MountItems);

            SaveConfigurationAsync();
            StatusMessage = $"Added: {sourcePath}";
        }
    }

    public void RemoveMount()
    {
        if (SelectedItem != null && (SelectedItem.Status == MountStatus.Unmounted || SelectedItem.Status == MountStatus.Error))
        {
            var itemToRemove = SelectedItem;
            var indexToRemove = MountItems.IndexOf(SelectedItem);

            SelectedItem.PropertyChanged -= MountItem_PropertyChanged;
            MountItems.Remove(SelectedItem);

            // Critical Fix #1: Clean up semaphore when removing item
            if (_mountLocks.TryRemove(itemToRemove, out var semaphore))
            {
                semaphore?.Dispose();
            }

            // Major Fix #11: Cancel and clean up monitoring task
            _mountMonitoringService.CancelMonitoring(itemToRemove);

            // Select next item if available
            if (MountItems.Count > 0)
            {
                if (indexToRemove < MountItems.Count)
                {
                    SelectedItem = MountItems[indexToRemove];
                }
                else if (indexToRemove > 0)
                {
                    SelectedItem = MountItems[indexToRemove - 1];
                }
            }

            _driveLetterManager.UpdateAllDriveLetters(MountItems);
            SaveConfigurationAsync();
            StatusMessage = $"Removed: {itemToRemove.SourcePath}";
        }
    }

    public async Task Mount(MountItem item)
    {
        await MountInternal(item, isAutoMount: false);
    }

    private async Task MountInternal(MountItem item, bool isAutoMount)
    {
        // Critical Fix #1: Race Condition - Prevent concurrent mount operations on same item
        var semaphore = _mountLocks.GetOrAdd(item, _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(0)) // Non-blocking check
        {
            // Another mount operation is already in progress for this item
            return;
        }

        try
        {
            if ((item.Status != MountStatus.Unmounted && item.Status != MountStatus.Error) || string.IsNullOrEmpty(item.DestinationLetter))
                return;

            // Check if drive letter is already in use
            if (DriveInfo.GetDrives().Any(d => d.Name.Equals(item.DestinationLetter, StringComparison.OrdinalIgnoreCase)))
            {
                var errorMsg = $"Drive letter {item.DestinationLetter} is already in use.";
                if (!isAutoMount)
                {
                    MessageBox.Show(errorMsg, "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    StatusMessage = $"AutoMount failed: {errorMsg}";
                    item.Status = MountStatus.Error;
                    item.ErrorMessage = errorMsg;
                }
                return;
            }

            // Check if another mount item is using this drive letter
            if (MountItems.Any(m => m != item && m.Status == MountStatus.Mounted && m.DestinationLetter == item.DestinationLetter))
            {
                var errorMsg = $"Drive letter {item.DestinationLetter} is already mounted by another item.";
                if (!isAutoMount)
                {
                    MessageBox.Show(errorMsg, "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    StatusMessage = $"AutoMount failed: {errorMsg}";
                    item.Status = MountStatus.Error;
                    item.ErrorMessage = errorMsg;
                }
                return;
            }

            var driveLetter = item.DestinationLetter;
            item.Status = MountStatus.Mounting;
            StatusMessage = $"Mounting {item.SourcePath} to {driveLetter}...";

            // Critical Fix #3 & #4: Replace DispatcherTimer with System.Threading.Timer to avoid memory leak and dispatcher affinity issues
            var startTime = DateTime.Now;
            System.Threading.Timer? updateTimer = null;
            updateTimer = new System.Threading.Timer(_ =>
            {
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                // Critical Fix #2: Update UI properties on dispatcher thread
                _dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"Mounting {driveLetter}... ({elapsed:F0}s)";
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

                        // Critical Fix #2: Update UI-bound properties on dispatcher thread
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

                        // Critical Fix #2: Update UI-bound properties on dispatcher thread
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
                    // Critical Fix #3: Dispose timer properly
                    updateTimer?.Dispose();
                    StatusMessage = $"Mounted {item.SourcePath} to {driveLetter}";
                    SaveConfigurationAsync();

                // Major Fix #11: Start background monitoring with CancellationToken support
                _ = Task.Run(() => _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, OnUnmountDetected));
            }
                else
                {
                    // Timeout occurred
                    // Critical Fix #3: Dispose timer properly
                    updateTimer?.Dispose();
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;

                // For AutoMount, automatically continue in background without user interaction
                MessageBoxResult result;
                if (isAutoMount)
                {
                    result = MessageBoxResult.No; // Automatically continue in background
                    StatusMessage = $"AutoMount: {driveLetter} taking longer than expected, continuing in background...";
                }
                else
                {
                    // Ask user for manual mounts
                    result = MessageBox.Show(
                        GetView() as Window,
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
                        StatusMessage = $"Still mounting {driveLetter}...";
                        // Critical Fix #3 & #4: Create new timer for extended timeout
                        updateTimer = new System.Threading.Timer(_ =>
                        {
                            var elapsed = (DateTime.Now - startTime).TotalSeconds;
                            _dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"Mounting {driveLetter}... ({elapsed:F0}s)";
                            });
                        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                        var extendedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                        var extendedCompletedTask = await Task.WhenAny(mountTask, extendedTimeoutTask);

                        // Critical Fix #3: Dispose timer properly
                        updateTimer?.Dispose();

                    if (extendedCompletedTask == mountTask)
                    {
                        var instance = await mountTask;
                        StatusMessage = $"Mounted {item.SourcePath} to {driveLetter}";
                        SaveConfigurationAsync();

                        // Major Fix #11: Start background monitoring
                        _ = Task.Run(() => _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, OnUnmountDetected));
                    }
                    else
                    {
                        // Still timed out after extended wait
                        StatusMessage = $"Mount of {driveLetter} is still in progress...";
                        // Major Fix #9: Don't show MessageBox for AutoMount
                        if (!isAutoMount)
                        {
                            MessageBox.Show(
                                GetView() as Window,
                                $"Mount is still in progress after {(DateTime.Now - startTime).TotalSeconds:F0} seconds.\n\n" +
                                "The mount will continue in the background.\n" +
                                "Please wait for completion.",
                                "Mount Taking Long Time",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }

                        // Continue waiting in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var instance = await mountTask;
                                // Major Fix #8: Use captured dispatcher
                                await _dispatcher.InvokeAsync(() =>
                                {
                                    StatusMessage = $"Mounted {item.SourcePath} to {driveLetter} (completed in background)";
                                    SaveConfigurationAsync();
                                });

                                // Major Fix #11: Start background monitoring
                                await _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, OnUnmountDetected);
                            }
                            catch (Exception ex)
                            {
                                // Major Fix #8: Use captured dispatcher
                                await _dispatcher.InvokeAsync(() =>
                                {
                                    item.Status = MountStatus.Error;
                                    item.ErrorMessage = ex.Message;
                                    StatusMessage = $"Background mount failed: {ex.Message}";
                                    // Major Fix #9: Don't show MessageBox for AutoMount
                                    if (!isAutoMount)
                                    {
                                        MessageBox.Show($"Failed to mount {item.SourcePath}:\n\n{ex.GetType().Name}: {ex.Message}\n\nSee mount_error.log for details",
                                            "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                });
                            }
                        });
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    // User chose to cancel - but we can't really cancel the mount once started
                    StatusMessage = $"Mount of {driveLetter} will continue in background...";
                    MessageBox.Show(
                        GetView() as Window,
                        "Note: The mount operation cannot be cancelled once started.\n" +
                        "It will continue in the background.\n\n" +
                        "The drive will be mounted when the operation completes.",
                        "Mount Continuing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Continue in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var instance = await mountTask;
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"Mounted {item.SourcePath} to {driveLetter} (completed in background)";
                                SaveConfigurationAsync();
                            });

                            // Major Fix #11: Start background monitoring
                            await _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, OnUnmountDetected);
                        }
                        catch (Exception ex)
                        {
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                item.Status = MountStatus.Error;
                                item.ErrorMessage = ex.Message;
                                StatusMessage = $"Background mount failed: {ex.Message}";
                                MessageBox.Show($"Failed to mount {item.SourcePath}:\n\n{ex.GetType().Name}: {ex.Message}\n\nSee mount_error.log for details",
                                    "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    });
                }
                else // Cancel
                {
                    // Same as No - continue in background
                    StatusMessage = $"Mount of {driveLetter} continuing in background...";

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var instance = await mountTask;
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"Mounted {item.SourcePath} to {driveLetter}";
                                SaveConfigurationAsync();
                            });

                            // Major Fix #11: Start background monitoring
                            await _mountMonitoringService.StartMonitoringAsync(instance, item, driveLetter, OnUnmountDetected);
                        }
                        catch (Exception ex)
                        {
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                item.Status = MountStatus.Error;
                                item.ErrorMessage = ex.Message;
                                StatusMessage = $"Background mount failed: {ex.Message}";
                                MessageBox.Show($"Failed to mount {item.SourcePath}:\n\n{ex.GetType().Name}: {ex.Message}\n\nSee mount_error.log for details",
                                    "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    });
                }
            }
            }
            catch (Exception ex)
            {
                // Critical Fix #3: Dispose timer properly in catch block
                updateTimer?.Dispose();
                item.Status = MountStatus.Error;
                item.ErrorMessage = ex.Message;
                StatusMessage = $"Failed to mount: {ex.Message}";

                if (!isAutoMount)
                {
                    MessageBox.Show(
                        GetView() as Window,
                        $"Failed to mount {item.SourcePath}:\n\n{ex.Message}",
                        "Mount Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            finally
            {
                // Critical Fix #3: Ensure timer is disposed in all code paths
                updateTimer?.Dispose();
            }
        }
        finally
        {
            // Critical Fix #1: Always release the semaphore
            semaphore.Release();
        }
    }

    // Major Fix #11: Callback invoked when external unmount is detected
    private void OnUnmountDetected(MountItem item)
    {
        _dispatcher.InvokeAsync(() =>
        {
            if (item.Status == MountStatus.Mounted)
            {
                item.Status = MountStatus.Unmounted;
                item.DokanInstance = null;
                item.Mirror = null;
                StatusMessage = $"{item.DestinationLetter} was unmounted externally";
                SaveConfigurationAsync();
            }
        });
    }

    public async Task Unmount(MountItem item)
    {
        if (item.Status != MountStatus.Mounted)
            return;

        // Major Fix #11: Cancel monitoring task
        _mountMonitoringService.CancelMonitoring(item);

        var driveLetter = item.DestinationLetter;
        StatusMessage = $"Unmounting {driveLetter}...";

        // Critical Fix #3 & #4: Replace DispatcherTimer with System.Threading.Timer
        var startTime = DateTime.Now;
        System.Threading.Timer? updateTimer = null;
        updateTimer = new System.Threading.Timer(_ =>
        {
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            // Critical Fix #2: Update UI properties on dispatcher thread
            _dispatcher.InvokeAsync(() =>
            {
                StatusMessage = $"Unmounting {driveLetter}... ({elapsed:F0}s)";
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
                // Major Fix #7: Don't set null here - will be set after task completion
            });

            // Wait with timeout (10 seconds initial timeout)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(unmountTask, timeoutTask);

            if (completedTask == unmountTask)
            {
                // Unmount completed successfully
                await unmountTask; // Propagate any exceptions
                // Critical Fix #3: Dispose timer properly
                updateTimer?.Dispose();
                // Major Fix #7: Set null on UI thread after unmount completes
                item.DokanInstance = null;
                item.Mirror = null;
                item.Status = MountStatus.Unmounted;
                item.ErrorMessage = string.Empty;
                StatusMessage = $"Unmounted {driveLetter}";
                SaveConfigurationAsync();
            }
            else
            {
                // Timeout occurred - ask user
                // Critical Fix #3: Dispose timer properly
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
                    GetView() as Window,
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
                    StatusMessage = $"Still unmounting {driveLetter}...";
                    // Critical Fix #3 & #4: Create new timer for extended timeout
                    updateTimer = new System.Threading.Timer(_ =>
                    {
                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                        _dispatcher.InvokeAsync(() =>
                        {
                            StatusMessage = $"Unmounting {driveLetter}... ({elapsed:F0}s)";
                        });
                    }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                    var extendedTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                    var extendedCompletedTask = await Task.WhenAny(unmountTask, extendedTimeoutTask);

                    // Critical Fix #3: Dispose timer properly
                    updateTimer?.Dispose();

                    if (extendedCompletedTask == unmountTask)
                    {
                        await unmountTask;
                        // Major Fix #7: Set null on UI thread after unmount completes
                        item.DokanInstance = null;
                        item.Mirror = null;
                        item.Status = MountStatus.Unmounted;
                        item.ErrorMessage = string.Empty;
                        StatusMessage = $"Unmounted {driveLetter}";
                        SaveConfigurationAsync();
                    }
                    else
                    {
                        // Still timed out after extended wait
                        StatusMessage = $"Unmount of {driveLetter} is still in progress...";
                        MessageBox.Show(
                            GetView() as Window,
                            $"Unmount is still in progress after {(DateTime.Now - startTime).TotalSeconds:F0} seconds.\n\n" +
                            "The unmount will continue in the background.\n" +
                            "Please close any programs using the drive and wait for completion.",
                            "Unmount Taking Long Time",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Continue waiting in background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await unmountTask;
                                // Major Fix #8: Use captured dispatcher
                                await _dispatcher.InvokeAsync(() =>
                                {
                                    // Major Fix #7: Set null on UI thread after unmount completes
                                    item.DokanInstance = null;
                                    item.Mirror = null;
                                    item.Status = MountStatus.Unmounted;
                                    item.ErrorMessage = string.Empty;
                                    StatusMessage = $"Unmounted {driveLetter} (completed in background)";
                                    SaveConfigurationAsync();
                                });
                            }
                            catch (Exception ex)
                            {
                                // Major Fix #8: Use captured dispatcher
                                await _dispatcher.InvokeAsync(() =>
                                {
                                    StatusMessage = $"Background unmount failed: {ex.Message}";
                                    item.ErrorMessage = ex.Message;
                                });
                            }
                        });
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    // User chose to cancel - but we can't really cancel the unmount once started
                    StatusMessage = $"Unmount of {driveLetter} will continue in background...";
                    MessageBox.Show(
                        GetView() as Window,
                        "Note: The unmount operation cannot be cancelled once started.\n" +
                        "It will continue in the background.\n\n" +
                        "The drive will be unmounted when all file handles are released.",
                        "Unmount Continuing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Continue in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await unmountTask;
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                // Major Fix #7: Set null on UI thread after unmount completes
                                item.DokanInstance = null;
                                item.Mirror = null;
                                item.Status = MountStatus.Unmounted;
                                item.ErrorMessage = string.Empty;
                                StatusMessage = $"Unmounted {driveLetter} (completed in background)";
                                SaveConfigurationAsync();
                            });
                        }
                        catch (Exception ex)
                        {
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"Background unmount failed: {ex.Message}";
                                item.ErrorMessage = ex.Message;
                            });
                        }
                    });
                }
                else // Cancel
                {
                    // Same as No - continue in background
                    StatusMessage = $"Unmount of {driveLetter} continuing in background...";

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await unmountTask;
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                // Major Fix #7: Set null on UI thread after unmount completes
                                item.DokanInstance = null;
                                item.Mirror = null;
                                item.Status = MountStatus.Unmounted;
                                item.ErrorMessage = string.Empty;
                                StatusMessage = $"Unmounted {driveLetter}";
                                SaveConfigurationAsync();
                            });
                        }
                        catch (Exception ex)
                        {
                            // Major Fix #8: Use captured dispatcher
                            await _dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"Background unmount failed: {ex.Message}";
                                item.ErrorMessage = ex.Message;
                            });
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Critical Fix #3: Dispose timer properly in catch block
            updateTimer?.Dispose();
            StatusMessage = $"Failed to unmount: {ex.Message}";
            item.ErrorMessage = ex.Message;
            MessageBox.Show(
                GetView() as Window,
                $"Failed to unmount {driveLetter}:\n\n{ex.Message}",
                "Unmount Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            // Critical Fix #3: Ensure timer is disposed in all code paths
            updateTimer?.Dispose();
        }
    }

    private void SelectedItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update CanRemoveMount when selected item's status changes
        if (e.PropertyName == nameof(MountItem.Status))
        {
            NotifyOfPropertyChange(() => CanRemoveMount);
        }
    }

    private void MountItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update available drive letters for all items when mount status or destination changes
        if (e.PropertyName == nameof(MountItem.Status) || e.PropertyName == nameof(MountItem.DestinationLetter))
        {
            _driveLetterManager.UpdateAllDriveLetters(MountItems);
        }

        // Save configuration when AutoMount, IsReadOnly, or DestinationLetter changes
        if (e.PropertyName == nameof(MountItem.AutoMount) ||
            e.PropertyName == nameof(MountItem.IsReadOnly) ||
            e.PropertyName == nameof(MountItem.DestinationLetter))
        {
            SaveConfigurationAsync();
        }
    }


    private async Task LoadConfigurationAsync()
    {
        try
        {
            var items = await _configurationService.LoadConfigurationAsync();

            foreach (var mountItem in items)
            {
                mountItem.PropertyChanged += MountItem_PropertyChanged;
                MountItems.Add(mountItem);

                // Critical Fix #1: Initialize semaphore for this mount item
                _mountLocks.TryAdd(mountItem, new SemaphoreSlim(1, 1));
            }

            _driveLetterManager.UpdateAllDriveLetters(MountItems);

            // Auto-select drive letters for all loaded items
            // Track used drive letters to handle duplicates in mounts.json
            var usedDriveLetters = new HashSet<string>();

            foreach (var item in MountItems)
            {
                // If this item's drive letter is already used by a previous item in the load, clear it
                if (!string.IsNullOrEmpty(item.DestinationLetter) && usedDriveLetters.Contains(item.DestinationLetter))
                {
                    item.DestinationLetter = string.Empty;
                }

                var availableLetters = _driveLetterManager.GetAvailableDriveLetters(MountItems, item);
                var selectedLetter = _driveLetterManager.AutoSelectDriveLetter(item, availableLetters);
                if (selectedLetter != null)
                {
                    item.DestinationLetter = selectedLetter;
                }

                // Track this item's selected drive letter
                if (!string.IsNullOrEmpty(item.DestinationLetter))
                {
                    usedDriveLetters.Add(item.DestinationLetter);
                }

                // Update available drive letters after each assignment to prevent duplicates
                _driveLetterManager.UpdateAllDriveLetters(MountItems);
            }

            // Auto-mount items that have AutoMount enabled (sequentially to avoid conflicts)
            Task.Run(async () =>
            {
                foreach (var item in MountItems.Where(m => m.AutoMount && m.CanMount).ToList())
                {
                    // Major Fix #8: Use captured dispatcher
                    await _dispatcher.InvokeAsync(async () =>
                    {
                        await MountInternal(item, isAutoMount: true);
                    });

                    // Wait for mount to complete before starting next one
                    var timeout = 0;
                    while (item.Status == MountStatus.Mounting && timeout < 100)
                    {
                        await Task.Delay(100);
                        timeout++;
                    }

                    // Small delay between mounts to ensure Dokan is ready
                    await Task.Delay(500);
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load configuration: {ex.Message}";
        }
    }

    private async Task SaveConfigurationAsync()
    {
        try
        {
            await _configurationService.SaveConfigurationAsync(MountItems);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save configuration: {ex.Message}";
        }
    }

    public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        // Window closing is handled by TrayIconManager
        // This method is only called when the application is truly exiting
        return Task.FromResult(true);
    }
}
