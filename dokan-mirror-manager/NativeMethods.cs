using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DokanMirrorManager;

internal static class NativeMethods
{
    /// <summary>
    /// Sets the date and time that the specified file or directory was created, last accessed, or last modified.
    /// </summary>
    /// <param name="hFile">A <see cref="SafeFileHandle"/> to the file or directory. 
    /// To get the handler, <see cref="System.IO.FileStream.SafeFileHandle"/> can be used.</param>
    /// <param name="lpCreationTime">A Windows File Time that contains the new creation date and time 
    /// for the file or directory. 
    /// If the application does not need to change this information, set this parameter to 0.</param>
    /// <param name="lpLastAccessTime">A Windows File Time that contains the new last access date and time 
    /// for the file or directory. The last access time includes the last time the file or directory 
    /// was written to, read from, or (in the case of executable files) run. 
    /// If the application does not need to change this information, set this parameter to 0.</param>
    /// <param name="lpLastWriteTime">A Windows File Time that contains the new last modified date and time 
    /// for the file or directory. If the application does not need to change this information, 
    /// set this parameter to 0.</param>
    /// <returns>If the function succeeds, the return value is <c>true</c>.</returns>
    /// \see <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/ms724933">SetFileTime function (MSDN)</a>
    [DllImport("kernel32", SetLastError = true)]
    public static extern bool SetFileTime(SafeFileHandle hFile, ref long lpCreationTime, ref long lpLastAccessTime, ref long lpLastWriteTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetFilePointerEx(SafeFileHandle hFile, long liDistanceToMove, out long lpNewFilePointer, [MarshalAs(UnmanagedType.U4)] System.IO.SeekOrigin dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(SafeFileHandle hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out int lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

    // For detecting processes using drive
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    // Constants for file operations
    public const uint GENERIC_READ = 0x80000000;
    public const uint FILE_SHARE_READ = 0x1;
    public const uint FILE_SHARE_WRITE = 0x2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    public const IntPtr INVALID_HANDLE_VALUE = -1;

    // IOCTL for getting volume info
    public const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x00090073;
}