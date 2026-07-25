using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using QuaverBot;
using QuaverBot.Database;

namespace QuaverBot.Modules;

public class History : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DiscordSocketClient client;
    public History(DiscordSocketClient c)
    {
        client = c;
    }

    [SlashCommand("history", "Get the moderation history of a user")]
    [RequireMod]
    public async Task HistoryAsync(string? user = null, ModHistory.ActionType? type = null, SocketGuildUser? mod = null, int page = 0)
    {
        ulong? user_id = null;
        if (user != null)
        {
            user = user.Trim(new char[] { '<', '>', '@', ' ' });
            if (!ulong.TryParse(user, out ulong _user_id))
            {
                await RespondAsync("invalid user.");
            }
            user_id = _user_id;
        }

        var query = DatabaseManager.Connection?.Table<ModHistory>();

        if (query == null)
        {
            await RespondAsync("No history found.");
            return;
        }

        if (user_id != null)
        {
            query = query.Where(h => h.DiscordId == user_id);
        }

        if (type != null)
        {
            query = query.Where(h => h.Action == type);
        }

        if (mod != null)
        {
            query = query.Where(h => h.ModId == mod.Id);
        }

        var history = query.ToList();

        if (history.Count == 0)
        {
            await RespondAsync("No history found.");
            return;
        }

        await DeferAsync();

        var embed = new EmbedBuilder
        {
            Title = "History",
            Description = $"Showing {history.Count} results",
            Color = new Color(0xdf9911),
            Footer = new EmbedFooterBuilder
            {
                Text = $"Page {page + 1}/{(history.Count + EmbedBuilder.MaxFieldCount - 1) / EmbedBuilder.MaxFieldCount}"
            }
        };

        if (user_id != null)
        {
            IUser u = await client.GetUserAsync(user_id.Value);
            embed.WithAuthor(u);
        }

        // var truncated = history.Take(EmbedBuilder.MaxFieldCount).ToList();
        var truncated = history.Skip(page * EmbedBuilder.MaxFieldCount).Take(EmbedBuilder.MaxFieldCount).ToList();

        foreach (var entry in truncated)
        {
            var mod_ = Context.Guild.GetUser(entry.ModId);
            embed.AddField(entry.Action.ToString(), $"By {mod_?.Username ?? "Unknown"} on {entry.Timestamp:yyyy-MM-dd HH:mm:ss}{(entry.Expiry != null ? $"\n{entry.Expiry - entry.Timestamp}" : "")}{(entry.Content != null ? $"\n{entry.Content}" : "")}");
        }

        await FollowupAsync(embed: embed.Build());
    }
}