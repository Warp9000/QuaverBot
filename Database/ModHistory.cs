using System;
using SQLite;

namespace QuaverBot.Database;

public class ModHistory
{
    [PrimaryKey, AutoIncrement, Unique]
    public int Id { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? Expiry { get; set; }

    public ulong DiscordId { get; set; }

    public ulong ModId { get; set; }

    public ActionType Action { get; set; }

    public string? Content { get; set; }

    public enum ActionType
    {
        Mute,
        Unmute,
        StreamBan,
        StreamUnban,
        Scma,
    }
}