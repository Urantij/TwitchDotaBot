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
        var dota = provider.GetRequiredService<DotaClient>();

        var chatBot = provider.GetRequiredService<ChatBot>();

        if (dota.CurrentMatch == null)
        {
            await chatBot.Channel.SendMessageAsync("Не нашёл, увы.", e.id);
            return;
        }

        if (dota.CurrentMatch.TvInfo == null)
        {
            await chatBot.Channel.SendMessageAsync("Информация ещё не пришла.", e.id);
            return;
        }

        if (dota.CurrentMatch.TvInfo.AverageMmr == null)
        {
            await chatBot.Channel.SendMessageAsync("В матче неизвестен средний ммр.", e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync($"Средний ммр в матче: {dota.CurrentMatch.TvInfo.AverageMmr}", e.id);
    }
}