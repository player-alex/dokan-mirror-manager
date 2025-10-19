using Caliburn.Micro;
using DokanMirrorManager.Models;
using DokanNet;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.Linq;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace DokanMirrorManager.ViewModels;

public class ShellViewModel : Screen
{
    private readonly IWindowManager _windowManager;
    private readonly Dokan _dokan;
    private string _statusMessage = string.Empty;
    private MountItem? _selectedItem;
    private TaskbarIcon? _taskbarIcon;
    private bool _isClosingToTray = false;
    private bool _isUpdatingDriveLetters = false;
    private bool _isHiding = false;
    private const string ConfigFileName = "mounts.json";
    private const int WM_SHOWWINDOW_CUSTOM = 0x8001;
    private HwndSource? _hwndSource;

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

    public List<string> AvailableDriveLetters
    {
        get
        {
            try
            {
                var allLetters = Enumerable.Range('A', 26).Select(i => $"{(char)i}:\\").ToList();
                var usedLetters = DriveInfo.GetDrives().Select(d => d.Name).ToHashSet();
                var mountedLetters = MountItems.Where(m => m.Status == MountStatus.Mounted && !string.IsNullOrEmpty(m.DestinationLetter))
                    .Select(m => m.DestinationLetter)
                    .ToHashSet();

                return allLetters.Where(letter => !usedLetters.Contains(letter) && !mountedLetters.Contains(letter)).ToList();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error getting drive letters: {ex.Message}";
                return new List<string>();
            }
        }
    }

    public ShellViewModel(IWindowManager windowManager)
    {
        _windowManager = windowManager;
        _dokan = new Dokan(null);
        DisplayName = "Dokan Mirror Manager";

        LoadConfiguration();
    }

    protected override void OnViewLoaded(object view)
    {
        base.OnViewLoaded(view);
        StatusMessage = "Ready";

        InitializeTaskbarIcon(view);
        SetupWindowMessageHook(view);
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
            ShowWindow();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void InitializeTaskbarIcon(object view)
    {
        if (view is Window window)
        {
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

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var openMenuItem = new System.Windows.Controls.MenuItem { Header = "Open" };
            openMenuItem.Click += (s, e) => ShowWindow();
            contextMenu.Items.Add(openMenuItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitMenuItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.Items.Add(exitMenuItem);

            _taskbarIcon.ContextMenu = contextMenu;
            _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowWindow();

            window.Closing += Window_Closing;
        }
    }

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

    private void ShowWindow()
    {
        if (GetView() is Window window)
        {
            _isHiding = false;
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void HideWindow()
    {
        if (GetView() is Window window)
        {
            _isHiding = true;
            window.Hide();
            StatusMessage = "Minimized to tray";

            // Show single balloon tip notification
            System.Diagnostics.Debug.WriteLine("[HideWindow] Showing balloon tip");
            _taskbarIcon?.ShowBalloonTip("Dokan Mirror Manager",
                                        "Application minimized to tray",
                                        BalloonIcon.Info);

            // Reset flag after a short delay
            Task.Delay(100).ContinueWith(_ => _isHiding = false);
        }
    }

    private async void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Show window first to provide proper parent for MessageBox
        if (GetView() is Window window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        // Small delay to ensure window is fully visible
        await Task.Delay(100);

        _isClosingToTray = true;

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
                _isClosingToTray = false;
                return;
            }

            // Unmount all drives before exiting
            foreach (var item in mountedItems)
            {
                await Unmount(item);
            }
        }

        _taskbarIcon?.Dispose();
        _dokan?.Dispose();
        Application.Current.Shutdown();
    }

    private async Task ExitApplication()
    {
        _isClosingToTray = true;

        if (await CanCloseAsync())
        {
            _taskbarIcon?.Dispose();
            Application.Current.Shutdown();
        }
        else
        {
            _isClosingToTray = false;
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

            UpdateAllAvailableDriveLetters();

            // Auto-select first available drive letter
            AutoSelectDriveLetter(mountItem);

            SaveConfiguration();
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

            UpdateAllAvailableDriveLetters();
            SaveConfiguration();
            StatusMessage = $"Removed: {itemToRemove.SourcePath}";
        }
    }

    public void Mount(MountItem item)
    {
        if ((item.Status != MountStatus.Unmounted && item.Status != MountStatus.Error) || string.IsNullOrEmpty(item.DestinationLetter))
            return;

        // Check if drive letter is already in use
        if (DriveInfo.GetDrives().Any(d => d.Name.Equals(item.DestinationLetter, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"Drive letter {item.DestinationLetter} is already in use.", "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Check if another mount item is using this drive letter
        if (MountItems.Any(m => m != item && m.Status == MountStatus.Mounted && m.DestinationLetter == item.DestinationLetter))
        {
            MessageBox.Show($"Drive letter {item.DestinationLetter} is already mounted by another item.", "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        item.Status = MountStatus.Mounting;
        StatusMessage = $"Mounting {item.SourcePath} to {item.DestinationLetter}...";

        // Run mount operation in a completely separate thread
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Mount] Starting mount for {item.SourcePath} to {item.DestinationLetter}");

                // Verify source path exists
                if (!Directory.Exists(item.SourcePath))
                {
                    throw new DirectoryNotFoundException($"Source path does not exist: {item.SourcePath}");
                }

                System.Diagnostics.Debug.WriteLine("[Mount] Creating Mirror instance");
                var mirror = new Mirror(null, item.SourcePath);
                item.Mirror = mirror;

                System.Diagnostics.Debug.WriteLine("[Mount] Building DokanInstance");
                var builder = new DokanInstanceBuilder(_dokan)
                    .ConfigureOptions(options =>
                    {
                        options.Options = DokanOptions.DebugMode | DokanOptions.EnableNotificationAPI;
                        options.MountPoint = item.DestinationLetter;
                    });

                System.Diagnostics.Debug.WriteLine("[Mount] Calling Build()");
                var instance = builder.Build(mirror);
                item.DokanInstance = instance;

                System.Diagnostics.Debug.WriteLine("[Mount] Build() completed successfully");

                // Update status immediately (before UI update) to avoid race conditions
                item.Status = MountStatus.Mounted;

                // Update UI on success (non-blocking)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StatusMessage = $"Mounted {item.SourcePath} to {item.DestinationLetter}";
                });

                System.Diagnostics.Debug.WriteLine("[Mount] Waiting for file system to close");
                // Wait for file system to close (this blocks the thread)
                var dokanInstance = instance as dynamic;
                if (dokanInstance != null)
                {
                    dokanInstance.WaitForFileSystemClosedAsync(uint.MaxValue).GetAwaiter().GetResult();
                }

                System.Diagnostics.Debug.WriteLine("[Mount] File system closed");
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

                // Update UI on error
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    item.Status = MountStatus.Error;
                    item.ErrorMessage = ex.Message;
                    StatusMessage = $"Failed to mount: {ex.Message}";
                    MessageBox.Show($"Failed to mount {item.SourcePath}:\n\n{ex.GetType().Name}: {ex.Message}\n\nSee mount_error.log for details",
                        "Mount Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });

        thread.IsBackground = true;
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Name = $"DokanMount-{item.DestinationLetter}";
        thread.Start();
    }

    public async Task Unmount(MountItem item)
    {
        if (item.Status != MountStatus.Mounted)
            return;

        StatusMessage = $"Unmounting {item.DestinationLetter}...";

        try
        {
            await Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(item.DestinationLetter))
                {
                    _dokan.RemoveMountPoint(item.DestinationLetter);
                }

                if (item.DokanInstance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                item.DokanInstance = null;
                item.Mirror = null;
            });

            item.Status = MountStatus.Unmounted;
            item.ErrorMessage = string.Empty;
            StatusMessage = $"Unmounted {item.DestinationLetter}";
            SaveConfiguration();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to unmount: {ex.Message}";
            MessageBox.Show($"Failed to unmount {item.DestinationLetter}: {ex.Message}", "Unmount Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // Ignore changes while we're updating to avoid infinite loop
        if (_isUpdatingDriveLetters)
            return;

        if (e.PropertyName == nameof(MountItem.Status) || e.PropertyName == nameof(MountItem.DestinationLetter))
        {
            UpdateAllAvailableDriveLetters();

            // After updating available drive letters, check if any unmounted items need auto-selection
            // This handles the case where a user manually selects a drive letter that was being used by another unmounted item
            try
            {
                _isUpdatingDriveLetters = true;
                foreach (var item in MountItems)
                {
                    if (item.Status == MountStatus.Unmounted || item.Status == MountStatus.Error)
                    {
                        AutoSelectDriveLetter(item);
                    }
                }
            }
            finally
            {
                _isUpdatingDriveLetters = false;
            }
        }

        // Save configuration when AutoMount, IsReadOnly, or DestinationLetter changes
        if (e.PropertyName == nameof(MountItem.AutoMount) ||
            e.PropertyName == nameof(MountItem.IsReadOnly) ||
            e.PropertyName == nameof(MountItem.DestinationLetter))
        {
            SaveConfiguration();
        }
    }

    private void AutoSelectDriveLetter(MountItem item)
    {
        System.Diagnostics.Debug.WriteLine($"[AutoSelect] Item: {item.SourcePath}, Current: {item.DestinationLetter}, Status: {item.Status}, Available count: {item.AvailableDriveLetters.Count}");

        // 이미 드라이브 레터가 있고 사용 가능하면 그대로 유지
        if (!string.IsNullOrEmpty(item.DestinationLetter) &&
            item.AvailableDriveLetters.Contains(item.DestinationLetter))
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSelect] Keeping current: {item.DestinationLetter}");
            return;
        }

        // 사용 가능한 첫 번째 드라이브 레터 선택
        if (item.AvailableDriveLetters.Count > 0)
        {
            var oldLetter = item.DestinationLetter;
            item.DestinationLetter = item.AvailableDriveLetters[0];
            System.Diagnostics.Debug.WriteLine($"[AutoSelect] Changed from '{oldLetter}' to '{item.DestinationLetter}'");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSelect] No available letters!");
        }
    }

    private void UpdateAllAvailableDriveLetters()
    {
        if (_isUpdatingDriveLetters)
            return;

        try
        {
            _isUpdatingDriveLetters = true;

            var allLetters = Enumerable.Range('A', 26).Select(i => $"{(char)i}:\\").ToList();
            var usedLetters = DriveInfo.GetDrives().Select(d => d.Name).ToHashSet();

            foreach (var item in MountItems)
            {
                // Get letters used by OTHER items (both mounted and unmounted with selected letters)
                var otherUsedLetters = MountItems
                    .Where(m => m != item && !string.IsNullOrEmpty(m.DestinationLetter))
                    .Select(m => m.DestinationLetter)
                    .ToHashSet();

                // Available letters = all letters - system used - other items' selected letters
                var available = allLetters.Where(letter => !usedLetters.Contains(letter) && !otherUsedLetters.Contains(letter)).ToList();

                // If this item has a selected letter, make sure it's in the list
                if (!string.IsNullOrEmpty(item.DestinationLetter) && !available.Contains(item.DestinationLetter))
                {
                    if (item.Status == MountStatus.Mounted || !usedLetters.Contains(item.DestinationLetter))
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateAvail] Adding {item.DestinationLetter} for {item.SourcePath} (Status: {item.Status})");
                        available.Add(item.DestinationLetter);
                        available.Sort();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateAvail] NOT adding {item.DestinationLetter} for {item.SourcePath} (in use by system)");
                    }
                }

                item.AvailableDriveLetters = available;
                System.Diagnostics.Debug.WriteLine($"[UpdateAvail] {item.SourcePath}: Dest={item.DestinationLetter}, Available=[{string.Join(", ", available)}]");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating drive letters: {ex.Message}";
        }
        finally
        {
            _isUpdatingDriveLetters = false;

            // Check if any item's selected drive letter is no longer available
            foreach (var item in MountItems)
            {
                if (item.Status == MountStatus.Unmounted || item.Status == MountStatus.Error)
                {
                    AutoSelectDriveLetter(item);
                }
            }
        }
    }

    private void LoadConfiguration()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

            if (!File.Exists(configPath))
                return;

            var json = File.ReadAllText(configPath);
            var items = JsonSerializer.Deserialize<List<MountItemDto>>(json);

            if (items != null)
            {
                foreach (var dto in items)
                {
                    // Expand environment variables in SourcePath
                    // e.g., %USERPROFILE%\Desktop -> C:\Users\Username\Desktop
                    var expandedSourcePath = Environment.ExpandEnvironmentVariables(dto.SourcePath);

                    var mountItem = new MountItem
                    {
                        SourcePath = expandedSourcePath,
                        OriginalSourcePath = dto.SourcePath, // Keep original path with env vars
                        DestinationLetter = dto.DestinationLetter,
                        AutoMount = dto.AutoMount,
                        IsReadOnly = dto.IsReadOnly,
                        Status = MountStatus.Unmounted
                    };

                    mountItem.PropertyChanged += MountItem_PropertyChanged;
                    MountItems.Add(mountItem);
                }

                UpdateAllAvailableDriveLetters();

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

                    AutoSelectDriveLetter(item);

                    // Track this item's selected drive letter
                    if (!string.IsNullOrEmpty(item.DestinationLetter))
                    {
                        usedDriveLetters.Add(item.DestinationLetter);
                    }

                    // Update available drive letters after each assignment to prevent duplicates
                    UpdateAllAvailableDriveLetters();
                }

                // Auto-mount items that have AutoMount enabled (sequentially to avoid conflicts)
                Task.Run(async () =>
                {
                    foreach (var item in MountItems.Where(m => m.AutoMount && m.CanMount).ToList())
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Mount(item);
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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load configuration: {ex.Message}";
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            var items = MountItems.Select(m => new MountItemDto
            {
                // Save original path (with environment variables) if available
                SourcePath = string.IsNullOrEmpty(m.OriginalSourcePath) ? m.SourcePath : m.OriginalSourcePath,
                DestinationLetter = m.DestinationLetter,
                AutoMount = m.AutoMount,
                IsReadOnly = m.IsReadOnly
            }).ToList();

            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save configuration: {ex.Message}";
        }
    }

    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"[CanCloseAsync] Called, _isClosingToTray={_isClosingToTray}");

        // Don't show dialog if just minimizing to tray
        if (!_isClosingToTray)
        {
            return true;
        }

        var mountedItems = MountItems.Where(m => m.Status == MountStatus.Mounted).ToList();

        if (mountedItems.Any())
        {
            var result = MessageBox.Show(
                $"There are {mountedItems.Count} mounted drive(s). Do you want to exit?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
                return false;

            // Unmount all drives before exiting
            foreach (var item in mountedItems)
            {
                await Unmount(item);
            }
        }

        _dokan?.Dispose();
        return true;
    }

    private class MountItemDto
    {
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationLetter { get; set; } = string.Empty;
        public bool AutoMount { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
