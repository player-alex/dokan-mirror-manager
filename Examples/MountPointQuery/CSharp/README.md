# C# Mount Point Query Client

C# implementation for querying mount points from Dokan Mirror Manager.

## Files

- **`MountPointQueryClient.cs`** - Complete client implementation with example usage

## Usage

### 1. Copy to Your Project

Copy `MountPointQueryClient.cs` to your C# project.

### 2. Query Mount Points

```csharp
using DokanMirrorManager.Examples;
using System.Text.Json;

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

### 3. Custom Timeout

```csharp
// Wait up to 30 seconds for response
var json = await MountPointQueryClient.QueryMountPointsAsync(
    timeout: TimeSpan.FromSeconds(30));
```

## Requirements

- .NET Framework 4.7.2+ or .NET Core 3.1+
- Windows OS
- System.IO.Pipes namespace
- System.Text.Json (or Newtonsoft.Json)

## Error Handling

```csharp
try
{
    var json = await MountPointQueryClient.QueryMountPointsAsync();
    // Process response...
}
catch (InvalidOperationException ex)
{
    // Dokan Mirror Manager not running
    Console.WriteLine($"Error: {ex.Message}");
}
catch (TimeoutException ex)
{
    // No response within timeout
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (Exception ex)
{
    // Other errors
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Integration Examples

### Console Application

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var json = await MountPointQueryClient.QueryMountPointsAsync();
        Console.WriteLine(json);
    }
}
```

### Windows Service

```csharp
public class MyService : ServiceBase
{
    protected override async void OnStart(string[] args)
    {
        try
        {
            var json = await MountPointQueryClient.QueryMountPointsAsync();
            // Process mount points...
        }
        catch (Exception ex)
        {
            EventLog.WriteEntry("MyService", ex.Message, EventLogEntryType.Error);
        }
    }
}
```

### Background Task

```csharp
// Periodic polling
var timer = new System.Threading.Timer(async _ =>
{
    try
    {
        var json = await MountPointQueryClient.QueryMountPointsAsync();
        // Update internal state...
    }
    catch { /* Handle error */ }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

## Troubleshooting

### Window Not Found

- Ensure Dokan Mirror Manager is running
- Verify window title: "Dokan Mirror Manager"
- Check task manager for process

### Timeout

- Increase timeout value
- Check if application is responsive
- Review `ipc.log` in application directory

### Access Denied

- Run with appropriate permissions
- Check if process is in different session

## See Also

- [Python Implementation](../Python/)
- [API Documentation](../../IPC_USAGE.md)
- [Parent README](../README.md)
