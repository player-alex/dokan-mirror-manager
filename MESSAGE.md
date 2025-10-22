# Commit Message for Phase 4: MountMonitoringService Extraction

## Title
```
refactor: Extract MountMonitoringService from ShellViewModel
```

## Body
```
Extract mount monitoring functionality from ShellViewModel into a dedicated
MountMonitoringService to improve separation of concerns and testability.

Changes:
1. Created IMountMonitoringService interface (37 lines)
   - StartMonitoringAsync(instance, item, driveLetter, onUnmountDetected)
   - CancelMonitoring(item)
   - CancelAllMonitoring()

2. Created MountMonitoringService implementation (133 lines)
   - Manages CancellationTokenSource dictionary for active monitoring tasks
   - Implements WaitForFileSystemClosedAsync with polling fallback
   - Handles external unmount detection for mounted drives
   - Invokes callback when unmount is detected

3. Modified ShellViewModel
   - Added IMountMonitoringService dependency injection
   - Removed MonitorFileSystemClosure() method (~99 lines)
   - Removed _monitoringTokens ConcurrentDictionary field
   - Added OnUnmountDetected() callback method (~14 lines)
   - Updated all mount operations to use service.StartMonitoringAsync()
   - Updated RemoveMount() to use service.CancelMonitoring()
   - Updated Unmount() to use service.CancelMonitoring()
   - Net reduction: ~104 lines

4. Registered service in App.xaml.cs Bootstrapper

Benefits:
- Separation of concerns: Monitoring logic isolated from view model
- Better testability: MountMonitoringService can be mocked for testing
- Cleaner ShellViewModel: Further reduced complexity and line count
- Reusability: Service can monitor any DokanInstance mount
- Maintainability: All monitoring logic centralized in one place

Files changed:
- Services/Interfaces/IMountMonitoringService.cs (new, 37 lines)
- Services/MountMonitoringService.cs (new, 133 lines)
- ViewModels/ShellViewModel.cs (modified, -104 lines)
  * Line 24: Added IMountMonitoringService field
  * Line 78: Added IMountMonitoringService parameter to constructor
  * Line 84: Store IMountMonitoringService in field
  * Line 35: Removed _monitoringTokens field
  * Lines 216: Updated RemoveMount to use service
  * Lines 401, 460, 493, 541, 575: Updated mount operations to use service
  * Lines 620-634: Replaced MonitorFileSystemClosure with OnUnmountDetected
  * Line 727: Updated Unmount to use service
- App.xaml.cs (modified, +1 line)
  * Line 117: Registered IMountMonitoringService service

Build status: Success (0 errors, existing warnings only)
```

## Notes
- Part of ongoing ShellViewModel refactoring effort (Phase 4 of 7)
- Maintains all existing functionality while improving code organization
- No breaking changes to public API or user-facing behavior
- Monitoring uses DokanInstance.WaitForFileSystemClosedAsync with polling fallback
- Callback-based design allows UI updates on dispatcher thread
