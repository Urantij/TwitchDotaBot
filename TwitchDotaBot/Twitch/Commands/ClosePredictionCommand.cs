using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class ClosePredictionCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var worker = provider.GetRequiredService<Worker>();
        var chatbot = provider.GetRequiredService<ChatBot>();
        
        Prediction? currentPrediction = worker.CurrentPrediction;

        if (currentPrediction == null)
        {
            await chatbot.Channel.SendMessageAsync("Не вижу предикт.", e.id);
            return;
        }
        
        if (currentPrediction.Status is PredictionStatus.CANCELED or PredictionStatus.RESOLVED)
        {
            await chatbot.Channel.SendMessageAsync("Предикт уже закончен.", e.id);
            return;
        }

        if (args.Length == 0)
        {
            await chatbot.Channel.SendMessageAsync("Варианты - вин, луз", e.id);
            return;
        }

        bool? result;
        if (args[0].Equals("вин", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
        }
        else if (args[0].Equals("луз", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
        }
        else
        {
            result = null;
        }

        if (result == null)
        {
            await chatbot.Channel.SendMessageAsync("Варианты - вин, луз", e.id);
            return;
        }

        await worker.ClosePredictionAsync(result, currentPrediction);
        await chatbot.Channel.SendMessageAsync("Сделана.", e.id);
    }
}