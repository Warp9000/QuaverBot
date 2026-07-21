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

    [SlashCommand("mute", "Mute a user")]
    [RequireMod]
    public async Task MuteAsync(SocketGuildUser user, string duration, string reason, bool dm_user = true, bool dm_reason = true)
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

        DateTimeOffset startTime = DateTimeOffset.UtcNow;
        DateTimeOffset endTime;
        TimeSpan? durationTime = null;
        if (permKeywords.Contains(duration.ToLower()))
        {
            endTime = DateTimeOffset.MaxValue;
        }
        else
        {
            TimeSpan? parsed = ParseTime(duration);
            if (parsed == null)
            {
                await RespondAsync("Invalid duration.");
                return;
            }

            endTime = startTime + parsed.Value;
            durationTime = parsed.Value;
        }

        await user.AddRoleAsync(QuaverBot.Config.MutedRole, new RequestOptions { AuditLogReason = $"{(durationTime != null ? $"{durationTime}" : "perm")} for {reason}" });

        var muteData = new DatabaseMute
        {
            DiscordId = user.Id,
            Until = endTime.ToUnixTimeSeconds(),
        };

        DatabaseManager.Connection?.Insert(muteData);

        var history = new ModHistory
        {
            Timestamp = startTime,
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

        if (dm_user)
        {
            var embed = new EmbedBuilder
            {
                Title = "Muted",
                Color = Color.Orange,
                Timestamp = DateTimeOffset.Now,
            };

            embed.AddField("Duration", durationTime != null ? FormatTime(durationTime.Value) : "Permanent");
            if (dm_reason && !string.IsNullOrEmpty(reason))
            {
                embed.AddField("Reason", reason);
            }

            try
            {
                await user.SendMessageAsync(embed: embed.Build());
            }
            catch
            {
                var resp = await GetOriginalResponseAsync();
                await resp.ReplyAsync("Couldnt DM user.");
            }
        }
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

    public static TimeSpan? ParseTime(string str)
    {
        TimeSpan t = new();
        int i = 0;
        foreach (var c in str)
        {
            if (c == ' ')
            {
                continue;
            }

            if (c >= '0' && c <= '9')
            {
                i = (i * 10) + (c - '0');
                continue;
            }

            TimeSpan t2;
            switch (c)
            {
                case 's': t2 = TimeSpan.FromSeconds(i); break;
                case 'm': t2 = TimeSpan.FromMinutes(i); break;
                case 'h': t2 = TimeSpan.FromHours(i); break;
                case 'd': t2 = TimeSpan.FromDays(i); break;
                case 'w': t2 = TimeSpan.FromDays(i * 7); break;
                case 'M': t2 = TimeSpan.FromDays(i * 30); break;
                case 'Y': t2 = TimeSpan.FromDays(i * 365); break;
                default:
                    return null;
            }
            t = t.Add(t2);
            i = 0;
        }

        if (t == TimeSpan.Zero)
        {
            return null;
        }

        return t;
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