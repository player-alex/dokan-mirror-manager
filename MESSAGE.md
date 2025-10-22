# Commit Message for Phase 2

## Title
```
refactor: Extract DriveLetterManager from ShellViewModel
```

## Body
```
Extract drive letter management logic into a separate service to improve
maintainability and testability.

Changes:
- Create IDriveLetterManager interface with methods:
  * GetAvailableDriveLetters: Calculate available drive letters for mount items
  * AutoSelectDriveLetter: Automatically select appropriate drive letter
  * UpdateAllDriveLetters: Update available letters for all mount items
- Implement DriveLetterManager class with drive letter allocation logic
- Update ShellViewModel to use IDriveLetterManager via dependency injection
- Remove inline drive letter management code from ShellViewModel:
  * AvailableDriveLetters property (now delegates to service)
  * UpdateAllAvailableDriveLetters() method (replaced by service call)
  * AutoSelectDriveLetter() method (replaced by service call)
- Remove _isUpdatingDriveLetters flag (now handled by service)
- Register DriveLetterManager in App.xaml.cs Bootstrapper

Benefits:
- Reduced ShellViewModel size from ~1,320 to ~1,180 lines (~140 lines)
- Drive letter management logic now testable in isolation
- Better separation of concerns
- Eliminates complex flag-based state management
- Easier to maintain and extend drive letter allocation logic

Files changed:
- Added: Services/Interfaces/IDriveLetterManager.cs (32 lines)
- Added: Services/DriveLetterManager.cs (161 lines)
- Modified: ViewModels/ShellViewModel.cs (-140 lines, simplified logic)
- Modified: App.xaml.cs (added DI registration)
```

## Notes for Direct Editing
- Feel free to modify the commit message as needed
- This is Phase 2 of 7 in the ShellViewModel refactoring plan
- Build verified: 0 errors, existing warnings only
- Total reduction so far: ~240 lines from ShellViewModel
