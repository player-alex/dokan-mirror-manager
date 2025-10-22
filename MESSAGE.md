# Commit Message for Phase 5: MountService Extraction

## Title
```
refactor: Extract MountService from ShellViewModel
```

## Body
```
Extract mount and unmount operations from ShellViewModel into a dedicated
MountService to improve separation of concerns and testability.

Changes:
1. Created IMountService interface (60 lines)
   - MountAsync(item, isAutoMount) - Handles mount operations with timeout
   - UnmountAsync(item) - Handles unmount operations with timeout
   - Initialize(getWindow, setStatusMessage, saveConfiguration) - Initializes callbacks
   - InitializeMountLock(item) - Initializes concurrency control
   - RemoveMountLock(item) - Cleans up concurrency control

2. Created MountResult and UnmountResult DTOs
   - Success flag
   - ErrorMessage (optional)
   - ContinuedInBackground flag

3. Created MountService implementation (611 lines)
   - Manages mount operations with race condition prevention
   - Implements timeout handling (10s initial, 30s extended)
   - Provides background continuation for long-running operations
   - Handles user prompts for manual mounts vs auto-mount
   - Integrates with IMountMonitoringService for mount monitoring
   - Manages ConcurrentDictionary of semaphores for mount locking
   - Creates unmount callbacks for external unmount detection
   - Checks for processes using drives during unmount timeout
   - Logs errors to mount_error.log file

4. Modified ShellViewModel
   - Added IMountService dependency injection
   - Removed MountInternal() method (~380 lines)
   - Removed Unmount() method (~270 lines)
   - Removed _mountLocks ConcurrentDictionary field
   - Removed OnUnmountDetected() callback method (~14 lines)
   - Simplified Mount() to call service.MountAsync()
   - Simplified Unmount() to call service.UnmountAsync()
   - Added MountService.Initialize() call in OnViewLoaded
   - Updated AddMount() to use service.InitializeMountLock()
   - Updated RemoveMount() to use service.RemoveMountLock()
   - Updated LoadConfigurationAsync() to use service methods
   - Net reduction: ~673 lines

5. Registered service in App.xaml.cs Bootstrapper

Benefits:
- Separation of concerns: Mount/unmount logic isolated from view model
- Better testability: MountService can be mocked for testing
- Cleaner ShellViewModel: Major reduction in complexity and line count
- Reusability: Service can handle any mount item with callbacks
- Maintainability: All mount/unmount logic centralized in one place
- Improved error handling: Consistent error handling across all operations
- Better timeout management: Centralized timeout and background handling

Files changed:
- Services/Interfaces/IMountService.cs (new, 60 lines)
- Services/MountService.cs (new, 611 lines)
- ViewModels/ShellViewModel.cs (modified, -673 lines)
  * Line 25: Added IMountService field
  * Line 73: Added IMountService parameter to constructor
  * Line 80: Store IMountService in field
  * Line 100-103: Initialize MountService with callbacks
  * Line 189: Updated AddMount to use service
  * Line 209: Updated RemoveMount to use service
  * Lines 233-236: Simplified Mount method
  * Lines 238-241: Simplified Unmount method
  * Lines 238-634: Removed MountInternal method
  * Lines 620-634: Removed OnUnmountDetected callback
  * Lines 636-906: Removed Unmount method
  * Line 282: Updated LoadConfigurationAsync to use service
  * Line 324: Updated auto-mount to use service
- App.xaml.cs (modified, +1 line)
  * Line 118: Registered IMountService service

Build status: Success (0 errors, 35 warnings - same existing warnings)
```

## Notes
- Part of ongoing ShellViewModel refactoring effort (Phase 5 of 7)
- Maintains all existing functionality while improving code organization
- No breaking changes to public API or user-facing behavior
- Service uses callback pattern to update UI and save configuration
- Mount operations handle both manual and auto-mount scenarios differently
- Background continuation ensures UI doesn't block on slow operations
- Timeout handling provides user feedback and control
- Process detection helps identify why unmounts are delayed
```
