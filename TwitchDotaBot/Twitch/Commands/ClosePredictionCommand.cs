using TwitchLib.Api.Core.Enums;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class ClosePredictionCommand : BaseCommand
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

        await worker.ClosePredictionAsync(result, worker.CurrentPrediction);
        await chatbot.Channel.SendMessageAsync("Сделана.", e.id);
    }
}