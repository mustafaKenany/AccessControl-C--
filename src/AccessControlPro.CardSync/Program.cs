using System.Runtime.InteropServices;
using System.IO;

namespace AccessControlPro.CardSync;

class Program
{
    // P/Invoke directly to CareaIfc.dll
    [DllImport("CareaIfc.dll", CharSet = CharSet.Unicode)]
    static extern void initNet(string language);

    [DllImport("CareaIfc.dll", CharSet = CharSet.Unicode)]
    static extern void clearNet();

    [DllImport("CareaIfc.dll", CharSet = CharSet.Unicode)]
    static extern int addUnSortCard(
        string devSN, string ip, int port, string password,
        int cardCount, string cardNo, string cardPassword,
        int cardMode, int[] ioFlag, int openCount,
        string openLock, string permitTime, string timePieceIndex,
        int holidayEnable);

    static int Main(string[] args)
    {
        // Usage: CardSync.exe <paramsFile>
        // The params file is a simple line-based format
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: CardSync.exe <paramsFile>");
            return 2;
        }

        var paramsFile = args[0];
        if (!File.Exists(paramsFile))
        {
            Console.Error.WriteLine($"Params file not found: {paramsFile}");
            return 2;
        }

        try
        {
            var lines = File.ReadAllLines(paramsFile);
            // Parse params: SN, IP, Port, Password, CardNo, CardPassword, OpenMode, DoorPermissions, PermitTime, EffectiveTimes, TimePeriodIndex, HolidayEnabled
            if (lines.Length < 12)
            {
                Console.Error.WriteLine("Invalid params file");
                return 2;
            }

            var sn = lines[0];
            var ip = lines[1];
            var port = int.Parse(lines[2]);
            var password = lines[3];
            var cardNo = lines[4];
            var cardPassword = lines[5];
            var openMode = int.Parse(lines[6]);
            var doorPermissions = lines[7]; // e.g. "01010000"
            var permitTime = lines[8];
            var effectiveTimes = int.Parse(lines[9]);
            var timePeriodIndex = lines[10];
            var holidayEnabled = int.Parse(lines[11]);

            // Parse ioFlag from door permissions
            var ioFlag = new int[4];
            for (int i = 0; i < 4 && i * 2 < doorPermissions.Length; i++)
            {
                ioFlag[i] = doorPermissions[i * 2] == '1' ? 2 : 0; // 2 = in+out, 0 = none
            }

            initNet("eng");

            var result = addUnSortCard(
                sn, ip, port, password,
                1, cardNo, cardPassword,
                openMode, ioFlag, effectiveTimes,
                doorPermissions, permitTime, timePeriodIndex,
                holidayEnabled);

            clearNet();

            Console.WriteLine(result); // Output: 1 (success) or -1 (fail)
            return result >= 1 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }
}
