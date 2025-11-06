using Dota2Dispenser.Shared.Consts;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Job;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class CreateMatchPredictionCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var tracker = provider.GetRequiredService<MatchTracker>();
        var worker = provider.GetRequiredService<Worker>();
        var chatbot = provider.GetRequiredService<ChatBot>();
        var logger = provider.GetRequiredService<ILogger<CreateMatchPredictionCommand>>();
        var config = provider.GetRequiredService<IOptions<AppConfig>>();
        var heroes = provider.GetRequiredService<DotaHeroes>();

        Prediction? currentPrediction = worker.CurrentPrediction;

        if (currentPrediction is { Status: not PredictionStatus.CANCELED and not PredictionStatus.RESOLVED })
        {
            await chatbot.Channel.SendMessageAsync("Предикт уже есть.", e.id);
            return;
        }

        MatchContainer? container = tracker.LatestMatch;

        if (container == null)
        {
            await chatbot.Channel.SendMessageAsync("Не вижу игру.", e.id);
            return;
        }

        if (container.Model.MatchResult != MatchResult.None)
        {
            await chatbot.Channel.SendMessageAsync("Матч уже закрыт.", e.id);
            return;
        }

        // TODO это оч плохое решение. у меня вся логика перепутана
        try
        {
            await worker.StartPredictionAsync(container);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось создать прогноз");

            await chatbot.Channel.SendMessageAsync(
                "Не удалось создать прогноз. Не пробуй юзать команду ещё раз, там чето совсем неправильно.", e.id);
            return;
        }

        // там внутри пишет.. нужно всё разделять
        TimeSpan passed = (DateTime.UtcNow - container.Model.GameDate) + container.Model.DetailsInfo?.Duration ??
                          TimeSpan.Zero;

        int? heroId = container.Model.Players?.FirstOrDefault(p => p.SteamId == config.Value.SteamId)?.HeroId;

        string heroString = "";
        if (heroId != null)
        {
            string? name = heroes.GerHeroName(heroId.Value);

            if (name != null)
            {
                heroString = $" ({name})";
            }
            else
            {
                heroString = " (Пока без героя)";
            }
        }

        await chatbot.Channel.SendMessageAsync($"Матч найден {GetTimeString(passed)} назад{heroString}", e.id);
    }
}