# Commit Message: Fix Action Icons Visibility and AutoMount Interaction

## Title
```
fix: Prevent action icons from disappearing and block interaction during AutoMount/Exit
```

## Body
```
Changed ListView binding from IsEnabled to IsHitTestVisible and updated
CanInteractWithList to block user interaction during both AutoMount and
exit processes while maintaining visual appearance.

Problem 1 - Icons Disappearing:
When the exit button was clicked with mounted items, the confirmation
MessageBox ("There are N mounted drive(s). Do you want to exit?") would
cause the action icons (Mount/Unmount buttons) in the Actions column to
disappear, leaving only the circular button outlines visible. The icons
would reappear when the user clicked "No" to cancel the exit.

Root Cause:
1. ShellView.xaml:48 bound ListView to IsEnabled="{Binding CanInteractWithList}"
2. ExitApplicationAsync() set _isExiting = true, making CanInteractWithList false
3. WPF disabled the entire ListView and all child controls
4. MahApps Circle button style renders disabled buttons with only the circle
   outline visible, hiding the inner PackIconMaterial icons (FolderOpen, FolderRemove)
5. When MessageBox was cancelled, _isExiting reset to false, re-enabling the ListView
   and restoring icon visibility

Problem 2 - AutoMount Interaction Not Blocked:
During AutoMount, the ListView remained interactive, allowing users to
manually click Mount/Unmount buttons. While internal logic prevented
duplicate operations, the UI incorrectly suggested these actions were
available, creating a confusing user experience.

Previous State:
- CanInteractWithList only checked !_isExiting
- During AutoMount, ListView was fully interactive
- Users could click Mount buttons while AutoMount was running
- Inconsistent with CanAddMount which already blocked during AutoMount

Solution:
1. Replace IsEnabled with IsHitTestVisible binding on the ListView
   - IsHitTestVisible=false blocks all user interaction (clicks, selections, hover)
   - Preserves visual appearance (icons remain visible)
   - Prevents disabled visual style from being applied

2. Update CanInteractWithList to check both exit and AutoMount states
   - Changed from: !_isExiting
   - Changed to: !_isExiting && !_isAutoMounting
   - Consistent with CanAddMount behavior

3. Add NotifyOfPropertyChange for CanInteractWithList
   - When AutoMount starts: notify CanInteractWithList change
   - When AutoMount ends: notify CanInteractWithList change
   - Ensures ListView IsHitTestVisible updates correctly

Comparison - IsHitTestVisible vs IsEnabled:

IsHitTestVisible=false (new):
- Blocks all user interaction (clicks, selections, hover events)
- Preserves visual appearance (icons remain fully visible)
- Maintains enabled state for child controls
- No disabled visual style applied

IsEnabled=false (old):
- Blocks user interaction
- Applies disabled visual style (grayed out, reduced opacity)
- Cascades disabled state to all children
- Causes MahApps Circle buttons to hide inner icons

Benefits:
- Action icons remain visible during exit confirmation and AutoMount
- User can see the current state while MessageBox is shown or AutoMount runs
- No visual glitch when cancelling exit or AutoMount completion
- ListView blocks interaction during both exit and AutoMount processes
- Consistent behavior: ListView interaction blocked whenever CanAddMount is blocked
- Individual controls (ComboBox, CheckBox, Buttons) remain controlled by
  their own IsEnabled bindings (CanEditDestination, CanMount, CanUnmount)

User Experience:
Before:
- Icons disappear during exit → confusing visual feedback
- ListView interactive during AutoMount → incorrect affordance

After:
- Icons stay visible during exit and AutoMount → clear UI state
- ListView blocked during AutoMount → consistent with disabled Add button

Files changed:
- Views/ShellView.xaml (modified, 1 line)
  * Line 48: Changed IsEnabled to IsHitTestVisible binding

- ViewModels/ShellViewModel.cs (modified, 3 lines)
  * Line 35: Updated CanInteractWithList to check !_isExiting && !_isAutoMounting
  * Line 366: Added NotifyOfPropertyChange(() => CanInteractWithList) when AutoMount starts
  * Line 412: Added NotifyOfPropertyChange(() => CanInteractWithList) when AutoMount ends
```

## Notes
- Fixes UI bug where action icons disappeared during exit confirmation
- Prevents user interaction with ListView during AutoMount
- IsHitTestVisible is more appropriate for blocking interaction without
  affecting visual appearance
- Makes CanInteractWithList consistent with CanAddMount logic
- Individual control enable states remain unchanged
- No breaking changes to existing functionality
```
