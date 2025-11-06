using TwitchDotaBot.Job;
using TwitchLib.Api.Core.Enums;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class TestPredictionCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var worker = provider.GetRequiredService<Worker>();
        var chatbot = provider.GetRequiredService<ChatBot>();

        if (worker.CurrentPrediction is { Status: PredictionStatus.CANCELED or PredictionStatus.RESOLVED })
        {
            await chatbot.Channel.SendMessageAsync("Предикт уже есть.", e.id);
            return;
        }

        await worker.StartPredictionAsync();
        await chatbot.Channel.SendMessageAsync("Сделана.", e.id);
    }
}