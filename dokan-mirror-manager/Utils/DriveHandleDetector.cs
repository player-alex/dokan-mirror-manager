using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace DokanMirrorManager.Utils;

/// <summary>
/// Detects processes that are using a specific drive
/// </summary>
public static class DriveHandleDetector
{
    /// <summary>
    /// Gets a list of process names that are currently using the specified drive
    /// </summary>
    /// <param name="driveLetter">Drive letter (e.g., "Z:\\")</param>
    /// <returns>List of process names</returns>
    public static List<string> GetProcessesUsingDrive(string driveLetter)
    {
        var processes = new HashSet<string>();

        try
        {
            // Normalize drive letter
            if (string.IsNullOrEmpty(driveLetter))
                return new List<string>();

            var drive = driveLetter.TrimEnd('\\');

            // Method 1: Check all running processes for handles to files on the drive
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    // Skip system processes and processes we can't access
                    if (process.Id == 0 || process.Id == 4)
                        continue;

                    // Check if process has any file handles on the drive
                    // This is a simplified check - we just verify if the process can access the drive
                    if (IsProcessUsingDrive(process, drive))
                    {
                        processes.Add($"{process.ProcessName} (PID: {process.Id})");
                    }
                }
                catch
                {
                    // Skip processes we can't access (access denied, etc.)
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Method 2: Use WMI to find processes with current directory on the drive
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT ProcessId, Name, ExecutablePath FROM Win32_Process");

                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var executablePath = obj["ExecutablePath"]?.ToString();
                        if (!string.IsNullOrEmpty(executablePath) &&
                            executablePath.StartsWith(drive, StringComparison.OrdinalIgnoreCase))
                        {
                            var name = obj["Name"]?.ToString();
                            var pid = obj["ProcessId"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                processes.Add($"{name} (PID: {pid})");
                            }
                        }
                    }
                    catch
                    {
                        // Skip if we can't read process info
                    }
                }
            }
            catch
            {
                // WMI might fail, continue with what we have
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error detecting processes using drive: {ex.Message}");
        }

        return processes.OrderBy(p => p).ToList();
    }

    private static bool IsProcessUsingDrive(Process process, string drive)
    {
        try
        {
            // Check if process main module is on the drive
            if (process.MainModule?.FileName?.StartsWith(drive, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            // Check if any loaded modules are on the drive
            foreach (ProcessModule module in process.Modules)
            {
                try
                {
                    if (module.FileName?.StartsWith(drive, StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
                catch
                {
                    // Skip modules we can't access
                }
            }
        }
        catch
        {
            // Access denied or other errors
        }

        return false;
    }

    /// <summary>
    /// Checks if any process is using the specified drive
    /// </summary>
    /// <param name="driveLetter">Drive letter (e.g., "Z:\\")</param>
    /// <returns>True if any process is using the drive</returns>
    public static bool IsDriveInUse(string driveLetter)
    {
        return GetProcessesUsingDrive(driveLetter).Any();
    }
}
