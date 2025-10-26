"""Test if we can find hidden Dokan Mirror Manager window"""

import win32gui
import win32con

def test_find_window():
    """Test finding window by title (works even if hidden)"""

    print("=" * 80)
    print("Testing FindWindow for hidden Dokan Mirror Manager window")
    print("=" * 80)
    print()

    # Try to find by exact title
    hwnd = win32gui.FindWindow(None, "Dokan Mirror Manager")

    if hwnd == 0:
        print("❌ Window NOT found!")
        print("\nMake sure Dokan Mirror Manager is running (even if minimized to tray)")
        return False

    print(f"✅ Window FOUND!")
    print(f"  Handle: 0x{hwnd:X} ({hwnd})")

    # Get window info
    try:
        title = win32gui.GetWindowText(hwnd)
        class_name = win32gui.GetClassName(hwnd)
        is_visible = win32gui.IsWindowVisible(hwnd)
        is_enabled = win32gui.IsWindowEnabled(hwnd)

        print(f"  Title: '{title}'")
        print(f"  Class: '{class_name}'")
        print(f"  IsVisible: {is_visible}")
        print(f"  IsEnabled: {is_enabled}")

        # Try to get window placement
        try:
            placement = win32gui.GetWindowPlacement(hwnd)
            show_cmd = placement[1]
            show_cmd_names = {
                0: "SW_HIDE",
                1: "SW_SHOWNORMAL",
                2: "SW_SHOWMINIMIZED",
                3: "SW_SHOWMAXIMIZED",
                4: "SW_SHOWNOACTIVATE",
                5: "SW_SHOW",
                6: "SW_MINIMIZE",
                7: "SW_SHOWMINNOACTIVE",
                8: "SW_SHOWNA",
                9: "SW_RESTORE"
            }
            print(f"  ShowCmd: {show_cmd_names.get(show_cmd, str(show_cmd))}")
        except Exception as e:
            print(f"  GetWindowPlacement failed: {e}")

        if not is_visible:
            print("\n⚠️  Window is HIDDEN (minimized to tray)")
            print("   This is OK - SendMessage should still work!")
        else:
            print("\n✓ Window is VISIBLE")

        return True

    except Exception as e:
        print(f"\n❌ Error getting window info: {e}")
        return False

if __name__ == "__main__":
    test_find_window()
