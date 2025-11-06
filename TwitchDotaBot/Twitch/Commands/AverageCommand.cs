using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class AverageCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var tracker = provider.GetRequiredService<MatchTracker>();

        var chatBot = provider.GetRequiredService<ChatBot>();

        if (tracker.LatestMatch == null)
        {
            await chatBot.Channel.SendMessageAsync("Не нашёл, увы.", e.id);
            return;
        }

        if (tracker.LatestMatch.Model.TvInfo == null)
        {
            await chatBot.Channel.SendMessageAsync("Информация ещё не пришла.", e.id);
            return;
        }

        if (tracker.LatestMatch.Model.TvInfo.AverageMmr == null)
        {
            await chatBot.Channel.SendMessageAsync("В матче неизвестен средний ммр.", e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync($"Средний ммр в матче: {tracker.LatestMatch.Model.TvInfo.AverageMmr}",
            e.id);
    }
}