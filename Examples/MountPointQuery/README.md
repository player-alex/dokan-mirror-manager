# Mount Point Query API

Query mount points from Dokan Mirror Manager using inter-process communication (IPC).

## Overview

This API allows external processes (services, background applications, etc.) to retrieve information about currently configured mount points in Dokan Mirror Manager.

## Communication Protocol

- **Message**: WM_COPYDATA with WM_GET_MOUNT_POINTS (0x8002)
- **Transport**: Named Pipes
- **Format**: JSON

## Available Examples

### C# Client
See [`CSharp/`](CSharp/) directory for C# implementation.

**Quick Start:**
```csharp
var json = await MountPointQueryClient.QueryMountPointsAsync();
var response = JsonSerializer.Deserialize<MountPointsResponse>(json);
```

### Python Client
See [`Python/`](Python/) directory for Python implementation.

**Quick Start:**
```bash
cd Python
pip install -r requirements.txt
python mount_point_query_client.py
```

## Response Format

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

## Status Values

- `Unmounted` - Not currently mounted
- `Mounting` - Mount operation in progress
- `Mounted` - Successfully mounted and active
- `Error` - Mount failed (check errorMessage)

## Documentation

See [API Documentation](../IPC_USAGE.md) for detailed information.
