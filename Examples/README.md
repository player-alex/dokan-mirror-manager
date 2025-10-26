# Dokan Mirror Manager - Examples

This directory contains example code and documentation for integrating with Dokan Mirror Manager.

## Available APIs

### 📁 Mount Point Query API

Query mount points from Dokan Mirror Manager using inter-process communication (IPC).

**Location**: [`MountPointQuery/`](MountPointQuery/)

**Languages**:
- C# - See [`MountPointQuery/CSharp/`](MountPointQuery/CSharp/)
- Python - See [`MountPointQuery/Python/`](MountPointQuery/Python/)

**Quick Start**:
```bash
# Python
cd MountPointQuery/Python
pip install -r requirements.txt
python mount_point_query_client.py

# C#
# Copy MountPointQuery/CSharp/MountPointQueryClient.cs to your project
```

## Documentation

- **`IPC_USAGE.md`** - Detailed IPC API documentation
- Each API directory has its own README with specific instructions

## Future APIs

This directory will be expanded with additional APIs as features are added:

- 🔄 Mount/Unmount Control API *(planned)*
- 📢 Event Notification API *(planned)*
- ⚙️ Configuration Management API *(planned)*
- 📊 Status Monitoring API *(planned)*

## General Information

### Communication Protocols

Currently supported:
- **Windows Messages** (WM_COPYDATA) - For sending requests
- **Named Pipes** - For receiving responses
- **JSON** - Response format

### Security

- All IPC mechanisms are local-only
- No authentication by default (assumes trusted local environment)
- Named Pipes use Windows default security
- See individual API documentation for security considerations

### Compatibility

- **OS**: Windows 10 1803+
- **Languages**: C#, Python (more coming)
- **API Version**: 1.0

## Contributing Examples

To add examples in other languages:

1. Create a subdirectory under the appropriate API folder
2. Include complete working example code
3. Add README with usage instructions
4. Update parent README files

Example structure:
```
Examples/
├── MountPointQuery/
│   ├── CSharp/
│   ├── Python/
│   ├── Rust/          ← New language
│   │   ├── README.md
│   │   └── client.rs
│   └── README.md
└── README.md
```

## Support

For issues or questions:
- Check API-specific README files
- Review `IPC_USAGE.md` for detailed documentation
- Check application logs in installation directory
- Open an issue on GitHub
