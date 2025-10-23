# Commit Message: Fix Action Icons Visibility During Exit

## Title
```
fix: Prevent action icons from disappearing during exit confirmation
```

## Body
```
Changed ListView binding from IsEnabled to IsHitTestVisible to maintain
visual appearance while blocking user interaction during exit process.

Problem:
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

Solution:
Replace IsEnabled with IsHitTestVisible binding on the ListView.

IsHitTestVisible=false:
- Blocks all user interaction (clicks, selections, hover events)
- Preserves visual appearance (icons remain visible)
- Maintains enabled state for child controls
- Prevents the disabled visual style from being applied

IsEnabled=false (old behavior):
- Blocks user interaction
- Applies disabled visual style (grayed out, reduced opacity)
- Cascades disabled state to all children
- Causes MahApps Circle buttons to hide inner icons

Benefits:
- Action icons remain visible during exit confirmation
- User can still see the current state while the MessageBox is shown
- No visual glitch when cancelling exit
- ListView still blocks interaction during exit process
- Individual controls (ComboBox, CheckBox, Buttons) remain controlled by
  their own IsEnabled bindings (CanEditDestination, CanMount, CanUnmount)

User Experience:
Before: Icons disappear → confusing visual feedback
After: Icons stay visible → clear, consistent UI state

Files changed:
- Views/ShellView.xaml (modified, 1 line)
  * Line 48: Changed IsEnabled to IsHitTestVisible binding
```

## Notes
- Fixes UI bug where action icons disappeared during exit confirmation
- No functional changes to exit flow or AutoMount behavior
- IsHitTestVisible is more appropriate for blocking interaction without
  affecting visual appearance
- Individual control enable states remain unchanged
```
