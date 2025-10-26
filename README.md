<div align="center">

# ☁️ Dokan Mirror Manager

![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net&logoColor=white)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-UI-0078D6?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

**A modern WPF application for managing Dokan file system mirrors with an intuitive user interface** 🚀

[Features](#-features) • [Installation](#-installation) • [Usage](#-usage) • [Building](#-building-from-source)

</div>

---

## ✨ Features

- 🗂️ **Multiple Mount Management** - Create and manage multiple mirror mounts simultaneously
- 💾 **Smart Drive Letter Selection** - Automatically assigns available drive letters, prevents conflicts
- 🤖 **Auto-Mount on Startup** - Configure mounts to automatically mount when the application starts
- 🔒 **Read-Only Mode** - Mount mirrors in read-only mode for data protection (enabled by default)
- 🌍 **Environment Variable Support** - Use environment variables in paths (e.g., `%USERPROFILE%\Desktop`)
- 🎯 **System Tray Support** - Minimize to system tray with notification support
- ⚡ **Single Instance** - Only one application instance runs at a time; launching again restores the window
- 🔄 **Auto-Unmount** - Automatically unmount drives on application exit with confirmation
- 💾 **Persistent Configuration** - Save and restore mount configurations between sessions
- 🏷️ **Dynamic Volume Labels** - Automatically sets volume labels based on source (drive label or folder name)
- 🎨 **Modern UI** - Built with MahApps.Metro for a clean, modern interface
- 🚀 **Non-Blocking Operations** - Async mount/unmount operations keep UI responsive
- ⏱️ **Smart Unmount** - Progress tracking with timeout handling and background completion
- 🔌 **IPC API** - Query mount points from external applications via WM_COPYDATA and Named Pipes

## 📸 Screenshots

![Main Window](screenshot.png)

## 📋 Requirements

- 🪟 **Windows 10/11** (x64)
- 🔷 **.NET 8.0 Runtime** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- 📦 **[Dokan Driver](https://github.com/dokan-dev/dokany/releases)** - User mode file system library for Windows
- 🔑 **Administrator privileges** - Required for mount operations

## 📥 Installation

1. **Download** the latest release from the [Releases](../../releases) page
2. **Install** [Dokan Driver](https://github.com/dokan-dev/dokany/releases) if not already installed
3. **Extract** the downloaded archive
4. **Run** `dokan-mirror-manager.exe` as Administrator

> ⚠️ **Important**: The application must be run with Administrator privileges to perform mount operations.

## 📖 Usage

### ➕ Adding a Mount

1. Click the **Add** button
2. Select a source directory or drive using the folder browser
3. A drive letter is **automatically assigned** from available letters
4. Optionally enable **Auto Mount** to automatically mount on application startup
5. **Read Only** mode is enabled by default for safety
6. Click the **Mount** 📂 button to mount the drive

### 🔄 Mounting/Unmounting

- **Mount**: Click the folder open icon (📂) next to the mount item
- **Unmount**: Click the folder remove icon (🗑️) next to the mounted item
  - Shows elapsed time during unmount: "Unmounting Z:\... (5s)"
  - If unmount takes longer than expected (>10s), displays timeout dialog with options
  - Can continue in background if needed

### ❌ Removing a Mount

1. Select an **unmounted** item from the list
2. Click the **Remove** button

> 💡 **Tip**: You cannot remove a mounted drive. Unmount it first.

### 🎯 System Tray

- **Minimize**: Close the window to minimize to system tray (shows notification)
- **Restore**: Click the tray icon or launch the application again
- **Exit**: Right-click tray icon → Exit (prompts to unmount drives)

### 🔐 Single Instance

The application runs as a single instance. If you try to launch it while already running:
- If **visible**: Nothing happens (already running)
- If **in tray**: Window automatically restores and comes to front

## 🛠️ Building from Source

### Prerequisites

- 🔧 **Visual Studio 2022** or later (or VS Code with C# extension)
- 🔷 **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- 📦 **Dokan Driver** (for testing) - [Download](https://github.com/dokan-dev/dokany/releases)

### Build Steps

```bash
# Clone the repository
git clone https://github.com/player-alex/dokan-mirror-manager.git
cd dokan-mirror-manager

# Restore dependencies
dotnet restore

# Build the project (Release configuration)
dotnet build -c Release

# Or build for x64 specifically
dotnet build -c Release --arch x64
```

### Run from Source

```bash
# Run as Administrator (required for Dokan operations)
dotnet run --project dokan-mirror-manager/dokan-mirror-manager.csproj
```

> ⚠️ **Note**: Must be run as Administrator for Dokan operations.

## ⚙️ Configuration

Mount configurations are automatically saved to `mounts.json` in the application directory.

**Configuration includes:**
- 📁 Source paths (supports environment variables)
- 💾 Destination drive letters
- 🤖 Auto-mount settings
- 🔒 Read-only settings

The configuration file is created automatically on first mount and updated whenever changes are made.

### Environment Variables

You can use Windows environment variables in source paths within `mounts.json`:

```json
[
  {
    "SourcePath": "%USERPROFILE%\\Desktop",
    "DestinationLetter": "Z:\\",
    "AutoMount": true,
    "IsReadOnly": true
  }
]
```

**Supported variables:**
- `%USERPROFILE%` - User's home directory
- `%APPDATA%` - Application data folder
- `%LOCALAPPDATA%` - Local application data folder
- `%ProgramFiles%` - Program Files directory
- `%TEMP%` - Temporary files directory
- Any other Windows environment variables

### Smart Drive Letter Management

The application automatically manages drive letters to prevent conflicts:
- **Auto-Assignment**: Automatically assigns available drive letters when adding new mounts
- **Conflict Resolution**: If a drive letter is already in use, automatically selects the next available one
- **Duplicate Handling**: When loading from `mounts.json`, duplicates are automatically resolved
- **Dynamic Updates**: Drive letter dropdowns update in real-time based on availability

## 🔌 IPC API

Dokan Mirror Manager provides an IPC (Inter-Process Communication) API that allows external applications to query mount point information.

### API Overview

The API uses Windows messages (`WM_COPYDATA`) and Named Pipes for communication:

1. **Client** finds the application window by title: `"Dokan Mirror Manager"`
2. **Client** sends `WM_COPYDATA` message with a unique pipe name
3. **Server** responds via the specified Named Pipe with JSON data

### Protocol Details

**Message:** `WM_COPYDATA (0x004A)`
- `dwData`: `0x8002` (WM_GET_MOUNT_POINTS)
- `lpData`: UTF-16LE encoded pipe name (with null terminator)
- `cbData`: Byte length of pipe name (including null terminator)

**Response Format (JSON):**
```json
{
  "success": true,
  "mountPoints": [
    {
      "srcPath": "C:\\SourceFolder",
      "dstPath": "Z:\\",
      "driveName": "SourceFolder",
      "status": "Mounted",
      "isReadOnly": true,
      "autoMount": false,
      "errorMessage": ""
    }
  ],
  "timestamp": "2025-10-26T18:00:00.000Z",
  "version": "1.0"
}
```

### Python Example

See [Examples/MountPointQuery/Python/mount_point_query_client.py](Examples/MountPointQuery/Python/mount_point_query_client.py) for a complete implementation:

```python
import win32gui
from ctypes import *

# Find application window
hwnd = win32gui.FindWindow(None, "Dokan Mirror Manager")

# Create Named Pipe name
pipe_name = f"DokanMirrorManager_Query_{uuid.uuid4().hex}"

# Send WM_COPYDATA request
# ... (see example file for complete code)

# Receive response via Named Pipe
# Response contains JSON with mount point information
```

### C# Example

See [Examples/MountPointQuery/CSharp/TestSendCopyData/Program.cs](Examples/MountPointQuery/CSharp/TestSendCopyData/Program.cs) for a complete C# implementation.

### Status Values

| Status | Description |
|--------|-------------|
| `Unmounted` | Not currently mounted |
| `Mounting` | Mount operation in progress |
| `Mounted` | Successfully mounted |
| `Unmounting` | Unmount operation in progress |
| `Error` | Mount operation failed |

### Security Notes

- **UIPI (User Interface Privilege Isolation)**: The application uses `ChangeWindowMessageFilterEx` to allow `WM_COPYDATA` messages from processes with different privilege levels
- **Named Pipes**: Each request uses a unique pipe name to prevent conflicts
- **Timeout**: The server has a 10-second timeout for pipe connections

## 🔧 Technologies Used

| Technology | Purpose | Version |
|------------|---------|---------|
| [.NET](https://dotnet.microsoft.com/) | Application framework | 8.0 |
| [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) | UI framework | - |
| [Caliburn.Micro](https://caliburnmicro.com/) | MVVM framework | 5.0.258 |
| [MahApps.Metro](https://mahapps.com/) | Modern UI toolkit | 2.4.11 |
| [DokanNet](https://github.com/dokan-dev/dokan-dotnet) | .NET wrapper for Dokan | 2.3.0.1 |
| [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) | System tray support | 2.0.1 |

## 🐛 Troubleshooting

<details>
<summary><b>❌ Application crashes on mount</b></summary>

- ✅ Ensure Dokan Driver is properly installed
- ✅ Run the application as Administrator
- ✅ Check `mount_error.log` in the application directory for details
- ✅ Verify the source path exists and is accessible

</details>

<details>
<summary><b>💾 Drive letter not available</b></summary>

- ✅ Make sure the drive letter is not already in use
- ✅ Check Windows Disk Management for assigned letters
- ✅ Try a different drive letter

</details>

<details>
<summary><b>🚫 Application won't start</b></summary>

- ✅ Verify .NET 8.0 Runtime is installed
- ✅ Check `crash.log` in the application directory
- ✅ Ensure you're running as Administrator
- ✅ Try running from command line to see error messages

</details>

<details>
<summary><b>⚠️ Already running message appears</b></summary>

This is normal behavior. The application uses single-instance mode:
- If the window is hidden in tray, it will be restored automatically
- If the window is already visible, the existing instance continues running

</details>

<details>
<summary><b>⏱️ Unmount is taking a long time</b></summary>

If unmount takes longer than expected:
- ✅ Close any programs accessing files on the drive (Explorer, cmd, etc.)
- ✅ Wait for the timeout dialog to show processes using the drive
- ✅ Choose to continue waiting or let it complete in background
- ✅ The drive will unmount automatically when all file handles are released

</details>

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Dokan Project](https://github.com/dokan-dev/dokany) - User mode file system library for Windows
- <a href="https://www.flaticon.com/free-icons/web-server" title="web server icons">Application icon created by Muhamad Ulum - Flaticon</a>

---