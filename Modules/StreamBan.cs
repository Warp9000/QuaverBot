using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using QuaverBot.Database;

namespace QuaverBot.Modules;

public class StreamBan : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("stream_ban", "Stream Ban")]
    [RequireMod]
    public async Task StreamBanAsync(SocketGuildUser user, string reason)
    {
        await DeferAsync();
        if (QuaverBot.Config.StreamBanRole == 0)
        {
            await FollowupAsync("StreamBan role is not set up.");
            return;
        }

        if (user.Roles.Any(x => x.Id == QuaverBot.Config.StreamBanRole)){
            await FollowupAsync("Already stream banned.");
            return;
        }

        await user.AddRoleAsync(QuaverBot.Config.StreamBanRole, new RequestOptions { AuditLogReason = reason });

        var history = new ModHistory
        {
            DiscordId = user.Id,
            ModId = Context.User.Id,
            Action = ModHistory.ActionType.StreamBan,
            Content = reason
        };

        DatabaseManager.Connection?.Insert(history);
        await Logging.LogStreamBan(user.Id, Context.User.Id, reason, Context.Client);

        string response = $"Stream Banned {user.Username}";
        if (reason != null) response += $": `{reason}`";

        await FollowupAsync(response);
    }

    [SlashCommand("stream_unban", "Stream Unban")]
    [RequireMod]
    public async Task UnmuteAsync(SocketGuildUser user, string? reason = null)
    {
        await DeferAsync();
        if (QuaverBot.Config.MutedRole == 0)
        {
            await FollowupAsync("Muted role is not set up.");
            return;
        }

        if (!user.Roles.Any(x => x.Id == QuaverBot.Config.StreamBanRole)){
            await FollowupAsync("Not stream banned.");
            return;
        }

        await user.RemoveRoleAsync(QuaverBot.Config.StreamBanRole, new RequestOptions { AuditLogReason = reason });

        var history = new ModHistory
        {
            DiscordId = user.Id,
            ModId = Context.User.Id,
            Action = ModHistory.ActionType.StreamUnban,
            Content = reason
        };

        DatabaseManager.Connection?.Insert(history);
        await Logging.LogStreamUnban(user.Id, Context.User.Id, reason, Context.Client);

        await FollowupAsync($"Stream Unbanned {user.Username}{(reason != null ? $" for {reason}" : "")}");
    }
}