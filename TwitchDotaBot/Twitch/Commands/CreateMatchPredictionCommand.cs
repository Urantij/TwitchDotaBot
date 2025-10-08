using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using TwitchDotaBot.Dota;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class CreateMatchPredictionCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var dota = provider.GetRequiredService<DotaClient>();
        var worker = provider.GetRequiredService<Worker>();
        var chatbot = provider.GetRequiredService<ChatBot>();
        var logger = provider.GetRequiredService<ILogger<CreateMatchPredictionCommand>>();

        Prediction? currentPrediction = worker.CurrentPrediction;

        if (currentPrediction is { Status: not PredictionStatus.CANCELED and not PredictionStatus.RESOLVED })
        {
            await chatbot.Channel.SendMessageAsync("Предикт уже есть.", e.id);
            return;
        }

        MatchModel? match = dota.CurrentMatch;

        if (match == null)
        {
            await chatbot.Channel.SendMessageAsync("Не вижу игру.", e.id);
            return;
        }

        if (match.MatchResult != MatchResult.None)
        {
            await chatbot.Channel.SendMessageAsync("Матч уже закрыт.", e.id);
            return;
        }

        // TODO это оч плохое решение. у меня вся логика перепутана
        try
        {
            await worker.StartPredictionAsync(match);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось создать прогноз");

            await chatbot.Channel.SendMessageAsync(
                "Не удалось создать прогноз. Не пробуй юзать команду ещё раз, там чето совсем неправильно.", e.id);
            return;
        }

        // там внутри пишет.. нужно всё разделять
        TimeSpan passed = (DateTime.UtcNow - match.GameDate) + match.DetailsInfo?.Duration ?? TimeSpan.Zero;

        await chatbot.Channel.SendMessageAsync("Матч ({GetTimeString(passed)} назад)", e.id);
    }
}