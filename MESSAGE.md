# Commit Message: Safe AutoMount Cancellation During Exit

## Title
```
fix: Add AutoMount cancellation during exit to prevent zombie drives
```

## Body
```
Implement CancellationToken-based safe exit mechanism to properly cancel
AutoMount operations and prevent zombie drives when exiting during AutoMount.

Problem 1 (Previous fix):
When the user clicks "Exit" in the tray menu, a confirmation dialog appears.
During this time, the UI remains responsive, allowing users to mount additional
items, creating a race condition where newly mounted items are not unmounted.

Problem 2 (This fix):
When AutoMount is running in background and user clicks "Exit":
1. AutoMount task runs independently in Task.Run (fire-and-forget)
2. Exit collects snapshot of mounted items at that moment
3. AutoMount continues mounting items in background
4. User confirms exit, only original items are unmounted
5. Application exits while AutoMount is still running
6. Newly mounted items during exit become zombie drives
7. AutoMount task may crash when app shuts down mid-operation

Example scenario:
- 10 items with AutoMount enabled
- Items 1-3 mounted, item 4 in progress
- User clicks Exit
- Items 4-6 complete mounting while dialog is shown
- User clicks "Yes"
- Only items 1-3 are unmounted
- Items 4-6 remain as zombie drives
- AutoMount task may still be running after app shutdown

Solution:
Implement CancellationToken-based cooperative cancellation:
1. Add CancellationTokenSource for AutoMount control
2. Check cancellation before each mount operation
3. Exit cancels AutoMount and waits for completion
4. Re-collect mounted items after cancellation
5. Safely unmount all items before shutdown

Changes:
1. Added CancellationTokenSource field
   - _autoMountCts: Controls AutoMount task cancellation
   - Created when AutoMount starts
   - Disposed when AutoMount completes

2. Updated LoadConfigurationAsync AutoMount loop
   - Create CancellationTokenSource at start
   - Check cancellationToken.IsCancellationRequested before each mount
   - Check cancellation during mount wait loop
   - Use CancellationToken.None for delays (no-throw behavior)
   - Catch OperationCanceledException (expected during exit)
   - Properly dispose CancellationTokenSource in finally

3. Updated ExitApplicationAsync
   - Check if AutoMount is running (_isAutoMounting && _autoMountCts != null)
   - Call _autoMountCts.Cancel() to signal cancellation
   - Wait up to 3 seconds for AutoMount to complete cancellation
   - Re-collect mounted items AFTER AutoMount is cancelled
   - Ensures all mounted items (including those mounted during wait) are unmounted

Benefits:
- Complete race condition elimination: AutoMount stops before collecting items
- Predictable exit: All mounted items are properly unmounted
- Safe cancellation: Cooperative cancellation, no abrupt task termination
- No zombie drives: Even items mounted during exit are cleaned up
- Graceful shutdown: AutoMount completes current operation before stopping
- Resource cleanup: CancellationTokenSource properly disposed

Behavior Timeline (10 items AutoMount, exit during item 4):

Before this fix:
T0: Items 1-3 mounted ✅
T1: Item 4 mounting...
T2: User clicks Exit
T3: Snapshot: [1,2,3]
T4: Items 4-6 complete ❌ (background continues)
T5: User clicks "Yes"
T6: Unmount items 1-3 only
T7: Exit (items 4-6 remain as zombies) 💥

After this fix:
T0: Items 1-3 mounted ✅
T1: Item 4 mounting...
T2: User clicks Exit
T3: Cancel AutoMount signal sent
T4: Item 4 completes, loop breaks ✅
T5: AutoMount task ends cleanly
T6: Re-collect: [1,2,3,4] ✅
T7: User clicks "Yes"
T8: Unmount items 1-2-3-4 all ✅
T9: Clean exit 🎉

Technical Implementation:
- CancellationToken checked at 3 points in loop:
  1. Before starting each mount
  2. During mount completion wait
  3. Before inter-mount delay
- Uses CancellationToken.None for delays to avoid exceptions
- Catches OperationCanceledException as expected flow
- 3-second timeout prevents indefinite wait if task hangs
- Finally block ensures cleanup even on unexpected errors

Edge Cases Handled:
- AutoMount not running: No cancellation needed, normal exit flow
- AutoMount already complete: Token disposed, normal exit flow
- Cancellation during mount: Current mount completes, no new mounts start
- Timeout waiting for cancel: Proceeds with exit anyway (safety fallback)
- Exception during AutoMount: Finally block ensures cleanup

Files changed:
- ViewModels/ShellViewModel.cs (modified, +30 lines)
  * Line 31: Added _autoMountCts field (CancellationTokenSource?)
  * Lines 147-159: Added AutoMount cancellation in ExitApplicationAsync
  * Line 162: Re-collect mounted items after cancellation
  * Lines 346-398: Updated AutoMount loop with cancellation support
    - Line 346-347: Create and store CancellationTokenSource
    - Line 357-358: Check cancellation before each mount
    - Lines 370-371: Check cancellation during wait loop
    - Line 373: Use CancellationToken.None for delay
    - Lines 378-381: Conditional delay with cancellation check
    - Lines 384-387: Catch OperationCanceledException
    - Lines 391-392: Dispose CancellationTokenSource

Build status: Success (0 errors, 35 warnings - unchanged)
```

## Notes
- Fixes critical race condition during AutoMount + Exit
- Builds upon previous UI locking fix (exit/AutoMount)
- No breaking changes to existing functionality
- Cooperative cancellation is safer than abrupt termination
- Properly handles all edge cases and cleanup
- Part of ongoing reliability improvements
```
