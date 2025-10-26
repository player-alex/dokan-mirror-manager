"""Test script to find Dokan Mirror Manager window"""

import win32gui
import win32con

def enum_windows_callback(hwnd, windows):
    if win32gui.IsWindowVisible(hwnd):
        title = win32gui.GetWindowText(hwnd)
        class_name = win32gui.GetClassName(hwnd)
        if title:
            windows.append((hwnd, title, class_name))

print("Searching for all visible windows...\n")
windows = []
win32gui.EnumWindows(enum_windows_callback, windows)

print(f"Found {len(windows)} visible windows:\n")

dokan_windows = []
for hwnd, title, class_name in windows:
    if "dokan" in title.lower() or "mirror" in title.lower():
        print(f">>> MATCH: 0x{hwnd:X} - '{title}' ({class_name})")
        dokan_windows.append((hwnd, title, class_name))
    else:
        # Uncomment to see all windows
        # print(f"    0x{hwnd:X} - '{title}' ({class_name})")
        pass

if not dokan_windows:
    print("\n❌ No Dokan/Mirror windows found!")
    print("\nTrying to find by exact title...")
    hwnd = win32gui.FindWindow(None, "Dokan Mirror Manager")
    if hwnd:
        print(f"✓ Found by exact title: 0x{hwnd:X}")
        print(f"  IsWindowVisible: {win32gui.IsWindowVisible(hwnd)}")
        print(f"  Class: {win32gui.GetClassName(hwnd)}")
    else:
        print("✗ Not found by exact title either")
else:
    print(f"\n✅ Found {len(dokan_windows)} potential Dokan windows")
    for hwnd, title, class_name in dokan_windows:
        print(f"\nWindow: {title}")
        print(f"  Handle: 0x{hwnd:X}")
        print(f"  Class: {class_name}")
        print(f"  IsWindowVisible: {win32gui.IsWindowVisible(hwnd)}")
