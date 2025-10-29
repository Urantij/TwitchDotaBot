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
        var dota = provider.GetRequiredService<DotaClient>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();

        (int wins, int loses, _, _) = await LoadMatchesStatsAsync(dota, appOptions.Value.SteamId, cancellationToken);

        var chatBot = provider.GetRequiredService<ChatBot>();

        if (wins == 0 && loses == 0)
        {
            await chatBot.Channel.SendMessageAsync("Матчей не наблюдаю.", e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync($"{wins}-{loses}", e.id);
    }

    public static async Task<(int wins, int loses, int streak, bool? isStreakWin)> LoadMatchesStatsAsync(
        DotaClient dota, ulong steamId,
        CancellationToken cancellationToken = default)
    {
        // Костыльно, но следить за временем стрима сложно.
        DateTimeOffset afterDate = DateTimeOffset.UtcNow - TimeSpan.FromHours(12);

        MatchModel[] matches = await dota.LoadMatchesAsync(steamId: steamId, afterDate: afterDate,
            cancellationToken: cancellationToken);

        int wins = 0;
        int loses = 0;

        int streak = 0;
        bool? isStreakWin = null;

        // Грузит от нового к старому по дефолту
        foreach (MatchModel matchModel in matches.Reverse())
        {
            bool? win = IsWinner(matchModel, steamId);

            switch (win)
            {
                case null:
                    continue;
                case true:
                    wins++;

                    if (isStreakWin == true)
                    {
                        streak++;
                    }
                    else
                    {
                        isStreakWin = true;
                        streak = 1;
                    }

                    break;
                default:
                    loses++;

                    if (isStreakWin == false)
                    {
                        streak++;
                    }
                    else
                    {
                        isStreakWin = false;
                        streak = 1;
                    }

                    break;
            }
        }

        if (streak <= 1)
        {
            streak = 0;
            isStreakWin = null;
        }

        return (wins, loses, streak, isStreakWin);
    }
}