using TwitchDotaBot.Dota;
using TwitchLib.Api.Core.Enums;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class CancelPredictionCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var worker = provider.GetRequiredService<Worker>();
        var chatbot = provider.GetRequiredService<ChatBot>();

        if (worker.CurrentPrediction == null)
        {
            await chatbot.Channel.SendMessageAsync("Не вижу предикт.", e.id);
            return;
        }

        if (worker.CurrentPrediction.Status is PredictionStatus.CANCELED or PredictionStatus.RESOLVED)
        {
            await chatbot.Channel.SendMessageAsync("Предикт уже закончен.", e.id);
            return;
        }

        var dota = provider.GetRequiredService<DotaClient>();

        worker.MatchModelToIgnore = dota.CurrentMatch;
        dota.DropCurrentMatch();

        await worker.ClosePredictionAsync(null, worker.CurrentPrediction);
        await chatbot.Channel.SendMessageAsync("Сделана.", e.id);
    }
}