# Commit Message for Phase 1

## Title
```
refactor: Extract ConfigurationService from ShellViewModel
```

## Body
```
Extract configuration management logic into a separate service to improve
maintainability and testability.

Changes:
- Create IConfigurationService interface with LoadConfigurationAsync and
  SaveConfigurationAsync methods
- Implement ConfigurationService class with JSON serialization logic
- Extract MountItemDto to separate Models/MountItemDto.cs file
- Update ShellViewModel to use IConfigurationService via dependency injection
- Register ConfigurationService in App.xaml.cs Bootstrapper

Benefits:
- Reduced ShellViewModel size from 1,420 to 1,320 lines (~100 lines)
- Configuration logic now testable in isolation
- Better separation of concerns
- Easier to maintain and extend configuration functionality

Files changed:
- Added: Services/Interfaces/IConfigurationService.cs (20 lines)
- Added: Services/ConfigurationService.cs (103 lines)
- Added: Models/MountItemDto.cs (12 lines)
- Modified: ViewModels/ShellViewModel.cs (-100 lines)
- Modified: App.xaml.cs (added DI registration)
```

## Notes for Direct Editing
- Feel free to modify the commit message as needed
- This is Phase 1 of 7 in the ShellViewModel refactoring plan
- Build verified: 0 errors, existing warnings only
