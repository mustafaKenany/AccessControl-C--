using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Services;

public class AccessEventService : IAccessEventService
{
    private readonly IAccessEventRepository _eventRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDoorRepository _doorRepository;
    private readonly IAccessCardRepository _cardRepository;
    private readonly IAccessControlSdk _sdk;

    public AccessEventService(
        IAccessEventRepository eventRepository,
        IDeviceRepository deviceRepository,
        IDoorRepository doorRepository,
        IAccessCardRepository cardRepository,
        IAccessControlSdk sdk)
    {
        _eventRepository = eventRepository;
        _deviceRepository = deviceRepository;
        _doorRepository = doorRepository;
        _cardRepository = cardRepository;
        _sdk = sdk;
    }

    public async Task<(IEnumerable<AccessEventDto> Items, int TotalCount)> GetEventsPagedAsync(
        int page, int pageSize, DateTime? from = null, DateTime? to = null, int? doorId = null, string? search = null,
        RecordType? eventType = null, int? deviceId = null)
    {
        var (items, total) = await _eventRepository.GetPagedAsync(page, pageSize, from, to, doorId, search, eventType, deviceId);

        var dtos = items.Select(e => new AccessEventDto
        {
            Id = e.Id,
            DeviceName = e.Door?.Device?.Name ?? e.Door?.Device?.SerialNumber ?? "",
            DoorName = e.Door?.Name ?? ExtractDoorLabel(e.Details),
            CardNumber = e.Card?.CardNumber ?? ExtractCardNumber(e.Details),
            PlayerName = e.Card?.Employee != null
                ? $"{e.Card.Employee.FullNameEn} | {e.Card.Employee.FullNameAr}"
                : ExtractPlayerName(e.Details),
            EventType = e.EventType.ToString(),
            EventDescription = GetEventDescription(e.EventType, e.EventCode),
            Direction = e.Details.Contains("Entry") ? "Entry" : e.Details.Contains("Exit") ? "Exit" : "",
            CardStatus = ExtractCardStatus(e.Details),
            CardStatusKey = ExtractCardStatus(e.Details),
            Timestamp = e.Timestamp
        });

        return (dtos, total);
    }

    public async Task SaveEventAsync(int doorId, int? cardId, int recordType, int eventCode, DateTime timestamp, string details)
    {
        var evt = new AccessEvent
        {
            DoorId = doorId,
            CardId = cardId,
            EventType = Enum.IsDefined(typeof(RecordType), recordType) ? (RecordType)recordType : RecordType.System,
            EventCode = Enum.IsDefined(typeof(EventCode), eventCode) ? (EventCode)eventCode : EventCode.CardOpen,
            Timestamp = timestamp,
            Details = details
        };
        await _eventRepository.AddAsync(evt);
    }

    public async Task<int> FetchAndSaveRecordsAsync(int deviceId, DateTime? fromDate = null)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return 0;

        var deviceInfo = new DeviceInfo
        {
            IP = device.IP, MAC = device.MAC, SerialNumber = device.SerialNumber,
            TCPPort = device.TCPPort, UDPPort = device.UDPPort,
            Password = device.Password, Gateway = device.Gateway, SubnetMask = device.SubnetMask
        };

        // Fetch ALL records from hardware (SDK dumps everything to CSV)
        var records = await _sdk.FetchAllRecordsAsync(deviceInfo, 30);
        if (records.Count == 0) return 0;

        // Filter by period if specified
        if (fromDate.HasValue)
            records = records.Where(r => r.EventDate >= fromDate.Value).ToList();

        // Get doors for this device — auto-create if none exist
        var doors = (await _doorRepository.GetByDeviceSerialAsync(device.SerialNumber)).ToList();
        if (doors.Count == 0)
        {
            // Determine door count from device type
            var doorCount = device.DeviceType.ToString() switch
            {
                "CR3212T" or "CR3216H" => 1,
                "CR3242T" or "CR3246H" => 4,
                _ => 2
            };
            var newDoors = Enumerable.Range(1, doorCount).Select(i => new Door
            {
                DeviceId = device.Id,
                DoorNumber = i,
                Name = $"Door {i}",
                CreatedAt = DateTime.UtcNow
            }).ToList();
            await _doorRepository.AddRangeAsync(newDoors);
            doors = (await _doorRepository.GetByDeviceSerialAsync(device.SerialNumber)).ToList();
        }
        int savedCount = 0;

        foreach (var rec in records)
        {
            // Match door by number, fallback to first door of device
            var door = doors.FirstOrDefault(d => d.DoorNumber == rec.DoorNumber)
                       ?? doors.FirstOrDefault();
            if (door == null) continue; // no doors at all for this device
            int doorId = door.Id;

            // Look up card — unregistered cards will have cardId=null
            // but we still save the raw card number in Details
            int? cardId = null;
            string cardStatus = "";
            string playerName = "";
            if (!string.IsNullOrEmpty(rec.CardNumber))
            {
                var card = await _cardRepository.GetByCardNumberAsync(rec.CardNumber);
                cardId = card?.Id;

                if (card == null)
                {
                    cardStatus = "Unregistered";
                }
                else if (card.Employee?.IsFrozen == true)
                {
                    cardStatus = "Frozen";
                }
                else if (!card.IsActive
                    || (card.ValidTo != default && card.ValidTo < DateTime.Now)
                    || (card.Employee?.EndDate != null && card.Employee.EndDate < DateTime.Now))
                {
                    cardStatus = "Expired";
                }
                else
                {
                    cardStatus = "Active";
                }

                // Capture player name for embedding in Details
                if (card?.Employee != null)
                    playerName = $"{card.Employee.FullNameEn} | {card.Employee.FullNameAr}";
            }

            // ReaderType: 0 = In (Entry), 1 = Out (Exit)
            string direction = rec.ReaderType == 0 ? "Entry" : "Exit";
            string doorLabel = $"Door {rec.DoorNumber}";

            // Human-readable record type
            string recordTypeLabel = ((RecordType)rec.RecordType) switch
            {
                RecordType.Card => "Card",
                RecordType.Button => "Button Press",
                RecordType.DoorSensor => "Door Sensor",
                RecordType.Software => "Remote",
                RecordType.Alarm => "Alarm",
                RecordType.System => "System",
                _ => "Unknown"
            };

            // Include raw card number, player name, and status in details
            var cardPart = !string.IsNullOrEmpty(rec.CardNumber) ? $" | #{rec.CardNumber}" : "";
            var playerPart = !string.IsNullOrEmpty(playerName) ? $" | {playerName}" : "";
            var statusPart = !string.IsNullOrEmpty(cardStatus) ? $" | @{cardStatus}" : "";
            string details = $"{direction} | {doorLabel} | {recordTypeLabel}{cardPart}{playerPart}{statusPart}";

            await SaveEventAsync(doorId, cardId, rec.RecordType, rec.EventCode, rec.EventDate, details);
            savedCount++;
        }

        return savedCount;
    }

    public async Task<int> CleanupOldEventsAsync(int monthsToKeep = 6)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-monthsToKeep);
        return await _eventRepository.DeleteOlderThanAsync(cutoff);
    }

    /// <summary>Extract raw card number from Details (format: "... | #12345678")</summary>
    private static string ExtractCardNumber(string details)
    {
        if (string.IsNullOrEmpty(details)) return "";
        var parts = details.Split('|', StringSplitOptions.TrimEntries);
        var cardPart = parts.FirstOrDefault(p => p.StartsWith('#'));
        return cardPart?.TrimStart('#') ?? "";
    }

    /// <summary>Extract player name from Details (appears after #CardNumber and before SN:)</summary>
    private static string ExtractPlayerName(string details)
    {
        if (string.IsNullOrEmpty(details)) return "";
        var parts = details.Split('|', StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith('#') && i + 1 < parts.Length)
            {
                var next = parts[i + 1];
                if (!next.StartsWith("SN:") && !next.StartsWith("@"))
                    return next;
            }
        }
        return "";
    }

    /// <summary>Extract card status from Details (format: "... | @Registered")</summary>
    private static string ExtractCardStatus(string details)
    {
        if (string.IsNullOrEmpty(details)) return "";
        var parts = details.Split('|', StringSplitOptions.TrimEntries);
        var statusPart = parts.FirstOrDefault(p => p.StartsWith('@'));
        return statusPart?.TrimStart('@') ?? "";
    }

    private static string ExtractDoorLabel(string details)
    {
        if (string.IsNullOrEmpty(details)) return "";
        var parts = details.Split('|', StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[1] : "";
    }

    // The controller's status field only carries card-access outcomes for CARD records. For every
    // other record type (button, door sensor, remote, alarm, system) that field means something
    // else, so rendering those as "Card Open" is misleading — describe them by record type instead.
    private static string GetEventDescription(RecordType type, EventCode code)
    {
        if (type == RecordType.Card)
        {
            return code switch
            {
                EventCode.CardOpen => "Card Open",
                EventCode.PasswordOpen => "Password Open",
                EventCode.CardAndPasswordOpen => "Card + Password",
                EventCode.CardRepeat => "Card Repeat",
                EventCode.CardExpired => "Card Expired",
                EventCode.CardInvalid => "Invalid Card",
                _ => "Card Event"
            };
        }

        return code switch
        {
            // Use the specific label only when the code genuinely belongs to this family.
            EventCode.ButtonOpen => "Button Open",
            EventCode.RemoteOpen => "Remote Open",
            EventCode.RemoteClose => "Remote Close",
            EventCode.DoorSensorOpen => "Door Opened",
            EventCode.DoorSensorClose => "Door Closed",
            EventCode.AlarmFire => "Fire Alarm",
            EventCode.AlarmPolice => "Police Alarm",
            EventCode.AlarmGas => "Gas Alarm",
            EventCode.AlarmMagnetic => "Magnetic Alarm",
            EventCode.AlarmTheft => "Theft Alarm",
            EventCode.AlarmAntiPassback => "Anti-Passback",
            EventCode.SystemStartup => "System Startup",
            EventCode.SystemRestart => "System Restart",
            EventCode.SystemHighTemp => "High Temperature",
            EventCode.SystemUPS => "UPS Power",
            // Placeholder/duplicate status code (e.g. the controller's generic "1"): describe by type.
            _ => type switch
            {
                RecordType.Button => "Button Press",
                RecordType.DoorSensor => "Door Sensor",
                RecordType.Software => "Remote",
                RecordType.Alarm => "Alarm",
                RecordType.System => "System Event",
                _ => "Event"
            }
        };
    }
}
