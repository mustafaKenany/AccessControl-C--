namespace AccessControlPro.SDK.Models;

public class RecordInfo
{
    public DateTime EventDate { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public int DoorNumber { get; set; }
    public int RecordType { get; set; }
    public int EventCode { get; set; }
    public int ReaderType { get; set; } // 1 = Entry, other = Exit
}
