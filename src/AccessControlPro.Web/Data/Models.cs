namespace AccessControlPro.Web.Data;

public record PlayerRow(int Id, string FullNameEn, string FullNameAr, string CardNo, string Phone,
    string SubscriptionType, DateTime StartDate, DateTime EndDate, decimal Fee, decimal Paid,
    int MaxVisits, int UsedVisits, bool IsFrozen, DateTime? FreezeStartDate);

public record EventRow(int Id, int DoorId, int? CardId, int EventCode, DateTime EventDate, string Details);

public record TransactionRow(int Id, int Type, string Category, decimal Amount, string Description, DateTime TransactionDate, string RecordedBy);

public record DeviceRow(int Id, string Name, string SerialNumber, string IP);

public record DoorRow(int Id, string Name, int DoorNumber, string DeviceName);
