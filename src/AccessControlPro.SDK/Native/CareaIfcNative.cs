using System.Runtime.InteropServices;

namespace AccessControlPro.SDK.Native;

/// <summary>
/// P/Invoke declarations for CareaIfc.dll native access control SDK.
/// </summary>
public static class CareaIfcNative
{
    private const string DllName = "CareaIfc.dll";
    private const CallingConvention Convention = CallingConvention.Cdecl;
    private static bool _preloaded;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);

    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    /// <summary>
    /// Preloads CareaIfc.dll and all its native dependencies.
    /// Uses SetDllDirectory + LoadLibraryEx so Windows resolves the entire
    /// dependency chain (DoorCtrller, egcproxy, CtrlEx, etc.) from the app's bin folder.
    /// </summary>
    public static void PreloadNativeLibraries()
    {
        if (_preloaded) return;
        _preloaded = true;

        var baseDir = AppContext.BaseDirectory;
        Environment.CurrentDirectory = baseDir;

        // Tell Windows to search our bin directory for DLL dependencies
        SetDllDirectory(baseDir);

        // Explicitly load CareaIfc.dll with LOAD_WITH_ALTERED_SEARCH_PATH
        // so its transitive native dependencies are resolved from the same folder
        var dllPath = Path.Combine(baseDir, DllName);
        var handle = LoadLibraryEx(dllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        if (handle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw new DllNotFoundException(
                $"Failed to load {DllName} from {dllPath}. Win32 error: {error}");
        }

        // The vendor SDK DLLs (CareaIfc/CtrlEx/DoorCtrller/MyCPlusPlus) were shipped as
        // *debug* MFC builds — they import msvcr100d.dll + mfc100ud.dll. On certain code
        // paths (notably opening the Real-Time Monitor) an internal MFC ASSERT fires
        // AfxGetInstanceHandle() with a NULL module handle, popping a modal
        // "Microsoft Visual C++ Debug Library — Debug Assertion Failed" dialog that blocks
        // the whole app until the operator clicks a button. A release build of the same DLL
        // would simply carry on. We can't rebuild the vendor binaries, so we neutralise the
        // dialog: route the debug CRT's assert/error reports away from the modal window
        // (WNDW) to the debugger channel, which makes _CrtDbgReport return 0 = "continue"
        // when no debugger is attached — i.e. the assert becomes a no-op, matching release
        // behaviour. msvcr100d.dll is a single shared instance for ALL vendor DLLs in this
        // process, so one call covers every one of them (and MFC's wide-char asserts too,
        // since the report mode is shared across the ANSI and wide report paths).
        SuppressNativeDebugAssertions();
    }

    // --- Debug-CRT assertion suppression (see PreloadNativeLibraries above) -----------------
    private const int _CRT_ERROR = 1;
    private const int _CRT_ASSERT = 2;
    private const int _CRTDBG_MODE_DEBUG = 0x2; // OutputDebugString channel (no modal dialog)

    [DllImport("msvcr100d.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int _CrtSetReportMode(int reportType, int reportMode);

    /// <summary>
    /// Redirects the debug CRT's assertion/error reports off the modal dialog so a vendor-DLL
    /// MFC ASSERT can never freeze the app (matching what a Release build of the DLL would do).
    /// Best-effort: silently no-ops if msvcr100d.dll is absent.
    ///
    /// ⚠️ NO one-time guard — this MUST be re-appliable. The vendor SDK ships DEBUG MFC binaries,
    /// and MFC resets the CRT report mode back to the modal "Debug Assertion Failed" dialog every
    /// time it re-initialises (e.g. each time the Real-Time Monitor opens / a new ConnectMain window
    /// is created). A single call at startup is therefore undone the moment the Monitor is opened a
    /// SECOND time — which is exactly when operators saw the popup return and the till froze. So we
    /// re-call this right before every assert-prone native path (see StartMonitoring). The call is
    /// cheap and idempotent, so re-applying it liberally is safe.
    /// </summary>
    public static void SuppressNativeDebugAssertions()
    {
        try
        {
            _CrtSetReportMode(_CRT_ASSERT, _CRTDBG_MODE_DEBUG);
            _CrtSetReportMode(_CRT_ERROR, _CRTDBG_MODE_DEBUG);
        }
        catch
        {
            // msvcr100d.dll not present / entry point missing — nothing to suppress.
        }
    }

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr initNet(string title);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr clearNet();

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr install(bool initialize, string[] s, int door);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr readDevNowTime(string[] s, string info);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr calibrationTime(string[] s);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr updateIP(string[] s);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr openTimeDelay(string[] s, int optFlag, string[] netIP, string openDoorTime);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr remoteOpen(string sn, string ip, int port, string pwd, int[] portNum, int accessCount, int monitor);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr setTimes(string[] s, int optFlag, int timeNum, string timePieces);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr regularTime(string[] s, int optFlag, string[] netIP, string[] oftenopenDoorTime);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr addUnSortCard(string DevSN, string ip, int port, string password, int cardCount, string cardNo,
        string cardPassword, int openmode, IntPtr ioFlag, int openTimeCount, string openLock, string permitTime,
        string timePieceIndex, int holidayEnable);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr setOpenDoorPwd(string[] s, string openlock);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr readOpenDoorPwd(string[] s, string openlock);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr clearAllDoorPwd(string[] s);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr getDevInfo(string devSN, string ip, int port, string password, string info);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr setAntiSneakBack(int zt, string[] s, string doorselect);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr policeOfficer(string[] s, int alarmAction);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string fireAlarm(string[] s, string FormText);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int getRecord(string devSN, string ip, int port, string password, int recTypeIndex);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string searchDev();

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int MultiCardVerifyMode(string[] s, int portNum, int creditMode, int antiMode);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int MultiCardOpenMode(string[] s, int portNum, int creditMode, int countA, int countB);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int ABOpenMode(string[] s, int groupType, int groupNumber, int cardCount, string cardInfo);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int FixOpenMode(string[] s, int portNum, int groupType, int groupNumber, int cardCount, string cardInfo);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string MultiGetCardVerifyMode(string[] s, int portNum, string info);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string getGroupModeInfo(string[] s, int portNum, string info);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr simpleInterlock(string[] s, int[] portNum);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int AreaInterlock(string[] s, int enabled, int type, int portNum, string homeSN, string homeIP, int num);
}
