using System;
using System.Runtime.InteropServices;
using System.IO;

class Diag
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibraryEx(string path, IntPtr hFile, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr hModule);

    static void Main()
    {
        var binDir = @"D:\AccessControlPro\src\AccessControlPro.WPF\bin\Debug\net8.0-windows";
        Console.WriteLine($"IntPtr.Size = {IntPtr.Size} ({(IntPtr.Size == 4 ? "32-bit" : "64-bit")})");
        Console.WriteLine($"Process: {Environment.ProcessPath}");
        Console.WriteLine($"Bin dir exists: {Directory.Exists(binDir)}");

        Environment.CurrentDirectory = binDir;
        SetDllDirectory(binDir);

        // Try loading each DLL in dependency order
        string[] dlls = {
            "msvcr100.dll", "msvcr100d.dll", "msvcp100d.dll",
            "mfc100.dll", "mfc100ud.dll",
            "libcrypto-1_1.dll", "libssl-1_1.dll",
            "regex2.dll", "util.dll", "cmproxy.dll", "dbproxy.dll",
            "MyCPlusPlus.dll", "netproxy.dll",
            "CtrlEx.dll", "egcproxy.dll", "DoorCtrller.dll",
            "DataTool.dll", "FCardCDrive.dll",
            "CareaIfc.dll"
        };

        foreach (var dll in dlls)
        {
            var path = Path.Combine(binDir, dll);
            if (!File.Exists(path)) { Console.WriteLine($"  FILE MISSING: {dll}"); continue; }
            var h = LoadLibraryEx(path, IntPtr.Zero, 0x00000008);
            if (h != IntPtr.Zero)
                Console.WriteLine($"  OK: {dll}");
            else
            {
                var err = Marshal.GetLastWin32Error();
                Console.WriteLine($"  FAIL: {dll} - Win32 error {err}");
            }
        }
    }
}
