using System;
using System.Runtime.InteropServices;
using System.Text;

class TestSendCopyData
{
    const int WM_COPYDATA = 0x004A;
    const int WM_GET_MOUNT_POINTS = 0x8002;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    static void Main(string[] args)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("Test WM_COPYDATA to Dokan Mirror Manager");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Find window
        IntPtr hwnd = FindWindow(null, "Dokan Mirror Manager");
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine("❌ Window not found!");
            Console.WriteLine("Make sure Dokan Mirror Manager is running.");
            return;
        }

        Console.WriteLine($"✅ Window found: 0x{hwnd.ToInt64():X}");
        Console.WriteLine();

        // Create pipe name
        string pipeName = $"DokanMirrorManager_Query_{Guid.NewGuid():N}";
        Console.WriteLine($"Pipe name: {pipeName}");
        Console.WriteLine();

        // Encode pipe name as Unicode (UTF-16 LE)
        byte[] pipeNameBytes = Encoding.Unicode.GetBytes(pipeName + "\0");

        // Allocate unmanaged memory for pipe name
        IntPtr pipeNamePtr = Marshal.AllocHGlobal(pipeNameBytes.Length);
        try
        {
            Marshal.Copy(pipeNameBytes, 0, pipeNamePtr, pipeNameBytes.Length);

            // Create COPYDATASTRUCT
            COPYDATASTRUCT cds = new COPYDATASTRUCT
            {
                dwData = (IntPtr)WM_GET_MOUNT_POINTS,
                cbData = pipeNameBytes.Length,
                lpData = pipeNamePtr
            };

            // Allocate unmanaged memory for COPYDATASTRUCT
            IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
            try
            {
                Marshal.StructureToPtr(cds, cdsPtr, false);

                Console.WriteLine($"Sending WM_COPYDATA...");
                Console.WriteLine($"  dwData: 0x{WM_GET_MOUNT_POINTS:X}");
                Console.WriteLine($"  cbData: {pipeNameBytes.Length}");
                Console.WriteLine($"  lpData: 0x{pipeNamePtr.ToInt64():X}");
                Console.WriteLine();

                // Send message
                IntPtr result = SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, cdsPtr);

                Console.WriteLine($"SendMessage result: {result.ToInt64()}");
                Console.WriteLine();

                if (result == IntPtr.Zero)
                {
                    Console.WriteLine("⚠️  Result is 0 - this might mean the message was not processed.");
                }
                else
                {
                    Console.WriteLine("✅ Message sent successfully!");
                }

                Console.WriteLine();
                Console.WriteLine("Check wndproc.log and ipc.log in the application folder.");
            }
            finally
            {
                Marshal.FreeHGlobal(cdsPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pipeNamePtr);
        }
    }
}
