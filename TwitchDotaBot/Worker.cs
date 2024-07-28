using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchLib.Api.Helix.Models.Predictions.CreatePrediction;
using Outcome = TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome;

namespace TwitchDotaBot;

public class Worker : IHostedService
{
    private readonly DotaClient _dota;
    private readonly ChatBot _chatBot;
    private readonly SuperApi _api;
    private readonly AppConfig _appConfig;
    private readonly ILogger<Worker> _logger;

    private MatchModel? _trackingMatch;
    private Prediction? _prediction;

    public Worker(DotaClient dota, ChatBot chatBot, SuperApi api, IOptions<AppConfig> appOptions,
        ILogger<Worker> logger)
    {
        _dota = dota;
        _chatBot = chatBot;
        _api = api;
        _appConfig = appOptions.Value;
        _logger = logger;

        _dota.NewMatchFound += DotaOnNewMatchFound;
        _dota.MatchClosed += DotaOnMatchClosed;
    }

    private void DotaOnNewMatchFound(MatchModel obj)
    {
        _trackingMatch = obj;

        Task.Run(async () =>
        {
            try
            {
                await StartPredictionAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при попытке запустить ставку.");
            }
        });
    }

    private void DotaOnMatchClosed(MatchModel obj)
    {
        if (obj.Id != _trackingMatch?.Id)
            return;

        if (_prediction == null)
            return;

        bool? win;
        if (obj.MatchResult == MatchResult.Finished)
        {
            bool? isRadiant = obj.Players?.FirstOrDefault(p => p.SteamId == _appConfig.SteamId)?.TeamNumber == 0;

            if (isRadiant == null)
            {
                win = null;
            }
            else
            {
                win = obj.DetailsInfo?.RadiantWin == isRadiant;
            }
        }
        else
        {
            win = null;
        }

        Task.Run(async () =>
        {
            try
            {
                await ClosePredictionAsync(win, _prediction);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при попытке закрыть ставку.");
            }
        });
    }

    private async Task StartPredictionAsync()
    {
        _logger.LogInformation("Запускаем ставку.");

        TwitchAPI api = await _api.GetApiAsync();

        CreatePredictionResponse? response = await api.Helix.Predictions.CreatePredictionAsync(
            new CreatePredictionRequest()
            {
                BroadcasterId = _appConfig.TwitchId, Title = "Победа в игре дота2?", PredictionWindowSeconds = 150,
                Outcomes = [new Outcome { Title = "Да" }, new Outcome { Title = "Нет" }]
            });

        _prediction = response.Data[0];

        await _chatBot.Channel.SendMessageAsync("Запустил ставку.");
    }

    private async Task ClosePredictionAsync(bool? win, Prediction prediction)
    {
        _logger.LogInformation("Закрываем ставку.");

        TwitchAPI api = await _api.GetApiAsync();

        if (win == null)
        {
            await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
                PredictionEndStatus.CANCELED);

            await _chatBot.Channel.SendMessageAsync("Отменил ставку.");
            return;
        }

        string targetTitle = win == true ? "Да" : "Нет";
        TwitchLib.Api.Helix.Models.Predictions.Outcome? targetOutcome =
            prediction.Outcomes.FirstOrDefault(p => p.Title == targetTitle);

        if (targetOutcome == null)
            return;

        await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
            PredictionEndStatus.RESOLVED, targetOutcome.Id);

        await _chatBot.Channel.SendMessageAsync("Закрыл ставку.");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dota.NewMatchFound -= DotaOnNewMatchFound;
        _dota.MatchClosed -= DotaOnMatchClosed;

        return Task.CompletedTask;
    }
}