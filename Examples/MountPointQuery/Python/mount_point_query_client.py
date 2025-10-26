"""
Example Python client for querying mount points from Dokan Mirror Manager via IPC.

Requirements:
    pip install pywin32

Usage:
    python mount_point_query_client.py
"""

import win32gui
import win32con
import json
import uuid
import struct
import threading
import time
from ctypes import *
from ctypes.wintypes import *

# Windows message constants
WM_COPYDATA = 0x004A
WM_GET_MOUNT_POINTS = 0x8002

# Define LRESULT type (missing in ctypes.wintypes)
LRESULT = c_ssize_t

# Define COPYDATASTRUCT for WM_COPYDATA
class COPYDATASTRUCT(Structure):
    _fields_ = [
        ("dwData", c_void_p),  # Can hold an integer or pointer
        ("cbData", DWORD),
        ("lpData", LPVOID)
    ]

# Win32 API functions
SendMessage = windll.user32.SendMessageW
SendMessage.argtypes = [c_void_p, UINT, WPARAM, c_void_p]
SendMessage.restype = LRESULT


class NamedPipeServer:
    """Simple Named Pipe server for receiving response."""

    def __init__(self, pipe_name, timeout=10):
        self.pipe_name = pipe_name
        self.timeout = timeout
        self.response_data = None
        self.error = None
        self.event = threading.Event()

    def start(self):
        """Start the pipe server in a background thread."""
        thread = threading.Thread(target=self._run_server, daemon=True)
        thread.start()

    def _run_server(self):
        """Run the Named Pipe server."""
        try:
            import win32pipe
            import win32file
            import pywintypes

            # Create Named Pipe
            pipe_path = rf'\\.\pipe\{self.pipe_name}'
            pipe = win32pipe.CreateNamedPipe(
                pipe_path,
                win32pipe.PIPE_ACCESS_INBOUND,
                win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_WAIT,
                1,  # max instances
                65536,  # out buffer size
                65536,  # in buffer size
                self.timeout * 1000,  # timeout in ms
                None
            )

            if pipe == win32file.INVALID_HANDLE_VALUE:
                self.error = "Failed to create Named Pipe"
                self.event.set()
                return

            try:
                # Wait for client connection
                win32pipe.ConnectNamedPipe(pipe, None)

                # Read data length (4 bytes)
                result, length_bytes = win32file.ReadFile(pipe, 4)
                if result != 0:
                    self.error = f"Failed to read length: error {result}"
                    self.event.set()
                    return

                data_length = struct.unpack('<I', length_bytes)[0]

                # Read actual data
                result, data = win32file.ReadFile(pipe, data_length)
                if result != 0:
                    self.error = f"Failed to read data: error {result}"
                    self.event.set()
                    return

                self.response_data = data.decode('utf-8')

            finally:
                win32file.CloseHandle(pipe)

        except Exception as e:
            self.error = str(e)
        finally:
            self.event.set()

    def wait_for_response(self):
        """Wait for response with timeout."""
        if self.event.wait(self.timeout):
            if self.error:
                raise Exception(f"Pipe server error: {self.error}")
            return self.response_data
        else:
            raise TimeoutError(f"No response received within {self.timeout} seconds")


def query_mount_points(timeout=10):
    """
    Query mount points from Dokan Mirror Manager.

    Args:
        timeout: Timeout in seconds for waiting for response (default 10)

    Returns:
        dict: Parsed JSON response containing mount point information

    Raises:
        Exception: If Dokan Mirror Manager is not running or query fails
        TimeoutError: If no response received within timeout
    """

    # 1. Find the Dokan Mirror Manager window
    hwnd_handle = win32gui.FindWindow(None, "Dokan Mirror Manager")
    if hwnd_handle == 0:
        raise Exception("Dokan Mirror Manager is not running or window not found")

    # Convert to integer for ctypes
    hwnd = int(hwnd_handle)

    # 2. Create a unique Named Pipe name
    pipe_name = f"DokanMirrorManager_Query_{uuid.uuid4().hex}"

    # 3. Start Named Pipe server
    pipe_server = NamedPipeServer(pipe_name, timeout)
    pipe_server.start()

    # Give the pipe server a moment to initialize
    time.sleep(0.1)

    # 4. Send WM_COPYDATA message with pipe name
    try:
        # Encode pipe name as Unicode (UTF-16 LE) with null terminator
        pipe_name_with_null = pipe_name + '\0'
        pipe_name_bytes = pipe_name_with_null.encode('utf-16le')

        # Allocate buffer for pipe name
        buffer = create_string_buffer(pipe_name_bytes)

        # Create COPYDATASTRUCT
        cds = COPYDATASTRUCT()
        cds.dwData = WM_GET_MOUNT_POINTS  # Just use the integer directly
        cds.cbData = len(pipe_name_bytes)
        cds.lpData = cast(buffer, LPVOID)

        # Debug output
        print(f"[DEBUG] Pipe name: {pipe_name}")
        print(f"[DEBUG] Pipe name bytes length: {len(pipe_name_bytes)}")
        print(f"[DEBUG] Window handle: 0x{hwnd:X}")
        print(f"[DEBUG] dwData: 0x{cds.dwData:X}")
        print(f"[DEBUG] cbData: {cds.cbData}")
        print(f"[DEBUG] lpData: 0x{cast(buffer, c_void_p).value:X}")
        print(f"[DEBUG] Sending WM_COPYDATA...")

        # Send message - use pointer to structure
        result = SendMessage(hwnd, WM_COPYDATA, 0, addressof(cds))

        print(f"[DEBUG] SendMessage result: {result}")

    except Exception as e:
        raise Exception(f"Failed to send WM_COPYDATA: {e}")

    # 5. Wait for response
    json_response = pipe_server.wait_for_response()

    if json_response is None:
        raise Exception("No response received")

    # 6. Parse JSON
    try:
        response = json.loads(json_response)
        return response
    except json.JSONDecodeError as e:
        raise Exception(f"Failed to parse JSON response: {e}")


def print_mount_points(response):
    """Pretty print mount points from response."""

    if not response.get('success', False):
        print(f"❌ Query failed: {response.get('error', 'Unknown error')}")
        return

    mount_points = response.get('mountPoints', [])

    if not mount_points:
        print("ℹ️  No mount points configured")
        return

    print(f"\n✅ Found {len(mount_points)} mount point(s):\n")
    print("=" * 80)

    for i, mp in enumerate(mount_points, 1):
        print(f"\n#{i}")
        print(f"  Source Path:    {mp.get('srcPath', 'N/A')}")
        print(f"  Destination:    {mp.get('dstPath', 'N/A')}")
        print(f"  Drive Name:     {mp.get('driveName', 'N/A')}")
        print(f"  Status:         {mp.get('status', 'N/A')}")
        print(f"  Read-Only:      {mp.get('isReadOnly', False)}")
        print(f"  Auto-Mount:     {mp.get('autoMount', False)}")

        error_msg = mp.get('errorMessage', '')
        if error_msg:
            print(f"  Error:          {error_msg}")

    print("\n" + "=" * 80)
    print(f"\nTimestamp: {response.get('timestamp', 'N/A')}")
    print(f"API Version: {response.get('version', 'N/A')}")


def main():
    """Main entry point."""

    print("=" * 80)
    print("Dokan Mirror Manager - Mount Point Query Client (Python)")
    print("=" * 80)
    print()

    try:
        print("🔍 Querying mount points from Dokan Mirror Manager...")
        response = query_mount_points(timeout=10)

        print_mount_points(response)

        # Optionally print raw JSON
        print("\n📄 Raw JSON Response:")
        print(json.dumps(response, indent=2))

    except TimeoutError as e:
        print(f"\n⏱️  Timeout: {e}")
        print("\nPossible causes:")
        print("  - Dokan Mirror Manager is not responding")
        print("  - Application is busy or frozen")
        print("  - Named Pipe communication issue")

    except Exception as e:
        print(f"\n❌ Error: {e}")
        print("\nPossible causes:")
        print("  - Dokan Mirror Manager is not running")
        print("  - Window title does not match exactly")
        print("  - Permission issues")
        print("  - pywin32 not installed (pip install pywin32)")


if __name__ == "__main__":
    main()
