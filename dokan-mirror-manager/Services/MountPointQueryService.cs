using DokanMirrorManager.Models;
using DokanMirrorManager.Services.Interfaces;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace DokanMirrorManager.Services;

/// <summary>
/// Implementation of mount point query service for IPC communication.
/// </summary>
public class MountPointQueryService : IMountPointQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task HandleMountPointQueryAsync(
        string pipeName,
        ObservableCollection<MountItem> mountItems,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create response object
            var response = CreateResponse(mountItems);

            // Serialize to JSON
            var json = JsonSerializer.Serialize(response, JsonOptions);
            var data = Encoding.UTF8.GetBytes(json);

            // Send via Named Pipe
            await SendViaPipeAsync(pipeName, data, cancellationToken);
        }
        catch
        {
            // Silently ignore IPC failures - they should not crash the app
        }
    }

    /// <inheritdoc/>
    public MountPointsResponse CreateResponse(ObservableCollection<MountItem> mountItems)
    {
        var response = new MountPointsResponse
        {
            Timestamp = DateTime.UtcNow.ToString("o"), // ISO 8601 format
            Version = "1.0",
            Success = true
        };

        foreach (var item in mountItems)
        {
            var info = new MountPointInfo
            {
                SrcPath = item.SourcePath,
                DstPath = item.DestinationLetter,
                DriveName = string.IsNullOrEmpty(item.DestinationLetter)
                    ? string.Empty
                    : $"Mirror Drive {item.DestinationLetter}",
                Status = item.Status.ToString(),
                IsReadOnly = item.IsReadOnly,
                AutoMount = item.AutoMount,
                ErrorMessage = item.ErrorMessage
            };

            response.MountPoints.Add(info);
        }

        return response;
    }

    /// <summary>
    /// Sends data to a Named Pipe client.
    /// </summary>
    private async Task SendViaPipeAsync(string pipeName, byte[] data, CancellationToken cancellationToken)
    {
        // Named Pipe client connection with timeout
        using var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: PipeDirection.Out);

        // Connect with 5 second timeout
        var connectTask = pipe.ConnectAsync(cancellationToken);
        var timeoutTask = Task.Delay(5000, cancellationToken);

        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException($"Failed to connect to Named Pipe '{pipeName}' within 5 seconds");
        }

        await connectTask; // Ensure any exceptions are thrown

        // Write data length first (4 bytes)
        var lengthBytes = BitConverter.GetBytes(data.Length);
        await pipe.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);

        // Write actual data
        await pipe.WriteAsync(data, 0, data.Length, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
    }
}
