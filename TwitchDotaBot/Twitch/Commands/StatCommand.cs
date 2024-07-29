using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class StatCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        // Костыльно, но следить за временем стрима сложно.
        DateTimeOffset afterDate = DateTimeOffset.UtcNow - TimeSpan.FromHours(12);

        var dota = provider.GetRequiredService<DotaClient>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();

        MatchModel[] matches = await dota.LoadMatchesAsync(steamId: appOptions.Value.SteamId, afterDate: afterDate,
            cancellationToken: cancellationToken);

        int wins = 0;
        int loses = 0;

        foreach (MatchModel matchModel in matches)
        {
            bool? win = IsWinner(matchModel, appOptions.Value.SteamId);

            switch (win)
            {
                case null:
                    continue;
                case true:
                    wins++;
                    break;
                default:
                    loses++;
                    break;
            }
        }

        var chatBot = provider.GetRequiredService<ChatBot>();

        if (wins == 0 && loses == 0)
        {
            await chatBot.Channel.SendMessageAsync("Матчей не наблюдаю.", e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync($"{wins}-{loses}", e.id);
    }
}