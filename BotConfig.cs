using System;
using Discord;
using Newtonsoft.Json;

namespace QuaverBot;

public struct BotConfig
{
    public string Token;

    [JsonConverter(typeof(LogSeverityConverter))]
    public LogSeverity LogLevel;
    public ulong GuildId;
    public ulong ModlogChannelId;
    public string MacroPrefix;
    public ulong MutedRole;
    public ulong StreamBanRole;
    public ulong[] ModRoles;
    public CleanConfig Clean;
    public LogConfig Log;

    public static BotConfig Default => new BotConfig
    {
        Token = "YOUR_TOKEN",
        LogLevel = LogSeverity.Info,
        MacroPrefix = "!",
        ModRoles = Array.Empty<ulong>(),
        Clean = new CleanConfig
        {
            Channels = Array.Empty<ulong>(),
            IgnoreMessages = Array.Empty<ulong>(),
            IgnoreUsers = Array.Empty<ulong>(),
            IgnoreRoles = Array.Empty<ulong>(),
            AfterHours = 6,
        },
        Log = new LogConfig
        {
            CacheAttachments = true,
            MaxCacheSize = 1024 * 1024 * 1024, // 1 GiB
        },
    };
}

public struct CleanConfig
{
    public ulong[] Channels;
    public ulong[] IgnoreMessages;
    public ulong[] IgnoreUsers;
    public ulong[] IgnoreRoles;
    public int AfterHours;
}

public struct LogConfig
{
    public ulong ChannelId;
    public bool CacheAttachments;
    public long MaxCacheSize;
}

public class LogSeverityConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => writer.WriteValue(value?.ToString());
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    => Enum.Parse<LogSeverity>(reader.Value?.ToString() ?? throw new InvalidOperationException("LogSeverity value is null."));
    public override bool CanConvert(Type objectType) => objectType == typeof(LogSeverity);
}