# IPC API: Querying Mount Points

Dokan Mirror Manager supports inter-process communication (IPC) for external applications to query the current mount points. This allows services, background processes, or other applications to retrieve information about mounted drives.

## Overview

The IPC mechanism uses:
- **Windows Messages**: WM_COPYDATA for sending requests
- **Named Pipes**: For receiving JSON responses
- **Message ID**: `WM_GET_MOUNT_POINTS` (0x8002)

## How It Works

1. External process finds the Dokan Mirror Manager window by title: `"Dokan Mirror Manager"`
2. Creates a unique Named Pipe server to receive the response
3. Sends a `WM_COPYDATA` message with:
   - `dwData` = `0x8002` (WM_GET_MOUNT_POINTS)
   - `lpData` = Named Pipe name (Unicode string)
   - `cbData` = Length of pipe name in bytes
4. Waits for response on the Named Pipe
5. Receives JSON response containing all mount points

## Response Format

The response is a JSON object with the following structure:

```json
{
  "mountPoints": [
    {
      "srcPath": "C:\\Source\\Folder",
      "dstPath": "M:\\",
      "driveName": "Mirror Drive M:\\",
      "status": "Mounted",
      "isReadOnly": true,
      "autoMount": true,
      "errorMessage": ""
    }
  ],
  "timestamp": "2025-10-26T12:34:56.789Z",
  "version": "1.0",
  "success": true,
  "error": null
}
```

### Fields

- **mountPoints**: Array of mount point objects
  - **srcPath**: Source directory being mirrored
  - **dstPath**: Destination drive letter (e.g., "M:\\")
  - **driveName**: Human-readable drive name
  - **status**: Current status - "Unmounted", "Mounting", "Mounted", or "Error"
  - **isReadOnly**: Whether the mount is read-only
  - **autoMount**: Whether auto-mount is enabled
  - **errorMessage**: Error details if status is "Error"
- **timestamp**: ISO 8601 timestamp of when the response was generated
- **version**: API version for compatibility checking
- **success**: Boolean indicating if the query succeeded
- **error**: Error message if success is false

## Example Usage (C#)

See `MountPointQueryClient.cs` for a complete implementation. Basic usage:

```csharp
using DokanMirrorManager.Examples;

// Query mount points
var json = await MountPointQueryClient.QueryMountPointsAsync();

// Parse response
var response = JsonSerializer.Deserialize<MountPointsResponse>(json);

if (response?.Success == true)
{
    foreach (var mp in response.MountPoints)
    {
        Console.WriteLine($"Source: {mp.SrcPath}");
        Console.WriteLine($"Destination: {mp.DstPath}");
        Console.WriteLine($"Status: {mp.Status}");
    }
}
```

## Example Usage (Other Languages)

### Python

```python
import win32gui
import win32con
import json
from ctypes import *
from ctypes.wintypes import *

# Define COPYDATASTRUCT
class COPYDATASTRUCT(Structure):
    _fields_ = [
        ("dwData", LPVOID),
        ("cbData", DWORD),
        ("lpData", c_void_p)
    ]

WM_COPYDATA = 0x004A
WM_GET_MOUNT_POINTS = 0x8002

# Find window
hwnd = win32gui.FindWindow(None, "Dokan Mirror Manager")
if hwnd == 0:
    raise Exception("Dokan Mirror Manager not found")

# Create Named Pipe (requires pywin32 or similar)
pipe_name = f"DokanMirrorManager_Query_{uuid.uuid4().hex}"

# Send WM_COPYDATA
pipe_name_bytes = pipe_name.encode('utf-16le')
copydata = COPYDATASTRUCT()
copydata.dwData = WM_GET_MOUNT_POINTS
copydata.cbData = len(pipe_name_bytes)
copydata.lpData = cast(pipe_name_bytes, c_void_p)

win32gui.SendMessage(hwnd, WM_COPYDATA, 0, addressof(copydata))

# Receive response from Named Pipe
# (Implementation details depend on your Named Pipe library)
```

### C++

```cpp
#include <windows.h>
#include <string>
#include <iostream>

const int WM_GET_MOUNT_POINTS = 0x8002;

std::string QueryMountPoints() {
    // Find window
    HWND hwnd = FindWindow(NULL, L"Dokan Mirror Manager");
    if (hwnd == NULL) {
        throw std::runtime_error("Dokan Mirror Manager not found");
    }

    // Generate unique pipe name
    std::wstring pipeName = L"DokanMirrorManager_Query_" + GenerateGuid();

    // Start Named Pipe server (async)
    // ...

    // Send WM_COPYDATA
    COPYDATASTRUCT cds;
    cds.dwData = WM_GET_MOUNT_POINTS;
    cds.cbData = pipeName.size() * sizeof(wchar_t);
    cds.lpData = (PVOID)pipeName.c_str();

    SendMessage(hwnd, WM_COPYDATA, 0, (LPARAM)&cds);

    // Receive response from Named Pipe
    // ...

    return jsonResponse;
}
```

## Error Handling

### Common Errors

1. **Window Not Found**: Dokan Mirror Manager is not running
   - Ensure the application is started and visible in system tray

2. **Timeout**: No response received within timeout period
   - Check if the application is responsive
   - Increase timeout value if needed

3. **Connection Failed**: Named Pipe connection failed
   - Ensure pipe name is unique
   - Check for permission issues

### Logging

IPC errors are logged to `ipc.log` in the application directory. Check this file for debugging.

## Thread Safety

- The IPC handler is thread-safe
- Mount point data is collected on the UI thread and sent asynchronously
- Multiple concurrent queries are supported

## Performance Considerations

- Response time is typically < 100ms for normal workloads
- Large numbers of mount points (>100) may take longer
- Named Pipe overhead is minimal (~1-2ms)

## Security Considerations

- Only processes with access to the window handle can send queries
- Named Pipes use default Windows security (local system only)
- No authentication mechanism is built-in (assumes trusted local processes)

## Compatibility

- **Windows Version**: Windows 10 1803+ (for Named Pipes)
- **API Version**: 1.0
- **Breaking Changes**: None planned, version field allows future compatibility checks

## Troubleshooting

### Query Returns Empty List

- Check if any mount points are configured in Dokan Mirror Manager
- Verify the application is fully initialized (wait a few seconds after startup)

### WM_COPYDATA Not Received

- Ensure window title matches exactly: `"Dokan Mirror Manager"`
- Check if application is minimized to tray (should still work)
- Verify message constant is correct: `0x8002`

### Named Pipe Connection Fails

- Ensure pipe name is unique (use GUID or process ID)
- Check pipe name format: no `\\.\pipe\` prefix in the string sent via WM_COPYDATA
- Verify timeout is sufficient (recommended: 5-10 seconds)

## Future Enhancements

Potential future additions:
- Notification API for mount/unmount events
- Command API for programmatic mounting/unmounting
- WebSocket or REST API alternative
- Authentication/authorization mechanism
