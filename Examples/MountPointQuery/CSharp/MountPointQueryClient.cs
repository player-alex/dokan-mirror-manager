// Example client code for querying mount points from Dokan Mirror Manager
// This file demonstrates how to send a WM_COPYDATA message to request mount point information

using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DokanMirrorManager.Examples;

/// <summary>
/// Example client for querying mount points from Dokan Mirror Manager via IPC.
/// </summary>
public class MountPointQueryClient
{
    // Windows message constants
    private const int WM_COPYDATA = 0x004A;
    private const int WM_GET_MOUNT_POINTS = 0x8002;

    // P/Invoke declarations
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    /// <summary>
    /// Query mount points from Dokan Mirror Manager.
    /// </summary>
    /// <param name="timeout">Timeout for waiting for response (default 10 seconds).</param>
    /// <returns>JSON string containing mount point information.</returns>
    public static async Task<string?> QueryMountPointsAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);

        // 1. Find the Dokan Mirror Manager window
        IntPtr hwnd = FindWindow(null, "Dokan Mirror Manager");
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Dokan Mirror Manager is not running or window not found.");
        }

        // 2. Create a unique Named Pipe for receiving the response
        var pipeName = $"DokanMirrorManager_Query_{Guid.NewGuid():N}";
        var fullPipeName = $@"\\.\pipe\{pipeName}";

        // 3. Start Named Pipe server to receive response
        var responseTask = ReceiveResponseAsync(pipeName, timeout.Value);

        // 4. Send WM_COPYDATA message with the pipe name
        SendQueryMessage(hwnd, pipeName);

        // 5. Wait for response
        return await responseTask;
    }

    /// <summary>
    /// Sends the query message via WM_COPYDATA.
    /// </summary>
    private static void SendQueryMessage(IntPtr hwnd, string pipeName)
    {
        // Encode pipe name as Unicode string
        var pipeNameBytes = Encoding.Unicode.GetBytes(pipeName);
        var pipeNamePtr = IntPtr.Zero;

        try
        {
            // Allocate unmanaged memory for pipe name
            pipeNamePtr = Marshal.AllocHGlobal(pipeNameBytes.Length);
            Marshal.Copy(pipeNameBytes, 0, pipeNamePtr, pipeNameBytes.Length);

            // Create COPYDATASTRUCT
            var copyData = new COPYDATASTRUCT
            {
                dwData = new IntPtr(WM_GET_MOUNT_POINTS),
                cbData = pipeNameBytes.Length,
                lpData = pipeNamePtr
            };

            // Send message
            SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, ref copyData);
        }
        finally
        {
            if (pipeNamePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pipeNamePtr);
            }
        }
    }

    /// <summary>
    /// Receives response from Named Pipe server.
    /// </summary>
    private static async Task<string?> ReceiveResponseAsync(string pipeName, TimeSpan timeout)
    {
        using var pipe = new NamedPipeServerStream(
            pipeName: pipeName,
            direction: PipeDirection.In,
            maxNumberOfServerInstances: 1,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous);

        // Wait for client connection with timeout
        var connectTask = pipe.WaitForConnectionAsync();
        var timeoutTask = Task.Delay(timeout);

        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException($"No response received within {timeout.TotalSeconds} seconds");
        }

        await connectTask; // Ensure any exceptions are thrown

        // Read data length (4 bytes)
        var lengthBytes = new byte[4];
        await pipe.ReadAsync(lengthBytes, 0, 4);
        var dataLength = BitConverter.ToInt32(lengthBytes, 0);

        // Read actual data
        var buffer = new byte[dataLength];
        var totalRead = 0;

        while (totalRead < dataLength)
        {
            var read = await pipe.ReadAsync(buffer, totalRead, dataLength - totalRead);
            if (read == 0)
            {
                throw new IOException("Unexpected end of stream");
            }
            totalRead += read;
        }

        // Convert to string
        return Encoding.UTF8.GetString(buffer);
    }
}

// Example usage in a console application or service:
public class ExampleUsage
{
    public static async Task Main()
    {
        try
        {
            Console.WriteLine("Querying mount points from Dokan Mirror Manager...");

            var json = await MountPointQueryClient.QueryMountPointsAsync();

            if (json != null)
            {
                Console.WriteLine("Response received:");
                Console.WriteLine(json);

                // Parse JSON response
                var response = JsonSerializer.Deserialize<MountPointsResponse>(json);

                if (response?.Success == true)
                {
                    Console.WriteLine($"\nFound {response.MountPoints.Count} mount points:");

                    foreach (var mp in response.MountPoints)
                    {
                        Console.WriteLine($"  - Source: {mp.SrcPath}");
                        Console.WriteLine($"    Destination: {mp.DstPath}");
                        Console.WriteLine($"    Status: {mp.Status}");
                        Console.WriteLine($"    Read-Only: {mp.IsReadOnly}");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine($"Query failed: {response?.Error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

// Response data structures (should match server-side DTOs)
public class MountPointsResponse
{
    public List<MountPointInfo> MountPoints { get; set; } = new();
    public string Timestamp { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

public class MountPointInfo
{
    public string SrcPath { get; set; } = string.Empty;
    public string DstPath { get; set; } = string.Empty;
    public string DriveName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool AutoMount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
