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

    /// <summary>
    /// Preloads CareaIfc.dll using LoadLibraryEx with LOAD_WITH_ALTERED_SEARCH_PATH.
    /// This tells Windows to resolve CareaIfc.dll's own dependencies (DoorCtrller, egcproxy, etc.)
    /// from the same directory where CareaIfc.dll is located.
    /// </summary>
    public static void PreloadNativeLibraries()
    {
        if (_preloaded) return;
        _preloaded = true;

        var baseDir = AppContext.BaseDirectory;

        // Preload all SDK DLLs in dependency order
        string[] dlls = ["msvcr100d.dll", "msvcp100d.dll", "mfc100ud.dll",
            "CtrlEx.dll", "util.dll", "DataTool.dll", "regex2.dll",
            "cmproxy.dll", "netproxy.dll", "dbproxy.dll",
            "DoorCtrller.dll", "egcproxy.dll", "MyCPlusPlus.dll",
            "CareaIfc.dll"];

        foreach (var dll in dlls)
        {
            var fullPath = Path.Combine(baseDir, dll);
            if (File.Exists(fullPath))
                LoadLibraryEx(fullPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        }
    }

    private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr initNet(string title);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr clearNet();

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr install(bool initialize, string[] s, int door);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string readDevNowTime(string[] s, string info);

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
    public static extern string getDevInfo(string devSN, string ip, int port, string password, string info);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr setAntiSneakBack(int zt, string[] s, string doorselect);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern IntPtr policeOfficer(string[] s, int alarmAction);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string fireAlarm(string[] s, string FormText);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern int getRecord(string devSN, string ip, int port, string password, int recTypeIndex);

    [DllImport(DllName, CharSet = CharSet.Unicode, CallingConvention = Convention)]
    public static extern string devInfo2();

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
