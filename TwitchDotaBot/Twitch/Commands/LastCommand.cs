using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class LastCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var dota = provider.GetRequiredService<DotaClient>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();

        MatchModel[] matches = await dota.LoadMatchesAsync(steamId: appOptions.Value.SteamId, limit: 5,
            cancellationToken: cancellationToken);

        MatchModel? recentOver = matches.FirstOrDefault(m =>
            m.MatchResult != MatchResult.None && m.MatchResult != MatchResult.EarlyLeave);

        var chatBot = provider.GetRequiredService<ChatBot>();

        if (recentOver == null)
        {
            await chatBot.Channel.SendMessageAsync("Не нашёл, увы.", e.id);
            return;
        }

        if (recentOver.MatchResult == MatchResult.Finished)
        {
            bool? isWinner = IsWinner(recentOver, appOptions.Value.SteamId);

            string reply = isWinner switch
            {
                true => "Вин.",
                false => "Луз.",
                _ => "Непонятно."
            };

            TimeSpan passed = (DateTime.UtcNow - recentOver.GameDate) + recentOver.DetailsInfo!.Duration;

            reply += $" ({GetTimeString(passed)} назад)";

            await chatBot.Channel.SendMessageAsync(reply, e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync("Непонятно.", e.id);
    }
}