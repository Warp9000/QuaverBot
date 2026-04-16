using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using QuaverBot.Database;

namespace QuaverBot.Modules;

public class Mute : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly string[] permKeywords = { "permanent", "perm", "p", "forever", "inf", "infinite", "infinity", "-1" };

    // https://github.com/discord-net/Discord.Net/blob/51f59bf1852fd8a13d1142170782bdb73eea9f5e/src/Discord.Net.Interactions/TypeConverters/SlashCommands/TimeSpanConverter.cs#L18
    private static readonly string[] Formats = {
        "%d'd'%h'h'%m'm'%s's'", //4d3h2m1s
        "%d'd'%h'h'%m'm'",      //4d3h2m
        "%d'd'%h'h'%s's'",      //4d3h  1s
        "%d'd'%h'h'",           //4d3h
        "%d'd'%m'm'%s's'",      //4d  2m1s
        "%d'd'%m'm'",           //4d  2m
        "%d'd'%s's'",           //4d    1s
        "%d'd'",                //4d
        "%h'h'%m'm'%s's'",      //  3h2m1s
        "%h'h'%m'm'",           //  3h2m
        "%h'h'%s's'",           //  3h  1s
        "%h'h'",                //  3h
        "%m'm'%s's'",           //    2m1s
        "%m'm'",                //    2m
        "%s's'",                //      1s
    };

    [SlashCommand("mute", "Mute a user")]
    [RequireMod]
    public async Task MuteAsync(SocketGuildUser user, string duration, string reason)
    {
        if (QuaverBot.Config.MutedRole == 0)
        {
            await RespondAsync("Muted role is not set up.");
            return;
        }

        var mute = DatabaseManager.Connection?.Find<DatabaseMute>(m => m.DiscordId == user.Id);

        if (mute != null)
        {
            await RespondAsync($"{user.Username} is already muted.");
            return;
        }

        await user.AddRoleAsync(QuaverBot.Config.MutedRole, new RequestOptions { AuditLogReason = reason });

        DateTimeOffset endTime;
        TimeSpan? durationTime = null;
        if (permKeywords.Contains(duration.ToLower()))
        {
            endTime = DateTimeOffset.MaxValue;
        }
        else
        {
            if (!TimeSpan.TryParseExact(duration, Formats, CultureInfo.InvariantCulture, out TimeSpan time))
            {
                await RespondAsync("Invalid duration.");
                return;
            }

            endTime = DateTimeOffset.UtcNow + time;
            durationTime = time;
        }

        // DateTimeOffset endTime = duration != null ? DateTimeOffset.UtcNow + duration.Value : DateTimeOffset.MaxValue;

        var muteData = new DatabaseMute
        {
            DiscordId = user.Id,
            Until = endTime.ToUnixTimeSeconds(),
        };

        DatabaseManager.Connection?.Insert(muteData);

        var history = new ModHistory
        {
            DiscordId = user.Id,
            ModId = Context.User.Id,
            Action = ModHistory.ActionType.Mute,
            Content = reason,
            Expiry = endTime
        };

        DatabaseManager.Connection?.Insert(history);
        await Logging.LogMute(user.Id, Context.User.Id, reason, durationTime, Context.Client);

        string response = $"Muted {user.Username}";
        if (durationTime != null) response += $" for {FormatTime(durationTime.Value)}";
        if (reason != null) response += $": `{reason}`";

        await RespondAsync(response);
    }

    [SlashCommand("unmute", "Unmute a user")]
    [RequireMod]
    public async Task UnmuteAsync(SocketGuildUser user, string? reason = null)
    {
        if (QuaverBot.Config.MutedRole == 0)
        {
            await RespondAsync("Muted role is not set up.");
            return;
        }

        var mute = DatabaseManager.Connection?.Find<DatabaseMute>(m => m.DiscordId == user.Id);

        if (mute == null)
        {
            await RespondAsync($"{user.Username} is not muted.");
            return;
        }

        await user.RemoveRoleAsync(QuaverBot.Config.MutedRole, new RequestOptions { AuditLogReason = reason });

        DatabaseManager.Connection?.Delete(mute);

        var history = new ModHistory
        {
            DiscordId = user.Id,
            ModId = Context.User.Id,
            Action = ModHistory.ActionType.Unmute,
            Content = reason
        };

        DatabaseManager.Connection?.Insert(history);
        await Logging.LogUnmute(user.Id, Context.User.Id, reason, Context.Client);

        await RespondAsync($"Unmuted {user.Username}{(reason != null ? $" for {reason}" : "")}");
    }

    public static void InitCheckMuted(DiscordSocketClient client)
    {
        CheckMutedTimer = new Timer(1000 * 60 * 15); // 15 minutes
        CheckMutedTimer.Elapsed += (s, e) => { CheckMuted(client); };
        CheckMutedTimer.AutoReset = true;
        CheckMutedTimer.Start();
    }

    private static Timer? CheckMutedTimer;

    private static async void CheckMuted(DiscordSocketClient client)
    {
        var mutes = DatabaseManager.Connection?.Table<DatabaseMute>().ToList();

        if (mutes == null) return;

        foreach (var mute in mutes)
        {
            if (DateTimeOffset.FromUnixTimeSeconds(mute.Until) <= DateTimeOffset.UtcNow)
            {
                var user = client.GetGuild(QuaverBot.Config.GuildId)?.GetUser(mute.DiscordId);

                if (user == null) continue;

                await user.RemoveRoleAsync(QuaverBot.Config.MutedRole);

                DatabaseManager.Connection?.Delete(mute);

                var history = new ModHistory
                {
                    DiscordId = user.Id,
                    ModId = client.CurrentUser.Id,
                    Action = ModHistory.ActionType.Unmute,
                    Content = "Automatic unmute"
                };

                DatabaseManager.Connection?.Insert(history);
                await Logging.LogUnmute(user.Id, client.CurrentUser.Id, "Automatic unmute", client);
            }
        }
    }

    public static string FormatTime(TimeSpan time)
    {
        return time switch
        {
            TimeSpan t when t.TotalDays >= 1 => $"{t.TotalDays} day{(t.TotalDays > 1 ? "s" : "")}",
            TimeSpan t when t.TotalHours >= 1 => $"{t.TotalHours} hour{(t.TotalHours > 1 ? "s" : "")}",
            TimeSpan t when t.TotalMinutes >= 1 => $"{t.TotalMinutes} minute{(t.TotalMinutes > 1 ? "s" : "")}",
            _ => $"{time.TotalSeconds} second{(time.TotalSeconds > 1 ? "s" : "")}"
        };
    }

    public static async Task OnUserJoined(SocketGuildUser user)
    {
        var mute = DatabaseManager.Connection?.Find<DatabaseMute>(m => m.DiscordId == user.Id);

        if (mute == null) return;

        await user.AddRoleAsync(QuaverBot.Config.MutedRole);
    }
}