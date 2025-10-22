# Commit Message for Phase 3: TrayIconManager Extraction

## Title
```
refactor: Extract TrayIconManager from ShellViewModel
```

## Body
```
Extract tray icon management functionality from ShellViewModel into a dedicated
TrayIconManager service to improve separation of concerns and testability.

Changes:
1. Created ITrayIconManager interface (44 lines)
   - Initialize(Window, showAction, exitAction, statusMessageAction)
   - ShowWindow(), HideWindow()
   - ShowBalloonTip(title, message, icon)
   - Dispose()

2. Created TrayIconManager implementation (169 lines)
   - Manages TaskbarIcon lifecycle and context menu
   - Handles window show/hide/close operations
   - Manages window closing behavior (minimize to tray vs actual exit)
   - Integrates with exit confirmation flow

3. Modified ShellViewModel
   - Added ITrayIconManager dependency injection
   - Removed InitializeTaskbarIcon(), ShowWindow(), HideWindow() methods
   - Removed Window_Closing(), ExitMenuItem_Click() methods
   - Removed _taskbarIcon, _isClosingToTray, _isHiding fields
   - Unified exit logic into ExitApplicationAsync() method
   - Simplified CanCloseAsync() override
   - Net reduction: ~120 lines

4. Registered service in App.xaml.cs Bootstrapper

Benefits:
- Separation of concerns: UI window management isolated from business logic
- Better testability: TrayIconManager can be mocked for testing
- Cleaner ShellViewModel: Reduced complexity and line count
- Reusability: Service can be used by other ViewModels if needed
- Maintainability: Tray icon logic centralized in one place

Files changed:
- Services/Interfaces/ITrayIconManager.cs (new, 44 lines)
- Services/TrayIconManager.cs (new, 169 lines)
- ViewModels/ShellViewModel.cs (modified, -120 lines)
  * Lines 20-26: Updated field declarations
  * Line 77: Added ITrayIconManager parameter to constructor
  * Line 82: Store ITrayIconManager in field
  * Lines 95-98: Initialize TrayIconManager in OnViewLoaded
  * Lines 120: Updated WndProc to use service
  * Lines 126-129: Simplified ShowWindow wrapper
  * Lines 131-159: Unified ExitApplicationAsync method
  * Lines 1117-1122: Simplified CanCloseAsync
- App.xaml.cs (modified, +1 line)
  * Line 116: Registered ITrayIconManager service

Build status: Success (0 errors, existing warnings only)
```

## Notes
- Part of ongoing ShellViewModel refactoring effort (Phase 3 of 7)
- Maintains all existing functionality while improving code organization
- No breaking changes to public API or user-facing behavior
