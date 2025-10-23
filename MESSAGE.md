# Commit Message: UI Locking During Critical Operations

## Title
```
fix: Prevent race condition by locking UI during exit and AutoMount
```

## Body
```
Add UI locking mechanism to prevent race conditions during critical operations
such as application exit and AutoMount, ensuring safe and predictable behavior.

Problem:
When the user clicks "Exit" in the tray menu, a confirmation dialog appears.
During this time, the UI remains responsive, allowing users to mount additional
items. This creates a race condition where:
1. Exit operation captures snapshot of 4 mounted items
2. User quickly mounts a 5th item while dialog is shown
3. Exit proceeds to unmount only the original 4 items
4. Application exits with the 5th item still mounted (zombie drive)

Similarly, during AutoMount operations, users could add new items or perform
other operations that might interfere with the sequential mounting process.

Solution:
Implement differential UI locking based on operation criticality:
- Exit operation: Lock entire ListView (complete freeze)
- AutoMount operation: Lock only AddMount button (allow monitoring)

Changes:
1. Added UI state management flags to ShellViewModel
   - _isExiting: Tracks if application is in exit process
   - _isAutoMounting: Tracks if AutoMount is in progress

2. Added computed properties for UI binding
   - CanInteractWithList: Returns !_isExiting
     * Completely disables ListView during exit
     * Prevents any mount/unmount operations
     * Prevents checkbox/combobox changes
   - CanAddMount: Returns !_isExiting && !_isAutoMounting
     * Disables Add button during exit
     * Disables Add button during AutoMount
     * Allows other operations during AutoMount

3. Updated ExitApplicationAsync method
   - Set _isExiting = true at start
   - Notify property changes for CanInteractWithList and CanAddMount
   - Wrap entire method in try-finally block
   - Reset _isExiting = false in finally (if user cancels exit)
   - Ensures UI re-enables if exit is cancelled

4. Updated LoadConfigurationAsync method
   - Wrap AutoMount Task.Run in try-finally block
   - Set _isAutoMounting = true before mounting loop
   - Use dispatcher to notify CanAddMount property change
   - Reset _isAutoMounting = false in finally
   - Ensures Add button re-enables after AutoMount completes

5. Updated ShellView.xaml
   - Bound ListView.IsEnabled to CanInteractWithList
   - Bound AddMount button IsEnabled to CanAddMount
   - Remove button already has its own CanRemoveMount logic

Benefits:
- Race condition eliminated: No new mounts during exit
- Predictable behavior: All mounted items are safely unmounted
- Better UX: Visual feedback that operation is in progress
- Flexible design: AutoMount doesn't freeze entire UI
- Safe cancellation: UI re-enables if exit is cancelled
- Thread-safe: Dispatcher ensures UI updates on correct thread

Behavior:
┌─────────────────────────────────────────┐
│ Operation         │ ListView │ Add Btn  │
├─────────────────────────────────────────┤
│ Normal            │ Enabled  │ Enabled  │
│ Exit (in dialog)  │ Disabled │ Disabled │
│ Exit (cancelled)  │ Enabled  │ Enabled  │
│ AutoMount         │ Enabled  │ Disabled │
└─────────────────────────────────────────┘

Technical Details:
- Individual mount/unmount buttons still controlled by item.CanMount/CanUnmount
- ListView disabled = all child controls (checkboxes, comboboxes, buttons) disabled
- Property notifications use Caliburn.Micro's NotifyOfPropertyChange
- AutoMount uses dispatcher.InvokeAsync for thread-safe property updates
- Finally blocks guarantee UI state restoration even on exceptions

Files changed:
- ViewModels/ShellViewModel.cs (modified, +51 lines)
  * Lines 29-30: Added _isExiting and _isAutoMounting flags
  * Lines 34-36: Added CanInteractWithList and CanAddMount properties
  * Lines 140-179: Updated ExitApplicationAsync with locking logic
  * Lines 342-377: Updated LoadConfigurationAsync AutoMount with locking
- Views/ShellView.xaml (modified, +2 lines)
  * Line 48: Added IsEnabled binding to ListView
  * Line 154: Added IsEnabled binding to AddMount button

Build status: Success (0 errors, 35 warnings - unchanged)
```

## Notes
- Fixes critical race condition bug identified in theoretical scenario
- No breaking changes to existing functionality
- All existing features continue to work as expected
- UI feedback makes locked state clear to users
- Proper cleanup ensures no permanent UI lockouts
- Part of ongoing code quality improvements post-refactoring
```
