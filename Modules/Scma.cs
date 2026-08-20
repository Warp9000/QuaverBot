
using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using QuaverBot.Database;

namespace QuaverBot.Modules;

public class Scma : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("scma", "soft ban user")]
    [RequireMod]
    public async Task ScmaAsync(IUser user)
    {
        await DeferAsync();

        await Context.Guild.AddBanAsync(user, 1, "scma");
        await Context.Guild.RemoveBanAsync(user, new RequestOptions() { AuditLogReason = "scma" });

        var history = new ModHistory
        {
            Timestamp = DateTimeOffset.UtcNow,
            DiscordId = user.Id,
            ModId = Context.User.Id,
            Action = ModHistory.ActionType.Scma,
            Content = "scma"
        };

        DatabaseManager.Connection?.Insert(history);

        await FollowupAsync("https://cdn.discordapp.com/emojis/1109900336912662538.webp?size=32");
    }
}