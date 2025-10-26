# Python Mount Point Query Client

Python implementation for querying mount points from Dokan Mirror Manager.

## Files

- **`mount_point_query_client.py`** - Complete client implementation
- **`requirements.txt`** - Python dependencies

## Installation

```bash
pip install -r requirements.txt
```

This will install:
- `pywin32` - Windows API bindings

## Usage

### Basic Usage

```bash
python mount_point_query_client.py
```

### As a Library

```python
from mount_point_query_client import query_mount_points

# Query mount points
response = query_mount_points(timeout=10)

if response['success']:
    for mp in response['mountPoints']:
        print(f"Source: {mp['srcPath']}")
        print(f"Destination: {mp['dstPath']}")
        print(f"Status: {mp['status']}")
```

### Custom Timeout

```python
# Wait up to 30 seconds
response = query_mount_points(timeout=30)
```

## Requirements

- Python 3.7+
- Windows OS
- pywin32 package

## Error Handling

```python
try:
    response = query_mount_points()
    print(f"Found {len(response['mountPoints'])} mount points")
except TimeoutError as e:
    print(f"Timeout: {e}")
except Exception as e:
    print(f"Error: {e}")
```

## Integration Examples

### Script

```python
#!/usr/bin/env python3
from mount_point_query_client import query_mount_points
import json

def main():
    response = query_mount_points()
    print(json.dumps(response, indent=2))

if __name__ == '__main__':
    main()
```

### Service/Daemon

```python
import time
from mount_point_query_client import query_mount_points

def monitor_mount_points():
    """Periodically check mount points."""
    while True:
        try:
            response = query_mount_points()
            # Process mount points...
            print(f"Status: {len(response['mountPoints'])} mounts")
        except Exception as e:
            print(f"Error: {e}")

        time.sleep(300)  # Check every 5 minutes

if __name__ == '__main__':
    monitor_mount_points()
```

### Flask API

```python
from flask import Flask, jsonify
from mount_point_query_client import query_mount_points

app = Flask(__name__)

@app.route('/api/mount-points')
def get_mount_points():
    try:
        response = query_mount_points()
        return jsonify(response)
    except Exception as e:
        return jsonify({'error': str(e)}), 500

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)
```

## Troubleshooting

### ImportError: No module named 'win32gui'

Install pywin32:
```bash
pip install pywin32
```

After installation, you may need to run:
```bash
python Scripts/pywin32_postinstall.py -install
```

### Exception: Dokan Mirror Manager is not running

- Ensure the application is started
- Check system tray for application icon
- Verify process in Task Manager

### TimeoutError: No response received

- Application may be busy or unresponsive
- Try increasing timeout: `query_mount_points(timeout=30)`
- Check `ipc.log` in application directory

### Permission Denied

- Run with appropriate permissions
- Check if process is in different user session

## Development

### Running Tests

```bash
python -m pytest test_mount_point_query_client.py
```

### Linting

```bash
pip install pylint
pylint mount_point_query_client.py
```

### Type Checking

```bash
pip install mypy
mypy mount_point_query_client.py
```

## See Also

- [C# Implementation](../CSharp/)
- [API Documentation](../../IPC_USAGE.md)
- [Parent README](../README.md)
