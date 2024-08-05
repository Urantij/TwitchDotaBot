using Microsoft.Extensions.Options;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchLib.Api.Helix.Models.Predictions.GetPredictions;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class CancelPredictionExtremeCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var chatbot = provider.GetRequiredService<ChatBot>();
        var apiService = provider.GetRequiredService<SuperApi>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();

        TwitchAPI api = await apiService.GetApiAsync();

        GetPredictionsResponse predictionResponse =
            await api.Helix.Predictions.GetPredictionsAsync(appOptions.Value.TwitchId, first: 1);

        if (predictionResponse.Data.Length == 0)
        {
            await chatbot.Channel.SendMessageAsync("Нет...", e.id);
            return;
        }

        Prediction prediction = predictionResponse.Data[0];

        if (prediction.Status is PredictionStatus.CANCELED or PredictionStatus.RESOLVED)
        {
            await chatbot.Channel.SendMessageAsync("Уже закрыто...", e.id);
            return;
        }

        await api.Helix.Predictions.EndPredictionAsync(appOptions.Value.TwitchId, prediction.Id,
            PredictionEndStatus.CANCELED);

        await chatbot.Channel.SendMessageAsync("сделана", e.id);
    }
}