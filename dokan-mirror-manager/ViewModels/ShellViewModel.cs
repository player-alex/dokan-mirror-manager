using Caliburn.Micro;
using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using DokanNet;
using Microsoft.Win32;
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
    private readonly IMountService _mountService;
    private readonly Dokan _dokan;
    private string _statusMessage = string.Empty;
    private MountItem? _selectedItem;
    private const int WM_SHOWWINDOW_CUSTOM = 0x8001;
    private HwndSource? _hwndSource;
    private readonly Dispatcher _dispatcher;
    private bool _isExiting = false;
    private bool _isAutoMounting = false;
    private CancellationTokenSource? _autoMountCts = null;

    public ObservableCollection<MountItem> MountItems { get; } = [];

    public bool CanInteractWithList => !_isExiting && !_isAutoMounting;

    public bool CanAddMount => !_isExiting && !_isAutoMounting;

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

    public ShellViewModel(IWindowManager windowManager, IConfigurationService configurationService, IDriveLetterManager driveLetterManager, ITrayIconManager trayIconManager, IMountMonitoringService mountMonitoringService, IMountService mountService)
    {
        _windowManager = windowManager;
        _configurationService = configurationService;
        _driveLetterManager = driveLetterManager;
        _trayIconManager = trayIconManager;
        _mountMonitoringService = mountMonitoringService;
        _mountService = mountService;
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

        // Initialize MountService with callbacks
        _mountService.Initialize(
            () => GetView() as Window,
            msg => StatusMessage = msg,
            SaveConfigurationAsync);

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
        _isExiting = true;
        NotifyOfPropertyChange(() => CanInteractWithList);
        NotifyOfPropertyChange(() => CanAddMount);

        try
        {
            // Cancel AutoMount if it's in progress
            if (_isAutoMounting && _autoMountCts != null)
            {
                _autoMountCts.Cancel();

                // Wait for AutoMount to complete cancellation (max 3 seconds)
                var waitCount = 0;
                while (_isAutoMounting && waitCount < 30)
                {
                    await Task.Delay(100);
                    waitCount++;
                }
            }

            // Collect mounted items after AutoMount is cancelled
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
        finally
        {
            // If exit is cancelled, re-enable UI
            _isExiting = false;
            NotifyOfPropertyChange(() => CanInteractWithList);
            NotifyOfPropertyChange(() => CanAddMount);
        }
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

            // Initialize semaphore for this mount item
            _mountService.InitializeMountLock(mountItem);

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

            // Clean up semaphore when removing item
            _mountService.RemoveMountLock(itemToRemove);

            // Cancel and clean up monitoring task
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
        await _mountService.MountAsync(item, isAutoMount: false);
    }

    public async Task Unmount(MountItem item)
    {
        await _mountService.UnmountAsync(item);
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

                // Initialize semaphore for this mount item
                _mountService.InitializeMountLock(mountItem);
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
                try
                {
                    _isAutoMounting = true;
                    _autoMountCts = new CancellationTokenSource();
                    var cancellationToken = _autoMountCts.Token;

                    await _dispatcher.InvokeAsync(() =>
                    {
                        NotifyOfPropertyChange(() => CanInteractWithList);
                        NotifyOfPropertyChange(() => CanAddMount);
                    });

                    foreach (var item in MountItems.Where(m => m.AutoMount && m.CanMount).ToList())
                    {
                        // Check if cancellation is requested (e.g., during exit)
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        // Use captured dispatcher
                        await _dispatcher.InvokeAsync(async () =>
                        {
                            await _mountService.MountAsync(item, isAutoMount: true);
                        });

                        // Wait for mount to complete before starting next one
                        var timeout = 0;
                        while (item.Status == MountStatus.Mounting && timeout < 100)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            await Task.Delay(100, CancellationToken.None); // Don't throw on cancel
                            timeout++;
                        }

                        // Small delay between mounts to ensure Dokan is ready
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(500, CancellationToken.None);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // AutoMount was cancelled (e.g., during application exit) - this is expected
                }
                finally
                {
                    _isAutoMounting = false;
                    _autoMountCts?.Dispose();
                    _autoMountCts = null;

                    await _dispatcher.InvokeAsync(() =>
                    {
                        NotifyOfPropertyChange(() => CanInteractWithList);
                        NotifyOfPropertyChange(() => CanAddMount);
                    });
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
