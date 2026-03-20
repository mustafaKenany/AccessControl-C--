using System.Text.Json;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class TimeGroupService : ITimeGroupService
{
    private readonly ITimeGroupRepository _repo;

    // Day keys in SDK order: Mon=0 .. Sun=6
    private static readonly string[] DayKeys = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    public TimeGroupService(ITimeGroupRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<TimeGroup>> GetAllAsync()
        => _repo.GetAllAsync();

    public Task<TimeGroup?> GetByIdAsync(int id)
        => _repo.GetByIdAsync(id);

    public async Task<TimeGroup> CreateAsync(string nameEn, string nameAr, string scheduleJson)
    {
        var nextIndex = await _repo.GetNextHardwareIndexAsync();
        if (nextIndex < 0)
            throw new InvalidOperationException("All 64 time group slots are in use. Delete an existing group first.");

        var timeGroup = new TimeGroup
        {
            NameEn = nameEn.Trim(),
            NameAr = nameAr.Trim(),
            HardwareIndex = nextIndex,
            ScheduleJson = scheduleJson,
            CreatedAt = DateTime.UtcNow
        };

        await _repo.AddAsync(timeGroup);
        return timeGroup;
    }

    public async Task UpdateAsync(int id, string nameEn, string nameAr, string scheduleJson)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null)
            throw new InvalidOperationException("Time group not found.");

        existing.NameEn = nameEn.Trim();
        existing.NameAr = nameAr.Trim();
        existing.ScheduleJson = scheduleJson;
        existing.UpdatedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(existing);
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repo.GetByIdAsync(id);
        if (existing == null)
            throw new InvalidOperationException("Time group not found.");
        if (existing.IsDefault)
            throw new InvalidOperationException("Cannot delete the default 24/7 time group.");

        await _repo.DeleteAsync(id);
    }

    /// <summary>
    /// Converts a JSON schedule to the SDK timePieces format.
    /// Each day has 8 time segments, each segment = "HHmmHHmm" (8 chars).
    /// Per day = 64 chars. 7 days = 448 chars total.
    /// </summary>
    public string BuildTimePiecesString(string scheduleJson)
    {
        Dictionary<string, List<string>>? schedule = null;

        if (!string.IsNullOrWhiteSpace(scheduleJson) && scheduleJson != "{}")
        {
            try
            {
                schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleJson);
            }
            catch
            {
                schedule = null;
            }
        }

        var result = new char[448]; // 7 days * 64 chars
        Array.Fill(result, '0');

        for (int day = 0; day < 7; day++)
        {
            int dayOffset = day * 64;

            List<string>? segments = null;
            if (schedule != null && schedule.TryGetValue(DayKeys[day], out var segs))
                segments = segs;

            if (segments == null || segments.Count == 0)
            {
                // No schedule entry for this day = 24 hours open
                // First segment: 00:00 - 23:59
                WriteSegment(result, dayOffset, 0, "00002359");
            }
            else
            {
                for (int s = 0; s < segments.Count && s < 8; s++)
                {
                    var seg = ParseTimeRange(segments[s]);
                    WriteSegment(result, dayOffset, s, seg);
                }
            }
        }

        return new string(result);
    }

    private static void WriteSegment(char[] buffer, int dayOffset, int segIndex, string segment)
    {
        int offset = dayOffset + (segIndex * 8);
        for (int i = 0; i < 8 && i < segment.Length; i++)
            buffer[offset + i] = segment[i];
    }

    /// <summary>
    /// Parses "HH:MM-HH:MM" into "HHmmHHmm"
    /// </summary>
    private static string ParseTimeRange(string range)
    {
        try
        {
            var parts = range.Split('-');
            if (parts.Length != 2) return "00000000";

            var start = parts[0].Trim().Replace(":", "");
            var end = parts[1].Trim().Replace(":", "");

            if (start.Length != 4 || end.Length != 4) return "00000000";

            return start + end;
        }
        catch
        {
            return "00000000";
        }
    }
}
